#!/usr/bin/env python3
"""One-time seeder for the Firefox slice of the Orion API test extension's corpus.

NOT part of the build. The core module's CMake target `webextensions` packages
the extension tree (external/orion-gtk/.../extensions/shared/orion-api-test) as-is;
nothing invokes this script automatically. It is a developer tool you run BY
HAND to (re)import test suites; its output under corpus/firefox/ is
committed and is the source of truth. Run it only to re-import from upstream.

What it does: Firefox's tests live on mozilla-central (mirrored at
github.com/mozilla/gecko-dev) under toolkit/components/extensions/test/xpcshell/,
embedded in the Gecko harness (ExtensionTestUtils.loadExtension({ manifest,
background() { ... } })). The extension-side background() body is portable
browser.test code. This script fetches each test_ext_*.js, extracts EVERY
background function body it defines (a file's add_task()s each load their own
little extension), sequences them as parts of one browser.test.runTests([...])
call, validates the result parses (node --check), and writes it to
src/corpus/firefox/<api>/test.js — then merges the Firefox entries into
corpus/index.json (the catalog the popup launcher and runner read).

Tests that coordinate with the harness via browser.test.sendMessage/onMessage
are flagged `nativeCoop` (they fail standalone — that failure is the point:
a TODO list). Best-effort extractor: files whose extracted bodies don't parse
are dropped (or reduced to their parseable parts) and reported.

Usage (from repo root):
    uv run python scripts/fetch_firefox_tests.py [--limit N] [--dry-run]

Requires network access to api.github.com + raw.githubusercontent.com.
Re-running OVERWRITES src/corpus/firefox/ and rewrites index.json's Firefox
entries, discarding any hand-edits to the extracted files.
"""

import argparse
import json
import re
import subprocess
import tempfile
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SRC = REPO / "external" / "orion-gtk" / "app" / "src" / "core_module" / "extensions" / "shared" / "orion-api-test"
CORPUS = SRC / "corpus"
INDEX = CORPUS / "index.json"

GH_API = ("https://api.github.com/repos/mozilla/gecko-dev/contents/"
          "toolkit/components/extensions/test/xpcshell?ref=master")

NATIVE_COOP = re.compile(r"\b(?:sendMessage|awaitMessage|onMessage)\b")
# Firefox tests define extension backgrounds in several shapes; we extract the
# body of every match (a file often has multiple add_task()s, each loading its
# own little extension):
#   function background() {...} / function backgroundScript() {...}   (named)
#   background: function (...) {...} / background: async function () {...}
#   background: () => {...} / background: async () => {...}
#   background() {...} / async background() {...}                     (method)
BG_START = re.compile(
    r"(?:"
    r"(?:async\s+)?function\s+(?:background|backgroundScript)\s*\([^)]*\)\s*\{"
    r"|background\s*:\s*(?:async\s+)?function\s*\([^)]*\)\s*\{"
    r"|background\s*:\s*(?:async\s+)?\([^)]*\)\s*=>\s*\{"
    r"|(?:async\s+)?background\s*\(\s*\)\s*\{"
    r")")


