#!/bin/bash
# Builds a universal mac/build/ToastFish.app (ad-hoc signed). Pass --zip to also produce a zip for sharing.
set -euo pipefail

VERSION="1.0.0"
MAKE_ZIP=false
for arg in "$@"; do
    case "$arg" in
        --zip) MAKE_ZIP=true ;;
        *) echo "用法：$0 [--zip]" >&2; exit 2 ;;
    esac
done

MAC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_DIR="$(cd "$MAC_DIR/.." && pwd)"
BUILD_DIR="$MAC_DIR/build"
APP_DIR="$BUILD_DIR/ToastFish.app"

# Build each architecture separately and merge them, so the app runs on Apple silicon and Intel Macs.
# (`swift build --arch a --arch b` needs Xcode's build system; per-triple builds work with only CLT.)
BINARIES=()
for arch in arm64 x86_64; do
    triple="$arch-apple-macosx13.0"
    swift build --package-path "$MAC_DIR" -c release --triple "$triple"
    BINARIES+=("$(swift build --package-path "$MAC_DIR" -c release --triple "$triple" --show-bin-path)/ToastFish")
done

mkdir -p "$BUILD_DIR"
STAGING_DIR="$(mktemp -d "$BUILD_DIR/.package.XXXXXX")"
trap 'rm -rf "$STAGING_DIR"' EXIT
CONTENTS="$STAGING_DIR/ToastFish.app/Contents"
mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources"
lipo -create "${BINARIES[@]}" -output "$CONTENTS/MacOS/ToastFish"

ICONSET="$STAGING_DIR/AppIcon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$MAC_DIR/Resources/AppIcon.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "$double" "$double" "$MAC_DIR/Resources/AppIcon.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$CONTENTS/Resources/AppIcon.icns"
cp "$MAC_DIR/Resources/AppIcon.png" "$CONTENTS/Resources/AppIcon.png"
# The macOS instructions replace the Windows ones; everything else is shared with the Windows app.
cp "$MAC_DIR/Resources/使用说明.html" "$CONTENTS/Resources/使用说明.html"
for resource in inami.db Star.pdf mute.mp3 自定义模板.xlsx; do
    cp "$REPO_DIR/Resources/$resource" "$CONTENTS/Resources/$resource"
done
cp -R "$REPO_DIR/Resources/Goin" "$CONTENTS/Resources/Goin"

cat > "$CONTENTS/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key><string>com.toastfish.mac</string>
    <key>CFBundleName</key><string>ToastFish</string>
    <key>CFBundleDisplayName</key><string>ToastFish</string>
    <key>CFBundleExecutable</key><string>ToastFish</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSUIElement</key><true/>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>NSPrincipalClass</key><string>NSApplication</string>
    <key>CFBundleDevelopmentRegion</key><string>zh_CN</string>
</dict>
</plist>
PLIST

codesign --force --deep -s - "$STAGING_DIR/ToastFish.app"
codesign --verify --deep --strict "$STAGING_DIR/ToastFish.app"
# Replace only this script's output after the new bundle has been verified.
rm -rf "$APP_DIR"
mv "$STAGING_DIR/ToastFish.app" "$APP_DIR"
printf '已生成：%s\n' "$APP_DIR"

if $MAKE_ZIP; then
    ZIP_PATH="$BUILD_DIR/ToastFish-macOS-$VERSION.zip"
    rm -f "$ZIP_PATH"
    ditto -c -k --keepParent "$APP_DIR" "$ZIP_PATH"
    printf '已打包：%s\n' "$ZIP_PATH"
fi
