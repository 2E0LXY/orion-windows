package main

import (
	"database/sql"
	"fmt"

	_ "modernc.org/sqlite"
)

const schema = `
CREATE TABLE IF NOT EXISTS accounts(
 id INTEGER PRIMARY KEY, iss TEXT NOT NULL, sub TEXT NOT NULL, created INTEGER NOT NULL, seq INTEGER NOT NULL DEFAULT 0,
 UNIQUE(iss,sub));
CREATE TABLE IF NOT EXISTS devices(
 id TEXT PRIMARY KEY, account INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
 name TEXT NOT NULL, platform TEXT NOT NULL, pubkey TEXT NOT NULL DEFAULT '',
 paired INTEGER NOT NULL DEFAULT 0, revoked INTEGER NOT NULL DEFAULT 0, created INTEGER NOT NULL, last_seen INTEGER NOT NULL);
CREATE INDEX IF NOT EXISTS devices_account ON devices(account);
CREATE TABLE IF NOT EXISTS sessions(
 hash TEXT PRIMARY KEY, device TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE, expires INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS records(
 account INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE, category TEXT NOT NULL, id TEXT NOT NULL,
 seq INTEGER NOT NULL, lamport INTEGER NOT NULL, deleted INTEGER NOT NULL, device TEXT NOT NULL,
 nonce TEXT NOT NULL, blob TEXT NOT NULL, updated INTEGER NOT NULL,
 PRIMARY KEY(account,category,id));
CREATE INDEX IF NOT EXISTS records_seq ON records(account,category,seq);
CREATE TABLE IF NOT EXISTS used_nonces(hash TEXT PRIMARY KEY, expires INTEGER NOT NULL);
`

func openStore(path string) (*sql.DB, error) {
	db, err := sql.Open("sqlite", fmt.Sprintf("file:%s?_pragma=journal_mode(WAL)&_pragma=busy_timeout(5000)&_pragma=foreign_keys(1)&_pragma=synchronous(NORMAL)", path))
	if err != nil {
		return nil, err
	}
	db.SetMaxOpenConns(4)
	if _, err := db.Exec(schema); err != nil {
		db.Close()
		return nil, err
	}
	return db, nil
}
