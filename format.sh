#!/bin/sh
# Format (default) or verify formatting of the C#/XAML sources.
#
# Usage:
#   format.sh                     Reformat in place (xstyler + dotnet format).
#   format.sh --verify            Check only; nonzero exit + message if files
#                                 are not formatted. Changes nothing.
#   format.sh --filter-commits [<range>...]
#                                 Only proceed if the given commit ranges touch
#                                 formatting-relevant files (*.cs, *.xaml,
#                                 *.csproj, *.props, *.targets, .editorconfig,
#                                 .globalconfig, dotnet-tools.json); otherwise
#                                 exit 0 immediately. With no <range> args,
#                                 reads "local_ref local_sha remote_ref
#                                 remote_sha" lines from stdin (the pre-push
#                                 hook protocol). --filter-git is an alias.
#
# The pre-push hook installed by scripts/install_git_pre-push_hook_formatter.bat
# runs: ./format.sh --verify --filter-commits

set -e
cd "$(dirname "$0")"

VERIFY=0
FILTER=0
RANGES=""

while [ $# -gt 0 ]; do
    case "$1" in
        --verify) VERIFY=1 ;;
        --filter-commits|--filter-git) FILTER=1 ;;
        -h|--help)
            sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        -*)
            echo "format.sh: unknown option: $1" >&2
            exit 2
            ;;
        *)
            if [ "$FILTER" -eq 1 ]; then
                RANGES="$RANGES $1"
            else
                echo "format.sh: unexpected argument: $1 (ranges require --filter-commits)" >&2
                exit 2
            fi
            ;;
    esac
    shift
done

# Does this file path affect C#/XAML formatting or its configuration?
is_relevant_file() {
    case "$1" in
        *.cs|*.xaml|*.csproj|*.props|*.targets) return 0 ;;
        *.editorconfig|*.globalconfig) return 0 ;;          # also matches bare .editorconfig
        dotnet-tools.json|*/dotnet-tools.json) return 0 ;;  # pinned formatter tool versions
    esac
    return 1
}

# List files changed in a ref update, tolerating shallow/unknown remote SHAs
# and brand-new branches (remote sha = all zeros).
ZERO=0000000000000000000000000000000000000000
changed_files_for_update() {
    local_sha=$1; remote_sha=$2
    if [ "$remote_sha" != "$ZERO" ] && git rev-parse --quiet --verify "$remote_sha^{commit}" >/dev/null 2>&1; then
        git diff --name-only "$remote_sha" "$local_sha"
    else
        # New branch (or unknown base): every commit not already on a remote.
        git rev-list "$local_sha" --not --remotes \
            | git diff-tree --stdin --no-commit-id --name-only -r
    fi
}

if [ "$FILTER" -eq 1 ]; then
    CHANGED=""
    if [ -n "$RANGES" ]; then
        for range in $RANGES; do
            CHANGED="$CHANGED
$(git diff --name-only "$range")"
        done
    else
        # pre-push protocol on stdin: <local_ref> <local_sha> <remote_ref> <remote_sha>
        while read -r _local_ref local_sha _remote_ref remote_sha; do
            [ -z "$local_sha" ] && continue
            [ "$local_sha" = "$ZERO" ] && continue   # branch deletion: nothing to check
            CHANGED="$CHANGED
$(changed_files_for_update "$local_sha" "$remote_sha")"
        done
    fi

    RELEVANT=0
    for f in $CHANGED; do
        if is_relevant_file "$f"; then
            RELEVANT=1
            break
        fi
    done

    if [ "$RELEVANT" -eq 0 ]; then
        echo "format.sh: no formatting-relevant changes; skipping format verify."
        exit 0
    fi
fi

MODE=""
if [ "$VERIFY" -eq 1 ]; then
    MODE="--verify"
fi

STATUS=0
ci/xstyler.sh $MODE || STATUS=1
ci/dotnet_format.sh $MODE || STATUS=1

if [ "$STATUS" -ne 0 ] && [ "$VERIFY" -eq 1 ]; then
    echo "" >&2
    echo "ERROR: format verify failed — please fix or run format.sh to try auto fix" >&2
fi
exit $STATUS
