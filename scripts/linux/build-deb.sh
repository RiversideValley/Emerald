#!/usr/bin/env bash
set -euo pipefail

OUTPUT_ROOT="${OUTPUT_ROOT:-$PWD/artifacts/linux}"
PACKAGE_VERSION="${PACKAGE_VERSION:?PACKAGE_VERSION is required}"
BUILD_TIMESTAMP_UTC="${BUILD_TIMESTAMP_UTC:?BUILD_TIMESTAMP_UTC is required}"
INSTALL_ROOT="$OUTPUT_ROOT/install-root"
DEB_ROOT="$OUTPUT_ROOT/deb/root"
FINAL_DIR="$OUTPUT_ROOT/final"
DEB_PATH="$FINAL_DIR/emerald_${PACKAGE_VERSION}_amd64.deb"

if [ ! -d "$INSTALL_ROOT/usr/lib/emerald" ]; then
  echo "Install root was not found. Run scripts/linux/publish-linux-x64.sh first."
  exit 1
fi

rm -rf "$DEB_ROOT"
mkdir -p "$DEB_ROOT/DEBIAN" "$FINAL_DIR"
cp -a "$INSTALL_ROOT/." "$DEB_ROOT/"

INSTALLED_SIZE="$(du -sk "$DEB_ROOT/usr" | awk '{print $1}')"

cat > "$DEB_ROOT/DEBIAN/control" <<EOF
Package: emerald
Version: $PACKAGE_VERSION
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Riverside Valley <support@riversidevalley.dev>
Installed-Size: $INSTALLED_SIZE
Depends: libgtk-3-0, libx11-6, libfontconfig1, libfreetype6, libgcc-s1, libstdc++6, libc6
Homepage: https://github.com/RiversideValley/Emerald
Description: Open-source cross-platform Minecraft launcher
 Emerald is an open-source cross-platform Minecraft launcher made with .NET.
EOF

install -d -m 755 \
  "$DEB_ROOT/usr/share/doc/emerald" \
  "$DEB_ROOT/usr/share/lintian/overrides"

CHANGELOG_DATE="$(date --date="$BUILD_TIMESTAMP_UTC" --rfc-email)"
{
  printf 'emerald (%s) stable; urgency=medium\n\n' "$PACKAGE_VERSION"
  printf '  * Automated GitHub release build.\n\n'
  printf ' -- Riverside Valley <support@riversidevalley.dev>  %s\n' "$CHANGELOG_DATE"
} | gzip -9n > "$DEB_ROOT/usr/share/doc/emerald/changelog.gz"

cat > "$DEB_ROOT/usr/share/lintian/overrides/emerald" <<'EOF'
# Uno's self-contained .NET publish intentionally carries these native runtime libraries.
emerald: embedded-library freetype usr/lib/emerald/libSkiaSharp.so
emerald: embedded-library libjpeg usr/lib/emerald/libSkiaSharp.so
emerald: embedded-library libpng usr/lib/emerald/libSkiaSharp.so
emerald: embedded-library zlib usr/lib/emerald/libSystem.IO.Compression.Native.so
emerald: unstripped-binary-or-object usr/lib/emerald/libSkiaSharp.so
EOF

find "$DEB_ROOT" -type d -exec chmod 755 {} +
find "$DEB_ROOT" -type f -exec chmod 644 {} +
chmod 755 "$DEB_ROOT/usr/bin/emerald" "$DEB_ROOT/usr/lib/emerald/Emerald"
if [ -f "$DEB_ROOT/usr/lib/emerald/createdump" ]; then
  chmod 755 "$DEB_ROOT/usr/lib/emerald/createdump"
fi
dpkg-deb --build --root-owner-group "$DEB_ROOT" "$DEB_PATH"
dpkg-deb --info "$DEB_PATH"
dpkg-deb --contents "$DEB_PATH" | grep 'usr/bin/emerald' >/dev/null
lintian --show-overrides --fail-on error "$DEB_PATH"
