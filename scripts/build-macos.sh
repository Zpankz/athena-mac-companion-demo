#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.1.7}"
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/AthenaCompanion/AthenaCompanion.csproj"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
APP_DIR="$REPO_ROOT/artifacts/macos/Athena Companion-$VERSION-$RID.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

rm -rf "$PUBLISH_DIR" "$APP_DIR"
mkdir -p "$PUBLISH_DIR" "$MACOS_DIR" "$RESOURCES_DIR"

dotnet publish "$PROJECT_PATH" \
  --configuration "$CONFIGURATION" \
  --runtime "$RID" \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  /p:Version="$VERSION" \
  /p:PublishSingleFile=false

cp -R "$PUBLISH_DIR"/. "$MACOS_DIR"/
chmod +x "$MACOS_DIR/AthenaCompanion"

cat > "$CONTENTS_DIR/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>Athena Companion</string>
  <key>CFBundleExecutable</key>
  <string>AthenaCompanion</string>
  <key>CFBundleIdentifier</key>
  <string>com.athena.companion</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>Athena Companion</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSMicrophoneUsageDescription</key>
  <string>Athena uses the microphone only while voice mode is active.</string>
  <key>NSScreenCaptureUsageDescription</key>
  <string>Athena captures the screen only when you explicitly ask for screen inspection or screen-based image generation.</string>
</dict>
</plist>
PLIST

if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP_DIR"
fi

(cd "$(dirname "$APP_DIR")" && ditto -c -k --keepParent "$(basename "$APP_DIR")" "Athena Companion-$VERSION-$RID.zip")

echo "Created $APP_DIR"
echo "Created $(dirname "$APP_DIR")/Athena Companion-$VERSION-$RID.zip"
