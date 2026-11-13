# FCS-Scribe

FCS-Scribe is an experimental FCS Extended companion for documenting Kenshi
mod edits. Track changes from an explicit baseline, hand selected evidence to
an external AI assistant, review its draft and approve any native annotations.
Optional Scribe notes and record-linked tasks keep ordinary project writing
separate from that AI workflow.

It is **not a replacement editor**, an autonomous gameplay editor, a complete
wiki generator or a mod-restoration tool. No AI provider or subscription is
required by the plugin.

## Start here

| What you need | Page |
|---|---|
| See what is implemented and where to find it | [Feature list and behavior](Feature-List) |
| Install and use the plugin | [Installation and workflow](Installation-and-Workflow) |
| Diagnose an error or recover safely | [Troubleshooting](Troubleshooting) |
| Check supported use and open acceptance gates | [Validation and limitations](Validation-and-Limitations) |
| Run self-guided release acceptance checks | [Testing checklist](Testing-Checklist) |

## Technical reference

| What you need | Page |
|---|---|
| Build, run checks, profile or package | [Building and releases](Building-and-Releases) |
| Understand component responsibilities and data flow | [Architecture](Architecture) |
| Understand why the design uses these boundaries | [Coding decisions](Coding-Decisions) |
| Read schemas, validation and storage contracts | [Data and AI contracts](Data-and-AI-Contracts) |
| Review the unimplemented saved-data proposal | [OpenConstructionSet integration](OpenConstructionSet-Integration) |
| Check authoritative upstream sources and attribution | [References and knowledge credits](References-and-Knowledge-Credits) |

## Project history

- [Release history](Release-History): version index, original release notes and
  upgrade differences.
- [Validation history](Validation-History): scoped automated results,
  performance measurements and dated physical acceptance observations.
- [Working plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md):
  implementation scope and acceptance checklists, not a user guide.

## Current status

The current release is
[v0.1.0-beta.25](https://github.com/Atlasroar/FCS-Scribe/releases/tag/v0.1.0-beta.25).
It remains experimental and restricted to the inspected Steam FCS 2.14.2
executable hash. The owner accepted beta 21's feature checklist for the tested
setup. Markdown/history features have core/copied-native evidence; the task
table and guided history archival have core/non-showing synthetic UI evidence, not a new physical
acceptance pass. Broader support and an unexplained saved-mod byte change remain
outside that acceptance. See [supported scope and limitations](Validation-and-Limitations).

**Save boundaries matter:** Scribe Save Changes saves enabled Scribe work and
tracking evidence, not the `.mod`, native annotations or AI draft workspace.
Native record annotations live in shared `data\__TODO__<user>.txt` sidecars,
not the mod. Scribe's optional `notes.txt` and `todo.txt` are separate files.
See the [workflow](Installation-and-Workflow) before relying on any saved state.

Implementation and documentation were developed with AI assistance from
GitHub Copilot. Maintainers remain responsible for review, testing and release
decisions; AI-generated record notes require human approval.

The [README](https://github.com/Atlasroar/FCS-Scribe#readme) is a short
feature/wiki index. Reviewed wiki source lives under
[`docs/wiki`](https://github.com/Atlasroar/FCS-Scribe/tree/main/docs/wiki)
and is published separately. Current-use pages describe current behavior;
chronology belongs in the two history pages. The
[feasibility report](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/feasibility.md)
retains the original integration research.
