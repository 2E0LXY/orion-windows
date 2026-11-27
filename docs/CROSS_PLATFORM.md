# Cross-platform development

Windows remains at the existing repository root until its build is reproducible. Linux integration should preserve upstream history, NOTICE and LICENSE, initially using the existing external/orion-gtk submodule after verifying its pinned revision and nested dependencies. Upstream Linux candidate: https://github.com/kagisearch/orion-linux at 3f041d912a86dc5d708afbba6d6d18f08f319f71 (not yet imported or audited).

Android is a new Kotlin/Jetpack Compose application; evaluate GeckoView as the initial engine. Keep engine-specific code isolated. Share only tested portable settings schemas, bookmark formats and privacy policies initially. No sync service or telemetry by default.

## Release gates

- Independently review Windows and Linux code, network endpoints, credentials, TLS, updates, dependencies and build scripts.
- Verify upstream revisions, licence obligations and dependency integrity.
- Resolve missing original Windows WebKit source or document and review the replacement.
- Separate CI per platform; test clean builds and runtime behaviour before releases.
- No executable distribution before security review and release approval.

The repository rename and directory migration are deferred until build paths and open PRs have been reviewed. This document is a plan, not an implementation.
