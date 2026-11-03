# Architecture

The implementation keeps the editor boundary separate from the evidence model.
It does not replace FCS or load an AI provider.

| Component | Responsibility | Must not do |
| --- | --- | --- |
| `src\Core.cs` | Typed snapshots, immutable baseline copy, deterministic changes, fingerprints, packets, and draft validation | Depend on FCS, WinForms, or a provider SDK |
| `src\FcsAdapter.cs` | Guard the FCS executable and read/update verified editor objects on the UI thread | Guess APIs, silently omit unsupported values, or write binary mods |
| `src\Plugin.cs` | Implement the upstream plugin interface, attach a toolbar action, and present review/import/approval UI | Automatically trust/apply AI text or save the mod |
| `scripts` and `tests` | Build/package artifacts, inspect metadata, and exercise core/disposable-FCS behavior | Change the live installation during validation |

## Startup

FCS Extended discovers `FCS_extended.json` in enabled extensions and loads the
assembly listed in `FCS_Plugins`. Its `IPlugin.Init(Assembly)` call happens
before the FCS entry point. FCS-Scribe checks the supplied assembly, then waits
for WinForms `Application.Idle` to find the actual `baseForm` and attach UI.

No Harmony or native hook is added. RE_Kenshi/KenshiLib's native game-runtime
interfaces are not assumed to be managed editor APIs.

## Evidence flow

```text
Live FCS model -> guarded adapter -> explicit baseline / current snapshot
                                      |
                                deterministic changes
                                      |
                              versioned JSON packet
                                      |
                              external AI assistant
                                      |
                         editable, validated draft + approval
                                      |
                        verified FCS Notes/TODO append + dirty flag
                                      |
                           user chooses normal FCS Save
```

Record identity uses stable IDs, not names. Base, active, locked, and effective
cells remain distinct. The exported packet includes change evidence and
context, not a full dump of the loaded record database.

## Planned extension point

An optional OCS helper may later produce saved-only snapshots for the same core.
Its disk-backed context must remain distinguishable from the live FCS model.
It must not replace the live adapter for unsaved edits or notes insertion.
See [OpenConstructionSet integration](OpenConstructionSet-Integration).

## Source and release relationship

Wiki source lives in `docs\wiki` so decisions can be reviewed with code. The
published wiki has its own Git history. Release archives carry assembly
version information and a `release.json` source-commit identifier; the matching
tag/source archive supplies the corresponding source.
