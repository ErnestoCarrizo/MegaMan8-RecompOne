[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$recompilerProject = Join-Path $projectRoot "RecompOne\RecompOne.Recompiler\RecompOne.Recompiler.csproj"
$gameConfig = Join-Path $projectRoot "config\megaman8.json"
$generatedDirectory = Join-Path $projectRoot "generated"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet finalizó con el código $LASTEXITCODE."
    }
}

Push-Location $projectRoot
try {
    Write-Host "[1/2] Compilando RecompOne.Recompiler ($Configuration)..."
    Invoke-DotNet -Arguments @(
        "build",
        $recompilerProject,
        "-c", $Configuration,
        "--nologo"
    )

    Write-Host "[2/2] Regenerando el código de Mega Man 8..."
    $resolvedGeneratedDirectory = [System.IO.Path]::GetFullPath($generatedDirectory)
    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $expectedPrefix = $resolvedProjectRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedGeneratedDirectory.StartsWith(
            $expectedPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "La carpeta generated resuelta queda fuera del proyecto: $resolvedGeneratedDirectory"
    }

    if (Test-Path -LiteralPath $resolvedGeneratedDirectory -PathType Container) {
        Get-ChildItem -LiteralPath $resolvedGeneratedDirectory -File -Filter "*.cs" |
            Remove-Item -Force
    }

    Invoke-DotNet -Arguments @(
        "run",
        "--project", $recompilerProject,
        "-c", $Configuration,
        "--no-build",
        "--",
        $gameConfig
    )

    $generatedFiles = @(Get-ChildItem -LiteralPath $generatedDirectory -File -Filter "*.cs")
    $generatedBytes = ($generatedFiles | Measure-Object -Property Length -Sum).Sum

    Write-Host "Recompilación terminada: $($generatedFiles.Count) archivos C#, $generatedBytes bytes."
}
finally {
    Pop-Location
}
