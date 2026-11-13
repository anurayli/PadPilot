# PadPilot kurulum dosyasini olusturur: artifacts\PadPilot-Setup-<surum>.exe
# Kullanim: powershell -ExecutionPolicy Bypass -File installer\build.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

# 1) Uygulamayi .NET'siz bilgisayarlarda da calisacak sekilde yayimla
$pub = Join-Path $root "artifacts\publish"
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish src\PadPilot.App\PadPilot.App.csproj -c Release -r win-x64 --self-contained true `
  -p:Version=$Version -p:DebugType=None -o $pub
if ($LASTEXITCODE) { throw "dotnet publish basarisiz" }

# 2) Surucu kurulum dosyalarini indir (resmi GitHub surumleri)
$redist = Join-Path $PSScriptRoot "redist"
New-Item -ItemType Directory -Force $redist | Out-Null
$files = @{
  "ViGEmBus_1.22.0_x64_x86_arm64.exe" = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"
  "HidHide_1.5.230_x64.exe"           = "https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe"
}
$sha = @{
  "ViGEmBus_1.22.0_x64_x86_arm64.exe" = "89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A"   # winget manifest ile ayni
  "HidHide_1.5.230_x64.exe"           = "F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6"
}
foreach ($f in $files.Keys) {
  $dst = Join-Path $redist $f
  if (!(Test-Path $dst)) { Write-Host "Indiriliyor: $f"; Invoke-WebRequest $files[$f] -OutFile $dst -UseBasicParsing }
  if ((Get-FileHash $dst -Algorithm SHA256).Hash -ne $sha[$f]) { Remove-Item $dst; throw "SHA256 uyusmuyor: $f" }
}

# 3) Inno Setup ile derle
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
  Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$iscc) { throw "Inno Setup 6 bulunamadi (winget install JRSoftware.InnoSetup)" }
& $iscc "/DAppVersion=$Version" (Join-Path $PSScriptRoot "PadPilot.iss")
if ($LASTEXITCODE) { throw "ISCC basarisiz" }
Get-Item (Join-Path $root "artifacts\PadPilot-Setup-$Version.exe")
