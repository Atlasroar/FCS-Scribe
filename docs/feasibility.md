# Integration feasibility

## Outcome

An editor-integrated documentation workflow is feasible for the inspected
Steam FCS 2.14.2 build through FCS Extended's managed plugin interface and an
isolated, version-guarded reflection adapter. No custom mod-file writer is
necessary. The supported destination is **per-record FCS Notes/TODO**, not
embedded mod documentation or Steam Workshop release notes.

This is an experimental, partial catalogue rather than a complete model of
every editor subsystem. The installed executable was inspected read-only.
All runtime save/reopen work used disposable copies in the repository's ignored
build directory. Nothing was installed into the user's live editor.

## Authoritative sources

The user's designated upstream documentation takes precedence over implementation
preferences; no contrary upstream guidance was overridden.

| Source | Inspected revision | Relevant evidence |
| --- | --- | --- |
| [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended/tree/8a53a855e45f946f47780387c615c734dc3b0b50) | `8a53a855e45f946f47780387c615c734dc3b0b50` | `FCS_extended_plugin\IPlugin.cs`; `FCS_extended\Program.cs`; project framework references |
| [RE_Kenshi README](https://github.com/BFrizzleFoShizzle/RE_Kenshi/blob/c8dfbc9621b9263863722852f819c4c0f440475a/README.md) | `c8dfbc9621b9263863722852f819c4c0f440475a` | Native plugin development points to KenshiLib and prebuilt dependencies |
| [KenshiLib README](https://github.com/BFrizzleFoShizzle/KenshiLib/blob/1ce4a163739bbdf17254259039d7aa74fcc11cc0/README.md) | `1ce4a163739bbdf17254259039d7aa74fcc11cc0` | Native game API, VS2010 requirements, built-in hooking and UI-thread guidance |

FCS Extended's interface is `int IPlugin.Init(Assembly assembly)`. Its loader
discovers `FCS_extended.json` in enabled extensions under `mods` (and subscribed
Workshop extension directories), loads paths in `FCS_Plugins`, and calls `Init`
before invoking FCS's entry point. The supplied assembly is not itself a
documented stable editing API.

The plugin therefore waits for WinForms `Application.Idle`, finds the actual
`baseForm`, and adds a toolbar action on the editor UI thread. It does not need
Harmony or native hooks.

## Installed-build evidence

Assembly identity: `forgotten construction set, Version=2.1.0.0`.
File/product version: `2.14.2`.

Supported executable SHA-256:

```text
3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796
```

`scripts\Inspect-Fcs.ps1` uses reflection-only loading to inspect metadata and
selected method bodies without invoking FCS code. It reports the hash and IL
offsets so lifecycle claims can be checked against the exact installed build.
FCS binaries and proprietary method bodies are not included in this repository.

| Question | Verified mechanism | Boundary |
| --- | --- | --- |
| Active editable mod | `baseForm.nav`, `navigation.ActiveFile`, `ReadOnly`, `FileMode`, and `baseForm.mergeModsMode` | Only writable `.mod` files in `USER` mode; no translation, merge, or single-file mode |
| Loaded dependency context | `baseForm.activeMods`, `GameData.header.Dependencies`, `GameData.lockedMods` | Editor load order/declarations, not verified per-field dependency provenance |
| Live records and unsaved edits | `navigation.ou.gameData.items`; `Item.stringID`, `type`, `Name`, layered field dictionaries and the `Bah` indexer | Model edits are visible before save; uncommitted controls and separate editor buffers are not guaranteed |
| Value layers | `data`, `modData`, `lockedData`; reference `original`, `mod`, `locked`, `Values` | FCS resolves effective values; original dependency source remains unresolved |
| Deletion and references | `getState()` removal bit `8`; `Reference.Removed`; `references` and `removed` | Removing an inherited reference moves the same object between collections; capture uses one stable reference path |
| Instances | `instances` contain `GameData.Instance`, derived from `Item` | Recursive scalar/reference capture with explicit presence/tombstone state; unsupported nesting/types fail explicitly |
| Notes scope | `ToDo.items` keyed by record `stringID`, `NotesItem.active`/`locked`, NOTE/TODO category | One existing entry may already be a TODO; append preserves that category and inherited text |
| Notes update | `ToDo.AddItem(Item, string, Type, Item, bool, bool)` with append enabled and show disabled | Existing-note append does not mark dirty itself; adapter explicitly sets `navigation.HasChanges = true` |
| Persistence | `baseForm.saveFile()` invokes `ToDo.SaveData()` before the normal mod save | FCS writes editor-side `data\__TODO__<user>.txt`; notes are not stored inside `.mod` |
| Embedded UI | `baseForm` is an MDI container; native forms set `MdiParent` | Scribe uses the same standard WinForms child-window mechanism; complete interactive layout acceptance remains |
| Workshop metadata | `GameData.header` and matching `ModInfo.info`; `txtChangeNotes` holds the staged upload draft | Title/tags/visibility/ID/update can be local or cached, not verified current Steam state; upload notes are not record notes or published history |
| Workshop fallback | `ModData.GetFileName()` identifies `_<mod>.info` under `navigation.ModPath` | Read known XML fields with DTD/external resolution disabled; do not invoke `Load`'s silent fallback |
| Workshop dependency list | `GameData.getRequiredMods()` and `getReferencedMods()` | Read the methods without `ModInfo.refreshDependencies()`, which rewrites header lists |
| Workshop preview | `ModData.GetAbsoluteImagePath()` identifies `_<mod>.img` | PNG/JPEG/GIF bytes are copied unchanged to a new archive outside the installation; no Steam image download |

`ModData.changeNotes` is a separate Workshop-related member, not used as a
substitute for the editor Notes/TODO facility. The Workshop upload handler
copies `txtChangeNotes.Text` into it only during upload; extraction must read
the current control, not invoke that handler (which also saves the mod).
FCS's cache loader does not reload this draft. `SteamManager` converts remote
update timestamps to local time; exports preserve the cached round-trip
date/time and its timezone kind rather than inventing a UTC suffix.

## Safety and verification

The adapter checks the executable hash before adding functionality and rejects
unknown builds with an actionable diagnostic. Reflection is confined to
`FcsAdapter`; extraction/comparison/export are independent of editor UI and AI.
An unsupported type stops extraction, rather than producing a success-shaped
partial value.

The baseline is an immutable copy of the explicitly captured state. Inherited
data already present is not counted as a new edit. Stored active overrides and
effective values are separate entries. Changes to base/locked values on existing
records are rejected as unsafe to attribute. Notes and tracking metadata are
outside gameplay snapshots.

Exported packets include only relevant change entries and context, not a dump of
the entire loaded database. Packet text is explicitly untrusted AI input. Drafts
require matching batch/fingerprint identifiers, valid evidence IDs, nonempty
text, surviving target IDs, and explicit human approval. Stale and duplicate
applications are blocked. Deleted-record documentation requires an explicitly
chosen surviving note target.

Core checks cover deterministic ordering, exact values, immutable baselines,
record additions/deletions/renames, reference changes, unchanged/inherited data,
missing/false/zero/empty/null/removal distinctions, draft validation, and packet
serialization.

Disposable runtime checks have exercised the actual FCS forms and methods using
copies of vanilla `gamedata.base` and a representative mod. They verify unsaved
overrides, active and inherited note preservation, TODO category preservation,
dirty state, duplicate prevention, no save on apply, normal save/reopen,
unchanged captured gameplay records, baseline retention, live record lifecycle,
reference quantities/removal, and instance tombstones.

The current runs also exercise all Workshop fields, unsaved/staged and cached
sources, unknown and unpublished values, missing/malformed XML and DTD rejection,
lossless preview archiving in three formats, source/overwrite protection and
the exact 1 MB bound, asynchronous refresh/upload blocking with offline capture
preserved, real MDI parenting, and all eight buttons' hover/focus
events without erasing persistent status. No real Steam upload is invoked.

The final local runs passed 47 core checks, 88 integration checks on the vanilla
data copy, and 87 on the representative mod copy. The latter explicitly skipped
instance mutation because that fixture contains no instances; the vanilla run
covers it.

This uncovered two important implementation details: ordinary save refreshes
declared dependency headers without advancing the baseline, and large snapshots
must use streaming fingerprints in the 32-bit editor.

## Remaining uncertainty and limitations

- The real FCS Extended startup/toolbar/draft-dialog and embedded-window workflow still needs a
  manual interactive acceptance pass. Automated checks invoke actual FCS forms
  and save methods, not the complete loader/message-loop workflow.
- Copy-based dependency tests establish layering behavior, not exhaustive
  compatibility with every real-world mod stack or third-party extension.
- Per-field source provenance, full dependency resolution, general external data
  files, metadata change history, and pending dialogue/control buffers are not fully extracted.
  Every packet warns against claiming completeness.
- Workshop information is local/staged/cached context, not a current Steam
  query, published changelog or metadata change history. Only the local preview
  image is supported for external-file archiving.
- Notes are shared editor-side record annotations. Distributing a `.mod` alone
  does not distribute these notes. No unknown global/mod-specific note storage
  format was invented.
- Applying multiple notes cannot be made transactional through this verified
  interface. Errors explicitly warn about partial in-memory application. FCS
  remains responsible for its own save/error dialogs.
- Baselines are session-only. No automatic baseline advancement, persistence,
  wiki/Markdown export, direct provider integration, or custom binary writing is
  included.
