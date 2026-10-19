# T07 — gathering and ordinary material processing

Implemented, tested and **deployed**19 October2026 (JST). See the [T01–T09 deployment](deployment-t01-t09.md); its explicit activation decision supersedes the historical disabled-release notes below. This card uses the existing disabled-by-default `sanctuary.professions.enabled` release flag. It does not enable the profession release or change installed saves, configuration, runtimes or client preferences. Equipment recipes remain T08, full vendor pricing/restock/trainer authoring T11, corresponding-tool equipment salvage T16, and tiered Imbuing T17. Gems and bark retain their own cards.

## Confirmed model

The user selected raw→processed at half weight, followed by processed→ten generic scraps at the same total weight. The final step does not halve weight again. Native integer per-stack carried weight rounding remains `ceil(unit weight × amount)`.

| Input | Unit weight | Direct output | Unit weight |
| --- | ---: | --- | ---: |
| Log / board | 2 / 1 | 10 Wooden Chips | 0.1 |
| Small / medium / large ore clump | 1 / 2 / 4 | 5 / 10 / 20 Metal Scraps | 0.1 |
| Ingot | 1 | 10 Metal Scraps | 0.1 |
| Hide / leather | 2 / 1 | 10 Leather Strips | 0.1 |
| Cloth / uncut cloth | 0.1 | 10 Cloth Scraps | 0.01 |
| Bolt of cloth | 5 | 500 Cloth Scraps | 0.01 |

Ore keeps its native½/1/2 ingots per small/medium/large clump; normal smelting keeps its skill thresholds, success check and failure losses. Odd small clumps remain when smelting. Native wood thresholds, ore/wood harvest definitions, hide/species assignments and skill-free skinning remain. Wool/tainted wool/cotton/flax retain their native weights and yields:3/1 yarn or6 thread, respectively; five yarn/thread inputs advance a native loom to one bolt, and scissors turn a bolt into50 cloth. Configured expansion behavior remains authoritative, including native human/facet harvesting bonuses.

There are only four generic outputs: **Metal Scraps, Wooden Chips, Leather Strips, Cloth Scraps**. They carry no coloured resource subtype. The quantity/confirmation panel warns that colour and subtype will be lost; ordinary smelting, board-making and hide cutting retain native output subtypes. Converted outputs have Crafted origin and merge only where normal origin/type/hue/name/state stacking permits. Weight migration preserves existing amounts, subtype, hue and origin.

| Tool | Inputs | Eligible gain skill |
| --- | --- | --- |
| Smithing hammer | Ore / ingots | Blacksmithing |
| Tinker's tools | Ore / ingots | Tinkering |
| Fletching tools | Logs / boards | Bowcraft/Fletching |
| Saw | Logs / boards | Carpentry |
| Tailoring kit | Hides / leather / cloth / bolts | Tailoring |

Valid scrap actions always produce their output and spend no tool uses, gold or station fee. No profession selection or town facility is required. A usable tool must remain accessible. Exactly one native-style skill check follows a committed action for the matching confirmed active craft, below30 and below the individual cap with the skill locked Up. Native gain steps, acceleration and anti-macro behavior remain; a scoped native gain ceiling prevents acceleration overshooting30. Inactive/unconfirmed crafts and trivial processing do not check for gains.

## Transactions and native integration

[MaterialProcessing](../src/Sanctuary.Content/MaterialProcessing.cs) and [ProcessingGump](../src/Sanctuary.Content/ProcessingGump.cs) provide partial quantities, all-input batches and cancel. Processing-capable tools open their panel before first-profession selection; choosing production still asks for confirmation. Existing production/practice activation remains enforced.

A quote captures its owner, connection, tool/uses/parent and exact source identity, amount, parent, art, hue, resource, origin and weight. Commit revalidates it and claims it once. Changed, moved, missing, foreign, disconnected or replayed work cannot consume inputs or gain skill.

Delivery preflights the entire result against native60000 stack limits, compatible existing stacks, freed source slots and exact integer pile-weight deltas. Maximum batches split into complete stacks; overflow is rejected without partial consumption or quantity clamping. Weight-reducing or weight-neutral processing remains possible while already encumbered; weight increases and additional item slots must fit. Consumption and delivery run together on the game loop, without a fallback to dropping output on the ground.

[Native patch0017](../patches/ModernUO/0017-material-processing-hooks.patch) supplies narrow UOContent callbacks for these native paths:

- Smelting preflights before its native success/gain roll; failure still applies native ore loss and clump-size changes. Same-resource ore combining preserves normalized content weight and origin.
- Board-making, hide cutting and bolt cutting preflight before consuming. Axe/scissors accessibility is checked at use.
- Spinning retains the native wheel timer but defers input consumption until delivery. Source changes, movement from the wheel, disconnect, restart or a pack filled before completion leave the input intact. Cotton/flax follow the same rule.
- Native loom phase remains its persisted progress authority. A rejected fifth input leaves phase4 and that input intact.
- Pure weight getters use canonical weights; deserialization clears obsolete explicit weight overrides for these materials. No serialized fields or versions were added, no world scan is required, and no items are deleted or automatically dropped during normalization.

