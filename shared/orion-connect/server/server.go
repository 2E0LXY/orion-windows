package main

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"database/sql"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"time"
)

const (
	maxBody        = 256 << 10
	maxRecordBlob  = 90000 // base64 characters, about 64 kB of ciphertext
	maxBatch       = 100
	maxAccountRows = 50000
	maxAccountSize = 50 << 20
	sessionTTL     = 30 * 24 * time.Hour
)

var (
	normalCategories    = map[string]bool{"bookmarks": true, "settings": true, "readinglist": true, "tabs": true, "history": true}
	sensitiveCategories = map[string]bool{"passwords": true, "cookies": true}
	idPattern           = regexp.MustCompile(`^[A-Za-z0-9._:-]{1,128}$`)
)

type Server struct {
	DB              *sql.DB
	Verifier        *Verifier
	Now             func() time.Time
	EnableSensitive bool // server switch; a device must also be paired (pairing arrives in a later phase)
	limiter         *limiter
}

func NewServer(db *sql.DB, v *Verifier, enableSensitive bool) *Server {
	return &Server{DB: db, Verifier: v, Now: time.Now, EnableSensitive: enableSensitive, limiter: newLimiter(120, time.Minute)}
}

type ctxKey int

type principal struct {
	Account int64
	Device  string
	Paired  bool
}

func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) { writeJSON(w, 200, map[string]string{"status": "ok"}) })
	mux.HandleFunc("POST /v1/auth/google", s.login)
	mux.Handle("POST /v1/auth/logout", s.auth(s.logout))
	mux.Handle("GET /v1/devices", s.auth(s.listDevices))
	mux.Handle("DELETE /v1/devices/{id}", s.auth(s.revokeDevice))
	mux.Handle("DELETE /v1/account", s.auth(s.deleteAccount))
	mux.Handle("GET /v1/sync/{category}", s.auth(s.syncGet))
	mux.Handle("PUT /v1/sync/{category}", s.auth(s.syncPut))
	return securityHeaders(mux)
}

func securityHeaders(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Cache-Control", "no-store")
		w.Header().Set("X-Content-Type-Options", "nosniff")
		next.ServeHTTP(w, r)
	})
}

func writeJSON(w http.ResponseWriter, code int, v any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(v)
}
func fail(w http.ResponseWriter, code int, msg string) { writeJSON(w, code, map[string]string{"error": msg}) }

func decode(w http.ResponseWriter, r *http.Request, v any) bool {
	dec := json.NewDecoder(http.MaxBytesReader(w, r.Body, maxBody))
	dec.DisallowUnknownFields()
	if err := dec.Decode(v); err != nil {
		var tooBig *http.MaxBytesError
		if errors.As(err, &tooBig) {
			fail(w, 413, "request too large")
		} else {
			fail(w, 400, "invalid request")
		}
		return false
	}
	if _, err := dec.Token(); err != io.EOF {
		fail(w, 400, "invalid request")
		return false
	}
	return true
}

func randomToken(n int) string {
	b := make([]byte, n)
	if _, err := rand.Read(b); err != nil {
		panic(err)
	}
	return base64.RawURLEncoding.EncodeToString(b)
}
func hashToken(t string) string { h := sha256.Sum256([]byte(t)); return hex.EncodeToString(h[:]) }

// ---- authentication ----

type loginReq struct {
	IDToken string `json:"id_token"`
	Nonce   string `json:"nonce"`
	Device  struct {
		Name     string `json:"name"`
		Platform string `json:"platform"`
		PubKey   string `json:"pubkey"`
	} `json:"device"`
}

