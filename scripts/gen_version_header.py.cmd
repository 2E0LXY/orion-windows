1>2# : vim: set ft=python: ^
'''
@echo off & cd %~dp0.. & bin\\uv.exe run python "%~dpf0" %* & exit /b ::'''

# Generates a C header defining ORION_VERSION from the plain-text version
# file: gen_version_header.py.cmd <version-file> <output-header>
import sys
from pathlib import Path

version = Path(sys.argv[1]).read_text().strip()
out = Path(sys.argv[2])
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(f'#pragma once\n#define ORION_VERSION "{version}"\n')
