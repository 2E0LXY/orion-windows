1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Fetch and serve web-platform-tests (WPT) locally.
#
# Usage:
#   scripts\wpt_run.py.cmd                Clone wpt (if needed) into vendor/wpt
#                                         and start `wpt serve`; prints links
#                                         to open tests.
#   scripts\wpt_run.py.cmd <wpt args...>  Pass through to the wpt CLI inside
#                                         the clone, e.g.:
#                                           scripts\wpt_run.py.cmd run chrome web-extensions
#                                           scripts\wpt_run.py.cmd make-hosts-file
#
# The vendor/wpt clone is temporary tooling (vendor/ is gitignored); delete it
# to reclaim ~1 GB. Anything that changes the system (hosts file entries,
# browser downloads) asks for confirmation first.

import os
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
from pathlib import Path

WPT_DIR = Path("vendor") / "wpt"
WPT_REPO = "https://github.com/web-platform-tests/wpt.git"
HOSTS_PATH = Path(os.environ.get("SystemRoot", r"C:\Windows")) / "System32" / "drivers" / "etc" / "hosts"
SERVE_URL = "http://localhost:8000"

_wpt_python = None


def wpt_python():
    """Interpreter used to run the wpt CLI (which bootstraps its own venv
    from it). uv's project Python (3.14) breaks that venv: its pip3.exe shim
    exits 1 silently and some wpt deps (aioquic) lack 3.14 wheels — so prefer
    a system CPython <= 3.13 via the Windows launcher."""
    global _wpt_python
    if _wpt_python:
        return _wpt_python
    py = shutil.which("py")
    if py:
        for ver in ("3.13", "3.12", "3.11"):
            r = subprocess.run(
                [py, f"-{ver}", "-c", "import sys; print(sys.executable)"],
                capture_output=True, text=True,
            )
            if r.returncode == 0 and r.stdout.strip():
                _wpt_python = r.stdout.strip()
                return _wpt_python
    print("warning: no system CPython <= 3.13 found via py launcher; "
          "using uv's Python (wpt venv bootstrap may fail)", file=sys.stderr)
    _wpt_python = sys.executable
    return _wpt_python


def ask(question):
    """Ask a yes/no question; EOF/non-interactive counts as no."""
    try:
        return input(f"{question} [y/N] ").strip().lower() in ("y", "yes")
    except EOFError:
        print("(non-interactive: assuming no)")
        return False


def ensure_clone():
    if (WPT_DIR / "wpt").exists():
        return
    if not shutil.which("git"):
        sys.exit("git not found on PATH; install git first.")
    print(f"Cloning {WPT_REPO} (shallow, several hundred MB) into {WPT_DIR} ...")
    WPT_DIR.parent.mkdir(exist_ok=True)
    subprocess.run(
        ["git", "clone", "--depth", "1", WPT_REPO, str(WPT_DIR)],
        check=True,
    )


def wpt_cli(args, **kwargs):
    """Run the wpt CLI inside the clone."""
    return subprocess.run(
        [wpt_python(), "wpt", *args], cwd=WPT_DIR, **kwargs
    )


def hosts_configured():
    try:
        return "web-platform.test" in HOSTS_PATH.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False


