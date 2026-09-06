[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$runtimeRoot = Join-Path $projectRoot "RecompOne"
$patchesDirectory = Join-Path $projectRoot "runtime-patches"
$patches = Get-ChildItem -LiteralPath $patchesDirectory -Filter "*.patch" -File |
    Sort-Object Name

foreach ($patch in $patches) {
    Push-Location $runtimeRoot
    try {
        & git apply --reverse --check $patch.FullName 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Parche de runtime ya aplicado: $($patch.Name)"
            continue
        }

        & git apply --check $patch.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "El parche de runtime no es compatible con este checkout: $($patch.Name)"
        }

        & git apply $patch.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "No se pudo aplicar el parche de runtime: $($patch.Name)"
        }

        Write-Host "Parche de runtime aplicado: $($patch.Name)"
    }
    finally {
        Pop-Location
    }
}
