#!/usr/bin/env bash
# Runs scripts/publish-linux.sh in a throwaway ubuntu:22.04 container, so the program links against
# glibc 2.35 and runs on Ubuntu 22.04, Debian 12 and ChromeOS's Linux as well as newer systems (D045).
# A build on a newer system picks up newer glibc symbol versions, which publish-linux.sh refuses.
# The tree is copied into the container, so nothing in it is written as root; the results are copied
# back to publish/ and owned by the caller. Releases and CI build this way.
# Usage: bash scripts/publish-linux-container.sh   (needs docker; builds for the machine's architecture)
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
image=ubuntu:22.04
mkdir -p "$root/publish"
docker run --rm -v "$root:/src:ro" -v "$root/publish:/out" -e OWNER="$(id -u):$(id -g)" "$image" bash -c '
    set -euo pipefail
    export DEBIAN_FRONTEND=noninteractive DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
    apt-get update -qq
    apt-get install -y -qq --no-install-recommends ca-certificates curl clang dpkg-dev python3 libicu70 libfontconfig1 > /dev/null
    mkdir /build
    tar -C /src --exclude=./.git --exclude=./publish --exclude="*/bin" --exclude="*/obj" -cf - . | tar -C /build -xf -
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --jsonfile /build/global.json --install-dir /opt/dotnet > /dev/null
    export PATH=/opt/dotnet:$PATH
    bash /build/scripts/publish-linux.sh
    rm -rf /out/linux-x64 /out/linux-arm64 /out/memory-map_*.deb
    cp -a /build/publish/linux-* /build/publish/memory-map_*.deb /out/
    chown -R "$OWNER" /out/linux-* /out/memory-map_*.deb'
