package main

import (
	"bytes"
	"crypto"
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

const aud = "synthetic-web-client.apps.example"

type env struct {
	t   *testing.T
	srv *Server
	h   http.Handler
	key *rsa.PrivateKey
	now time.Time
	n   int
}

func newEnv(t *testing.T, sensitive bool) *env {
	key, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatal(err)
	}
	db, err := openStore(filepath.Join(t.TempDir(), "t.db"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { db.Close() })
	e := &env{t: t, key: key, now: time.Unix(1_800_000_000, 0)}
	v := &Verifier{Audiences: map[string]bool{aud: true}, Now: func() time.Time { return e.now },
		Keys: func() (map[string]*rsa.PublicKey, error) { return map[string]*rsa.PublicKey{"k1": &key.PublicKey}, nil }}
	e.srv = NewServer(db, v, sensitive)
	e.srv.Now = func() time.Time { return e.now }
	e.h = e.srv.Handler()
	return e
}

func (e *env) idToken(sub, nonce string, mut func(map[string]any), alg string, key *rsa.PrivateKey) string {
	c := map[string]any{"iss": "https://accounts.google.com", "aud": aud, "sub": sub, "nonce": nonce, "iat": e.now.Unix(), "exp": e.now.Unix() + 3600}
	if mut != nil {
		mut(c)
	}
	hb, _ := json.Marshal(map[string]string{"alg": alg, "kid": "k1"})
	cb, _ := json.Marshal(c)
	signing := base64.RawURLEncoding.EncodeToString(hb) + "." + base64.RawURLEncoding.EncodeToString(cb)
	sum := sha256.Sum256([]byte(signing))
	sig, _ := rsa.SignPKCS1v15(rand.Reader, key, crypto.SHA256, sum[:])
	return signing + "." + base64.RawURLEncoding.EncodeToString(sig)
}

func (e *env) do(method, path, token string, body any) (int, map[string]any) {
	var buf bytes.Buffer
	if body != nil {
		if s, ok := body.(string); ok {
			buf.WriteString(s)
		} else {
			json.NewEncoder(&buf).Encode(body)
		}
	}
	req := httptest.NewRequest(method, path, &buf)
	if token != "" {
		req.Header.Set("Authorization", "Bearer "+token)
	}
	rec := httptest.NewRecorder()
	e.h.ServeHTTP(rec, req)
	out := map[string]any{}
	json.Unmarshal(rec.Body.Bytes(), &out)
	return rec.Code, out
}

func (e *env) login(sub string) (string, string) {
	e.n++
	nonce := fmt.Sprintf("nonce-%016d", e.n)
	return e.loginWith(e.idToken(sub, nonce, nil, "RS256", e.key), nonce)
}

func (e *env) loginWith(tok, nonce string) (string, string) {
	code, out := e.do("POST", "/v1/auth/google", "", map[string]any{"id_token": tok, "nonce": nonce, "device": map[string]string{"name": "Test phone", "platform": "android"}})
	if code != 200 {
		e.t.Fatalf("login failed: %d %v", code, out)
	}
	return out["session"].(string), out["device_id"].(string)
}

func put(id string, lamport int64) map[string]any {
	return map[string]any{"records": []map[string]any{{"id": id, "lamport": lamport, "nonce": "bm9uY2U", "ciphertext": "Y2lwaGVydGV4dA"}}}
}

func TestLoginRejections(t *testing.T) {
	e := newEnv(t, false)
	other, _ := rsa.GenerateKey(rand.Reader, 2048)
	n := "nonce-aaaaaaaaaaaaaaaa"
	cases := map[string]string{
		"wrong audience": e.idToken("s", n, func(c map[string]any) { c["aud"] = "other" }, "RS256", e.key),
		"expired":        e.idToken("s", n, func(c map[string]any) { c["exp"] = e.now.Unix() - 600 }, "RS256", e.key),
		"wrong issuer":   e.idToken("s", n, func(c map[string]any) { c["iss"] = "https://evil.example" }, "RS256", e.key),
		"wrong nonce":    e.idToken("s", "nonce-bbbbbbbbbbbbbbbb", nil, "RS256", e.key),
		"bad signature":  e.idToken("s", n, nil, "RS256", other),
		"alg none":       e.idToken("s", n, nil, "none", e.key),
		"missing sub":    e.idToken("", n, nil, "RS256", e.key),
		"future iat":     e.idToken("s", n, func(c map[string]any) { c["iat"] = e.now.Unix() + 7200 }, "RS256", e.key),
	}
	for name, tok := range cases {
		code, _ := e.do("POST", "/v1/auth/google", "", map[string]any{"id_token": tok, "nonce": n, "device": map[string]string{"name": "d", "platform": "android"}})
		if code != 401 {
			t.Errorf("%s: got %d want 401", name, code)
		}
	}
}

func TestNonceReplayRejected(t *testing.T) {
	e := newEnv(t, false)
	nonce := "nonce-cccccccccccccccc"
	tok := e.idToken("s", nonce, nil, "RS256", e.key)
	e.loginWith(tok, nonce)
	code, _ := e.do("POST", "/v1/auth/google", "", map[string]any{"id_token": tok, "nonce": nonce, "device": map[string]string{"name": "d", "platform": "android"}})
	if code != 401 {
		t.Fatalf("replayed token accepted: %d", code)
	}
}

func TestSyncRoundTripCursorConflictAndIsolation(t *testing.T) {
	e := newEnv(t, false)
	a, devA := e.login("user-a")
	b, _ := e.login("user-b")
	if c, out := e.do("PUT", "/v1/sync/bookmarks", a, put("bm1", 1)); c != 200 || len(out["accepted"].([]any)) != 1 {
		t.Fatalf("put: %d %v", c, out)
	}
	e.do("PUT", "/v1/sync/bookmarks", a, put("bm2", 1))
	_, out := e.do("GET", "/v1/sync/bookmarks?cursor=0", a, nil)
	recs := out["records"].([]any)
	if len(recs) != 2 || recs[0].(map[string]any)["device_id"] != devA {
		t.Fatalf("get: %v", out)
	}
	next := fmt.Sprint(int64(out["next"].(float64)))
	_, inc := e.do("GET", "/v1/sync/bookmarks?cursor="+next, a, nil)
	if len(inc["records"].([]any)) != 0 {
		t.Fatalf("incremental cursor returned old records: %v", inc)
	}
	// Same lamport from the same device is not newer: conflict. Higher lamport wins.
	if _, o := e.do("PUT", "/v1/sync/bookmarks", a, put("bm1", 1)); len(o["conflicts"].([]any)) != 1 {
		t.Fatalf("expected conflict: %v", o)
	}
	if _, o := e.do("PUT", "/v1/sync/bookmarks", a, put("bm1", 2)); len(o["accepted"].([]any)) != 1 {
		t.Fatalf("expected accept: %v", o)
	}
	// Tombstone.
	e.do("PUT", "/v1/sync/bookmarks", a, map[string]any{"records": []map[string]any{{"id": "bm1", "lamport": 3, "deleted": true}}})
	_, after := e.do("GET", "/v1/sync/bookmarks?cursor="+next, a, nil)
	r0 := after["records"].([]any)[0].(map[string]any)
	if r0["deleted"] != true || r0["ciphertext"] != "" {
		t.Fatalf("tombstone: %v", after)
	}
	// Other account sees nothing.
	if _, o := e.do("GET", "/v1/sync/bookmarks?cursor=0", b, nil); len(o["records"].([]any)) != 0 {
		t.Fatalf("cross-account leak: %v", o)
	}
}

func TestCategoryRules(t *testing.T) {
	for _, sensitive := range []bool{false, true} {
		e := newEnv(t, sensitive)
		s, _ := e.login("u")
		if c, _ := e.do("GET", "/v1/sync/bogus", s, nil); c != 400 {
			t.Errorf("unknown category: %d", c)
		}
		for _, cat := range []string{"passwords", "cookies"} {
			// Unpaired device: forbidden even when the server switch is on.
			if c, _ := e.do("PUT", "/v1/sync/"+cat, s, put("x", 1)); c != 403 {
				t.Errorf("%s sensitive=%v: %d want 403", cat, sensitive, c)
			}
		}
	}
	e := newEnv(t, true)
	s, dev := e.login("u")
	e.srv.DB.Exec(`UPDATE devices SET paired=1 WHERE id=?`, dev)
	if c, _ := e.do("PUT", "/v1/sync/passwords", s, put("x", 1)); c != 200 {
		t.Errorf("paired device with sensitive enabled should be allowed: %d", c)
	}
}

func TestRevokeLogoutDelete(t *testing.T) {
	e := newEnv(t, false)
	s1, _ := e.login("u")
	s2, dev2 := e.login("u")
	e.do("PUT", "/v1/sync/settings", s1, put("k", 1))
	if c, _ := e.do("DELETE", "/v1/devices/"+dev2, s1, nil); c != 204 {
		t.Fatalf("revoke: %d", c)
	}
	if c, _ := e.do("GET", "/v1/devices", s2, nil); c != 401 {
		t.Fatalf("revoked device still authorised: %d", c)
	}
	if c, _ := e.do("POST", "/v1/auth/logout", s1, nil); c != 204 {
		t.Fatalf("logout: %d", c)
	}
	if c, _ := e.do("GET", "/v1/devices", s1, nil); c != 401 {
		t.Fatalf("logged-out session still valid: %d", c)
	}
	s3, _ := e.login("u")
	if c, _ := e.do("DELETE", "/v1/account", s3, nil); c != 204 {
		t.Fatalf("delete account: %d", c)
	}
	var n int
	e.srv.DB.QueryRow(`SELECT (SELECT COUNT(*) FROM records)+(SELECT COUNT(*) FROM devices)+(SELECT COUNT(*) FROM sessions)+(SELECT COUNT(*) FROM accounts)`).Scan(&n)
	if n != 0 {
		t.Fatalf("data left after account deletion: %d rows", n)
	}
}

func TestLimitsAndValidation(t *testing.T) {
	e := newEnv(t, false)
	s, _ := e.login("u")
	big := strings.Repeat("A", maxRecordBlob+1)
	if c, _ := e.do("PUT", "/v1/sync/tabs", s, map[string]any{"records": []map[string]any{{"id": "a", "lamport": 1, "nonce": "n", "ciphertext": big}}}); c != 400 {
		t.Errorf("oversized record: %d", c)
	}
	if c, _ := e.do("PUT", "/v1/sync/tabs", s, strings.Repeat(" ", maxBody+10)); c != 413 && c != 400 {
		t.Errorf("oversized body: %d", c)
	}
	if c, _ := e.do("PUT", "/v1/sync/tabs", s, `{"records":[{"id":"bad id!","lamport":1,"nonce":"n","ciphertext":"c"}]}`); c != 400 {
		t.Errorf("bad id: %d", c)
	}
	if c, _ := e.do("PUT", "/v1/sync/tabs", s, `{"records":[],"extra":1}`); c != 400 {
		t.Errorf("unknown field: %d", c)
	}
	if c, _ := e.do("GET", "/v1/devices", "", nil); c != 401 {
		t.Errorf("no token: %d", c)
	}
	limited := false
	for i := 0; i < 200; i++ {
		if c, _ := e.do("GET", "/v1/devices", s, nil); c == 429 {
			limited = true
			break
		}
	}
	if !limited {
		t.Error("per-device rate limit never triggered")
	}
}

func TestSessionExpiry(t *testing.T) {
	e := newEnv(t, false)
	s, _ := e.login("u")
	e.now = e.now.Add(sessionTTL + time.Hour)
	if c, _ := e.do("GET", "/v1/devices", s, nil); c != 401 {
		t.Fatalf("expired session accepted: %d", c)
	}
}
