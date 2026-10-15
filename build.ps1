param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Instale o Visual Studio Build Tools com suporte ao .NET Framework 4.8.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild não encontrado.' }
& $msbuild (Join-Path $projectRoot 'SolidTweaks.sln') /t:Build /p:Configuration=Release /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação Release.' }
if ($Test) {
    & $msbuild (Join-Path $projectRoot 'tests\SolidTweaks.Tests.csproj') /t:Build /nologo /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação dos testes.' }
    & (Join-Path $projectRoot 'tests\bin\SolidTweaks.Tests.exe') $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'Falha na validação.' }
}
Write-Host (Join-Path $projectRoot 'dist\SolidTweaks-1.16.0-SW2025-SW2026\SolidTweaks.exe')
