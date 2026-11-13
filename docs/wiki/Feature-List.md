# Feature list and behavior

This page describes **implemented features in the current release**: what
they do, where to find them and their important boundaries. Follow
[Installation and workflow](Installation-and-Workflow) for procedures,
[Troubleshooting](Troubleshooting) for errors and
[Release history](Release-History) for version-by-version changes.

## At a glance

| Feature | Access | Output or boundary |
|---|---|---|
| Embedded documentation window | FCS-Scribe toolbar button | MDI child inside FCS, with separate contextual help and status |
| Explicit, resumable tracking | Core > Start tracking | Current-editor baseline and timestamped session evidence, not a vanilla comparison |
| Save Scribe changes | Core > Save Changes | Enabled notes/tasks and durable tracking evidence; not native mod Save |
| Journal consolidation | Core > Compile journal | Idempotent compilation without deleting session originals |
| Recording-history browser | Core > Browse history | Read-only session/search filters and original evidence; complete range preview before export |
| Manual AI handoff | Core > Export AI packet | Selected recording-range evidence, not the full loaded database |
| Markdown changelog | Core > Export Markdown | Readable recording-range facts; no AI required |
| Reviewed Markdown | Draft > Export reviewed Markdown | Complete validated draft prose, separate from its original captured facts |
| Editable AI response | Draft > Import draft / Paste draft | Reviewable JSON; import/paste does not apply it |
| Portable draft workspace | Workspace > Save workspace / Open workspace | Exact unfinished text with its original packet |
| Approved native annotations | Apply approved notes | Validated FCS Notes/TODO appends; marks FCS dirty without saving |
| Plain Scribe notes | Enable in Settings, then Notes tab | Free-form `notes.txt`, separate from native annotations |
| Record-linked Scribe tasks | Enable in Settings, then main record-list right-click > Add Mod To Do | Separate `todo.txt`; edit/remove/complete in Mod To Do tab |
| Workshop information | Workshop info tab / Workshop > Export Workshop info | Local/staged/cached metadata and availability warnings |
| Preview-image archive | Workshop > Archive preview image | New byte-identical local preview copy |
| Project and recording settings | Settings | Mod-local/external storage, Manual or scheduled recording and export defaults |

## 1. Embedded window and contextual help

The toolbar opens or activates Scribe inside FCS. Its top-level entries are
**Core**, **Workshop**, **Draft**, **Workspace**, **Apply approved notes** and
**Settings**. A shared, scrollable bottom information box normally shows tracking,
privacy, settings-recovery and pending-compilation guidance. Hover or
keyboard-select an action to temporarily replace that information with help;
leaving the action/menu restores current warnings. Activity, results and errors
remain in a separate status area. Narrow windows use menu overflow. Busy actions disable
menus and notes/task editing; automatic work waits for open menus/modals.

