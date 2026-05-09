#!/usr/bin/env bash
set -euo pipefail

OUTPUT_ROOT="${OUTPUT_ROOT:-$PWD/artifacts/linux}"
PACKAGE_VERSION="${PACKAGE_VERSION:?PACKAGE_VERSION is required}"
INSTALL_ROOT="$OUTPUT_ROOT/install-root"
ARCH_ROOT="$OUTPUT_ROOT/arch"
PKGBUILD_DIR="$ARCH_ROOT/pkgbuild"
SOURCE_ROOT="$ARCH_ROOT/source"
SOURCE_NAME="emerald-${PACKAGE_VERSION}-1-x86_64.tar.gz"

if [ ! -d "$INSTALL_ROOT/opt/emerald" ]; then
  echo "Install root was not found. Run scripts/linux/publish-linux-x64.sh first."
  exit 1
fi

rm -rf "$ARCH_ROOT"
mkdir -p "$PKGBUILD_DIR" "$SOURCE_ROOT/package"
cp -a "$INSTALL_ROOT/." "$SOURCE_ROOT/package/"
tar -czf "$PKGBUILD_DIR/$SOURCE_NAME" -C "$SOURCE_ROOT" package
SOURCE_SHA256="$(sha256sum "$PKGBUILD_DIR/$SOURCE_NAME" | awk '{print $1}')"

cat > "$PKGBUILD_DIR/PKGBUILD" <<EOF
pkgname=emerald
pkgver=$PACKAGE_VERSION
pkgrel=1
pkgdesc='Open-source cross-platform Minecraft launcher made with .NET'
arch=('x86_64')
url='https://github.com/RiversideValley/Emerald'
license=('custom')
depends=('gtk3' 'libx11' 'fontconfig' 'freetype2' 'glibc')
source=('$SOURCE_NAME')
sha256sums=('$SOURCE_SHA256')

package() {
  cp -a "\$srcdir/package/." "\$pkgdir/"
}
EOF
