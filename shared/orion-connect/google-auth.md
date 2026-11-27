# Google authentication for Orion Connect

Use Google OpenID Connect with system browser authorization, authorization code flow and PKCE S256. Register platform-specific OAuth clients; never embed a confidential client secret in an application. Android uses a registered app identity and supported native authentication flow; desktop uses loopback redirect. Validate issuer, audience, expiry, nonce and state; identify accounts by issuer and subject, not email. Server issues revocable device-scoped sessions. Never collect Google passwords or use embedded WebView for authentication.

Google authentication does not grant access to Chrome Sync or serve as an encryption key. Separate device pairing and audited end-to-end encryption are required. Start with opt-in bookmarks and settings, then link delivery and tabs. History optional; passwords disabled pending independent security review. Exclude private sessions, cookies and tokens. Account deletion, device revocation and recovery must be supported.

QuickHost VPS1: Go HTTPS API, SQLite WAL, optional Cloudflare, bounded storage and requests. Use synthetic accounts/data for initial tests. No production credentials or live data until review. Google Cloud OAuth project, consent screen, client IDs, verified redirect configuration and server hostname are outstanding. No authentication or sync is operational yet.
