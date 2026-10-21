#!/bin/sh
# Build uc_host for linux-x64. Needs cc and Unicorn (headers + lib):
#   UNICORN_INCLUDE=<dir containing unicorn/unicorn.h>  (from the Unicorn source include/)
#   UNICORN_BIN=<dir containing libunicorn.so>          (e.g. the UnicornEngine.Unicorn NuGet
#                                                        runtimes/linux-x64/native, or a source build)
# Produces native/out/linux-x64/uc_host and copies libunicorn.so beside it.
set -e
cd "$(dirname "$0")"
: "${UNICORN_INCLUDE:?set UNICORN_INCLUDE (see README.md)}"
: "${UNICORN_BIN:?set UNICORN_BIN (see README.md)}"
mkdir -p out/linux-x64
cc -O2 -I "$UNICORN_INCLUDE" uc_host/uc_host.c -o out/linux-x64/uc_host \
   -L "$UNICORN_BIN" -lunicorn -Wl,-rpath,'$ORIGIN'
cp "$UNICORN_BIN"/libunicorn.so* out/linux-x64/ 2>/dev/null || true
echo "built out/linux-x64/uc_host"
