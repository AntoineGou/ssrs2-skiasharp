#!/usr/bin/env bash
# build.sh <work> <macos|linux|alpine> [arch]: SkiaSharp at the revision in skiasharp_revision with the Skia it pins, the
# patches in patches/skia applied, and its native library built by SkiaSharp's own scripts: on macOS both architectures
# in one file, on Linux in SkiaSharp's Docker images (Debian 10's cross compilers for glibc, Alpine for musl; arch x64,
# arm64 or arm). The library is copied to <work>/out.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
work=$1
target=$2
arch=${3:-}
revision=$(cat "$here/skiasharp_revision")

mkdir -p "$work/out"
cd "$work"
[ -d SkiaSharp/.git ] || git clone -q --filter=blob:none --no-checkout https://github.com/mono/SkiaSharp.git SkiaSharp
cd SkiaSharp
git checkout -q "$revision"
git submodule update --init --depth 1 externals/skia externals/depot_tools
echo "SkiaSharp $(git rev-parse HEAD), Skia $(git -C externals/skia rev-parse HEAD)"

# The patches, in order, each once (a second run finds them applied)
for patch in "$here"/patches/skia/*.patch; do
  if git -C externals/skia apply --reverse --check "$patch" 2>/dev/null; then
    echo "Applied already: $(basename "$patch")"
  else
    echo "Applying $(basename "$patch")"
    git -C externals/skia apply "$patch"
  fi
done

case "$target" in
  macos)
    (cd native/macos && dotnet tool restore && dotnet cake build.cake --target=libSkiaSharp --configuration=Release)
    built=$(find output/native/osx -maxdepth 1 -name libSkiaSharp.dylib | head -1)
    ;;
  linux)
    # (not executable in SkiaSharp's tree, unlike Alpine's)
    bash scripts/Docker/debian/10/build-local.sh "$arch"
    built=output/native/linux/$arch/libSkiaSharp.so
    ;;
  alpine)
    scripts/Docker/alpine/build-local.sh "$arch"
    built=output/native/alpine/$arch/libSkiaSharp.so
    ;;
  *)
    echo "usage: build.sh <work> <macos|linux|alpine> [arch]" >&2
    exit 2
    ;;
esac

[ -n "$built" ] && [ -f "$built" ] || { echo "No library built ($built)" >&2; exit 1; }
cp "$built" "$work/out/"
ls -la "$work/out"
