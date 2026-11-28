# Orion Connect server (phase 1)

Status: development only. Synthetic accounts and tests only; no production deployment, credentials or live data.

Go + SQLite (pure Go driver, WAL). Behind Caddy for TLS in any real deployment.

| Area | Behaviour |
|---|---|
| Login | `POST /v1/auth/google`: Google ID token (RS256, JWKS) checked for issuer, audience, expiry, issued-at, nonce. Nonce is single use. Account key is `(iss, sub)`, never email. |
| Sessions | Opaque device-scoped token, stored hashed, 30 day sliding expiry, revocable per device, logout, account deletion removes all rows. |
| Sync | `GET/PUT /v1/sync/{category}`. Server stores only ciphertext + nonce. Per-account cursor, tombstones, last-writer-wins on `(lamport, device_id)`; stale writes return `conflicts`. |
| Categories | `bookmarks settings readinglist tabs history`. `passwords` and `cookies` need `ORION_ENABLE_SENSITIVE=1` AND a device that declared `encrypted:true` at login (it holds the account data key and encrypts client-side). |
| Limits | 256 kB request, 100 records per batch, 64 kB per record, 50k rows / 50 MB per account, 120 req/min per device, 10 logins/min per IP, 20 devices per account. |

Key model (owner decision): a new device logs in with the same Google account and fetches the account data key from the user's own Google Drive app-data folder (scope `drive.appdata`); the server never sees the key. Anyone with the Google account can therefore read the key. Not in this phase: the Android client, client-side encryption code, push pages (FCM), Drive backup.

Environment: `ORION_GOOGLE_AUDIENCES` (required, comma-separated Google web client IDs), `ORION_DB`, `ORION_ADDR` (default `127.0.0.1:8788`), `ORION_ENABLE_SENSITIVE`.

Test: `go test -race ./...`
