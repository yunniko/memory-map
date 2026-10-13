#!/usr/bin/env bash
# Checks a built package (G-003 criterion 6) in throwaway ubuntu:24.04 containers, so nothing on this
# machine is installed or changed:
#   1. lintian finds no error or warning, and desktop-file-validate passes the menu entry;
#   2. in a container holding only the package and what apt pulls in for it, `memory-map` is on the
#      PATH, opens its window (class memory-map, as the menu entry's StartupWMClass) on a virtual
#      display, is still running 5 s later and has unpacked nothing into the home folder (D035). The
#      display is served from a second container, so the X server's libraries cannot stand in for a
#      missing dependency;
#   3. every library the app opens at run time (dlopen, seen with LD_DEBUG) belongs to a package
#      named in Depends, so the hand-measured list in publish-linux.sh cannot drift (D036);
#   4. `apt-get remove` leaves none of its files behind.
# Usage: scripts/deb-check.sh publish/memory-map_<version>_<arch>.deb   (needs docker, on a machine of
# the package's architecture: the containers are of the machine's own)
set -euo pipefail
deb=$(realpath "$1")
image=ubuntu:24.04
name=memory-map-check-$$
cleanup() {
    docker rm -f "$name-x" "$name-app" > /dev/null 2>&1 || true
    docker volume rm "$name-x11" > /dev/null 2>&1 || true
}
trap cleanup EXIT
apt_install='apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends'

# The X server installs itself while lintian runs.
docker volume create "$name-x11" > /dev/null
docker run -d --name "$name-x" -v "$name-x11:/tmp/.X11-unix" "$image" bash -c "
    $apt_install xvfb x11-utils > /dev/null && exec Xvfb :99 -screen 0 1280x800x24 -nolisten tcp" > /dev/null

echo "== lintian and the menu entry"
docker run --rm -v "$deb:/pkg/memory-map.deb:ro" "$image" bash -c "
    set -e
    $apt_install lintian desktop-file-utils > /dev/null
    useradd -m checker
    su checker -c 'lintian --version && lintian --fail-on error,warning --display-info /pkg/memory-map.deb'
    echo 'ok: lintian reports no error or warning'
    dpkg-deb -x /pkg/memory-map.deb /tmp/x
    desktop-file-validate /tmp/x/usr/share/applications/memory-map.desktop
    echo 'ok: desktop-file-validate'"

echo "== install"
docker run -d --name "$name-app" -e DISPLAY=:99 -v "$name-x11:/tmp/.X11-unix" -v "$deb:/pkg/memory-map.deb:ro" \
    "$image" sleep infinity > /dev/null
docker exec "$name-app" bash -c "$apt_install /pkg/memory-map.deb > /dev/null && useradd -m tester"
docker exec "$name-app" dpkg-query -W -f='installed: ${Package} ${Version}\n' memory-map
docker exec "$name-app" bash -c 'echo "on the PATH: $(command -v memory-map)"'

echo "== run"
x_ready=
for _ in $(seq 180); do
    docker exec "$name-x" test -S /tmp/.X11-unix/X99 2> /dev/null && x_ready=1 && break
    sleep 1
done
if [[ -z $x_ready ]]; then
    docker logs "$name-x"
    echo "FAIL: the virtual display did not start within 180 s"
    exit 1
fi
docker exec -d "$name-app" su tester -c 'LD_DEBUG=files LD_DEBUG_OUTPUT=/tmp/ld memory-map > /tmp/app.log 2>&1'
window=
for _ in $(seq 120); do
    window=$(docker exec "$name-x" xwininfo -display :99 -root -tree | grep '"Memory Map": ("memory-map" "memory-map")') && break
    sleep 0.5
done
if [[ -z $window ]]; then
    docker exec "$name-app" cat /tmp/app.log
    echo "FAIL: no Memory Map window of class memory-map within 60 s"
    exit 1
fi
echo "$window"
sleep 5
# The bare image has no procps: the app is found by its name in /proc (the name it was started by).
app_pids='for p in /proc/[0-9]*; do [[ $(cat $p/comm 2> /dev/null) == memory-map ]] && echo ${p#/proc/}; done; true'
if [[ -z $(docker exec "$name-app" bash -c "$app_pids") ]]; then
    docker exec "$name-app" cat /tmp/app.log
    echo "FAIL: the app exited after showing its window"
    exit 1
fi
if docker exec "$name-app" test -e /home/tester/.net; then
    echo "FAIL: the packaged app unpacked files into the home folder (/home/tester/.net)"
    exit 1
fi
docker exec "$name-app" cat /tmp/app.log
echo "ok: window shown, still running 5 s later, nothing unpacked into the home folder"
docker exec "$name-app" bash -c "kill \$($app_pids)"

echo "== libraries opened at run time"
docker exec "$name-app" bash -c '
    depends=$(dpkg-query -W -f="\${Depends}" memory-map | tr ",|" "\n\n" | sed "s/(.*//; s/ //g")
    missing= checked=0
    # Names the program (shown by the name it was started as) and its two libraries asked dlopen for;
    # a name no package ships was a probe that failed (.NET tries ICU versions from the newest down).
    loader="\(memory-map\|/usr/lib/memory-map/[^ ]*\)"
    for so in $(sed -n "s%.*file=\([^ ]*\) \[0\]; *dynamically loaded by $loader \[0\]%\1%p" /tmp/ld.* | sort -u); do
        owner=$(dpkg -S "*/$so" 2> /dev/null | head -1 | cut -d: -f1) || continue
        [[ -z $owner || $owner == memory-map ]] && continue
        checked=$((checked + 1))
        echo "  $so ($owner)"
        grep -qx "$owner" <<< "$depends" || missing+=" $owner"
    done
    if [[ $checked == 0 || -n $missing ]]; then
        echo "FAIL: $checked libraries seen in /tmp/ld.*; opened at run time but not in Depends:$missing"
        exit 1
    fi
    echo "ok: all $checked libraries opened at run time are in Depends"'

echo "== remove"
docker exec "$name-app" bash -c '
    files=$(dpkg -L memory-map)
    DEBIAN_FRONTEND=noninteractive apt-get remove -y -qq memory-map > /dev/null
    left=$(for f in $files; do [[ -e $f && ! -d $f ]] && echo "$f"; done; [[ -e /usr/lib/memory-map ]] && echo /usr/lib/memory-map; true)
    status=$(dpkg-query -W -f="\${db:Status-Status}" memory-map 2> /dev/null || echo not-installed)
    if [[ -n $left || $status != not-installed ]]; then
        echo "FAIL: after remove, status $status; left: $left"
        exit 1
    fi
    echo "ok: removed, status $status, none of its files left"'
