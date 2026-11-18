# Retiring the class-goal run archives

On 18 November 2026 the user explicitly approved deleting historical run artifacts after the class goal was finished. `/media/aomukai/FILES/Sanctuary-checkpoint-archives/` has been deleted, without creating a replacement archive. Its 874 recorded staging-link locations were retired; some locations aliased the same physical link. Approximately 11.5 GiB was freed on FILES.

## What remains necessary

The eight current class configurations use 349 distinct class-specific runtime input paths. Including shared source cases and direct C# readers gives 354 required input files. These are expected source/reference vectors and other test inputs, not outputs of the historical runs. Their existing locations, checksums and consuming classes are listed in `tools/reference/current-class-regression-inputs.json`. None resolved into the deleted directory, so no reference input needed copying or moving. They remain available in the retained development workspace; some are local pinned-reference material rather than files distributed with a fresh Git clone.

The game source, balance data, approved rules, tests, release information, installed binaries, current saves, configuration and SDK remain. No old binary or generated test world is needed to run the game. This cleanup does not delete the separate host archive tree or the current rollback packages.

## What was retired

Historical compiled binaries, logs, generated worlds, duplicated source snapshots and failed-run bundles in the requested directory were discarded. `tools/reference/class-skill-history-retirement.json` marks the affected receipt chain as historical and no longer supported for full raw-run replay. The recorded results and their original hashes remain unchanged in Git; deletion does not turn those historical results into new test results.

The former release checker reopened those old files. The current `tools/verify_class_skill_release.py` instead checks the committed source policy, data hashes, recorded qualification summaries, talent identities and asset registers. It does not promise to replay unavailable runs. Installation checks are optional and use the actual installed binaries. Its live smoke operates on a disposable copy of the current world and compares real saves/configuration before and after; it neither depends on the old world snapshot nor saves over the actual world.

```bash
python3 tools/verify_class_skill_release.py --check
python3 tools/verify_class_skill_release.py --check --runtime-root /media/aomukai/FILES/Sanctuary
python3 tools/verify_class_skill_release.py --smoke --runtime-root /media/aomukai/FILES/Sanctuary
```

The old `tools/record_*`/historical audit readers remain provenance recipes. Their former local file lists are historical, not a requirement to keep old runs indefinitely. The one-off `prepare_unresisted_shield_native.py` command is disabled; its resulting native patch 0075 is in Git and normal `./dev build server` applies it.

## Fresh regression work

Use `tests/check_current_class.py` to select current native/content/Integration binaries explicitly. It reuses the existing class test configuration and places outputs in a temporary directory that is removed afterwards, rather than reopening a past run's compiled DLLs and overwriting its old receipts. The existing per-class lifecycle modules remain configuration providers. A current Integration build is required for actual execution; `--plan` only validates input availability.

```bash
python3 tests/check_current_class.py necromancer --plan --runtime-root /media/aomukai/FILES/Sanctuary
python3 tests/check_current_class.py necromancer --runtime-root /media/aomukai/FILES/Sanctuary
```

`--content-assembly` and `--integration-assembly` can select freshly compiled test DLLs. Build Integration against the matching installed server and content API, for example from the runtime checkout:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration -p:ModernUODistribution="$PWD/.local/server" -p:SanctuaryContentAssembly="$PWD/.local/content/Sanctuary.Content.dll"
```

Cleanup validation compiled current Integration with zero warnings/errors and passed focused Iron Golem birth, Find Item acquisition and Barbarian landing/broken-hand checks against current installed native/content binaries. A copy of the current world loaded/listened/shut down without saving. These focused checks and fixture preservation validate the cleanup; the eight full class cohorts were not repeated.

The working Codex fallback and ChatGPT installation were not modified by this retirement. App repair/reinstallation is a separate action.
