[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipRecompile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$portProject = Join-Path $projectRoot "MegaMan8Recomp.csproj"
$recompileScript = Join-Path $PSScriptRoot "recompile.ps1"

if (-not $SkipRecompile) {
    & $recompileScript -Configuration $Configuration
    if (-not $?) {
        throw "No se pudo regenerar el código del juego."
    }
}

Push-Location $projectRoot
try {
    Write-Host "Compilando MegaMan8Recomp ($Configuration)..."
    & dotnet build $portProject -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "La compilación del port finalizó con el código $LASTEXITCODE."
    }

    $outputDll = Join-Path $projectRoot "bin\$Configuration\net10.0\MegaMan8Recomp.dll"
    if (-not (Test-Path -LiteralPath $outputDll -PathType Leaf)) {
        throw "La compilación terminó, pero no se encontró $outputDll."
    }

    Write-Host "Compilación terminada: $outputDll"
}
finally {
    Pop-Location
}
