1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Legacy entrypoint retained for existing scripts. The WebKit vcpkg port now
# fetches its pinned source directly from the public upstream Git repository.
# There is no need for gcloud, access tokens, or a private source archive.

from pathlib import Path
import re
import sys


def main() -> int:
    ref = Path("ports/webkit-win/WEBKIT_REF").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"[0-9a-f]{40}", ref):
        print("WEBKIT_REF must contain a 40-character commit SHA", file=sys.stderr)
        return 1
    print(f"WebKit source is fetched by vcpkg from upstream at {ref}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
