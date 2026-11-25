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

# Format the whole solution in a single workspace load (faster than looping per
# .csproj, which reloads MSBuild+Roslyn for each project). Loading the solution
# also enumerates its non-C# members (.vcxproj/.wapproj), which the dotnet CLI's
# MSBuild can't open and reports as "Cannot open project ... is not associated
# with a language." Those are harmless (non-fatal, exit 0), so filter them out.
# (The Microsoft.Cpp.* import errors from following Orion.csproj's native
# ProjectReferences are separately avoided by the MSBuildRuntimeType guard in
# Orion/Orion.csproj, so this single filter is sufficient.)
NOISE="Cannot open project .* is not associated with a language\."

# Run dotnet format, streaming output through the filter while preserving the
# REAL exit code (a pipe's status would be grep's, not dotnet format's).
run_format() {
    st_file=$(mktemp)
    { $DOTNET format Orion.slnx "$@" 2>&1; echo $? > "$st_file"; } \
        | grep -v --line-buffered "$NOISE" || true
    st=$(cat "$st_file")
    rm -f "$st_file"
    return "$st"
}

if [ "$1" = "--verify" ]; then
    echo "Running dotnet format verify: Orion.slnx"
    # --no-restore: skip the implicit restore (build cache is warm), saves ~7s.
    run_format --verify-no-changes --no-restore --verbosity diagnostic
else
    echo "Running dotnet format: Orion.slnx"
    run_format --verbosity diagnostic
fi
