#!/usr/bin/env bash
set -euo pipefail

OUTPUT_ROOT="${OUTPUT_ROOT:-$PWD/artifacts/linux}"
PACKAGE_VERSION="${PACKAGE_VERSION:?PACKAGE_VERSION is required}"
RELEASE_TAG="${RELEASE_TAG:?RELEASE_TAG is required}"
TARBALL_SHA256="${TARBALL_SHA256:?TARBALL_SHA256 is required}"
REPOSITORY="${REPOSITORY:-RiversideValley/Emerald}"
PKGBUILD_DIR="$OUTPUT_ROOT/arch/pkgbuild"

rm -rf "$PKGBUILD_DIR"
mkdir -p "$PKGBUILD_DIR"

cat > "$PKGBUILD_DIR/PKGBUILD" <<EOF
pkgname=emerald-bin
pkgver=$PACKAGE_VERSION
pkgrel=1
pkgdesc='Open-source cross-platform Minecraft launcher made with .NET'
arch=('x86_64')
url='https://github.com/$REPOSITORY'
license=('custom')
depends=('gtk3' 'libx11' 'fontconfig' 'freetype2' 'glibc')
source=('Emerald-linux-x64.tar.gz::https://github.com/$REPOSITORY/releases/download/$RELEASE_TAG/Emerald-linux-x64.tar.gz')
sha256sums=('$TARBALL_SHA256')

package() {
  install -dm755 "\$pkgdir/usr/lib/emerald"
  cp -a "\$srcdir/Emerald-linux-x64/." "\$pkgdir/usr/lib/emerald/"

  install -Dm755 /dev/stdin "\$pkgdir/usr/bin/emerald" <<'LAUNCHER'
#!/usr/bin/env sh
exec /usr/lib/emerald/Emerald "\$@"
LAUNCHER

  install -Dm644 /dev/stdin "\$pkgdir/usr/share/applications/emerald.desktop" <<'DESKTOP'
[Desktop Entry]
Name=Emerald
Comment=Open-source cross-platform Minecraft launcher
Exec=emerald
Icon=emerald
Terminal=false
Type=Application
Categories=Game;
DESKTOP

  install -Dm644 "\$srcdir/Emerald-linux-x64/emerald.png" "\$pkgdir/usr/share/icons/hicolor/256x256/apps/emerald.png"
  install -Dm644 "\$srcdir/Emerald-linux-x64/LICENSE.md" "\$pkgdir/usr/share/licenses/emerald-bin/LICENSE.md"
}
EOF
