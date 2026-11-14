# FCS-Scribe

An experimental **FCS Extended plugin** for documenting Kenshi mod edits, not
a replacement editor. Detailed instructions and limitations live in the
[project wiki](https://github.com/Atlasroar/FCS-Scribe/wiki).

**Developed with the assistance of AI (GitHub Copilot).** AI-generated notes
remain editable drafts and require human review and approval.

## Features

- [Embedded FCS window and contextual button help](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#1-embedded-window-and-contextual-help).
- [Resumable tracking with timestamped session logs](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#2-start-tracking-establish-an-explicit-baseline).
- [Read-only recording-history browser](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#browse-recording-history): filter sessions/search evidence, then preview a complete AI or Markdown export range.
- [Typed before/after record, field, reference and instance evidence](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#3-save-changes-inspect-and-preserve-scribe-work).
- [Manual AI-packet export](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#4-export-ai-packet-hand-evidence-to-an-external-assistant), [editable imported/pasted drafts](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#5-import-draft-and-paste-draft-review-before-applying) and [approved FCS Notes/TODO appends](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#6-apply-approved-notes-append-through-fcs).
- [Markdown changelog exports](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#markdown-changelog-exports): captured facts or validated reviewed prose, with exact values and linked evidence.
- [Portable draft workspaces](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#portable-draft-workspaces-beta-7): save/open unfinished text with its original AI packet, plus save/discard protection.
- [Guided history archival](https://github.com/Atlasroar/FCS-Scribe/wiki/Installation-and-Workflow#manage-history-guided-archive-and-restore): Workspace selection, space preview and manually confirmed archive/restore of compiled originals; every recording remains.
- [Mod-local plain notes and record-linked tasks](https://github.com/Atlasroar/FCS-Scribe/wiki/Installation-and-Workflow#scribe-notes-and-mod-to-do-beta-20): separate text files, an opt-in **Add Mod To Do** context action and optional external project storage.
- [Task search, filters and sorting](https://github.com/Atlasroar/FCS-Scribe/wiki/Installation-and-Workflow#scribe-notes-and-mod-to-do-beta-20): tab-local table, editable priority and captured type/creation dates; controls hidden when Mod To Do is off.
- [Local Workshop metadata capture and export](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#7-workshop-info-inspect-and-export-local-publishing-context).
- [Byte-identical preview-image archiving](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#8-archive-preview-image-preserve-the-original-bytes).
- [Manual or scheduled recording and configurable export folders](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#9-plugin-settings-review-scheduling-and-export-defaults).
- [Manual or age-based automatic journal compilation](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List#10-compile-journal-consolidate-without-deleting-sessions).
- [Export destination protection](https://github.com/Atlasroar/FCS-Scribe/wiki/Installation-and-Workflow#export-destination-protection-beta-11): keep exports separate from active/inactive tracking storage.

## Wiki guide

- [Home](https://github.com/Atlasroar/FCS-Scribe/wiki): purpose, current release and project status.
- [Feature list and behavior](https://github.com/Atlasroar/FCS-Scribe/wiki/Feature-List): every action, safeguards and planned features.
- [Troubleshooting](https://github.com/Atlasroar/FCS-Scribe/wiki/Troubleshooting): installation checks, error/warning catalogue, solutions and safe recovery.
- [Installation and workflow](https://github.com/Atlasroar/FCS-Scribe/wiki/Installation-and-Workflow): prerequisites, setup, tracking storage, AI handoff, backups and recovery.
- [Building and releases](https://github.com/Atlasroar/FCS-Scribe/wiki/Building-and-Releases): source builds, validation commands, inspection, packaging and release contents.
- [Architecture](https://github.com/Atlasroar/FCS-Scribe/wiki/Architecture): core, FCS adapter, UI and session journals.
- [Coding decisions](https://github.com/Atlasroar/FCS-Scribe/wiki/Coding-Decisions): integration choices and safety boundaries.
- [Data and AI contracts](https://github.com/Atlasroar/FCS-Scribe/wiki/Data-and-AI-Contracts): snapshots, schemas, evidence, fingerprints and draft validation.
- [Validation and limitations](https://github.com/Atlasroar/FCS-Scribe/wiki/Validation-and-Limitations): current supported scope, remaining acceptance and known risks.
- [Testing checklist](https://github.com/Atlasroar/FCS-Scribe/wiki/Testing-Checklist): self-guided history/Markdown, task-table and archive/restore acceptance checks.
- [Release history](https://github.com/Atlasroar/FCS-Scribe/wiki/Release-History): version index, original release notes and upgrade differences.
- [Validation history](https://github.com/Atlasroar/FCS-Scribe/wiki/Validation-History): historical test results, performance measurements and dated acceptance evidence.
- [OpenConstructionSet integration](https://github.com/Atlasroar/FCS-Scribe/wiki/OpenConstructionSet-Integration): planned read-only saved-data analysis and its boundaries; not implemented.
- [References and knowledge credits](https://github.com/Atlasroar/FCS-Scribe/wiki/References-and-Knowledge-Credits): authoritative sources, supplementary research, AI assistance and licensing.

## Support and security

Use the [focused issue forms](https://github.com/Atlasroar/FCS-Scribe/issues/new/choose)
for bugs, test results and requests. Report vulnerabilities privately using the
[security policy](SECURITY.md); do not attach private baselines or shared notes.

## Authoritative upstream references

BFrizzleFoShizzle's documentation is authoritative for integration decisions
and must not be overridden without explicit instruction:

- [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended):
  managed editor plugin contract and loader.
- [RE_Kenshi](https://github.com/BFrizzleFoShizzle/RE_Kenshi):
  game-runtime extension and installation guidance.
- [KenshiLib](https://github.com/BFrizzleFoShizzle/KenshiLib):
  native game-runtime API and plugin-development best practices.

RE_Kenshi/KenshiLib's native game interfaces are not assumed to be managed FCS
editor APIs. This plugin uses FCS Extended's contract and WinForms's UI thread;
it adds no hooks or third-party detouring system.

Licensed under [GPL-3.0](LICENSE).
