# Release history

This is the version index: what changed and where to find each release's
original notes. For current capabilities use [Features](Feature-List);
for measured results use [Validation history](Validation-History).
Historical release notes describe their own version, not current support.

## Current release and versioning

The current plugin release is
[v0.1.0-beta.22](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.22).
All releases remain experimental prereleases. A newer beta number does not
close the [interactive acceptance gate](Validation-and-Limitations#remaining-interactive-acceptance).

The assembly informational version identifies the release; `release.json`
records its exact source commit and supported executable hash. Match the ZIP
and `.sha256` asset from the same release. The Git tag/source archive supplies
corresponding source. See [Building and releases](Building-and-Releases#package-a-release)
for packaging, and [Installation](Installation-and-Workflow#install-a-release)
for installation rather than using this page as a how-to.

Wiki documentation can be corrected independently of plugin releases.
Documentation-only changes do not imply a new DLL, compatibility claim or
reissued historical archive.

## Releases

| Version and original release notes | Main change |
|---|---|
| [v0.1.0-beta.22](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.22) | Deterministic Markdown exports of recording facts or validated reviewed prose, with typed values, linked evidence and guarded destinations |
| [v0.1.0-beta.21](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.21) | Shared bottom warnings/temporary menu help, distinct activity status and tab-local notes/task messages |
| [v0.1.0-beta.20](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.20) | Opt-in Scribe plain notes and record-linked tasks, mod-local/external project storage, Save Changes wording and a Workspace dropdown |
| [v0.1.0-beta.19](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.19) | Bounded compatibility/provenance matrix and copied dependency/locked-layer tests; no new supported executable |
| [v0.1.0-beta.18](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.18) | Readback-confirmed per-record note outcomes, explicit uncertain writes and shared-sidecar/retry guidance |
| [v0.1.0-beta.17](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.17) | Tracking-only move, fresh-baseline and divergent-history verification/guidance; no automatic relocation or merging |
| [v0.1.0-beta.16](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.16) | Complete-set/privacy notices, closed tracking-copy/restore and compiled-session archival evidence |
| [v0.1.0-beta.15](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.15) | Snapshot-local immutable string sharing to reduce measured capture memory |
| [v0.1.0-beta.14](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.14) | Bounded retained-history performance improvements and reproducible x86 profiling |
| [v0.1.0-beta.13](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.13) | Persistent partial-capture/sampling guidance and supported/omitted-state documentation |
| [v0.1.0-beta.12](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.12) | Explicit recorded-versus-saved-state boundaries, last-recorded UTC and separate backup workflow |
| [v0.1.0-beta.11](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.11) | Active/inactive tracking-anchor, backup and reserved-storage export protection |
| [v0.1.0-beta.10](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.10) | Compilation debt based on oldest pending recording, retained across settings/manual/closed periods |
| [v0.1.0-beta.9](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.9) | Labelled settings recovery, explicit reload/repair and unique original-byte/concurrent-save protection |
| [v0.1.0-beta.8](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.8) | Recoverable new-project initialization and legacy conversion without claiming unrelated storage |
| [v0.1.0-beta.7](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.7) | Portable exact-text/original-packet draft workspaces and save/discard protection |
| [v0.1.0-beta.6](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.6) | Explicit optional Workshop/cache/preview failure isolation without stopping ordinary gameplay tracking |
| [v0.1.0-beta.5](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.5) | Inclusive recording-range AI exports with original evidence and qualified citations |
| [v0.1.0-beta.4](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.4) | Complete exactly-once evidence coverage and guarded retries of matching completed notes |
| [v0.1.0-beta.3](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.3) | Grouped window menus and contextual help instead of the flat action row |
| [v0.1.0-beta.2](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.2) | Resumable tracking, scheduled recording, journal compilation and persistent settings |
| [v0.1.0-beta.1](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.1) | Initial beta documentation workflow with session-only baselines |
| [v0.1.0-alpha.1](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-alpha.1) | Initial experimental plugin prerelease |

Original Markdown release notes also live under
[`docs/releases`](https://github.com/Atlasroar/FCS-Scribe/tree/main/docs/releases).

## Upgrade notes

- **Settings location:** releases before beta 20 used
  `%LOCALAPPDATA%\FCS-Scribe\settings.json`. Current settings use
  `<project folder>\FCS-Scribe.settings.json`; the old file is preserved, not
  silently imported. Configure each mod explicitly. Notes/TODO default off.
- **Menus:** Review changes is now **Core > Save Changes**. Save/Open workspace
  moved from Draft to **Workspace**. Save Changes is not normal FCS Save.
- **Evidence formats:** packet/workspace/tracking schema numbers are separate
  from plugin release numbers. Read the [data contracts](Data-and-AI-Contracts)
  before using an older document; never relabel its schema or identity to
  bypass validation.
- **Existing work:** new defaults do not migrate tracking, workspaces or text.
  Preserve complete backups and original locations before upgrading.
