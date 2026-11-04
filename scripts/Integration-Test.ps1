param(
    [Parameter(Mandatory = $true)][string]$FcsDirectory,
    [Parameter(Mandatory = $true)][string]$FixtureMod,
    [switch]$SettingsOnly,
    [switch]$ScheduleOnly,
    [switch]$ExportsOnly,
    [switch]$RecoveryOnly
)
$ErrorActionPreference = 'Stop'
if (([int]$SettingsOnly.IsPresent + [int]$ScheduleOnly.IsPresent + [int]$ExportsOnly.IsPresent +
    [int]$RecoveryOnly.IsPresent) -gt 1) {
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
Copy-Item -LiteralPath (Join-Path $FcsDirectory 'FCS_extended\FCS_extended_plugin.dll') -Destination $fixture
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /platform:x86 /debug:pdbonly /warnaserror+ "/out:$fixture\IntegrationTests.exe" `
    /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Runtime.Serialization.dll "/reference:$fixture\FCS_extended_plugin.dll" `
    $sources (Join-Path $root 'tests\IntegrationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
Push-Location $fixture
try {
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe')
        if ($LASTEXITCODE -ne 0) { throw "Integration tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $ScheduleOnly -and -not $ExportsOnly -and -not $RecoveryOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --settings-recovery
        if ($LASTEXITCODE -ne 0) { throw "Settings recovery tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ExportsOnly -and -not $RecoveryOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --schedule
        if ($LASTEXITCODE -ne 0) { throw "Scheduling tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $RecoveryOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --exports
        if ($LASTEXITCODE -ne 0) { throw "Export protection tests failed; disposable fixture retained at $fixture" }
    }
    if (-not $SettingsOnly -and -not $ScheduleOnly -and -not $ExportsOnly) {
        & (Join-Path $fixture 'IntegrationTests.exe') --tracking-recovery
        if ($LASTEXITCODE -ne 0) { throw "Tracking recovery boundary tests failed; disposable fixture retained at $fixture" }
    }
}
finally { Pop-Location }
Write-Output "Disposable fixture retained for inspection: $fixture"
