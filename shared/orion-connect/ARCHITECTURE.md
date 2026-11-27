# Orion Connect
Status: proposed shared architecture; no production sync enabled.

Clients: Windows, Linux, Android. Optional self-hosted HTTPS endpoint; no mandatory Cloudflare or Kagi account. Pair devices with expiring single-use invitations and explicit approval. Device revocation, scoped authentication, replay protection and rate limits required.

Sync categories: links, bookmarks, settings, tabs, reading lists, history and eventually passwords/autofill. Each independently opt-in; private sessions excluded. Incoming links require user confirmation. Versioned records, stable IDs, tombstones, incremental cursors and deterministic conflict resolution. Never sync cookies or session tokens.

Security: audited end-to-end encryption protocol using established libraries, authenticated device keys, encrypted backups and explicit recovery. Password sync disabled until independent review and device tests. Server cannot decrypt payloads. Platform key storage: DPAPI, libsecret, Android Keystore. No secrets in logs. Account and device deletion required.

Initial deployment: dedicated QuickHost VPS1 (planning assumption 1 vCPU, 2 GB RAM, 40 GB disk), Debian, Go, SQLite WAL, HTTPS and WebSocket notifications. Cloudflare optional reverse proxy/tunnel; direct TLS supported. Off-site encrypted backups. Bound queues, request sizes, storage and connection limits. No browsing proxy.

Capacity: benchmark 100, 500, 1000 and 5000 active users with realistic concurrent devices and reconnect bursts. Monitor CPU, RAM, disk, p95 latency, failures and database contention. Initial alert thresholds 70% sustained CPU, 75% RAM, 70% disk, 250ms p95 and 1% failures; calibrate from tests. No guaranteed user capacity.

Implementation order: threat model and protocol tests; pairing; encrypted link delivery; bookmarks; settings; remaining categories; independently reviewed password sync. Cross-platform interoperability tests before rollout. No production deployment or release approval implied.
