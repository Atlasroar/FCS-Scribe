param(
    [Parameter(Mandatory = $true)][string]$FcsDirectory,
    [Parameter(Mandatory = $true)][string]$FixtureMod
)
$ErrorActionPreference = 'Stop'
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
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
& $compiler /nologo /target:exe /platform:x86 /warnaserror+ "/out:$fixture\IntegrationTests.exe" `
    /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Runtime.Serialization.dll `
    (Join-Path $root 'src\Core.cs') (Join-Path $root 'src\FcsAdapter.cs') (Join-Path $root 'tests\IntegrationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
Push-Location $fixture
try {
    & (Join-Path $fixture 'IntegrationTests.exe')
    if ($LASTEXITCODE -ne 0) { throw "Integration tests failed; disposable fixture retained at $fixture" }
}
finally { Pop-Location }
Write-Output "Disposable fixture retained for inspection: $fixture"
