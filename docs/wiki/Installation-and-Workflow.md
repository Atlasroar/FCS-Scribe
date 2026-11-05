# Installation and workflow

For missing extensions, startup failures, warning flags and recovery remedies,
see [Troubleshooting](Troubleshooting).

This is the current-release user guide. For older behavior and upgrade
differences, see [Release history](Release-History). For supported use and open
acceptance gates, see [Validation and limitations](Validation-and-Limitations).

## Prerequisites

Use Windows, .NET Framework 4.8, an installed FCS Extended distribution, and
the inspected Steam FCS 2.14.2 executable. Compatibility is guarded by this
exact SHA-256, not just its display version:

```text
3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796
```

Unknown builds are rejected. Do not remove the guard to try another build:
inspect and validate its integration first. GOG compatibility is not claimed.
The [verified configuration matrix](Validation-History#s16-compatibility-and-provenance-matrix-beta-19)
separates disposable native-host evidence from full FCS Extended acceptance.

## Install a release

Download a release from [GitHub releases](https://github.com/Atlasroar/FCS-Scribe/releases)
and verify its ZIP against the matching SHA-256 checksum. Close FCS.
Extract `FCS-Scribe-v<version>.zip`, then copy its `FCS-Scribe` folder into
`<Kenshi>\mods\FCS-Scribe`. Launch FCS Extended normally, enable FCS-Scribe in
the extension selector, and open a writable `.mod` in normal editing mode.
The plugin adds an FCS-Scribe toolbar button and opens as an embedded MDI child.
Hover over action buttons, or focus them with Tab, to see descriptions at the
bottom; operation status stays visible separately.
Actions are grouped into **Core**, **Workshop**, **Draft** and **Workspace**
menus, with separate **Apply approved notes** and **Settings** entries.
See the [feature list](Feature-List#1-embedded-window-and-contextual-help)
for the menu layout and keyboard help.

Install FCS Extended from its upstream distribution; this project's archive
does not include or replace upstream binaries. Start with disposable mods and
back up the editor's notes files. See [Validation and limitations](Validation-and-Limitations)
before using the experimental plugin on important work.

## Build from source

See [Building and releases](Building-and-Releases) for requirements, the build
command, output files, validation and packaging. Building does not install
anything or modify your game. Do not replace FCS Extended's own contract DLL.

## Document a batch

1. Finish any active field edit and select **Start tracking**. Create a tracking
   JSON or resume an existing one after background validation. Earlier edits
   are deliberately outside a new file's captured baseline.
2. Edit normally and select **Core > Save Changes**.
   Read the warnings as well as the exact before/after evidence.
   This saves enabled Scribe notes/TODOs and flushes changed records/context to
   the current timestamped session log, then advances its baseline.
   It does **not** save the `.mod`, native FCS annotations or AI draft workspace.
3. **Export AI packet**: select an inclusive first/last recording
   range and inspect its preview before choosing the JSON destination.
   Earlier sessions remain selectable after resume. Current unrecorded edits
   are explicitly labelled; including them does not create a recording.
   Inspect the packet before sharing it with an external assistant; the plugin
   sends nothing itself.
4. Ask the assistant to follow the packet instructions and return the response
   **outer** template as JSON, using its qualified change IDs rather than
   nested archival templates. Import or paste it into the editable draft.
5. Verify target record IDs, quantities, conditions, and prose;
   cover every exported change ID exactly once across the complete draft.
   Select **Apply approved notes** and explicitly approve.
6. Inspect FCS Notes, then use FCS's ordinary **Save** action when ready.

Existing active and inherited notes are preserved. An existing TODO stays a
TODO rather than being silently reclassified. Applying notes marks the editor
dirty but does not save or advance the tracking baseline.

<a id="finish-edits-and-understand-sampling-beta-13"></a>

### Finish edits and understand sampling

Finish the field using its normal editor workflow before Start/Review/export,
then inspect the evidence rather than relying only on the count. The guarded
Name textbox's Enter and lost-focus routes commit to the model; Escape
discards its pending text. This does not establish every numeric/dropdown,
dialogue or separate editor's behavior. Clicking Scribe may change focus and
commit a field **before** its capture; Review itself never flushes controls.

Recordings are observations, not an event log. If a value changes A-to-B-to-A
between recordings, the intermediate edit can be absent. Review at B and
again at A to retain both observations. Zero evidence is not proof of no
editor changes: pending controls, omitted buffers and external file changes
can remain invisible. Automatic intervals can also be deferred.

This caveat appears with partial-extraction warnings on new baselines,
reviews, recording-range previews and restored evidence, and in new packet
instructions. Existing history is not rewritten. Consult the
[supported/omitted-state matrix](Data-and-AI-Contracts#supported-and-omitted-state-beta-13).

## Checkpoint scheduling and storage

For large stacks, start with **Manual**. Native capture runs on the editor
thread and can take seconds. A longer interval reduces frequency, not each
pause/peak. Normal mod/notes saving and separate backups remain essential;
there is no universal safe record-count or memory ceiling. See
[current performance limits](Validation-and-Limitations#performance-and-long-sessions)
and [historical measurements](Validation-History#s12-performance-measurements-partial-beta-14).

**Plugin settings** offers Manual (default), 30 seconds, 1 minute, 5 minutes
and 10 minutes. Automatic reviews durably append session deltas while the window
is visible, not minimized/busy or blocked by visible modal forms; they never
save the mod or apply AI notes. Compilation becomes due when the
oldest durable uncompiled recording is 30 minutes old, including time closed or
manual. It runs on the next eligible automatic tick, not inside settings Save
or Resume. Manual mode uses **Compile journal** explicitly.
Errors pause automation; reopen/validate after a journal write failure.

The persistent **Pending compilation** notice shows the count, oldest UTC,
due/overdue state and any manual/recovery/error hold. Settings saves/reloads and
short sessions retain this debt. A folder-only edit does not reset recording
either; an interval change re-arms recording without moving compilation.
Hidden/minimized/busy windows, open menus and modal dialogs defer work without
acknowledging the deadline. After successful compilation there is no empty
timer: the next first pending recording starts a fresh age window.

New JSON destinations default to the **Scribe project folder**:
the active mod's actual directory unless an external project folder is enabled.
The independent custom export-directory setting still overrides dialog defaults;
dialogs allow individual locations. Changing defaults does not move existing
tracking/workspace files.

Settings now live at `<project folder>\FCS-Scribe.settings.json`, bound to the
active mod path. The earlier `%LOCALAPPDATA%\FCS-Scribe\settings.json` is left
untouched and is **not silently imported**: a missing local file starts with
Manual and both text features disabled. A notice explains this when old
user-wide settings exist. Configure each mod explicitly.

<a id="scribe-notes-and-mod-to-do-beta-20"></a>

### Scribe notes and Mod To Do

In **Settings**, independently enable **Scribe free-form notes** and/or **Mod
To Do**. Both default off. The **Notes** tab is a basic plain-text editor:
writing only, with no rich formatting. It uses `notes.txt`. The **Mod To Do**
tab uses a separate `todo.txt`, retaining task text, linked record IDs/names and
completion checkboxes.

With Mod To Do enabled, select one or more records in the main FCS Game World
record list and right-click. **Add Mod To Do** appears immediately below native
**Add Todo Item** and opens Scribe's task editor. Editing, removing or checking
a task saves it. The native menu item, its behavior and FCS's own Notes/TODO
window/files remain unchanged. Other native property-window menus are not
extended.

**Core > Save Changes** saves enabled notes/TODOs, even without tracking.
With tracking open it also records the captured gameplay/context evidence.
Other Scribe actions, eligible automatic recording and normal Scribe close
also save enabled work. Editable notes/tasks are disabled during asynchronous
actions. This is separate from **Workspace > Save workspace / Open workspace**,
which preserve the editable AI draft and its original packet. Closing or an
action that cannot save fails visibly and retains in-memory work; it does not
pretend the files were saved.

By default text and settings are in the active mod folder. **Use an external
Scribe project folder** selects an existing directory for them and new default
destinations. A small `FCS-Scribe.location.json` pointer remains in the mod
folder so Scribe can find it next time. Use a distinct project folder per mod.
Switching folders selects the destination's text, **not a migration**: old notes,
tasks, tracking sets and workspaces stay where they were. Copy/archive them
deliberately with writers closed. Settings belonging to another mod block
adoption. A failed pointer update may leave safely written candidate settings
in the new folder while the window remains associated with the old one; resolve
the reported problem and retry rather than deleting either version.

Existing plain UTF-8 `notes.txt` is readable as notes. An unrelated/malformed
`todo.txt` is not overwritten or converted: preserve it and select a separate
folder, repair it or disable the feature. Disabling features leaves both files
untouched. Text saves compare observed hashes, use cooperating leases and
atomic replacement, and retain prior bytes in unique `.backup-<ID>` files.
The two files save **independently**, not as a transaction; notes can be saved
even if a later TODO save fails. Back up unsaved editor text before explicit
**Reload notes / TODOs**, which discards local edits only after confirmation.
Do not delete lock files to force a write.

Mod-folder notes/tasks may intentionally ship with the mod. Review them before
distribution. Private settings/locators, tracking data, draft workspaces and
all backups must be excluded from uploads; Scribe does not inspect native
Workshop uploads. Neither Scribe text file is FCS's shared
`data\__TODO__<user>.txt`, `.mod` data or Workshop change notes. See the
[text contract](Data-and-AI-Contracts#scribe-text-files-and-project-locator-beta-20).

<a id="settings-recovery-beta-9"></a>

### Settings recovery

If settings are invalid, unsupported, unreadable or reference an unavailable
custom directory, Scribe opens with a persistent **SETTINGS RECOVERY** banner.
It names the file and reason. Safe Manual mode uses project-folder export defaults;
automatic recording and compilation are disabled. Start/resume, manual Review
and Compile, Workshop actions and draft work remain available. No original
settings are rewritten just by opening Scribe.

Open **Settings**. **Discard edits and reload** discards only the dialog's
unsaved configuration choices and validates the current disk file; it does not
discard the working draft or project. Fix access/directory problems and reload,
or choose valid values and explicitly **Save repaired settings**. Repair
preserves readable original bytes in a unique
`<settings filename>.recovery-<UTC>_<unique-ID>.bak` before atomic replacement.
Later saves never rotate away that original. Cancel writes nothing.

An unreadable original cannot be safely replaced until access is restored and
Reload obtains its bytes. If another instance changes or locks shared settings,
Save fails explicitly; reload after it finishes before choosing and saving
values again. Valid reload/Save exits recovery and adopts its selected interval.
The current tracking project, original AI packet and exact draft text remain
in place. Compilation debt is preserved through these settings actions;
recovery never enables automatic work until a valid reload or explicit repair.

### Project storage

The selected JSON is a full baseline, not a per-tick rewrite. Adjacent
`<file>.json.scribe\project.json` tracks identity/head, `sessions` holds one UTC
timestamp + unique-ID JSONL log per start/resume, and `journal.jsonl` receives
compiled entries. Compilation is idempotent and keeps session originals.
For example:

```text
fcs-scribe-tracking.json                  Initial full captured baseline
fcs-scribe-tracking.json.scribe\
  project.json                           Project identity and durable head
  journal.jsonl                          Compiled change entries
  sessions\<UTC timestamp>_<ID>.jsonl     One append-only log per session
  recovery\<ID>.bak                       Originals retained after explicit recovery
```

Each recording stores changed complete records and current context, not another
full loaded snapshot. Added/deleted records, provenance and context-only changes
remain replayable. Unchanged state adds no entry. Each recording is flushed
before its baseline is adopted; a crash before compilation does not lose
already completed recordings.

Recording and ordinary button actions do not postpone the compilation deadline.
Busy work defers one due operation, not a backlog of overlapping ticks.
An exclusive project lease coordinates Scribe writers, and hash/chain checks
detect observed external edits; these are not signatures or a universal lock
against external tools. Closing stops new ticks; an in-flight write may finish
before its writer lease is released.

Back up this entire storage with the baseline. Close Scribe before manually
archiving fully compiled sessions; uncompiled logs remain required. No pruning
or history-management UI is implemented.

An interrupted last line requires recovery approval; the original is preserved
under `.json.scribe\recovery` before removing only that tail. Complete corrupt
entries are rejected. New roots flag missing storage/head; older compatible
full-state roots cannot themselves detect missing sidecars.

Loading a legacy AI packet requires conversion approval: retain original bytes at
`<file>.json.legacy.bak` and its evidence as history, then establish a **fresh
current-editor baseline** in that same JSON. Old packets cannot reconstruct
their missing baseline. Detected external edits, corruption and incompatible
mod context fail explicitly rather than being overwritten or truncated.
Only original schema-1/2 AI packets support this conversion. A schema-3
recording-range handoff is not a baseline; resume the original tracking JSON
with its `.scribe` folder instead.

**Tracking JSONs/backups contain the full captured loaded model, may be large
and private, and must not go to AI or Workshop.** Use the compact **Export AI
packet** action, which exports only the chosen recording range, original
compact evidence and relevant context, not full loaded snapshots or deltas.
The outer fingerprint describes the current editor at export even for an
older range; subsequent captured changes make its draft stale. Exporting does
not advance tracking or compile history. Ranges preserve repeated edits and
reversals rather than calculating a net diff. Overlapping ranges may document
the same observations again; check existing notes and citations before approval.

<a id="recorded-state-saved-mods-and-backups-beta-12"></a>

## Recorded state, saved mods and backups

**A successful recording is not a successful FCS Save.** The persistent Scribe
notice and Start/Resume chooser distinguish captured editor state, including
unsaved edits, from saved `.mod` and Notes/TODO files. Project status names the
last recorded UTC, not the last successful FCS save; the latter is **not
verified** by tracking.

| Action/data | What it preserves or changes | What it does not do |
|---|---|---|
| New tracking baseline / Save Changes / automatic recording | Observed supported editor state and durable evidence; enabled Scribe work is saved on actions | Save or restore the `.mod`, assets or native notes sidecars |
| Resume | Validate/replay tracking history and compare it with the current editor | Put recorded values back into FCS or roll back the saved mod |
| Compile journal / interrupted-tail recovery | Consolidate or explicitly repair tracking storage | Recover lost unsaved gameplay/notes or perform FCS Save |
| Apply approved notes | Append reviewed notes in memory and mark FCS dirty | Persist them to disk or create a complete mod backup |
| Save workspace | Preserve editable draft text and its original packet | Save gameplay, apply notes or resume tracking |
| Normal FCS Save | Use the editor's native save workflow for the mod/notes | Advance or compile Scribe history or prove every save target succeeded |

A recording can be **ahead of the saved mod**: you record a rename but close
without saving, or the editor crashes before normal Save. Reopening loads the
old saved name; Resume detects the difference. **No** declines this tracking
session. **Yes** keeps the current editor unchanged; the next manual or
eligible automatic review records the reversal as a new observation, without
rewriting the earlier entry. Nothing restores the lost rename.

The saved mod can also be **ahead of tracking**: you save an edit after the
last recording. Resume still keeps the current editor; review records that
difference. Zero gameplay evidence can indicate context-only changes rather
than an identical fingerprint. Inspect the discrepancy before accepting it.
Neither Yes nor No saves the mod/notes.

### Practical private backup workflow

1. Decide what should survive. Finish committing fields, inspect pending notes
   and explicitly use normal **FCS Save** if you want those edits on disk.
   Resolve save errors; do not infer success from Review, Compile, Apply or a
   closed dialog. Save unfinished draft workspaces separately.
2. Close Scribe and **all FCS instances** and wait for pending work to finish.
   Do not copy a project while a writer is active, delete its lock to force
   access, or force-close the editor as a backup method.
3. Make one timestamped private backup **outside Kenshi/mods and Workshop upload
   staging**, keeping these components together:

   | Component | Include |
   |---|---|
   | Actual mod | The complete mod folder, including its `.mod`, assets and local supporting files; record required dependency versions/load order |
   | FCS Notes/TODO | The relevant complete `data\__TODO__<user>.txt` sidecars, including contributing authors' files when needed; these can contain unrelated mods' annotations |
   | Tracking, wherever selected | The baseline JSON and entire matching `.json.scribe` folder, including marker, journal, retained sessions and recovery originals; retain associated `.bak`/`.legacy.bak` files when present |
   | Drafts | Explicitly saved workspace JSONs and desired previous-file backups; they may be outside the mod folder |
   | Settings and Scribe work, if wanted | `<project folder>\FCS-Scribe.settings.json`, enabled `notes.txt`/`todo.txt`, any repair originals and mod-local `FCS-Scribe.location.json` when external storage is used |

4. Record original locations, the plugin/editor versions and backup time.
   Check file counts/sizes and preferably SHA-256 hashes against the closed
   originals. Keep more than one known-good backup. A coherent copy can still
   contain intentionally different recorded and saved states.
5. To restore, first preserve the current files separately, with FCS closed.
   Restore a matching set to its original locations; do not merge journals,
   mix sidecars from different histories, alter identity hashes or assume a
   moved mod is compatible. Reopen normally and inspect saved gameplay and
   target notes before resuming the matching tracking project. A discrepancy
   is a comparison to inspect, not a command to restore recorded values.

This is a manual backup workflow, not a new restoration tool, relocation
guarantee or power-loss-safe snapshot service. The tracking catalogue cannot
reconstruct excluded assets/buffers. Do not distribute private backups or an
entire shared notes file with a mod. The storage procedures below do not grant
redistribution permission or establish transparent mod-relocation support.

### Complete tracking sets and restore failures

The **PRIVATE TRACKING SET** notice appears in the main window and Start/Resume
chooser. Default destinations follow the project/export folder; the warning
does not move files, create backups or protect a native Workshop upload.

Keep the baseline JSON and **entire matching `.json.scribe` folder** together,
including `project.json`, `journal.jsonl` when present, all session logs and
recovery originals/backups. Retain associated baseline `.bak` and
`.legacy.bak` files when present. A marker backup by itself is not a substitute
for a coherent project. Close all writers before copying or restoring.

| Missing or mixed component | Consequence and safe response |
|---|---|
| Baseline JSON | Deltas cannot replace the original full anchor. Restore the exact baseline from the matching private backup. |
| `project.json` for a modern journal-enabled baseline | Resume rejects missing/incomplete storage. Restore the complete matching set, not an empty folder or a marker from another project. |
| Latest or middle uncompiled session | The required head or chain cannot replay. Restore the exact missing session; do not archive pending logs or edit IDs/hashes. |
| Compiled session original, with its entries intact in `journal.jsonl` | Closed-project archival can replay from the journal; follow the procedure below, not deletion based on age. |
| `journal.jsonl`, with **all** original session history intact | Resume can replay the originals. Recovered entries are pending; Compile reconstructs the journal. Preserve the damaged set first and validate a private copy. |
| Journal plus required archived/missing originals | The remaining files cannot reconstruct the required chain; Resume rejects it. Restore the exact journal or the complete original history from the same project. |
| Foreign marker or journal | Identity/chain validation rejects mixed projects. Restore a coherent set; never merge files to make validation pass. |

These are tracking-storage rules, not mod/notes restoration. A copied tracking
root can replay under the same mod identity; moving the `.mod`, changing load
order or merging divergent histories is not thereby supported. Older compatible
anchors without `JournalVersion` cannot themselves detect missing sidecars.
A coherent older backup can load its older head: Scribe cannot prove that a
newer backup/history exists elsewhere. Keep backup times, inventories and hashes,
and compare expected recording counts/head before treating restoration as complete.

### Safely archiving compiled session originals

There is **no automatic pruning or archive UI**. Keeping the originals is the
simplest recovery option. If storage management is necessary:

1. In the correct project, explicitly Compile until there are no pending
   recordings. Resolve errors first. Then close Scribe and all FCS instances
   and wait for writers to finish; never move observed files while open.
2. Make and hash-check a complete private backup using the workflow above.
   Validate a disposable copy under the same mod identity before changing
   the working set, then close its writers too.
3. Move only known **fully compiled session `.jsonl` originals** to a private
   archive outside upload content. Keep the baseline, `project.json`, main
   `journal.jsonl`, all pending sessions and recovery/backups. Age, filename or
   an empty pending display before a failed compilation is not proof of eligibility.
4. Resume the working project and inspect retained history, recording count
   and current-state comparison. Previously compiled recordings must remain
   available from the main journal without duplicates or new pending debt.
   If validation fails, close writers and restore the exact archived originals
   or the complete known-good set; do not bypass the guard.

Archival reduces redundant originals, **not the required full baseline**.
It also makes the main journal more important: losing it after originals
have been removed can make the working set unrecoverable without the archive.
Keep that archive and complete backups private.

### Before a Workshop upload

Baselines contain the full captured loaded model, including dependency data.
Journals contain changed complete records; recovery files and backups retain
originals. Draft workspaces preserve original AI packets and opaque draft text.
These are private working material, not mod distribution assets. Compact
exports still require review before sharing.

1. Save deliberately through FCS, resolve errors, close writers and make a
   verified complete private backup outside every upload/staging location.
2. Inspect the **actual content selected by your native uploader**, including
   nested/hidden files. Do not assume it excludes `.scribe`, JSON or backups.
   Scribe neither inspects nor blocks native uploads; no Steam upload/filter
   behavior is established by these checks.
3. Keep the chosen tracking baseline, its entire matching storage folder,
   associated backups and saved draft workspaces out of the selected content.
   Do not delete every JSON file indiscriminately: a mod can have legitimate
   JSON assets. Preserve private working originals and use your publisher's
   supported content-selection workflow. If its selected contents cannot be
   confirmed, do not upload.
4. Do not resume a tracking project against a relocated publication copy or
   mix private history from different paths. If files were temporarily set
   aside with writers closed, restore the matching set to its original location
   before Resume. Check saved mod/notes and any discrepancy separately.

Do not distribute shared Notes/TODO sidecars wholesale: they can include other
mods' annotations. These manual safeguards are not an upload scrubber, a
backup wizard, proof of redistribution rights or a mod-relocation guarantee.

<a id="portability-and-linear-history-beta-17"></a>

## Portability and linear history

**Moving tracking storage is not moving the active mod.** Scribe's native
`ModId` hashes `Path.GetFullPath(activeFile).ToUpperInvariant()`; `ModName`
is the filename and is compared exactly. The tracking JSON's own location
does not determine that mod identity. No content-based identity, machine ID,
path-alias resolver or automatic history rebinding is implemented.

| Change | Supported route and boundary |
|---|---|
| Move/rename only the closed tracking baseline and entire matching storage | Keep the actual mod/path and loaded/locked context unchanged. Preserve associated backups and rename the adjacent folder to match the chosen baseline filename, e.g. `renamed.json.scribe`. Resume the new JSON location; do not copy just the anchor. |
| Move/copy/rename the active `.mod` | Even byte-identical copies with the same filename at another path have a different native identity. Preserve old history and deliberately create a fresh baseline with a new filename; do not edit stored identities/hashes. |
| Change loaded-mod order or locked-mod list | Restore the original context to Resume, or deliberately start a separate baseline after verifying the intended setup. The same names in a different order are not equivalent. |
| Change only declared dependency metadata | Declaration metadata alone is not the loaded/locked-context guard; save-normalized declarations can be recorded as context without gameplay evidence. This does not certify dependency versions or unchanged inherited data. |
| Use another machine | Reinstall the supported editor/plugin, verify the intended saved mod/notes/dependencies and use the fresh-baseline procedure below. Equal path strings can produce equal identities across machines: the guard cannot prove machine identity or dependency byte equality. |

### Moving only private tracking storage

1. Use Manual mode, finish pending work and close Scribe/all FCS instances.
   Make a verified [complete private backup](#practical-private-backup-workflow).
2. Move the baseline, **entire matching storage folder**, and associated
   baseline backups together. If renaming `old.json` to `new.json`, rename
   `old.json.scribe` to `new.json.scribe` and retain matching backup names.
   Do not change their contents. A multi-file move is not an atomic snapshot:
   if it fails partway, restore the coherent set before attempting Resume.
3. Keep the actual active mod at its original path and the intended loaded
   context unchanged. Reopen normally and **Resume existing JSON** at its
   new storage location. Inspect project/history counts and any discrepancy.
   Resume creates a new session and may rotate the small marker backup; it
   does not relabel old recordings. Review/Compile with unchanged state must
   not add duplicate observations.

Keep the full generated path short enough for .NET Framework/Windows limits,
including `.scribe\sessions\<timestamp>_<ID>.jsonl` and temporary suffixes.
Moving a set to a longer path can exceed those limits even if the baseline
filename alone fits. This is not new long-path, alias or network-sync support.

### Fresh baseline after moving a mod or changing machines

1. On the source, deliberately save wanted gameplay/notes through FCS and
   resolve errors. Preserve unsaved drafts separately. Close all writers and
   make the full private backup, including old tracking, actual mod/assets,
   relevant Notes/TODO sidecars and dependency/load-order information.
2. On the target, use the normal editor/game workflow to place and load the
   intended saved mod and dependencies with the supported FCS build and
   FCS Extended/plugin. Inspect saved values and notes; copying tracking logs
   cannot restore them. Actual physical cross-machine installation was not
   tested by the same-host relocation checks.
3. Use Manual mode and **Start tracking > New tracking JSON**, choosing a
   **fresh filename** with no existing tracking/storage set. Do not select
   Resume for the old mod identity, overwrite the old anchor, transplant its
   `.scribe` contents, edit hashes, or convert an old packet to bypass the
   mismatch. If an old export folder no longer exists, choose a valid private
   folder through Settings/the picker; existing project defaults do not migrate files.
4. Inspect the new baseline's current state and perform an unchanged Review.
   The new project begins with no historical recordings; future reviews
   document observations **after this baseline**, not a recovered relocation
   diff or the old branch. Keep the original history separately for reference.

Old AI packets/workspaces keep their original evidence; New does not rebind
them. A changed mod path changes current-state identity/fingerprints. Restoring
text is not approval or a way around current-state checks; export fresh evidence
for new work and inspect any retained drafts separately.

### Divergent branches and version control

History is **linear**, not a mergeable event graph. Two copies of one project
can independently record different successors to the same head. Both branches
can validate alone; combining their distinct session filenames can still be a
fork even when Git reports no text conflict. Unique entry IDs and valid hashes
do not make two parents-to-successors sequences compatible.

Do not concatenate journals, union session folders, pick the newest marker,
reorder JSONL lines or recalculate IDs/hashes. Replay rejects divergent or
reordered history; a sibling branch's marker can also name missing required
history. Automatic merge/rebase/cherry-pick of Scribe journals is unsupported.

Preserve both complete private branches before recovery. Choose one coherent
tracking branch only when its saved mod/context is the intended one. If gameplay
was reconciled through a separately verified editor workflow, create a **new
baseline of that resolved current state**; neither branch's recordings are
automatically imported or rewritten. Tracking does not resolve native mod
conflicts or recover unsaved work. Keep one writer lineage; independent copies
do not share a distributed lock, and timestamps are not conflict resolution.
Do not publish private histories merely to put them under version control.

## Capture Workshop information and the preview

Open the active mod's FCS **Steam Workshop** window to make its staged values
and change-notes draft available. Scribe's **Workshop info** tab shows the
current local metadata, source and warnings. **Export Workshop info** writes
JSON without requiring a tracking baseline or gameplay edits. AI packets also
include Workshop context, but do not treat it as new record changes.

If that dialog has not been opened for the active mod, header fields and any
saved `.info` cache remain readable; staged change notes are unknown. No Steam
query, refresh, save or upload occurs. Title/author/version/description,
required and referenced mods, tags, change-notes draft, Steam ID, cached update
time, visibility and preview details are all represented, including unknowns.

Use **Archive preview image** for a separate, byte-identical image copy.
Choose a new PNG/JPEG/GIF filename outside Kenshi; existing archives are not
overwritten. The preview hash in JSON associates it with the copied image.
Missing images and unsupported/oversized encodings fail explicitly. Local
metadata is not proof of the current published Steam listing or its changelog.

Known optional Workshop failures do not stop Start tracking,
Save Changes, automatic recording or AI range export. The Workshop tab/JSON
reports `CaptureStatus: "partial"` with component-specific `Issues`; tracking
warnings and bottom status repeat the notice. Header fields and any readable
component remain available. Expected absent files stay explicitly unknown.
During an active upload/refresh, staged metadata and preview reads wait while
gameplay capture continues; Scribe itself never invokes those operations.

Repair the source/cache/image or wait for the Workshop operation to finish,
then capture/review again. Recovery is recorded as context and can stale an
existing draft, requiring re-export. Scribe does not repair or replace source
files. Preview archiving remains strict: unavailable/unsupported images cannot
be archived, and an upload/refresh beginning during file selection blocks it.
Unavailable payloads are not validated; unchanged error status does not prove
their bytes stayed unchanged. Unexpected model/contract and fatal resource
errors still stop the action.
Workshop-only export/archive action errors are reported without pausing
gameplay recording; they do not re-enable already-paused automation. Ordinary
tracking/model/journal failures still pause it.

<a id="export-destination-protection-beta-11"></a>

## Export destination protection

Choose a separate filename for exports, not a tracking baseline or any file in
`<baseline>.json.scribe`. These folders are reserved even when empty or left by
an interrupted initialization. AI/Workshop exports and draft workspace saves
protect recognized inactive projects too; Workshop export needs no open
tracking project for these checks.

Recognition uses adjacent project storage or top-level tracking/session/journal
format identifiers, without requiring the complete project to be healthy.
Recognized standalone anchors, copied tracking JSON and backups are protected.
Workspace saves also check the `.bak` that atomic replacement would rotate.
Preview archives cannot create new files inside reserved storage and retain
their existing new-file-only and outside-Kenshi rules.

Unreadable or malformed existing JSON cannot safely be classified and is
refused, not replaced after a generic overwrite prompt. Preserve it for
inspection/recovery and select another filename. Valid existing AI packets
(including recording ranges), Workshop exports and ordinary JSON objects
remain replaceable. A standalone old AI packet without tracking identity or
adjacent storage is still an export, not an identifiable tracking anchor.
Workspace Save retains its stricter workspace-only replacement rule.

AI/Workshop export writes now stage and flush a temporary file, recheck
protection and the observed original hash, then atomically create/replace the
destination. They retain formatted UTF-8 output and do not rotate a neighboring
`.bak`. A pre-commit failure leaves the prior destination intact; no partial
success is reported. Checks are not a universal lock or protection against
every filesystem alias, hostile last-instant replacement or power loss.
Keep backups; S09 full interactive acceptance remains open. Follow the
[storage/privacy procedures](#complete-tracking-sets-and-restore-failures)
before copying, archiving or preparing an upload.

## Recovery and lifecycle

<a id="failed-project-initialization-beta-8"></a>

### Failed project initialization

New projects and approved legacy conversions use a pending initialization marker
before committing the baseline. Ordinary errors retain recognizable pending
state rather than leaving an unexplained "project already exists" blocker.

1. Fix the reported write, permissions, sharing or destination-path problem.
2. If a new project's baseline JSON **does not exist**, choose **Start tracking >
   New** with the same filename. Only recognized pending metadata with no
   history can be reused. This captures a fresh current-editor baseline; it does
   not restore the failed attempt's in-memory snapshot.
3. If the baseline JSON **exists**, choose **Resume existing JSON**. Resume
   validates exact baseline bytes, project identity, empty initial history and
   any required legacy backup before finalizing a pending marker. It does not
   rewrite the committed baseline. Normal current-editor difference confirmation
   still applies.
4. If the file is still an original schema-1/2 AI packet, Resume offers the
   ordinary explicit conversion again. Retry checks the same source hash and
   intact `.legacy.bak`; after baseline replacement, use normal Resume instead.

Initialization never claims preexisting session/journal/recovery data, a foreign
marker, unrelated baseline/marker backups or a changed legacy source. Missing
or changed required data fails explicitly. Restore the matching files from
backup or choose a separate filename; preserve unrelated files before doing
manual moves. Older markers without pending metadata cannot establish
ownership, so they are not automatically deleted or repaired. A missing marker
with only a backup also requires restoration, not automatic claiming.

Do not delete the whole `.scribe` directory to get past an error. No recursive
cleanup is part of recovery. Ordinary failed atomic writes remove only their
own temporary file; forced termination may leave temporary files, which are
not automatically swept. Source snapshots and backups may contain private data.
This recovery covers initialization, not missing recorded history, mod
restoration or arbitrary filesystem/power-loss guarantees.

### Drafts, notes and tracking

A deleted record cannot receive a new note: explicitly choose a surviving
record to hold its documentation. Combine entries if they share a target.
Stale drafts and repeat application of a batch to the same record are rejected.

**Workspace > Save workspace** preserves exact unfinished text with its
original packet. **Open workspace** restores it after restart, independently
of tracking; it reports stale/invalid responses and never applies them.
Unsaved work is protected before close, Start/resume, Export, Import, Paste and
Open with Yes (save), No (discard), or Cancel. A failed/cancelled save retains
the current draft. Later edits during saving remain unsaved; edits during
background Open/Start cancel replacement. Closing during work must be retried
when it finishes. Save explicitly before quitting: forced termination is not
covered by these prompts or an autosave system.

Workspaces are separate JSON documents, not tracking baselines or AI responses.
They include private evidence/prose and keep a previous-file `.bak` on replacement.
Observed external edits block overwrite; save to a new filename instead.
Neither tracking journals nor a workspace restore the mod or confer approval.
Resume tracking separately using its JSON after reopening. A changed active mod
or actual loaded/locked-mod context requires a new baseline. Normal saving does
not reset tracking, although dependency-header refreshes may require re-export
before applying an older draft.

Resume replays the main journal and uncompiled sessions. Differences from the
current editor require confirmation because they may be lost/discarded unsaved
edits. Tracking is **not a `.mod` backup** and does not restore lost mod changes.
See [recorded versus saved state and private backups](#recorded-state-saved-mods-and-backups-beta-12).

Multi-record application is not transactional. If an error reports partial
notes, inspect them before retrying or saving. Do not infer safety from a zero
completed-entry count: an interrupted FCS operation may have made partial
in-memory changes.
Retries require the original complete draft and unchanged captured
state. Matching completed notes are skipped; only pending targets are appended.
Conflicting text/evidence, partial blocks or multiple same-batch markers stop
the operation for inspection. Do not delete completed entries from the draft.
Reopen the saved workspace containing the original complete draft
and packet for a retry; fresh current-state/existing-notes checks still apply.

<a id="per-record-notes-outcomes-and-safe-retries-beta-18"></a>

### Per-record notes outcomes and safe retries

Approval names the **actual active Notes/TODO file** and warns that these
shared annotations are not embedded in the `.mod`. With all FCS instances
closed, back up relevant `data\__TODO__<user>.txt` files separately, including
other authors' files when needed. They can contain unrelated mods' annotations:
do not distribute entire sidecars blindly or overwrite another author's file.
Use the [complete private backup workflow](#practical-private-backup-workflow).
If the active destination changes while approval is open, application rejects
before writes and requires reviewing/approving the new destination.

During approved application, a target is marked newly confirmed only after
the native append returns and its unique complete block passes readback
validation. FCS is marked dirty **before** an append attempt, since a failing
native operation could already have changed memory. An attempted failure can
therefore leave the editor dirty even with zero confirmed records.

The read-only review area shows the active file and record IDs grouped as:

| Outcome | Meaning |
|---|---|
| Newly confirmed | This attempt appended and read back a matching complete block. This is not a disk-save confirmation. |
| Previously completed/skipped | Prevalidation found one exact matching completed block; Scribe did not append that target again. |
| Unconfirmed | No completion was confirmed in this attempt. This includes not-yet-attempted targets and potentially partially changed targets; it is **not proof that nothing changed**. |

Failures also name the last target, operation stage and original exception.
The menu's error path pauses automatic work and preserves the detailed report,
original packet and full editable draft. A Notes-list refresh failure can occur
after every append was confirmed; do not blindly reapply the batch.

1. Inspect the report and actual native Notes for each affected target before
   retrying or saving. Preserve the full draft/packet using **Save workspace**.
   There is no rollback or automatic save.
2. Keep the **original complete draft**, including completed entries, their
   original text and ordered citations. With unchanged captured state, Apply
   validates all coverage again, asks for approval again and skips only exact,
   unique completed blocks. Partial/conflicting blocks and duplicate markers
   require inspection; do not erase markers or edit identifiers to force retry.
3. If you deliberately Save and restart, inspect both gameplay and notes.
   Native Save can normalize dependency metadata and native notes formatting.
   A changed fingerprint remains stale: do not assume the old draft can retry,
   rebind its IDs or weaken checks. Preserve the old evidence and resolve the
   changed context before preparing new documentation. Do not lose or duplicate
   already-written notes while doing so.
4. If the original fingerprint still matches after reload, open the saved
   workspace and reapprove the validated pending targets. Inspect the result;
   use **normal FCS Save** only when ready. A fully present batch still rejects
   further application instead of silently reporting a new success.

The automated restart check uses a separate native test-host process, not the
complete physical FCS Extended startup/approval workflow. Tracking does not
back up these sidecars, and Apply does not change Workshop changelog text.
