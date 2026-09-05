[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$CuePath = "disc\Mega Man 8 (USA).cue",

    [switch]$SkipBuild,

    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$portProject = Join-Path $projectRoot "MegaMan8Recomp.csproj"
$buildScript = Join-Path $PSScriptRoot "build-port.ps1"
$logsDirectory = Join-Path $projectRoot "logs"

if ([System.IO.Path]::IsPathRooted($CuePath)) {
    $resolvedCuePath = $CuePath
}
else {
    $resolvedCuePath = Join-Path $projectRoot $CuePath
}

if (-not (Test-Path -LiteralPath $resolvedCuePath -PathType Leaf)) {
    throw "No se encontró el archivo CUE: $resolvedCuePath"
}

$resolvedCuePath = (Resolve-Path -LiteralPath $resolvedCuePath).Path
New-Item -ItemType Directory -Path $logsDirectory -Force | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPath = Join-Path $logsDirectory "run-$timestamp.log"

Write-Host "Registro de esta ejecución: $logPath"

if (-not $SkipBuild) {
    & $buildScript -Configuration $Configuration
    if (-not $?) {
        throw "No se pudo preparar el ejecutable."
    }
}

Push-Location $projectRoot
try {
    Write-Host "Iniciando Mega Man 8 con: $resolvedCuePath"
    if ($Quiet) {
        & dotnet run --project $portProject -c $Configuration --no-build -- $resolvedCuePath *> $logPath
    }
    else {
        & dotnet run --project $portProject -c $Configuration --no-build -- $resolvedCuePath 2>&1 |
            Tee-Object -FilePath $logPath
    }

    $portExitCode = $LASTEXITCODE
    if ($portExitCode -ne 0) {
        throw "El port finalizó con el código $portExitCode."
    }
}
finally {
    Pop-Location
    Write-Host "Registro guardado en: $logPath"
}
