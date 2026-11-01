# build.ps1 <work> <x64|x86|arm64>: SkiaSharp at the revision in skiasharp_revision with the Skia it pins, the patches in
# patches/skia applied, and its Windows library built by SkiaSharp's own scripts (clang-cl from LLVM, Visual Studio's
# libraries). The library and its symbols are copied to <work>\out.
param([Parameter(Mandatory)][string]$Work, [Parameter(Mandatory)][string]$Arch)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$here = $PSScriptRoot
$revision = (Get-Content "$here/skiasharp_revision").Trim()
# (files as committed: a CRLF checkout breaks Skia's scripts and patches)
$env:GIT_CONFIG_COUNT = '1'
$env:GIT_CONFIG_KEY_0 = 'core.autocrlf'
$env:GIT_CONFIG_VALUE_0 = 'false'

New-Item -ItemType Directory -Force "$Work/out" | Out-Null
Set-Location $Work
if (-not (Test-Path SkiaSharp/.git)) { git clone -q --filter=blob:none --no-checkout https://github.com/mono/SkiaSharp.git SkiaSharp }
Set-Location SkiaSharp
git checkout -q $revision
git submodule update --init --depth 1 externals/skia externals/depot_tools
Write-Host "SkiaSharp $(git rev-parse HEAD), Skia $(git -C externals/skia rev-parse HEAD)"

foreach ($patch in Get-ChildItem "$here/patches/skia/*.patch" | Sort-Object Name) {
  git -C externals/skia apply --reverse --check $patch.FullName 2>$null
  if ($LASTEXITCODE -eq 0) { Write-Host "Applied already: $($patch.Name)"; continue }
  Write-Host "Applying $($patch.Name)"
  git -C externals/skia apply $patch.FullName
}

$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
Push-Location native/windows
dotnet tool restore
dotnet cake build.cake --target=libSkiaSharp --configuration=Release --buildarch=$Arch "--vsinstall=$vs"
Pop-Location

$dir = @{ x64 = 'x64'; x86 = 'x86'; arm64 = 'ARM64' }[$Arch]
Copy-Item "output/native/windows/$dir/libSkiaSharp.dll", "output/native/windows/$dir/libSkiaSharp.pdb" "$Work/out/"
Get-ChildItem "$Work/out"