func (s *Server) login(w http.ResponseWriter, r *http.Request) {
	if !s.limiter.allow("login:"+clientIP(r), 10) {
		fail(w, 429, "too many attempts")
		return
	}
	var req loginReq
	if !decode(w, r, &req) {
		return
	}
	d := req.Device
	if len(d.Name) == 0 || len(d.Name) > 64 || len(d.Platform) == 0 || len(d.Platform) > 32 || len(d.PubKey) > 512 || len(req.Nonce) < 16 || len(req.Nonce) > 128 {
		fail(w, 400, "invalid request")
		return
	}
	c, err := s.Verifier.Verify(req.IDToken, req.Nonce)
	if err != nil {
		fail(w, 401, "authentication failed")
		return
	}
	now := s.Now().Unix()
	tx, err := s.DB.BeginTx(r.Context(), nil)
	if err != nil {
		fail(w, 500, "server error")
		return
	}
	defer tx.Rollback()
	// Each ID token nonce is single use: a captured token cannot be replayed.
	_, _ = tx.Exec(`DELETE FROM used_nonces WHERE expires<?`, now)
	if _, err := tx.Exec(`INSERT INTO used_nonces(hash,expires) VALUES(?,?)`, hashToken(req.Nonce), now+3600); err != nil {
		fail(w, 401, "authentication failed")
		return
	}
	if _, err := tx.Exec(`INSERT OR IGNORE INTO accounts(iss,sub,created) VALUES(?,?,?)`, c.Iss, c.Sub, now); err != nil {
		fail(w, 500, "server error")
		return
	}
	var account int64
	if err := tx.QueryRow(`SELECT id FROM accounts WHERE iss=? AND sub=?`, c.Iss, c.Sub).Scan(&account); err != nil {
		fail(w, 500, "server error")
		return
	}
	var devices int
	_ = tx.QueryRow(`SELECT COUNT(*) FROM devices WHERE account=? AND revoked=0`, account).Scan(&devices)
	if devices >= 20 {
		fail(w, 409, "device limit reached; revoke a device first")
		return
	}
	deviceID := randomToken(12)
	if _, err := tx.Exec(`INSERT INTO devices(id,account,name,platform,pubkey,created,last_seen) VALUES(?,?,?,?,?,?,?)`, deviceID, account, d.Name, d.Platform, d.PubKey, now, now); err != nil {
		fail(w, 500, "server error")
		return
	}
	token := "oc_" + randomToken(32)
	expires := now + int64(sessionTTL.Seconds())
	if _, err := tx.Exec(`INSERT INTO sessions(hash,device,expires) VALUES(?,?,?)`, hashToken(token), deviceID, expires); err != nil {
		fail(w, 500, "server error")
		return
	}
	if err := tx.Commit(); err != nil {
		fail(w, 500, "server error")
		return
	}
	writeJSON(w, 200, map[string]any{"session": token, "device_id": deviceID, "expires": expires})
}

type authed func(w http.ResponseWriter, r *http.Request, p principal)

func (s *Server) auth(next authed) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		h := r.Header.Get("Authorization")
		if !strings.HasPrefix(h, "Bearer oc_") {
			fail(w, 401, "unauthorised")
			return
		}
		hash := hashToken(strings.TrimPrefix(h, "Bearer "))
		now := s.Now().Unix()
		var p principal
		var expires int64
		var paired int
		err := s.DB.QueryRowContext(r.Context(), `SELECT d.account,d.id,d.paired,s.expires FROM sessions s JOIN devices d ON d.id=s.device WHERE s.hash=? AND d.revoked=0 AND s.expires>?`, hash, now).Scan(&p.Account, &p.Device, &paired, &expires)
		if err != nil {
			fail(w, 401, "unauthorised")
			return
		}
		p.Paired = paired == 1
		if !s.limiter.allow("dev:"+p.Device, 120) {
			fail(w, 429, "rate limit exceeded")
			return
		}
		if expires-now < int64(sessionTTL.Seconds())/2 { // sliding expiry, written at most twice per TTL
			_, _ = s.DB.Exec(`UPDATE sessions SET expires=? WHERE hash=?`, now+int64(sessionTTL.Seconds()), hash)
		}
		_, _ = s.DB.Exec(`UPDATE devices SET last_seen=? WHERE id=? AND last_seen<?`, now, p.Device, now-300)
		next(w, r, p)
	})
}

func (s *Server) logout(w http.ResponseWriter, r *http.Request, p principal) {
	_, _ = s.DB.Exec(`DELETE FROM sessions WHERE hash=?`, hashToken(strings.TrimPrefix(r.Header.Get("Authorization"), "Bearer ")))
	w.WriteHeader(204)
}

