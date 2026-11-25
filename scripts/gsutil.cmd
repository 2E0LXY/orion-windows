:<<"::CMDLITERAL" # vim: set ft=sh:
@echo off
bash "%~dpf0" %*
exit /b %errorlevel%
::CMDLITERAL

# Translates the gsutil commands vcpkg issues into gcloud storage calls.
# Must be named exactly gsutil.cmd, because vcpkg locates the tool by searching
# PATH for "gsutil".
#
# vcpkg only uses: gsutil version, gsutil -q cp src dest

set -euo pipefail

if [ "$1" = version ]; then
    echo "gsutil version: 5.35"
    exit 0
fi

[ "$1" = -q ] && shift

cmd="$1"
src="$2"
dst="$3"

# vcpkg restores each package in its own process, and gcloud costs ~5s of
# startup per call.  curl needs a bearer token, so downloads only take that
# path when the caller supplies one.
if [[ -n ${GSUTIL_CMD_CURL_AUTHTOKEN-} && $cmd == cp && $src == gs://* ]]; then
    code=$(curl -s -w '%{http_code}' -H "Authorization: Bearer $GSUTIL_CMD_CURL_AUTHTOKEN" \
                -o "$dst" "https://storage.googleapis.com/${src#gs://}") || true
    # Only 404 is a real miss.  401 from an expired token, 403, or a transport
    # failure retries on gcloud -- reporting those as a miss would silently
    # rebuild the package.
    case $code in
        200) exit 0 ;;
        404) rm -f "$dst"; exit 1 ;;
        *) rm -f "$dst" ;;
    esac
fi

# MSBuild thinks anything matching its error pattern is a build error, so
# remove "ERROR:" from output; if the object does not exist, it's just a cache
# miss.
gcloud storage "$@" 2>&1 | sed 's/^ERROR:/WARNING:/'