Tabs separate changes/warnings, editable AI draft JSON, Workshop information,
plain Notes and Mod To Do. Notes/task settings and file messages stay inside
their own tabs, without duplicate enable/disable warnings in the bottom box.
The adapter requires the
[supported executable and editing mode](Validation-and-Limitations#supported-environment).

## 2. Start tracking: establish an explicit baseline

New tracking captures supported **current in-memory state**, including
unsaved edits already committed to the model. Earlier edits are outside the
new baseline; this is not "compare my mod against vanilla."

Resume validates the saved baseline, complete history and mod/context identity
before comparing with the current editor. Differences require confirmation;
nothing is restored to the mod. Each start/resume creates a session log.
Keep the baseline and its entire matching `.json.scribe` storage together.
Tracking data is private working material, not an AI or Workshop handoff.

See [tracking storage and recovery](Installation-and-Workflow#project-storage).

<a id="3-review-changes-inspect-the-evidence"></a>

## 3. Save Changes: inspect and preserve Scribe work

**Core > Save Changes** saves enabled Scribe notes/tasks and, with tracking
open, records changed supported records/context. Without tracking it can save
enabled Scribe text alone. Read **Changes and warnings**: exact typed
before/after values and warnings matter more than the count.

The tracker advances only after a successful durable recording. Unchanged
captures add no entry. Stored overrides and effective values can describe the
same edit. Sampling can miss intermediate changes; unsupported/omitted state
is not silently certified by a zero count.

This action **does not save `.mod`, native FCS annotations or the AI draft
workspace**. See [save and backup boundaries](Installation-and-Workflow#recorded-state-saved-mods-and-backups-beta-12).

## 4. Export AI packet: hand evidence to an external assistant

Select an inclusive first/last recording range and inspect its preview.
Earlier sessions, compiled/uncompiled observations and explicitly labelled
unrecorded current edits can be included. Ranges retain repeated edits and
reversals rather than calculating a net diff.

The packet retains original evidence and supplies unique outer change IDs and
an outer response template. Its validation context is the current editor at
export. Exporting does not record/compile history or save the mod. Overlapping
ranges can document the same evidence again; inspect citations and existing
notes. The plugin sends nothing online. Review private content before sharing.

See the [recording-range contract](Data-and-AI-Contracts#recording-range-packets-beta-5).

## Browse recording history

**Core > Browse history** reads the same validated catalogue used by range
exports. Filter by session or search names, stable IDs, fields, typed values,
source labels and context/Workshop warnings. Ctrl/Shift selects several
entries. Original timestamps, batch/baseline/fingerprint identities, qualified
citations, exact changes and historical context remain visible.

Compiled and uncompiled recordings appear once in their verified order.
Legacy checkpoints/imports and current **UNRECORDED** edits have explicit
groups; sessions without evidence contain no invented recordings. The browser
is a detached snapshot: close/reopen to refresh. It is not a saved-mod view,
event log, history editor or list of already applied notes.

**Export AI range...** and **Export Markdown range...** open the existing
inclusive first/last selector, seeded from the selected global entries.
Its preview includes every intervening entry, even hidden/unselected ones.
Filters never silently drop evidence or collapse reversals. AI export retains
the normal draft save/discard guard; Markdown facts do not replace the draft.
Context-only selections are browseable but need record-change evidence to export.

Opening, filtering and closing this browser save nothing, including enabled
unsaved Scribe notes/tasks. Explicit exports retain ordinary Scribe text saves
and existing destination/current-state guards. Nothing records, compiles,
prunes, restores gameplay or applies native notes. See the
[history workflow](Installation-and-Workflow#browse-and-export-recording-history).

## 5. Import draft and Paste draft: review before applying

Import requires an associated exported packet, including one restored from a
workspace. Paste only replaces editable text. Neither operation approves or
applies notes. Review prose, stable target IDs and every evidence citation.

Every exported change must be cited **exactly once** across the full draft.
Each target must survive and appear once; combine entries sharing a target,
including documentation for deleted records. Validation cannot prove that
the prose is correct. See the [draft contract](Data-and-AI-Contracts#draft-shape).

## Markdown changelog exports

**Core > Export Markdown** uses the same inclusive recording-range selector
and catalogue as AI export. It needs tracking, but no AI response. Records and
field/reference paths group the evidence views; typed before/after values,
source/layer labels, unresolved references and original recording identities
remain visible. Reversals are not collapsed into a net diff.

**Draft > Export reviewed Markdown** includes the associated original catalogue
and complete draft prose in separate sections, with citations linking to
evidence rows. Review the prose first. Batch identity, current state, surviving
targets and exactly-once coverage must pass; native annotations are not queried
or applied. A valid restored workspace needs no open tracking project.

Both actions preserve the editable draft and its packet/workspace association.
They do not record, compile, save the mod or approve native notes. Partial and
historical warnings remain in the document, and untrusted content is escaped
as literal text rather than active Markdown/HTML or external links.
Choose `.md`; an intact Scribe-generated export can be replaced, but edited,
unrelated, damaged and tracking/storage destinations are refused.
See [exporting Markdown](Installation-and-Workflow#export-a-markdown-changelog)
and the [Markdown contract](Data-and-AI-Contracts#markdown-export-contract).

<a id="portable-draft-workspaces-beta-7"></a>

## Workspace: preserve unfinished drafts

**Workspace > Save workspace** preserves exact editable text, even invalid
JSON, and its original AI packet in one portable file. **Open workspace**
validates/restores both independently of tracking. Restoring is not approval:
Apply still checks current state and asks for confirmation.

Close/replacement uses Save/Discard/Cancel protection. Failed/cancelled saves
retain the draft; edits made while saving remain unsaved. External changes
block stale overwrite. Save explicitly: this is not autosave or crash recovery.
See the [workspace contract](Data-and-AI-Contracts#portable-draft-workspace-contract-beta-7).

## 6. Apply approved notes: append through FCS

Scribe validates packet identity, current captured state, surviving targets,
complete coverage and existing annotations before asking for approval.
Approved appends preserve existing/inherited text and existing TODO categories.
The actual shared annotation destination is shown before writing.

Application marks FCS dirty but does not Save. Multi-record writes can be
partial: inspect the per-record outcome report and keep the original complete
draft for a retry. Only unique exact completed blocks can be skipped.
See [safe retries](Installation-and-Workflow#per-record-notes-outcomes-and-safe-retries-beta-18).

## 7. Workshop info: inspect and export local publishing context

The tab and JSON export report known header data, derived dependencies,
matching staged/cache fields, change-notes draft and preview details.
Unavailable values remain explicitly unknown with reasons. Export needs no
tracking baseline or gameplay edit.

This is not a Steam query, published changelog or upload. Known optional
Workshop failures permit ordinary gameplay recording with partial-capture
warnings; unexpected model/contract failures still stop the action.
See the [Workshop workflow](Installation-and-Workflow#capture-workshop-information-and-the-preview).

## 8. Archive preview image: preserve the original bytes

Archive copies the readable local PNG/JPEG/GIF preview byte-for-byte, with a
hash associating it with exported metadata. Choose a new filename outside
Kenshi. Existing images are not overwritten or re-encoded. Missing, busy,
unsupported or oversized previews fail explicitly.

## 9. Plugin settings: review scheduling and export defaults

Settings offers **Manual** or **30 seconds**, **1 minute**, **5 minutes** or
**10 minutes** recording. Automatic work runs only while eligible, visible and
unblocked. Errors pause it. Changing folders does not move existing tracking
or reset compilation debt.

Project settings/text/default destinations are mod-local unless an external
project directory is enabled. A small mod-local locator finds the external
root. A separate export-directory override remains available. Invalid settings
use labelled recovery, not silent success; repair preserves original bytes.
See [settings and storage](Installation-and-Workflow#checkpoint-scheduling-and-storage).

## Scribe notes and Mod To Do

Both features are independently **off by default**. Notes is a basic plain-text
editor writing `notes.txt`. Mod To Do uses a separate readable `todo.txt` with
record IDs/names and task completion state.

The **unreleased beta 24 source** adds a task table within this tab: item name,
String ID, captured item type, editable priority, task-creation date and
description, retaining Done checkboxes. Search all or one field, filter by
completion/type/priority/inclusive local date bounds, and sort by clicking
headers. The entire task workspace is hidden when the feature is off. Legacy
type/date metadata remains Unknown and missing priority Unspecified, rather than
inventing creation dates. See the [task-table workflow and format upgrade](Installation-and-Workflow#scribe-notes-and-mod-to-do-beta-20).

When enabled, **Add Mod To Do** appears immediately below native **Add Todo
Item** in the main Game World record-list context menu. Select one or more
records, enter task text, then manage it in Scribe's Mod To Do tab. Native menu
behavior and shared annotations remain unchanged; other property menus are
not extended.

Enabled work saves on Scribe actions, eligible automatic recording and normal
close. Conflicting external edits fail visibly and retain local/external intent.
Text files commit independently; switching roots does not migrate old files.
See the [notes/task workflow](Installation-and-Workflow#scribe-notes-and-mod-to-do-beta-20).

## 10. Compile journal: consolidate without deleting sessions

Compile appends missing durable session entries to the main journal without
duplicates or deleting originals. Automatic compilation becomes due when the
oldest pending recording reaches 30 minutes, including time closed or Manual.
It waits for an eligible tick; Manual requires explicit Compile.

The default bottom pending notice shows count, age and any hold. Settings changes
and short sessions do not restart that debt. Compilation does not Save the
mod, apply notes or recover lost gameplay.

## Planned features: not implemented

History retention/editing controls, wiki-ready exports and direct
AI connections are not shipped. The [OCS proposal](OpenConstructionSet-Integration)
is also unimplemented. Scope/checklists belong in the
[working plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md),
not the current feature catalogue. Support gates belong in
[Validation and limitations](Validation-and-Limitations).
