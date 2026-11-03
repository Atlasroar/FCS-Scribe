# FCS-Scribe

Welcome to the FCS-Scribe wiki!

FCS-Scribe is an experimental documentation companion for Kenshi mod authors,
integrated into FCS Extended. Edit normally, explicitly start a baseline,
catalogue the changes, ask an external AI assistant to draft explanations, and
approve the resulting notes before appending them through FCS.

It is **not a replacement editor**, an autonomous gameplay editor, or a complete
wiki generator today. Its reusable evidence model is intended to support
history, changelogs, saved-data analysis, and eventual wiki-ready exports.

## Start here

- [Feature list and behavior](Feature-List): what each feature does, how to
  operate it, expected outputs, safeguards, and what remains planned.
- [Installation and workflow](Installation-and-Workflow): prerequisites, tracking,
  AI handoff, approval, and normal saving.
- [Architecture](Architecture): the reusable core, managed FCS adapter, and UI.
- [Coding decisions](Coding-Decisions): why the implementation uses explicit
  baselines, guarded reflection, typed evidence, and no direct mod writer.
- [Data and AI contracts](Data-and-AI-Contracts): snapshots, changes, draft
  validation, and untrusted mod text.
- [OpenConstructionSet integration](OpenConstructionSet-Integration): the
  planned optional read-only saved-data backend and its boundaries.
- [Validation and limitations](Validation-and-Limitations): verified behavior,
  incomplete coverage, and the remaining acceptance gate.
- [References and knowledge credits](References-and-Knowledge-Credits):
  authoritative upstream sources, supplementary research, and licensing.

## Current status

The current [v0.1.0-beta.2 prerelease](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.2)
remains experimental and restricted to the inspected Steam FCS 2.14.2 executable
hash. It includes local Workshop metadata, preview-image archiving, embedded
MDI hosting and contextual button help alongside change documentation.
Live-model extraction and disposable save/reopen checks exist; the full
interactive FCS Extended workflow still requires acceptance.

Beta 2 also includes resumable tracking, automatic
delta session recording, manual/30-minute journal compilation and export-folder
settings. Feature/workflow pages explain the session files, recovery and
privacy boundaries.

**FCS notes are per-record editor-side annotations**, stored in
`data\__TODO__<user>.txt`, not inside the mod file. Distributing only a `.mod`
does not distribute these annotations.

The [repository README](https://github.com/Atlasroar/FCS-Scribe#readme),
[working feature plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md),
and [feasibility report](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/feasibility.md)
are the source of truth for scope and evidence. Wiki source pages are reviewed
alongside the code under `docs\wiki` and published to this separate GitHub wiki.
