#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.1.7}"
PRODUCT="AthenaNative"
APP_NAME="Athena Native"
RID="osx-arm64"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="$ROOT_DIR/artifacts/native-macos"
APP_DIR="$ARTIFACT_DIR/$APP_NAME-$VERSION-$RID.app"

cd "$ROOT_DIR"
swift build -c release --product "$PRODUCT"
BIN_DIR="$(swift build -c release --show-bin-path)"

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp "$BIN_DIR/$PRODUCT" "$APP_DIR/Contents/MacOS/$PRODUCT"

cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>$PRODUCT</string>
  <key>CFBundleIdentifier</key>
  <string>com.athena.native-companion</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>LSMinimumSystemVersion</key>
  <string>14.0</string>
  <key>NSMicrophoneUsageDescription</key>
  <string>Athena uses the microphone only while voice mode is active.</string>
  <key>NSScreenCaptureUsageDescription</key>
  <string>Athena captures the screen only after an explicit screen-inspection request.</string>
</dict>
</plist>
PLIST

if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP_DIR"
fi

(cd "$ARTIFACT_DIR" && ditto -c -k --keepParent "$(basename "$APP_DIR")" "$APP_NAME-$VERSION-$RID.zip")

echo "Created $APP_DIR"
echo "Created $ARTIFACT_DIR/$APP_NAME-$VERSION-$RID.zip"
