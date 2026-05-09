#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="${PROJECT_PATH:-./Emerald/Emerald.csproj}"
OUTPUT_ROOT="${OUTPUT_ROOT:-$PWD/artifacts/linux}"
PACKAGE_VERSION="${PACKAGE_VERSION:?PACKAGE_VERSION is required}"
PUBLIC_VERSION="${PUBLIC_VERSION:?PUBLIC_VERSION is required}"
UPDATE_CHANNEL="${UPDATE_CHANNEL:?UPDATE_CHANNEL is required}"
RELEASE_TAG="${RELEASE_TAG:?RELEASE_TAG is required}"
COMMIT_SHA="${COMMIT_SHA:?COMMIT_SHA is required}"
BUILD_TIMESTAMP_UTC="${BUILD_TIMESTAMP_UTC:?BUILD_TIMESTAMP_UTC is required}"

PUBLISH_ROOT="$OUTPUT_ROOT/publish"
PUBLISH_DIR="$PUBLISH_ROOT/Emerald-linux-x64"
INSTALL_ROOT="$OUTPUT_ROOT/install-root"
APPDIR="$OUTPUT_ROOT/appimage/AppDir"
FINAL_DIR="$OUTPUT_ROOT/final"

rm -rf "$PUBLISH_DIR" "$INSTALL_ROOT" "$APPDIR"
mkdir -p "$PUBLISH_DIR" "$INSTALL_ROOT" "$APPDIR" "$FINAL_DIR"

dotnet publish "$PROJECT_PATH" \
  -c Release \
  -f net10.0-desktop \
  -r linux-x64 \
  -p:SelfContained=true \
  -p:PublishTrimmed=false \
  -p:PublishSingleFile=false \
  -p:Version="$PACKAGE_VERSION" \
  -p:FileVersion="$PACKAGE_VERSION" \
  -p:AssemblyVersion="$PACKAGE_VERSION" \
  -p:InformationalVersion="$PUBLIC_VERSION" \
  -p:EmeraldPackageVersion="$PACKAGE_VERSION" \
  -p:EmeraldPublicVersion="$PUBLIC_VERSION" \
  -p:EmeraldUpdateChannel="$UPDATE_CHANNEL" \
  -p:EmeraldReleaseTag="$RELEASE_TAG" \
  -p:EmeraldCommitSha="$COMMIT_SHA" \
  -p:EmeraldBuildTimestampUtc="$BUILD_TIMESTAMP_UTC" \
  -o "$PUBLISH_DIR"

if [ ! -x "$PUBLISH_DIR/Emerald" ]; then
  echo "Expected Linux executable was not found at $PUBLISH_DIR/Emerald"
  exit 1
fi

tar -czf "$FINAL_DIR/Emerald-linux-x64.tar.gz" -C "$PUBLISH_ROOT" "Emerald-linux-x64"

install -dm755 "$INSTALL_ROOT/opt/emerald"
cp -a "$PUBLISH_DIR/." "$INSTALL_ROOT/opt/emerald/"

install -dm755 "$INSTALL_ROOT/usr/bin"
cat > "$INSTALL_ROOT/usr/bin/emerald" <<'EOF'
#!/usr/bin/env sh
exec /opt/emerald/Emerald "$@"
EOF
chmod 755 "$INSTALL_ROOT/usr/bin/emerald"

install -dm755 "$INSTALL_ROOT/usr/share/applications"
cat > "$INSTALL_ROOT/usr/share/applications/emerald.desktop" <<'EOF'
[Desktop Entry]
Name=Emerald
Comment=Open-source cross-platform Minecraft launcher
Exec=emerald
Icon=emerald
Terminal=false
Type=Application
Categories=Game;
EOF

install -dm755 "$INSTALL_ROOT/usr/share/icons/hicolor/256x256/apps"
install -m644 "$PWD/Emerald/Assets/icon.png" "$INSTALL_ROOT/usr/share/icons/hicolor/256x256/apps/emerald.png"

install -dm755 "$INSTALL_ROOT/usr/share/doc/emerald" "$INSTALL_ROOT/usr/share/licenses/emerald"
install -m644 "$PWD/LICENSE.md" "$INSTALL_ROOT/usr/share/doc/emerald/copyright"
install -m644 "$PWD/LICENSE.md" "$INSTALL_ROOT/usr/share/licenses/emerald/LICENSE.md"

if command -v desktop-file-validate >/dev/null 2>&1; then
  desktop-file-validate "$INSTALL_ROOT/usr/share/applications/emerald.desktop"
fi

install -dm755 "$APPDIR/usr/lib/emerald" "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"
cp -a "$PUBLISH_DIR/." "$APPDIR/usr/lib/emerald/"
install -m644 "$INSTALL_ROOT/usr/share/applications/emerald.desktop" "$APPDIR/usr/share/applications/emerald.desktop"
install -m644 "$INSTALL_ROOT/usr/share/icons/hicolor/256x256/apps/emerald.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/emerald.png"
ln -sf "usr/share/applications/emerald.desktop" "$APPDIR/emerald.desktop"
ln -sf "usr/share/icons/hicolor/256x256/apps/emerald.png" "$APPDIR/emerald.png"
ln -sf "emerald.png" "$APPDIR/.DirIcon"

cat > "$APPDIR/usr/bin/emerald" <<'EOF'
#!/usr/bin/env sh
APPDIR="${APPDIR:-$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)}"
export LD_LIBRARY_PATH="$APPDIR/usr/lib/emerald${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$APPDIR/usr/lib/emerald/Emerald" "$@"
EOF
chmod 755 "$APPDIR/usr/bin/emerald"

cat > "$APPDIR/AppRun" <<'EOF'
#!/usr/bin/env sh
HERE="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec "$HERE/usr/bin/emerald" "$@"
EOF
chmod 755 "$APPDIR/AppRun"
