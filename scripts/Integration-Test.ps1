param(
    [Parameter(Mandatory = $true)][string]$FcsDirectory,
    [Parameter(Mandatory = $true)][string]$FixtureMod,
    [switch]$SettingsOnly,
    [switch]$ScheduleOnly,
    [switch]$ExportsOnly,
    [switch]$RecoveryOnly,
    [switch]$SamplingOnly,
    [switch]$NotesOnly,
    [switch]$CompatibilityOnly,
    [switch]$PerformanceOnly,
    [switch]$VanillaStack,
    [ValidateRange(1, 120)][int]$ProfileIterations = 3
)
$ErrorActionPreference = 'Stop'
if ($VanillaStack -and -not $PerformanceOnly -and -not $CompatibilityOnly) {
    throw 'VanillaStack requires PerformanceOnly or CompatibilityOnly.'
}
if (([int]$SettingsOnly.IsPresent + [int]$ScheduleOnly.IsPresent + [int]$ExportsOnly.IsPresent +
    [int]$RecoveryOnly.IsPresent + [int]$SamplingOnly.IsPresent + [int]$NotesOnly.IsPresent +
    [int]$CompatibilityOnly.IsPresent + [int]$PerformanceOnly.IsPresent) -gt 1) {
    throw 'Choose at most one targeted integration suite.'
}
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('build\integration-fixtures\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $fixture | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fixture 'data') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $fixture 'mods') | Out-Null
foreach ($name in @('forgotten construction set.exe', 'PropertyGrid.dll', 'DialogConditionControl.dll',
    'Steamworks.NET.dll', 'CSteamworks.dll', 'steam_api.dll', 'fcs.def', 'fcs_enums.def',
    'fcs_layout.def', 'fcs_theme.def')) {
    Copy-Item -LiteralPath (Join-Path $FcsDirectory $name) -Destination $fixture
}
Copy-Item -LiteralPath $FixtureMod -Destination (Join-Path $fixture 'fixture.mod')
if ($VanillaStack) {
    $dependencies = Join-Path $fixture 'dependencies'
    New-Item -ItemType Directory -Path $dependencies | Out-Null
    foreach ($name in @('gamedata.base', 'Newwworld.mod', 'Dialogue.mod', 'rebirth.mod')) {
        Copy-Item -LiteralPath (Join-Path $FcsDirectory ('data\' + $name)) -Destination $dependencies
    }
}
Copy-Item -LiteralPath (Join-Path $FcsDirectory 'FCS_extended\FCS_extended_plugin.dll') -Destination $fixture
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /platform:x86 /debug:pdbonly /warnaserror+ /main:IntegrationTests "/out:$fixture\IntegrationTests.exe" `
    /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Runtime.Serialization.dll "/reference:$fixture\FCS_extended_plugin.dll" `
    $sources (Join-Path $root 'tests\IntegrationTests.cs') (Join-Path $root 'tests\PerformanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
Push-Location $fixture
try {
    if ($CompatibilityOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --compatibility
        if ($LASTEXITCODE -ne 0) { throw "Compatibility tests failed; disposable fixture retained at $fixture" }
        & (Join-Path $fixture 'IntegrationTests.exe') --workshop-provenance
        if ($LASTEXITCODE -ne 0) { throw "Workshop provenance tests failed; disposable fixture retained at $fixture" }
        Write-Output "Disposable fixture retained for inspection: $fixture"
        return
    }
    if ($NotesOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --notes-safety
        if ($LASTEXITCODE -ne 0) { throw "Notes safety tests failed; disposable fixture retained at $fixture" }
        & (Join-Path $fixture 'IntegrationTests.exe') --notes-reopen
        if ($LASTEXITCODE -ne 0) { throw "Cold-process notes reopen failed; disposable fixture retained at $fixture" }
        Write-Output "Disposable fixture retained for inspection: $fixture"
        return
    }
    if ($PerformanceOnly) {
        $env:FCS_SCRIBE_PROFILE_SOURCE = (git -C $root rev-parse HEAD).Trim() + ' (working tree)'
        $env:FCS_SCRIBE_PROFILE_ITERATIONS = $ProfileIterations.ToString()
        & (Join-Path $fixture 'IntegrationTests.exe') --performance
        if ($LASTEXITCODE -ne 0) { throw "Native performance run failed; fixture retained at $fixture" }
        return
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe')
        if ($LASTEXITCODE -ne 0) { throw "Integration tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --settings-recovery
        if ($LASTEXITCODE -ne 0) { throw "Settings recovery tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ExportsOnly -and -not $RecoveryOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --schedule
        if ($LASTEXITCODE -ne 0) { throw "Scheduling tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $RecoveryOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --exports
        if ($LASTEXITCODE -ne 0) { throw "Export protection tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --tracking-recovery
        if ($LASTEXITCODE -ne 0) { throw "Tracking recovery boundary tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --sampling
        if ($LASTEXITCODE -ne 0) { throw "Sampling boundary tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --notes-safety
        if ($LASTEXITCODE -ne 0) { throw "Notes safety tests failed; disposable fixture retained at $fixture" }
        & (Join-Path $fixture 'IntegrationTests.exe') --notes-reopen
        if ($LASTEXITCODE -ne 0) { throw "Cold-process notes reopen failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly -and -not $SamplingOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --compatibility
        if ($LASTEXITCODE -ne 0) { throw "Compatibility tests failed; disposable fixture retained at $fixture" }
    }
}
finally { Pop-Location }
Write-Output "Disposable fixture retained for inspection: $fixture"
