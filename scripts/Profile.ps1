param(
    [ValidateRange(1, 100000)][int]$Records = 2000,
    [ValidateRange(1, 10000)][int]$Entries = 100,
    [ValidateRange(1, 1000)][int]$Sessions = 10,
    [ValidateRange(1, 100000)][int]$LargeRecordCells = 1000
)
$ErrorActionPreference = 'Stop'
if ($Entries -lt $Sessions -or $Entries % $Sessions -ne 0) {
    throw 'Entries must divide evenly across Sessions.'
}
$root = Split-Path $PSScriptRoot -Parent
$source = git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify source revision.' }
$fixture = Join-Path $root ('build\performance-fixtures\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$sources = @('Core.cs', 'Tracking.cs', 'Journal.cs', 'RangeExport.cs') |
    ForEach-Object { Join-Path $root ('src\' + $_) }
& $compiler /nologo /target:exe /platform:x86 /debug:pdbonly /warnaserror+ /main:PerformanceProgram `
    "/out:$fixture\PerformanceTests.exe" /reference:System.Core.dll /reference:System.Runtime.Serialization.dll `
    $sources (Join-Path $root 'tests\PerformanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Performance host compilation failed.' }
& (Join-Path $fixture 'PerformanceTests.exe') $fixture ($source.Trim() + ' (working tree)') $Records $Entries $Sessions $LargeRecordCells
if ($LASTEXITCODE -ne 0) { throw "Performance run failed; fixture retained at $fixture" }
Write-Output "Synthetic data only; fixture retained at $fixture"
