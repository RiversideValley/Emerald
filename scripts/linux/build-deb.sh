#!/usr/bin/env bash
set -euo pipefail

OUTPUT_ROOT="${OUTPUT_ROOT:-$PWD/artifacts/linux}"
PACKAGE_VERSION="${PACKAGE_VERSION:?PACKAGE_VERSION is required}"
INSTALL_ROOT="$OUTPUT_ROOT/install-root"
DEB_ROOT="$OUTPUT_ROOT/deb/root"
FINAL_DIR="$OUTPUT_ROOT/final"
DEB_PATH="$FINAL_DIR/emerald_${PACKAGE_VERSION}_amd64.deb"

if [ ! -d "$INSTALL_ROOT/opt/emerald" ]; then
  echo "Install root was not found. Run scripts/linux/publish-linux-x64.sh first."
  exit 1
fi

rm -rf "$DEB_ROOT"
mkdir -p "$DEB_ROOT/DEBIAN" "$FINAL_DIR"
cp -a "$INSTALL_ROOT/." "$DEB_ROOT/"

INSTALLED_SIZE="$(du -sk "$DEB_ROOT/opt/emerald" "$DEB_ROOT/usr" | awk '{total += $1} END {print total}')"

cat > "$DEB_ROOT/DEBIAN/control" <<EOF
Package: emerald
Version: $PACKAGE_VERSION
Section: games
Priority: optional
Architecture: amd64
Maintainer: Riverside Valley <support@riversidevalley.dev>
Installed-Size: $INSTALLED_SIZE
Depends: libgtk-3-0, libx11-6, libfontconfig1, libfreetype6, libgcc-s1, libstdc++6, libc6
Homepage: https://github.com/RiversideValley/Emerald
Description: Open-source cross-platform Minecraft launcher
 Emerald is an open-source cross-platform Minecraft launcher made with .NET.
EOF

find "$DEB_ROOT" -type d -exec chmod 755 {} +
chmod 644 "$DEB_ROOT/DEBIAN/control"
dpkg-deb --build --root-owner-group "$DEB_ROOT" "$DEB_PATH"
dpkg-deb --info "$DEB_PATH"
dpkg-deb --contents "$DEB_PATH" | grep -q 'usr/bin/emerald'
lintian --fail-on error "$DEB_PATH"