func (s *Server) listDevices(w http.ResponseWriter, r *http.Request, p principal) {
	rows, err := s.DB.Query(`SELECT id,name,platform,paired,created,last_seen FROM devices WHERE account=? AND revoked=0 ORDER BY created`, p.Account)
	if err != nil {
		fail(w, 500, "server error")
		return
	}
	defer rows.Close()
	out := []map[string]any{}
	for rows.Next() {
		var id, name, platform string
		var paired int
		var created, seen int64
		if rows.Scan(&id, &name, &platform, &paired, &created, &seen) == nil {
			out = append(out, map[string]any{"id": id, "name": name, "platform": platform, "paired": paired == 1, "created": created, "last_seen": seen, "current": id == p.Device})
		}
	}
	writeJSON(w, 200, map[string]any{"devices": out})
}

func (s *Server) revokeDevice(w http.ResponseWriter, r *http.Request, p principal) {
	id := r.PathValue("id")
	res, err := s.DB.Exec(`UPDATE devices SET revoked=1 WHERE id=? AND account=?`, id, p.Account)
	if n, _ := res.RowsAffected(); err != nil || n == 0 {
		fail(w, 404, "no such device")
		return
	}
	_, _ = s.DB.Exec(`DELETE FROM sessions WHERE device=?`, id)
	w.WriteHeader(204)
}

func (s *Server) deleteAccount(w http.ResponseWriter, r *http.Request, p principal) {
	if _, err := s.DB.Exec(`DELETE FROM accounts WHERE id=?`, p.Account); err != nil {
		fail(w, 500, "server error")
		return
	}
	w.WriteHeader(204)
}

// ---- sync ----

func (s *Server) categoryAllowed(w http.ResponseWriter, category string, p principal) bool {
	switch {
	case normalCategories[category]:
		return true
	case sensitiveCategories[category]:
		if !s.EnableSensitive || !p.Paired {
			fail(w, 403, "category not enabled for this device")
			return false
		}
		return true
	}
	fail(w, 400, "unknown category")
	return false
}

type record struct {
	ID         string `json:"id"`
	Lamport    int64  `json:"lamport"`
	Deleted    bool   `json:"deleted"`
	DeviceID   string `json:"device_id,omitempty"`
	Nonce      string `json:"nonce"`
	Ciphertext string `json:"ciphertext"`
	Seq        int64  `json:"seq,omitempty"`
}

func (s *Server) syncGet(w http.ResponseWriter, r *http.Request, p principal) {
	category := r.PathValue("category")
	if !s.categoryAllowed(w, category, p) {
		return
	}
	cursor, _ := strconv.ParseInt(r.URL.Query().Get("cursor"), 10, 64)
	limit := 100
	if v, err := strconv.Atoi(r.URL.Query().Get("limit")); err == nil && v > 0 && v <= 200 {
		limit = v
	}
	rows, err := s.DB.QueryContext(r.Context(), `SELECT id,seq,lamport,deleted,device,nonce,blob FROM records WHERE account=? AND category=? AND seq>? ORDER BY seq LIMIT ?`, p.Account, category, cursor, limit+1)
	if err != nil {
		fail(w, 500, "server error")
		return
	}
	defer rows.Close()
	out := []record{}
	more := false
	next := cursor
	for rows.Next() {
		var rec record
		var del int
		if rows.Scan(&rec.ID, &rec.Seq, &rec.Lamport, &del, &rec.DeviceID, &rec.Nonce, &rec.Ciphertext) != nil {
			continue
		}
		if len(out) == limit {
			more = true
			break
		}
		rec.Deleted = del == 1
		out = append(out, rec)
		next = rec.Seq
	}
	writeJSON(w, 200, map[string]any{"records": out, "next": next, "more": more})
}

type putReq struct {
	Records []record `json:"records"`
}

func newer(lamport int64, device string, curLamport int64, curDevice string) bool {
	return lamport > curLamport || (lamport == curLamport && device > curDevice)
}

