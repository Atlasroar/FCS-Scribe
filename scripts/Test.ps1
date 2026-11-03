param(
    [string]$FcsDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$out = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sources = @((Join-Path $root 'src\Core.cs'), (Join-Path $root 'tests\CoreTests.cs'))
& $compiler /nologo /target:exe /warnaserror+ "/out:$out\CoreTests.exe" `
    /reference:System.Core.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $out 'CoreTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
if ($FcsDirectory) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -FcsDirectory $FcsDirectory
}
