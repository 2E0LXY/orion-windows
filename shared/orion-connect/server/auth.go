package main

import (
	"crypto"
	"crypto/rsa"
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"errors"
	"math/big"
	"net/http"
	"strings"
	"sync"
	"time"
)

// Verifier checks Google ID tokens (RS256) against Google's published keys.
type Verifier struct {
	Audiences map[string]bool
	Keys      func() (map[string]*rsa.PublicKey, error)
	Now       func() time.Time
}

type claims struct {
	Iss   string          `json:"iss"`
	Aud   json.RawMessage `json:"aud"`
	Sub   string          `json:"sub"`
	Exp   int64           `json:"exp"`
	Iat   int64           `json:"iat"`
	Nonce string          `json:"nonce"`
}

var errToken = errors.New("invalid id token")

// Verify validates signature, issuer, audience, expiry and nonce. Accounts are keyed by (iss, sub), never email.
func (v *Verifier) Verify(token, nonce string) (*claims, error) {
	parts := strings.Split(token, ".")
	if len(parts) != 3 || nonce == "" {
		return nil, errToken
	}
	hdrRaw, err1 := base64.RawURLEncoding.DecodeString(parts[0])
	body, err2 := base64.RawURLEncoding.DecodeString(parts[1])
	sig, err3 := base64.RawURLEncoding.DecodeString(parts[2])
	if err1 != nil || err2 != nil || err3 != nil {
		return nil, errToken
	}
	var hdr struct{ Alg, Kid string }
	if json.Unmarshal(hdrRaw, &hdr) != nil || hdr.Alg != "RS256" || hdr.Kid == "" {
		return nil, errToken
	}
	keys, err := v.Keys()
	if err != nil {
		return nil, err
	}
	pub := keys[hdr.Kid]
	if pub == nil {
		return nil, errToken
	}
	sum := sha256.Sum256([]byte(parts[0] + "." + parts[1]))
	if rsa.VerifyPKCS1v15(pub, crypto.SHA256, sum[:], sig) != nil {
		return nil, errToken
	}
	var c claims
	if json.Unmarshal(body, &c) != nil {
		return nil, errToken
	}
	now := v.Now().Unix()
	if c.Iss != "accounts.google.com" && c.Iss != "https://accounts.google.com" {
		return nil, errToken
	}
	var aud string
	if json.Unmarshal(c.Aud, &aud) != nil || !v.Audiences[aud] {
		return nil, errToken
	}
	if c.Sub == "" || len(c.Sub) > 255 || c.Exp <= now-60 || c.Iat > now+60 || c.Nonce != nonce {
		return nil, errToken
	}
	return &c, nil
}

// googleKeys fetches and caches Google's JWKS.
func googleKeys(url string, client *http.Client) func() (map[string]*rsa.PublicKey, error) {
	var mu sync.Mutex
	var cache map[string]*rsa.PublicKey
	var fetched time.Time
	return func() (map[string]*rsa.PublicKey, error) {
		mu.Lock()
		defer mu.Unlock()
		if cache != nil && time.Since(fetched) < time.Hour {
			return cache, nil
		}
		resp, err := client.Get(url)
		if err != nil {
			if cache != nil {
				return cache, nil
			}
			return nil, err
		}
		defer resp.Body.Close()
		var set struct {
			Keys []struct{ Kid, N, E, Kty string } `json:"keys"`
		}
		if resp.StatusCode != 200 || json.NewDecoder(http.MaxBytesReader(nil, resp.Body, 1<<16)).Decode(&set) != nil {
			if cache != nil {
				return cache, nil
			}
			return nil, errors.New("jwks unavailable")
		}
		out := map[string]*rsa.PublicKey{}
		for _, k := range set.Keys {
			n, e := base64.RawURLEncoding.DecodeString(k.N)
			ex, e2 := base64.RawURLEncoding.DecodeString(k.E)
			if k.Kty != "RSA" || e != nil || e2 != nil || len(ex) > 4 {
				continue
			}
			exp := 0
			for _, b := range ex {
				exp = exp<<8 | int(b)
			}
			out[k.Kid] = &rsa.PublicKey{N: new(big.Int).SetBytes(n), E: exp}
		}
		cache, fetched = out, time.Now()
		return cache, nil
	}
}
