#!/bin/sh -e

cd "$(dirname "$0")/.."

if command -v dotnet >/dev/null 2>&1; then
    DOTNET=dotnet
elif command -v dotnet.exe >/dev/null 2>&1; then
    DOTNET=dotnet.exe
else
    echo "ERROR: dotnet not found" >&2
    exit 1
fi

files=$(git ls-files -- '*.xaml' | paste -sd,)

if [ "$1" = "--verify" ]; then
    echo "Running XAML Styler format verify..."
    # xstyler --passive's exit code is unreliable (as of 3.2501.8 it has been
    # observed returning 0 on failing single files but nonzero on failing
    # in-repo file lists), so honor the exit code OR a FAIL line in the
    # output — and always echo the output so the failing files are visible.
    status=0
    out=$($DOTNET xstyler --passive -f "$files") || status=$?
    printf '%s\n' "$out"
    if [ "$status" -ne 0 ] || printf '%s\n' "$out" | grep -q "FAIL"; then
        exit 1
    fi
else
    echo "Running XAML Styler..."
    $DOTNET xstyler -f "$files"
fi
