# FCS-Scribe

Welcome to the FCS-Scribe wiki!

FCS-Scribe is an experimental documentation companion for Kenshi mod authors,
integrated into FCS Extended. Edit normally, explicitly start a baseline,
catalogue the changes, ask an external AI assistant to draft explanations, and
approve the resulting notes before appending them through FCS.

It is **not a replacement editor**, an autonomous gameplay editor, or a complete
wiki generator today. Its reusable evidence model is intended to support
history, changelogs, saved-data analysis, and eventual wiki-ready exports.
The goal is an auditable catalogue of the edits you actually make, not invented
intent. An AI provider or subscription is not required by the plugin.

Implementation and documentation were developed with assistance from AI
(GitHub Copilot). Maintainers remain responsible for review, testing and
release decisions, and AI-generated record notes require human approval.

## Start here

- [Feature list and behavior](Feature-List): what each feature does, how to
  operate it, expected outputs, safeguards, and what remains planned.
- [Installation and workflow](Installation-and-Workflow): prerequisites, tracking,
  AI handoff, approval, and normal saving.
- [Building and releases](Building-and-Releases): source builds, validation and
  inspection commands, packaging, release contents and documentation sources.
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

The current [v0.1.0-beta.9 prerelease](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.9)
remains experimental and restricted to the inspected Steam FCS 2.14.2 executable
hash. It includes local Workshop metadata, preview-image archiving, embedded
MDI hosting and contextual action help alongside change documentation.
Live-model extraction and disposable save/reopen checks exist; the full
interactive FCS Extended workflow still requires acceptance.

Beta 2 also includes resumable tracking, automatic
delta session recording, manual/30-minute journal compilation and export-folder
settings. Feature/workflow pages explain the session files, recovery and
privacy boundaries.

Beta 3 groups the ten actions into Core, Workshop and Draft menus, with
separate approval and Settings actions, keyboard/hover help and narrow-window
overflow access. Automatic work waits while menus are open. The
[stabilization plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md#stabilization-gate-before-further-feature-work)
tracks sixteen issues/support boundaries before further feature expansion.
Beta 4 closes S01: drafts must cover every exported change exactly once, and
partial-application retries retain the complete draft while skipping only
matching completed notes.
Beta 5 closes S02 with previewable inclusive recording ranges for AI exports,
including earlier sessions and explicitly labelled unrecorded edits. Original
batch/evidence identities remain available; outer citations are uniquely
qualified and drafts still validate against current editor state. Exporting
does not record or compile history.
Beta 6 closes S03: known optional Workshop cache/preview failures and busy
upload/refresh states report explicit partial availability without stopping
ordinary gameplay recording. Known fields/readable components remain available;
availability changes still affect stale-draft checks. Workshop-only action
errors do not pause gameplay automation or clear an existing pause.
Beta 7 closes S04 with portable draft workspaces: save exact unfinished text
and its original packet, reopen after restart, and protect close/replacement
with Save/Discard/Cancel. Restored work still needs current-state validation
and approval; a workspace never saves the mod.
Beta 8 closes S05 with recoverable project initialization. Known unfinished
attempts can retry before baseline commit or finish through Resume afterward;
unknown storage, history and legacy backups are not silently replaced.
Beta 9 closes S06: invalid/unreadable settings open in explicitly labelled
safe manual mode with a persistent warning. Settings can reload or explicitly
repair readable originals with unique byte-preserving backups and cooperating
concurrent-save protection, without losing the current project or draft.
S07-S16 remain open; these fixes do not close full interactive acceptance.

**FCS notes are per-record editor-side annotations**, stored in
`data\__TODO__<user>.txt`, not inside the mod file. Distributing only a `.mod`
does not distribute these annotations.
Notes are keyed by stable record ID and can span mods.

This wiki is the detailed user guide; the
[repository README](https://github.com/Atlasroar/FCS-Scribe#readme) is a short
feature/wiki index. The
[working feature plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md)
tracks scope and acceptance checklists, while the
[feasibility report](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/feasibility.md)
records compatibility evidence. Wiki source pages are reviewed alongside the
code under `docs\wiki` and published to this separate GitHub wiki.
