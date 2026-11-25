#!/bin/bash
# Follow the vcpkg WebKit build logs of the current triplet while vcpkg runs.
# Runs from Git for Windows bash or from WSL.
#
#   ./vcpkg_log.sh [-t seconds] [-n lines]
#
# Streams the configure + debug/release build logs (stdout and stderr) of
# ports/webkit-win with clang errors/warnings colorized. Exits once the logs
# have been quiet for -t seconds (default 30) AND no vcpkg.exe is running, so
# a long silent link step does not end the session while a build is alive,
# and viewing the logs of a finished build still returns promptly.

set -u

TRIPLET=x64-windows
LOG_DIR=external/vcpkg/buildtrees/webkit-win

idle=30
lines=10
while getopts "t:n:" opt; do
    case $opt in
        t) idle=$OPTARG ;;
        n) lines=$OPTARG ;;
        *) echo "Usage: $0 [-t idle-seconds] [-n initial-lines]" >&2; exit 1 ;;
    esac
done

cd "$(dirname "${BASH_SOURCE[0]}")" || exit 1

# The logs vcpkg writes for this port, in the order the phases run. Older
# triplets (x64-windows-webkit-*) leave stale logs in the same directory;
# a glob would tail those too and show last June's errors first.
logs=(
    "$LOG_DIR/config-$TRIPLET-out.log"
    "$LOG_DIR/config-$TRIPLET-err.log"
    "$LOG_DIR/build-$TRIPLET-dbg-out.log"
    "$LOG_DIR/build-$TRIPLET-dbg-err.log"
    "$LOG_DIR/build-$TRIPLET-rel-out.log"
    "$LOG_DIR/build-$TRIPLET-rel-err.log"
)
existing=()
for f in "${logs[@]}"; do [[ -e $f ]] && existing+=("$f"); done
if [[ ${#existing[@]} -eq 0 ]]; then
    echo "$0: no $TRIPLET logs in $LOG_DIR yet (is a webkit-win build running?)" >&2
    exit 1
fi

# --- platform shims: Git for Windows bash vs WSL (C: at /c or /mnt/c) ---

# tasklist.exe is on PATH in both (WSL: interop appendWindowsPath); the
# explicit paths cover a WSL with that turned off. MSYS_NO_PATHCONV stops
# Git Bash from turning /FI into a path; WSL ignores the variable.
for t in tasklist.exe /c/Windows/System32/tasklist.exe /mnt/c/Windows/System32/tasklist.exe; do
    if command -v "$t" >/dev/null 2>&1; then TASKLIST=$t; break; fi
done
vcpkg_running() {
    [[ -n ${TASKLIST:-} ]] || return 1
    # tasklist prints "INFO: No tasks are running..." when nothing matches;
    # the filter is an exact image-name match, so VS's vcpkgsrv.exe is excluded.
    MSYS_NO_PATHCONV=1 "$TASKLIST" /FI "IMAGENAME eq vcpkg.exe" 2>/dev/null | grep -q '^vcpkg.exe'
}

# GNU tail follows 9p/drvfs mounts by polling on its own, and Cygwin has no
# inotify at all; ---disable-inotify just makes that explicit. Ubuntu 25.10+
# links `tail` to uutils, which prints NOTHING for these files once any
# polling flag is given (-F alone works), so prefer the GNU binary Ubuntu
# keeps as `gnutail` and otherwise adapt the flags to whatever `tail` is.
if command -v gnutail >/dev/null 2>&1; then TAIL=gnutail; else TAIL=tail; fi
if "$TAIL" --version 2>/dev/null | head -n 1 | grep -q uutils; then
    follow=(-F)
else
    follow=(-F ---disable-inotify)
fi

# Per line in bash rather than piping through awk: a pipe would run the read
# loop in a subshell (breaking the trap/cleanup below) and awk block-buffers
# when stdout is not a terminal.
print_line() {
    case $1 in
        *error:*|*FAILED:*) printf '\033[1;31m%s\033[0m\n' "$1" ;;
        *warning:*)         printf '\033[1;33m%s\033[0m\n' "$1" ;;
        *)                  printf '%s\n' "$1" ;;
    esac
}

# -F follows by name: vcpkg truncates these logs at the start of every phase
# and sometimes deletes/recreates them, which a plain -f would follow into the
# void.
exec {fd}< <("$TAIL" -n "$lines" "${follow[@]}" -- "${logs[@]}" 2>&1)
tail_pid=$!
cleanup() {
    kill "$tail_pid" 2>/dev/null
    wait "$tail_pid" 2>/dev/null
    exec {fd}<&-
}
trap 'cleanup; exit 130' INT TERM

while :; do
    if IFS= read -t "$idle" -r -u "$fd" line; then
        print_line "$line"
    elif (( $? > 128 )); then
        # Idle. Keep waiting while vcpkg is alive (a long link step is silent);
        # otherwise the build is over and so are we.
        vcpkg_running || break
    else
        break   # tail exited
    fi
done

cleanup
