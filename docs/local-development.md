# Local development and deployment

Use the [README setup commands](../README.md#running-locally) for a fresh installation. The checkout, build output, installed runtime, and running processes can be at different stages. Check the stage relevant to the change.

## Build and run

| Need | Command or location |
| --- | --- |
| Diagnose prerequisites, configuration, and runtime | `./dev doctor` |
| Build everything or just Sanctuary content | `./dev build` or `./dev build content` |
| Stage and start the server from source | `./dev server` |
| Start the existing installed server | `./dev server --installed` |
| Connect the local client | `./dev client` |
| Use the Linux desktop launcher | `./dev launcher` |
| Runtime and saves | `.local/server` |
| Client and profiles | `.local/client` |
| Classic game data | `.local/uo-data` |
| Launcher logs | `.local/logs/launcher-server.log` and `.local/logs/launcher-client.log` |

Keep native ModernUO and ClassicUO changes in the tracked [patch stacks](../patches). Preserve existing worktree and submodule edits. Runtime content reads copied data from `Data/Sanctuary` under the installed server. Pair server protocol updates with the matching ClassicUO build. For game-art changes, check which installed client data or patch actually supplies the asset. [Data layout](../data/Sanctuary/README.md)

## Authored dialogue and reading

Choose **English**, **Deutsch**, or **日本語** in the launcher before starting the client. The launcher writes `lang` in the ClassicUO settings file. In game, **Options → Sound** controls speech playback and its volume independently of effects and music.

Use stable semantic IDs in new scripts. For portrait dialogue, supply a `LineId` and `SpeakerId` on each `ScriptedDialogue.Node`. Use a `TextId` on replies for translation. The Avatar’s choices are silent. For overhead NPC lines, use `AuthoredWriting.Bark` with a line ID and speaker ID.

Place an `AuthoredReadable` for a sign, letter, or book. The short constructor creates a sign from an ID, title, and text. The full constructor accepts a graphic, kind, ID, title, author, text, and optional narrator recording ID. Existing scripted interactions can call `AuthoredWriting.Show` after their usual access checks. The reading window displays the document, and its Play button appears when a recording is available. Short second-person narrator observations can use `AuthoredWriting.Observe` to show yellow text above the Avatar. Reading a letter aloud keeps the text in the window.

Put translation catalogs in `data/Sanctuary/Localization/en.json`, `de.json`, or `ja.json`. Each catalog is a JSON object mapping IDs to translated strings. Use the line ID for dialogue, the speaker ID plus `.name` for display names, and the document ID plus `.title`, `.author`, or `.body` for readings. Missing translations fall back to English. The English text in the script or item is the final fallback.

When recordings are ready, place PCM WAV clips under `data/Sanctuary/Voice/<locale>/<id>.wav`. Dialogue and observations use their line IDs. Documents use their optional narration IDs, such as `despise.guild.folded_letter.body`. The client tries the selected language first, then English. If neither recording exists, the text remains available and playback is silent.

## Verification and evidence

- For data or documentation, check links and generator inputs. Run the relevant Python checks and inspect exact differences before regenerating pinned evidence.
- For game behavior, use disposable world copies and separate client settings. Check the selected runner’s flags and runtime paths before launching it. Finish tests that use source assemblies before rebuilding those inputs.
- `python3 tests/check_current_class.py druid --plan` checks fixture inputs without starting a server. Substitute the class under test and consult `--help` for runtime and output options.
- Keep a distinction between what a specific fixture exercised, what was seen in ClassicUO, and what is installed for play. When save migration is involved, exercise a saved character as well as a fresh one.
- Run the Python suite with `python3 -m pytest -p no:cacheprovider -q tests`. Its catalogue checks use tracked inputs. The named-item and affix tests exercise report rules separately from live export provenance.
- Use `tests/export_runtime_audits.py --stage <stage>` for current native named-item and affix evidence. Keep the export stage in a disposable directory under `.local/` with the current content and integration assemblies, and remove it once its reports have been verified. [Report and evidence commands](../tools/reference/README.md#items-and-professions)
- The original class release verifier and its installed-build summary are available in Git history at `bc09bbc9`. The class roadmap still distinguishes that historical qualification from current evidence. The archived class fixture plans can require local probes retired during disk cleanup. [Requalification](roadmap.md#combat-classes-and-companions)

## Dungeon layout trials

The Hythloth layout tool creates a fixed four-room trial using installed floor and wall art. Its trial corridors are two tiles wide. The current Haven crypts have separate four-wide layouts.

```sh
python3 tools/generate_dungeon.py .local/dungeon-previews/trial --seed 27 --origin 7100 3950 0
```

Open `plan.svg` for the floor plan. `layout.cfg` contains ModernUO decoration placements and records the entry and suggested treasure location. Keep the configuration of a layout you want to reuse. Pass another seed, or omit `--seed` for a new layout. Use a clear 32×32 trial footprint with a suitable base Z.

On a disposable world, copy `layout.cfg` into `Data/Decoration/Trammel` and use `[Decorate` as a Developer. This processes the entire stock decoration set as well. For seed 27 and the example origin, use `[Go 7108 3957 0` on Trammel. A permanent content installer can apply an individual file with `DecorationList.ReadAll` on the intended facet. [Custom placements](worldbuilding.md#placeable-templates)

## Installed updates

Save the world and wait for the snapshot to finish before shutting down the relevant live processes. Keep a focused rollback copy for the parts being changed. Include saves and configuration for world migrations and profiles and any affected client data for client changes. Stage matching server/client files as needed. Verify startup and load the affected saved state on a disposable copy before normal use. The launcher’s **Save & Shut Down** provides the local save-and-stop path.

## Documentation maintenance

Put ongoing system rules in the handbook, tasks in the [roadmap](roadmap.md), and generated technical records under `tools/reference`. Use the [historical tree](README.md#historical-records) for earlier checkpoint detail and exact old output paths. Keep release hashes tied to the evidence that supports them.