func (s *Server) syncPut(w http.ResponseWriter, r *http.Request, p principal) {
	category := r.PathValue("category")
	if !s.categoryAllowed(w, category, p) {
		return
	}
	var req putReq
	if !decode(w, r, &req) {
		return
	}
	if len(req.Records) == 0 || len(req.Records) > maxBatch {
		fail(w, 400, "batch must contain 1 to 100 records")
		return
	}
	for _, rec := range req.Records {
		if !idPattern.MatchString(rec.ID) || rec.Lamport < 0 || len(rec.Ciphertext) > maxRecordBlob || len(rec.Nonce) > 64 || (!rec.Deleted && (rec.Ciphertext == "" || rec.Nonce == "")) {
			fail(w, 400, "invalid record")
			return
		}
	}
	ctx, cancel := context.WithTimeout(r.Context(), 10*time.Second)
	defer cancel()
	tx, err := s.DB.BeginTx(ctx, nil)
	if err != nil {
		fail(w, 500, "server error")
		return
	}
	defer tx.Rollback()
	var rows, size int64
	_ = tx.QueryRow(`SELECT COUNT(*),COALESCE(SUM(LENGTH(blob)),0) FROM records WHERE account=?`, p.Account).Scan(&rows, &size)
	now := s.Now().Unix()
	accepted := []string{}
	conflicts := []map[string]any{}
	for _, rec := range req.Records {
		var curLamport int64
		var curDevice string
		err := tx.QueryRow(`SELECT lamport,device FROM records WHERE account=? AND category=? AND id=?`, p.Account, category, rec.ID).Scan(&curLamport, &curDevice)
		exists := err == nil
		if err != nil && err != sql.ErrNoRows {
			fail(w, 500, "server error")
			return
		}
		if exists && !newer(rec.Lamport, p.Device, curLamport, curDevice) {
			conflicts = append(conflicts, map[string]any{"id": rec.ID, "lamport": curLamport, "device_id": curDevice})
			continue
		}
		if !exists && (rows+1 > maxAccountRows || size+int64(len(rec.Ciphertext)) > maxAccountSize) {
			fail(w, 413, "account storage quota exceeded")
			return
		}
		var seq int64
		if err := tx.QueryRow(`UPDATE accounts SET seq=seq+1 WHERE id=? RETURNING seq`, p.Account).Scan(&seq); err != nil {
			fail(w, 500, "server error")
			return
		}
		blob, nonce := rec.Ciphertext, rec.Nonce
		if rec.Deleted {
			blob, nonce = "", ""
		}
		del := 0
		if rec.Deleted {
			del = 1
		}
		if _, err := tx.Exec(`INSERT INTO records(account,category,id,seq,lamport,deleted,device,nonce,blob,updated) VALUES(?,?,?,?,?,?,?,?,?,?)
ON CONFLICT(account,category,id) DO UPDATE SET seq=excluded.seq,lamport=excluded.lamport,deleted=excluded.deleted,device=excluded.device,nonce=excluded.nonce,blob=excluded.blob,updated=excluded.updated`,
			p.Account, category, rec.ID, seq, rec.Lamport, del, p.Device, nonce, blob, now); err != nil {
			fail(w, 500, "server error")
			return
		}
		if !exists {
			rows++
		}
		size += int64(len(blob))
		accepted = append(accepted, rec.ID)
	}
	if err := tx.Commit(); err != nil {
		fail(w, 500, "server error")
		return
	}
	writeJSON(w, 200, map[string]any{"accepted": accepted, "conflicts": conflicts})
}

// ---- rate limiting ----

type limiter struct {
	mu     sync.Mutex
	n      int
	window time.Duration
	hits   map[string][]time.Time
}

func newLimiter(n int, window time.Duration) *limiter {
	return &limiter{n: n, window: window, hits: map[string][]time.Time{}}
}

func (l *limiter) allow(key string, limit int) bool {
	l.mu.Lock()
	defer l.mu.Unlock()
	now := time.Now()
	if len(l.hits) > 10000 {
		l.hits = map[string][]time.Time{}
	}
	kept := l.hits[key][:0]
	for _, t := range l.hits[key] {
		if now.Sub(t) < l.window {
			kept = append(kept, t)
		}
	}
	if len(kept) >= limit {
		l.hits[key] = kept
		return false
	}
	l.hits[key] = append(kept, now)
	return true
}

func clientIP(r *http.Request) string {
	// Behind Caddy/Cloudflare the real address arrives in a header set by the trusted proxy only.
	if v := r.Header.Get("X-Forwarded-For"); v != "" {
		return strings.TrimSpace(strings.Split(v, ",")[0])
	}
	if i := strings.LastIndex(r.RemoteAddr, ":"); i > 0 {
		return r.RemoteAddr[:i]
	}
	return r.RemoteAddr
}
