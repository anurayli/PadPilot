#!/bin/bash
# PadPilot macOS paketini olusturur: dist/PadPilot.app ve dist/PadPilot-<surum>-mac.dmg
# Kullanim (macOS + Xcode): ./build.sh 1.1.0
set -euo pipefail
VERSION="${1:-1.1.0}"
cd "$(dirname "$0")"

rm -rf .build/release-universal dist
mkdir -p dist

# Apple Silicon + Intel (universal) derleme
swift build -c release --arch arm64 --arch x86_64
BIN="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)/PadPilot"

APP="dist/PadPilot.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/PadPilot"

# Simge (.icns)
ICONSET="dist/AppIcon.iconset"
mkdir -p "$ICONSET"
for s in 16 32 128 256 512; do
  sips -z $s $s Resources/icon-1024.png --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  d=$((s * 2))
  sips -z $d $d Resources/icon-1024.png --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf "$ICONSET"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>PadPilot</string>
  <key>CFBundleDisplayName</key><string>PadPilot</string>
  <key>CFBundleIdentifier</key><string>com.anurayli.padpilot</string>
  <key>CFBundleExecutable</key><string>PadPilot</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleLocalizations</key><array><string>en</string><string>tr</string></array>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>© 2026 Ali Nuraylı · MIT</string>
</dict>
</plist>
PLIST

# Imzasiz dagitim: yerel (ad-hoc) imza, Apple Silicon'da calismasi icin gerekli
codesign --force --deep --sign - "$APP"

# DMG (Uygulamalar kisayoluyla)
STAGE="dist/dmg"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "PadPilot" -srcfolder "$STAGE" -ov -format UDZO "dist/PadPilot-${VERSION}-mac.dmg"
rm -rf "$STAGE"
echo "Hazir: dist/PadPilot-${VERSION}-mac.dmg"
