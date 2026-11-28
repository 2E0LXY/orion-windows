// Orion Connect server (phase 1): Google ID-token login, device sessions, opt-in encrypted sync categories.
// Development status: synthetic accounts only; no production deployment.
package main

import (
	"log"
	"net/http"
	"os"
	"strings"
	"time"
)

func main() {
	audiences := map[string]bool{}
	for _, a := range strings.Split(os.Getenv("ORION_GOOGLE_AUDIENCES"), ",") {
		if a = strings.TrimSpace(a); a != "" {
			audiences[a] = true
		}
	}
	if len(audiences) == 0 {
		log.Fatal("set ORION_GOOGLE_AUDIENCES to the Google web client ID(s)")
	}
	dbPath := os.Getenv("ORION_DB")
	if dbPath == "" {
		dbPath = "orion-connect.db"
	}
	addr := os.Getenv("ORION_ADDR")
	if addr == "" {
		addr = "127.0.0.1:8788"
	}
	db, err := openStore(dbPath)
	if err != nil {
		log.Fatal(err)
	}
	v := &Verifier{Audiences: audiences, Now: time.Now, Keys: googleKeys("https://www.googleapis.com/oauth2/v3/certs", &http.Client{Timeout: 10 * time.Second})}
	s := NewServer(db, v, os.Getenv("ORION_ENABLE_SENSITIVE") == "1")
	srv := &http.Server{Addr: addr, Handler: s.Handler(), ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 15 * time.Second, WriteTimeout: 20 * time.Second, IdleTimeout: 60 * time.Second, MaxHeaderBytes: 8192}
	log.Printf("orion-connect listening on %s", addr)
	log.Fatal(srv.ListenAndServe())
}
