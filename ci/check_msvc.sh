#!/bin/bash
set -e
VS_TARGETPATH="${VS_TARGETPATH:-/c/Program Files/Microsoft Visual Studio/18/Community}"
if ! [ -f "$VS_TARGETPATH/VC/Tools/MSVC/$(cat MSVC_VERSION)/bin/Hostx64/x64/cl.exe" ]; then
  echo "MSVC tools $(cat MSVC_VERSION) not found! Builder image needs to be updated." >&2
  exit 1
fi
