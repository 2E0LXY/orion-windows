# Cross-platform development

Windows remains at the repository root until its build is reproducible. The unchanged publicly released Orion Linux source is now in `platforms/linux/` on this development branch, copied from `kagisearch/orion-linux` commit `3f041d912a86dc5d708afbba6d6d18f08f319f71`.

The independent `archive/orion-linux` branch contains an identical source snapshot in `linux/` and `source-archives/orion-linux-original.bundle`, a verified, portable Git bundle with the complete **publicly released** upstream commit history (one commit). The archive branch's commit is a new snapshot commit; original Git objects/history are preserved *inside the bundle*, not through this repository branch's ancestry. All 2,504 upstream files match by Git blob SHA and file mode across both copies, including original `LICENSE`, `NOTICE`, `.gitmodules` and `.github/workflows/core-module.yml`.

To restore the original repository and its public Git history after cloning the archive branch:

```sh
sha256sum -c source-archives/orion-linux-original.bundle.sha256
git clone -b original-orion-linux source-archives/orion-linux-original.bundle restored-orion-linux
git -C restored-orion-linux rev-parse HEAD
# Expected: 3f041d912a86dc5d708afbba6d6d18f08f319f71
```

The bundle's SHA-256 is `09d0852dffb1813ec7e1f9d8c59226893439a5f83842fa5f888550cfff6d4c68`. An additional independent VPS mirror and bundle live under `/root/source-archives/`.

**Source preservation is not build integration or a security clearance.** The upstream `.gitmodules` lists eleven dependencies but embeds no pinned gitlink objects in its published main tree. Those dependencies are not copied into our archive and require independent provenance/revision review. The existing Windows `external/orion-gtk` submodule is a separate integration and should be audited rather than blindly repointed.

Android is a proposed new Kotlin/Jetpack Compose application; evaluate GeckoView as an initial engine. Keep platform-specific engines isolated. Share portable browser settings, bookmarks and privacy policies only after testing; no default telemetry or sync service.

## Release gates

- Independently review Windows and Linux source, network endpoints, credentials, TLS, update and build mechanisms.
- Verify original published revision, licence notices and all external dependency integrity.
- Resolve missing original Windows WebKit source or document and review the replacement.
- Establish separate clean Linux, Windows and Android CI builds and real runtime tests.
- No executable distribution before security review and explicit release approval.

Repository rename and Windows directory migration are deferred until existing build paths and open pull requests are reconciled.
