param(
    [Parameter(Mandatory = $true)][string]$FcsDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$changes = @(git -C $root status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect source checkout.' }
if ($changes.Count -gt 0) { throw 'Release packaging requires a clean, committed checkout.' }
$commit = git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not determine source commit.' }
& (Join-Path $PSScriptRoot 'Build.ps1') -FcsDirectory $FcsDirectory
$binary = Join-Path $root 'build\FCS-Scribe\FCS-Scribe.dll'
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($binary).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') {
    throw "Invalid release version in plugin metadata: $version"
}
$release = Join-Path $root 'build\release'
New-Item -ItemType Directory -Force -Path $release | Out-Null
$staging = Join-Path $release ('stage-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $staging 'FCS-Scribe'
New-Item -ItemType Directory -Force -Path $payload | Out-Null
foreach ($file in @('FCS-Scribe.dll', 'FCS_extended.json')) {
    Copy-Item -LiteralPath (Join-Path $root ('build\FCS-Scribe\' + $file)) -Destination $payload
}
foreach ($file in @('README.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $payload
}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $payload 'docs') -Recurse
$metadata = [ordered]@{
    version = $version
    source_commit = $commit.Trim()
    source_repository = 'https://github.com/Atlasroar/FCS-Scribe'
    supported_fcs_sha256 = '3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796'
    status = 'experimental'
    upstream_binaries_included = $false
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'release.json') -Encoding UTF8
$archive = Join-Path $release ("FCS-Scribe-v$version.zip")
Compress-Archive -LiteralPath $payload -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archive + '.sha256'),
    ($hash + '  ' + [IO.Path]::GetFileName($archive) + "`n"), (New-Object Text.UTF8Encoding($false)))
Get-ChildItem -LiteralPath $staging -Force | Out-Null
Remove-Item -LiteralPath $staging -Recurse -Force
Write-Output "Packaged $archive from $($commit.Trim())"
Write-Output "SHA256 $hash"
