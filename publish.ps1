$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$out = Join-Path $root "artifacts\publish"
$exe = Join-Path $root "artifacts\PasswordWallet.exe"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root "src\Vault.App") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $out

if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou" }

Copy-Item (Join-Path $out "Vault.App.exe") $exe -Force
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLower()

Write-Host ""
Write-Host "Pronto: $exe ($size MB)"
Write-Host "SHA-256: $hash"