Native crafting-menu production and its general recipe completion remain the T06/T08 production boundary. This card covers the ordinary direct forge/axe/scissors/wheel/loom processing handlers and new scrap actions.

Cloth, uncut cloth and oil cloth cannot create new bandages. Existing bandages remain. NPC stock/restock and buyback exclude bandages and raw/processed metal, wood and leather, including generic metal/wood/leather scraps. Buyback filtering also checks commodity contents and nested bundles. Checks cover listing and purchase validation, before gold is taken. NPCs may still buy these resources from players; their prices and full economy authoring remain T11. Textile supply stays available.

## Verification

[ProcessingChecks](../tests/Sanctuary.Integration/ProcessingChecks.cs) exercises12 material/tool routes, partial/all quantities, coloured-input generic output, origin and merges, no profession selection/charges/inactive gains, individual caps/locks, one gain check and accelerated gain ceiling. It checks60000 logs→ten60000-chip stacks, rejects a500-stack bolt result intact, checks partial merges across60000, source-slot reuse in full packs, weight reduction while encumbered, invalid quantities, foreign/moved/changed/deleted sources and moved tools/axes.

Native checks cover all four ore arts and matching ingot quantities, odd remainders, successful/rejected/failing smelts, clump reweighting, mixed-clump combining, Oak boards, Barbed leather, dyed bolt cutting, all three bandage-cutting exclusions, loom progress and rejected fifth inputs, normal/tainted wool and cotton/flax yields/hues, stale and full-pack delayed wheel completion. Restart checks retain pending wool without replaying output and retain loom phase4. Mining definitions/gathering activation and native cow/lizardman/dragon skinning verify preserved thresholds, species quantities and native bonuses. Vendor checks cover forbidden stock after restock, allowed cloth, nested/deeded resources and forged buyback purchases through actual smith/carpenter/tanner/fortune-teller handlers without charging gold.

The [actual client test](../tests/check_processing_ui.py) uses real mouse replies for the tool processing action, warning/cancel, partial quantity, full-pack rejection and encumbered make-all. The fixture supplies material hand-in through the native target callback. The final disconnected quote rejects. Output, uses, skill and open profession choice survive four disposable boots/saves/restarts. Separate actual-client T05 and T06 regressions still pass confirmation/training/practice and native tool crafting/full-pack rejection.

Sixteen synthetic existing stacks verify canonical weight normalization without changing counts/hue/origin, including preserved bandages. A disposable copy of installed saves passes disabled→enabled→enabled boots/save/shutdowns: two characters keep profiles, allocations, native skills/caps/locks, worn identities and one gear state. The copy's existing material cohort is only one10-bandage stack, which stays intact; it is not evidence of a large live resource migration. Enabling the feature in the copy adds the eleven configured town facilities (Drop-origin count510→521).

The disabled comparison verifies original ingot/hide weights, explicit weight preservation, native bandage cutting/vendor supply and unavailable scrap quotes. Broader comparison coverage includes creation/class names, new/legacy item state, economy, monster profiles/XP/revival, belts, pets, resurrection and combat. First-loop evidence retains24 pools/1536 seeded rolls, with only content/source provenance hashes changed; all12 Python tests pass. Server/content/integration builds have zero warnings/errors. Fresh/reverse/repeated patch reproduction passes17 ModernUO patches/51 paths and22 ClassicUO patches/36 paths; no client production changes were needed. Evidence is recorded locally under `.local/logs/t07-processing/`.

All217 existing installed runtime/configuration/save/preference files recorded at entry remained byte-identical. No installed server was started or stopped; the original questionnaire replies remain byte-identical.

## Reproduction and release boundary

Use the non-staging builds in [T04](t04-item-state.md), then:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_PROFESSIONS=1 SANCTUARY_TEST_PROCESSING=1 \
SANCTUARY_TEST_CRAFTING=1 SANCTUARY_TEST_PROCESSING_UI=1 \
SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/t07-processing/ui-state.json" \
python3 tests/check_britain.py
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_PROFESSIONS=1 SANCTUARY_TEST_PROCESSING=1 \
python3 tests/check_working_baseline.py --output "$PWD/.local/logs/t07-processing/copied-world"
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation --check
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Use `SANCTUARY_TEST_PROFESSIONS_UI=1` or `SANCTUARY_TEST_CRAFTING_UI=1` instead of the processing UI flag for their separate client regressions. The feature remains disabled in installed configuration; a later release requires the matching server/content build and a full save/runtime backup before weight normalization. No staging, deployment or GitHub push is part of this card.
