#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <Icon Composer .icon bundle> <macOS .app bundle>" >&2
  exit 64
fi

icon_source="$1"
app_bundle="$2"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "Liquid Glass icons can only be compiled on macOS." >&2
  exit 1
fi

if [[ ! -d "$icon_source" || ! -f "$icon_source/icon.json" ]]; then
  echo "Icon Composer bundle is invalid: $icon_source" >&2
  exit 1
fi

if [[ ! -d "$app_bundle" ]]; then
  echo "macOS app bundle is invalid: $app_bundle" >&2
  exit 1
fi

icon_source="$(cd "$(dirname "$icon_source")" && pwd -P)/$(basename "$icon_source")"
app_bundle="$(cd "$(dirname "$app_bundle")" && pwd -P)/$(basename "$app_bundle")"
icon_name="$(basename "$icon_source" .icon)"
info_plist="$app_bundle/Contents/Info.plist"
resources_directory="$app_bundle/Contents/Resources"

if [[ ! -f "$info_plist" || ! -d "$resources_directory" ]]; then
  echo "macOS app bundle is invalid: $app_bundle" >&2
  exit 1
fi

actool_path="$(xcrun --find actool)"
echo "Using $(xcodebuild -version | tr '\n' ' ')"
echo "Using asset compiler: $actool_path"

minimum_system_version="$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' "$info_plist" 2>/dev/null || true)"
minimum_system_version="${minimum_system_version:-10.15}"

compiled_directory="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/emerald-icon.XXXXXX")"
trap 'rm -rf "$compiled_directory"' EXIT

partial_info_plist="$compiled_directory/assetcatalog_generated_info.plist"

"$actool_path" "$icon_source" \
  --compile "$compiled_directory" \
  --app-icon "$icon_name" \
  --include-all-app-icons \
  --enable-on-demand-resources NO \
  --development-region en \
  --target-device mac \
  --platform macosx \
  --minimum-deployment-target "$minimum_system_version" \
  --output-partial-info-plist "$partial_info_plist" \
  --output-format human-readable-text \
  --notices \
  --warnings \
  --errors

assets_catalog="$compiled_directory/Assets.car"
compiled_icns="$compiled_directory/$icon_name.icns"

if [[ ! -s "$assets_catalog" ]]; then
  echo "actool did not generate Assets.car." >&2
  exit 1
fi

install -m 0644 "$assets_catalog" "$resources_directory/Assets.car"

compiled_icon_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIconName' "$partial_info_plist" 2>/dev/null || true)"
compiled_icon_name="${compiled_icon_name:-$icon_name}"

/usr/libexec/PlistBuddy -c 'Delete :CFBundleIconName' "$info_plist" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :CFBundleIconName string $compiled_icon_name" "$info_plist"

# Xcode versions that emit an ICNS fallback from the Icon Composer source should
# replace Uno's generated fallback. Otherwise, keep Uno's existing icon.icns.
if [[ -s "$compiled_icns" ]]; then
  install -m 0644 "$compiled_icns" "$resources_directory/$icon_name.icns"
  /usr/libexec/PlistBuddy -c 'Delete :CFBundleIconFile' "$info_plist" 2>/dev/null || true
  /usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string $icon_name.icns" "$info_plist"
fi

plutil -lint "$info_plist"

configured_icon_name="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIconName' "$info_plist")"
configured_icon_file="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIconFile' "$info_plist")"

if [[ "$configured_icon_name" != "$compiled_icon_name" ]]; then
  echo "The app bundle icon metadata was not configured correctly." >&2
  exit 1
fi

fallback_icon_path="$resources_directory/$(basename "$configured_icon_file")"
if [[ "$fallback_icon_path" != *.icns ]]; then
  fallback_icon_path="$fallback_icon_path.icns"
fi

if [[ ! -s "$fallback_icon_path" ]]; then
  echo "The app bundle's ICNS fallback is missing: $fallback_icon_path" >&2
  exit 1
fi

echo "Injected Liquid Glass icon '$compiled_icon_name' into $app_bundle"
echo "Using ICNS fallback: $fallback_icon_path"
