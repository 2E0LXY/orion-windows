"""Load Visual Studio environment variables into the current Python process."""

import os
import subprocess
from pathlib import Path


def ensure_vsenv():
    """Run vsenv.bat and import the resulting environment variables.

    Skips if VSCMD_VER is already set (matching the guard in vsenv.bat).
    """
    if os.environ.get("VSCMD_VER"):
        return

    root = Path(os.getcwd())
    vsenv_bat = root / "vsenv.bat"
    if not vsenv_bat.exists():
        print(f"WARNING: {vsenv_bat} not found, skipping VS env setup")
        return

    # Run vsenv.bat in a child cmd, then dump all env vars.
    # Use shell=True so cmd.exe handles the call/&& syntax directly.
    result = subprocess.run(
        f'call "{vsenv_bat}" && set',
        capture_output=True,
        text=True,
        shell=True,
    )
    if result.returncode != 0:
        print(f"WARNING: vsenv.bat failed:\n{result.stderr}")
        return

    # Parse KEY=VALUE lines and apply to os.environ.
    for line in result.stdout.splitlines():
        eq = line.find("=")
        if eq > 0:
            key = line[:eq]
            value = line[eq + 1:]
            os.environ[key] = value