def http_get(url):
    req = urllib.request.Request(url, headers={"User-Agent": "orion-corpus-seed"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read().decode("utf-8", errors="replace")


def _matched_body(text, start):
    """Brace-match a function body starting at the opening brace; returns
    (body, end_index) or (None, start+1)."""
    i = text.index("{", start)
    depth = 0
    in_str = None
    esc = False
    j = i
    while j < len(text):
        c = text[j]
        if in_str:
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == in_str:
                in_str = None
        else:
            if c in "\"'`":
                in_str = c
            elif c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
                if depth == 0:
                    return text[i + 1:j], j + 1
        j += 1
    return None, start + 1


def extract_backgrounds(text):
    """Extract every background function body in the file (deduplicated,
    in order). A file's add_task()s each load their own extension."""
    bodies = []
    seen = set()
    pos = 0
    while True:
        m = BG_START.search(text, pos)
        if not m:
            break
        body, end = _matched_body(text, m.start())
        pos = end
        if body and body.strip():
            key = re.sub(r"\s+", " ", body).strip()
            if key not in seen:
                seen.add(key)
                bodies.append(body)
    return bodies


def node_check(src):
    """Return True if `src` parses as JS (best-effort; skips if node absent)."""
    try:
        with tempfile.NamedTemporaryFile("w", suffix=".js", delete=False, encoding="utf-8") as f:
            f.write(src)
            tmp = f.name
        r = subprocess.run(["node", "--check", tmp], capture_output=True)
        return r.returncode == 0
    except FileNotFoundError:
        return True  # node not available: don't block seeding
    finally:
        try:
            Path(tmp).unlink()
        except Exception:
            pass


def wrap(bodies):
    # Each extracted background body becomes one part of a single
    # browser.test.runTests() call: the shim gives every part its own PASS/FAIL
    # row, per-test timeout, and exception capture, and notifyPass/notifyFail
    # inside a part settles only that part. Parts came from separate little
    # extensions upstream, so shared globals are wrapped per-function scope.
    parts = []
    for i, body in enumerate(bodies, 1):
        indented = "\n".join("    " + ln for ln in body.strip("\n").splitlines())
        parts.append(f"  async function part_{i}() {{\n{indented}\n  }},")
    joined = "\n".join(parts)
    return ("\"use strict\";\n\n"
            "// Extracted from mozilla-central toolkit/components/extensions/test/\n"
            "// xpcshell (Mozilla Public License 2.0). Each part is the background()\n"
            "// body of one loadExtension() call in the upstream test file, sequenced\n"
            "// under the corpus harness. Harness handshakes (sendMessage/awaitMessage)\n"
            "// have no peer here and will fail — that failure is the TODO signal.\n\n"
            "browser.test.runTests([\n" + joined + "\n]);\n")


def load_index():
    if INDEX.exists():
        try:
            return json.loads(INDEX.read_text(encoding="utf-8"))
        except Exception:
            pass
    return []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--limit", type=int, default=0, help="max files (0 = all)")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    print("Listing xpcshell tests from gecko-dev...")
    listing = json.loads(http_get(GH_API))
    files = [f for f in listing
             if f["type"] == "file"
             and f["name"].startswith("test_ext_")
             and f["name"].endswith(".js")]
    if args.limit:
        files = files[:args.limit]
    print(f"  {len(files)} candidate test files")

    entries = []
    extracted = parts_total = skipped_nobg = skipped_invalid = 0
    for f in files:
        try:
            text = http_get(f["download_url"])
        except Exception as e:
            print(f"  fetch failed {f['name']}: {e}")
            continue
        bodies = extract_backgrounds(text)
        if not bodies:
            skipped_nobg += 1
            continue
        wrapped = wrap(bodies)
        if not node_check(wrapped):
            # Retry with only the bodies that validate individually (a single
            # malformed extraction shouldn't discard the whole file).
            good = [b for b in bodies if node_check(wrap([b]))]
            if not good:
                skipped_invalid += 1
                continue
            wrapped = wrap(good)
            bodies = good

        stem = f["name"][:-3]  # drop .js
        api = stem.replace("test_ext_", "")
        dest = CORPUS / "firefox" / api / "test.js"
        if not args.dry_run:
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(wrapped, encoding="utf-8")
        entries.append({
            "id": f"firefox/{api}",
            "group": "Firefox",
            "api": api,
            "title": f"Firefox: {api} ({len(bodies)} part{'s' if len(bodies) != 1 else ''})",
            "type": "chrometest",
            "flavors": ["firefox"],
            "minMv": 2, "maxMv": 3,
            "nativeCoop": any(NATIVE_COOP.search(b) for b in bodies),
            "scripts": [f"corpus/firefox/{api}/test.js"],
        })
        extracted += 1
        parts_total += len(bodies)

    print(f"Firefox: extracted {extracted} files / {parts_total} parts, "
          f"skipped {skipped_nobg} no-background, {skipped_invalid} invalid-after-extract")

    if not args.dry_run:
        merged = [e for e in load_index() if e.get("group") != "Firefox"] + entries
        merged.sort(key=lambda e: (e["group"], e.get("api", ""), e["id"]))
        INDEX.write_text(json.dumps(merged, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {INDEX.relative_to(REPO)} ({len(merged)} entries total)")


if __name__ == "__main__":
    main()
