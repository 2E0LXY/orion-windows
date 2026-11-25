#!/usr/bin/env python3
"""One-time seeder for the Chromium slice of the Orion API test extension's corpus.

NOT part of the build. The core module's CMake target `webextensions` packages
the extension tree (external/orion-gtk/.../extensions/shared/orion-api-test) as-is;
nothing invokes this script automatically. It is a developer tool you run BY
HAND to (re)import test suites; its output under corpus/ is committed and
is the source of truth. Run it only to re-import from upstream.

What it does: walks the locally vendored Chromium extension test tree
(vendor/cefs/.../chrome/test/data/extensions/api_test) and, for every suite
with a runnable background entry point, copies that suite's background
script(s) into the extension's corpus/chromium/<api>/<suite>/ and
regenerates the Chromium entries in corpus/index.json (the catalog the popup
launcher and runner read). Suites that need a native test server
(chrome.test.getConfig().testServer) or a native message peer
(chrome.test.sendMessage) are imported anyway and flagged `nativeCoop` — they
fail in the in-page harness on purpose; that failure is the TODO list.

Local-only (no network). Re-running OVERWRITES src/corpus/chromium/ and
rewrites index.json's Chromium entries, discarding any hand-edits to the
copied files, so treat those edits as source and re-run only to reset.

Usage (from repo root):
    uv run python scripts/seed_corpus_tests.py --chromium [--dry-run]
    uv run python scripts/seed_corpus_tests.py --firefox  [--dry-run]
    uv run python scripts/seed_corpus_tests.py --all
"""

import argparse
import json
import re
import shutil
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SRC = REPO / "external" / "orion-gtk" / "app" / "src" / "core_module" / "extensions" / "shared" / "orion-api-test"
CORPUS = SRC / "corpus"
INDEX = CORPUS / "index.json"

CHROMIUM_ROOT = (REPO / "vendor" / "cefs" / "chromium" / "src" /
                 "chrome" / "test" / "data" / "extensions" / "api_test")

# Background scripts referencing these need some native cooperation (a test
# server via getConfig().testServer, or a C++ message peer via sendMessage).
# We still IMPORT them -- most are meant to fail here and that failure is the
# point (a TODO list for API work) -- but flag them so the launcher can mark
# them. sendMessage/sendScriptResult throw in the shim; getConfig is stubbed.
NATIVE_COOP = re.compile(
    r"\b(?:getConfig|sendMessage|sendScriptResult|waitForRoundTrip)\b")


def read_manifest(mdir):
    try:
        return json.loads((mdir / "manifest.json").read_text(encoding="utf-8"))
    except Exception:
        return None


def background_scripts(manifest):
    """Return the list of background script filenames a suite runs, or None if
    it has no runnable background entry point (page/popup-driven suites)."""
    bg = manifest.get("background") or {}
    if isinstance(bg, dict):
        if "scripts" in bg and isinstance(bg["scripts"], list):
            return list(bg["scripts"])
        if "service_worker" in bg:
            return [bg["service_worker"]]
    return None


def import_chromium(dry_run):
    if not CHROMIUM_ROOT.exists():
        print(f"Chromium tree not found at {CHROMIUM_ROOT}; skipping.")
        return []

    entries = []
    kept = native = skipped_nobg = skipped_missing = 0

    for manifest_path in sorted(CHROMIUM_ROOT.rglob("manifest.json")):
        mdir = manifest_path.parent
        manifest = read_manifest(mdir)
        if manifest is None:
            continue
        scripts = background_scripts(manifest)
        if not scripts:
            # Page/content-script/tab-driven suites have no entry point our
            # in-page runner can load; those need more harness than we have.
            skipped_nobg += 1
            continue

        script_paths = [mdir / s for s in scripts]
        if not all(p.exists() for p in script_paths):
            skipped_missing += 1
            continue

        blob = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in script_paths)
        native_coop = bool(NATIVE_COOP.search(blob))
        if native_coop:
            native += 1

        rel = mdir.relative_to(CHROMIUM_ROOT)  # e.g. alarms/count
        api = rel.parts[0]
        dest = CORPUS / "chromium" / rel
        mv = manifest.get("manifest_version", 2)

        if not dry_run:
            dest.mkdir(parents=True, exist_ok=True)
            for s, p in zip(scripts, script_paths):
                out = dest / s
                out.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(p, out)

        entries.append({
            "id": "chromium/" + rel.as_posix(),
            "group": "Chromium",
            "api": api,
            "title": f"Chromium: {rel.as_posix()}",
            "type": "chrometest",
            "flavors": ["chrome"],
            "minMv": mv,
            "maxMv": mv,
            "nativeCoop": native_coop,
            "scripts": [f"corpus/chromium/{rel.as_posix()}/{s}" for s in scripts],
        })
        kept += 1

    print(f"Chromium: imported {kept} ({native} flagged native-coop), "
          f"skipped {skipped_nobg} no-background, {skipped_missing} missing-scripts")
    return entries


def load_existing_index():
    if INDEX.exists():
        try:
            return json.loads(INDEX.read_text(encoding="utf-8"))
        except Exception:
            pass
    return []


def write_index(new_entries, replace_groups):
    """Merge: keep index entries whose group is NOT in replace_groups, then add
    the freshly imported ones. Lets --chromium and --firefox run independently."""
    existing = [e for e in load_existing_index() if e.get("group") not in replace_groups]
    merged = existing + new_entries
    merged.sort(key=lambda e: (e["group"], e.get("api", ""), e["id"]))
    INDEX.write_text(json.dumps(merged, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {INDEX.relative_to(REPO)} ({len(merged)} entries total)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--chromium", action="store_true")
    ap.add_argument("--firefox", action="store_true")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    if args.all:
        args.chromium = args.firefox = True

    if args.chromium:
        entries = import_chromium(args.dry_run)
        if not args.dry_run:
            write_index(entries, replace_groups={"Chromium"})

    if args.firefox:
        # Firefox import is fetched separately (see fetch_firefox_tests.py).
        print("Firefox import handled by fetch_firefox_tests.py (network).")


if __name__ == "__main__":
    main()
