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
APPDIR="${APPDIR:-$PWD/AppDir}"
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
  -p:EmeraldMSFTClientId="${EMERALD_MSFT_CLIENT_ID:-}" \
  -p:EmeraldElyByClientId="${EMERALD_ELYBY_CLIENT_ID:-}" \
  -p:EmeraldElyByClientSecret="${EMERALD_ELYBY_CLIENT_SECRET:-}" \
  -p:EmeraldElyByRedirectUri="${EMERALD_ELYBY_REDIRECT_URI:-}" \
  -o "$PUBLISH_DIR"

if [ ! -x "$PUBLISH_DIR/Emerald" ]; then
  echo "Expected Linux executable was not found at $PUBLISH_DIR/Emerald"
  exit 1
fi

# Include the application icon in the portable archive so the Arch recipe can
# build a complete desktop package from the GitHub release asset alone.
install -m644 "$PWD/Emerald/Assets/icon.png" "$PUBLISH_DIR/emerald.png"
install -m644 "$PWD/LICENSE.md" "$PUBLISH_DIR/LICENSE.md"

tar -czf "$FINAL_DIR/Emerald-linux-x64.tar.gz" -C "$PUBLISH_ROOT" "Emerald-linux-x64"
tar -tzf "$FINAL_DIR/Emerald-linux-x64.tar.gz" | grep -qx 'Emerald-linux-x64/Emerald'

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
install -m644 "$PUBLISH_DIR/emerald.png" "$INSTALL_ROOT/usr/share/icons/hicolor/256x256/apps/emerald.png"

install -dm755 "$INSTALL_ROOT/usr/share/doc/emerald" "$INSTALL_ROOT/usr/share/licenses/emerald"
install -m644 "$PWD/LICENSE.md" "$INSTALL_ROOT/usr/share/doc/emerald/copyright"
install -m644 "$PWD/LICENSE.md" "$INSTALL_ROOT/usr/share/licenses/emerald/LICENSE.md"

desktop-file-validate "$INSTALL_ROOT/usr/share/applications/emerald.desktop"

install -dm755 "$APPDIR/usr/lib/emerald" "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"
cp -a "$PUBLISH_DIR/." "$APPDIR/usr/lib/emerald/"
install -m644 "$INSTALL_ROOT/usr/share/applications/emerald.desktop" "$APPDIR/usr/share/applications/emerald.desktop"
install -m644 "$INSTALL_ROOT/usr/share/applications/emerald.desktop" "$APPDIR/usr/share/applications/com.riversidevalley.Emerald.desktop"
install -m644 "$PUBLISH_DIR/emerald.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/emerald.png"

cat > "$APPDIR/usr/bin/emerald" <<'EOF'
#!/usr/bin/env sh
APPDIR="${APPDIR:-$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)}"
export LD_LIBRARY_PATH="$APPDIR/usr/lib/emerald${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$APPDIR/usr/lib/emerald/Emerald" "$@"
EOF
chmod 755 "$APPDIR/usr/bin/emerald"