def offer_hosts_setup():
    """wpt serve refuses to start unless *.web-platform.test resolves, so the
    hosts entries are a hard requirement. Explain exactly what will change,
    then offer to append the entries via an elevated process.

    (Alternatives considered and rejected: Windows' resolver does not resolve
    *.localhost at the getaddrinfo level, so a .localhost config is dead on
    arrival; public wildcard-loopback DNS like nip.io works without admin but
    needs internet, breaks behind DNS-rebinding-protecting routers, and
    diverges from upstream host expectations; a local DNS server is a bigger
    system change than a delimited hosts block.)"""
    if hosts_configured():
        return True

    generated = wpt_cli(
        ["make-hosts-file"], capture_output=True, text=True, check=True
    ).stdout
    entries = [ln for ln in generated.splitlines() if ln and not ln.startswith("#")]
    tmp = Path(tempfile.gettempdir()) / "wpt_hosts_entries.txt"
    tmp.write_text(generated, encoding="ascii")
    add_content = f'Add-Content -Path "{HOSTS_PATH}" -Value (Get-Content "{tmp}")'

    print(f"""
================================ HOSTS FILE SETUP =================================
wpt serve cannot start yet: its test hostnames (web-platform.test and
subdomains) must resolve on this machine, and the system hosts file has no
entries for them.

What this script would like to do:
  - Append {len(entries)} lines to {HOSTS_PATH}
  - Every line maps a WPT test hostname to a loopback/blackhole address
    (127.0.0.1, or 0.0.0.0 for intentionally-unresolvable test names).
    Nothing else on the system is affected: only lookups of these exact
    names change, and they never leave this machine.
  - The block is delimited by '# Start web-platform-tests hosts' /
    '# End web-platform-tests hosts' markers. To undo, delete that block.
  - Full content to be appended has been written for review to:
      {tmp}

Why a UAC prompt is needed:
  The hosts file is a protected system file writable only by Administrators.
  If you accept, an elevated PowerShell runs exactly one command:
      {add_content}
  Nothing else runs elevated.
===================================================================================""")
    if not ask("Proceed (a UAC prompt will appear)?"):
        print("Not touching the hosts file. To set it up manually, append the")
        print(f"contents of {tmp} (or the output of")
        print("'scripts\\wpt_run.py.cmd make-hosts-file') to the hosts file as")
        print("administrator, then re-run this script.")
        return False

    ps = (
        f"Start-Process powershell -Verb RunAs -Wait -ArgumentList "
        f"'-NoProfile -Command {add_content}'"
    )
    subprocess.run(["powershell.exe", "-NoProfile", "-Command", ps], check=True)
    if hosts_configured():
        print("Hosts file updated.")
        return True
    print("Hosts file still missing the entries (UAC declined or the elevated")
    print("command failed). Cannot serve without them.")
    return False


def wait_for_server(timeout_s=90):
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        try:
            urllib.request.urlopen(SERVE_URL, timeout=2)
            return True
        except Exception:
            time.sleep(1)
    return False


def serve():
    if not offer_hosts_setup():
        return 1
    print()
    print("Starting wpt serve (first run bootstraps a virtualenv; may take a minute)...")
    proc = subprocess.Popen([wpt_python(), "wpt", "serve"], cwd=WPT_DIR)
    try:
        if wait_for_server():
            base = "http://web-platform.test:8000"
            print()
            print("=" * 64)
            print("WPT server is up. Open tests in any browser (including Orion):")
            print(f"  Test index:        {base}/")
            print(f"  WebExtensions:     {base}/web-extensions/")
            print(f"  Legacy runner UI:  {base}/tools/runner/index.html")
            print()
            print("Note: web-extensions/*.extension.html pages need WebDriver")
            print("(testdriver) to install the extension, so in a plain browser")
            print("they will time out. The raw test extensions live in")
            print("  vendor/wpt/web-extensions/resources/<api>/ (manifest + background.js)")
            print("and can be loaded/installed directly by a browser instead.")
            print("=" * 64)
            print("Ctrl+C to stop.")
        else:
            print("Server did not come up within 90s; check the output above.", file=sys.stderr)
        proc.wait()
    except KeyboardInterrupt:
        pass
    finally:
        if proc.poll() is None:
            proc.terminate()
    return proc.returncode or 0


def main():
    ensure_clone()
    args = sys.argv[1:]
    if not args or args == ["serve"]:
        sys.exit(serve())
    # Pass-through mode. Commands that download browsers/drivers ask first.
    if args[0] == "run" and "--install-browser" in args:
        if not ask(f"'wpt {' '.join(args)}' will download a browser build. Proceed?"):
            sys.exit(1)
    sys.exit(wpt_cli(args).returncode)


if __name__ == "__main__":
    main()
