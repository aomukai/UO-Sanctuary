# T05 — first profession, activation and training

## Confirmed scope and acceptance

Started 19 October 2026 (JST). The user accepted all six recommendations: preserve existing skills/caps and staff settings with an unconfirmed first craft; crafting-only practice30–70; linear100%→1% gain chance per ten-point band; configurable1gold/+0.1 trainer prices; first-choice confirmation at crafting trainers before payment; unrestricted basic processing, with crafting gains restricted to the matching active craft and gathering always active.

Implement one selected craft with a distinct confirmed bit; Alchemy is the initial display default, not a lock. Cartography joins the ten crafts. Keep native skill values/caps/locks authoritative; the total cap must not force forgetting or block independent progression. Power Scroll consumption rules and full wardrobe swapping remain later work.

Acceptance checks before implementation:

- Confirmation/cancel, repeated/stale gumps, another tool, changed/missing tool/trainer, death/disconnect and reload cannot change a confirmed choice or consume costs on rejection.
- Server checks cover native recipe start, both completion paths and delayed gain, repair/enhance, the prototype Imbuing action and trainer payment. Harvest, skinning, basic processing and salvage remain exempt from profession selection; inactive craft cannot gain through them.
- All thirteen retained skills can be trained to30 through a valid trainer for exact/partial payment without losing other skills; inactive crafts and unrelated skills cannot train. Caps/locks are respected and existing values preserved.
- Ten crafts have four explicit practice bands. Each attempt consumes the specified ordinary inputs, yields nothing, rolls exactly once for +0.1, consumes the native tool use and cannot cross its band/cap or run through stale/replayed requests. Quantities are provisional authoring data, not a change to later production recipes.
- Test old-character defaults, new selection persistence, independent gains over the native total cap, exceptions and native bypass entry points; exercise actual client confirmation/practice/trainer interaction and save/restart.
- Preserve installed binaries/saves/configuration/preferences. `sanctuary.professions.enabled` defaults to false until T14/T15 supply the earned switching route; test it only in disposable worlds. New trainer/tool types may be tested with fixtures; their production stock/placement remains T11/T17, not a GM-kit acquisition claim.

Status: **implemented and verified; disabled by default and not deployed**.

## Result and state ownership

[ProfessionProfiles](../src/Sanctuary.Content/ProfessionProfiles.cs) owns only a character's selected craft and confirmation bit in the new version-0 `SanctuaryProfessions` persistence. Missing records mean Alchemy displayed with an open choice. Existing skill values, caps, locks, class progression and staff settings are neither inferred into a profession choice nor rewritten. Confirmed Alchemy is distinct from the unconfirmed alphabetical default. There is no ordinary-tool route to change a confirmed profession; T14/T15 must provide the earned switch.

Native `Skills` remain the sole owners of trained values, per-skill caps and locks. New native skills start at100; existing caps remain intact, including higher legacy caps. Under the enabled feature, ordinary class characters gain retained skills independently of the aggregate cap: the native shared-cap lowering step is skipped, and total-skill room is treated as full when calculating gain chance. The stored aggregate cap itself is preserved. This keeps fresh-character gathering pace without forcing another skill down as the character develops additional professions. Staff and characters without a class keep their existing rules. Power Scroll acquisition/order/trading policy remains T37/Q09 work.

`[Profession` displays the current choice/status and native craft values/caps. Native tools open a profession tool panel with access to their normal actions and one practice attempt. The confirmation explains class80 OR crafting100 plus earned town-hall access, and cancellation leaves the choice open. Repeated prompts replace the pending token; replies validate the current character/connection, live accessible tool or nearby trainer and unchanged first-choice status. Pending prompts and training quotes are transient and cleared on disconnect/pruned on save.

Cartography is in the shared ten-craft list and the client's Crafting group. Gathering remains a separate three-skill group. [Client patch0021](../patches/ClassicUO/0021-cartography-crafting-group.patch) removes the obsolete Treasure Hunting group; existing client loading already rebuilds these fixed sections without changing server skills.

## Native actions and exceptions

[Server patch0015](../patches/ModernUO/0015-profession-activation-hooks.patch) exposes optional UOContent callbacks; no new server-engine save format is required.

| Path | Enabled behavior |
| --- | --- |
| Native tool double-click | Confirm first craft before action; confirmed characters see tool actions/practice. Equipped accessible tools and backpack tools are supported. |
| Both native recipe starts/completions | Reject inactive production before costs, output or gain. A delayed craft checks again before effects/completion; the gain-producing success check is also gated. |
| Native repair/deeds/enhancement | Check the applicable craft at entry and repair target resolution. The later NPC maintenance service remains T12. |
| Native trainer quote/gold hand-in | Confirm before offering a crafting lesson; reject inactive/unretained training, respect locks/caps and validate a per-student trainer/skill/base/cap/connection quote before consuming payment. |
| Existing project Imbuing | Requires selected Imbuing in quote/commit paths. The legacy `[Essence` target route can request the same first-choice confirmation before costs. Its prototype prices/rarity rolls remain unchanged until T17. |
| Native treasure-map decoding | Checks active Cartography. This does not implement the new map/treasure progression in T32/T36. |
| Harvest, skinning, ore/board/textile processing and salvage | Do not select a profession. Their existing native outputs remain available; crafting gains require the active matching craft, gathering gains remain active. The Board/Cloth/Yarn/Thread recipe exceptions are explicit. New scrap conversions/weights remain T07. |

Inactive tool menus may still expose native processing/salvage actions; opening a menu is not permission to produce. The server transaction checks enforce production independently of the menu. Existing ore, textile and skinning handlers are retained, rather than applying a blanket tool ban.

## Training and practice data

Trainer lessons stop at the smaller of30.0 and the native per-skill cap. Default price is **1gold/+0.1**, configurable with `sanctuary.professions.goldPerTenth` (integer1–100). Smaller payments buy the corresponding partial lesson; excess gold is returned through the native backpack/stack path with its origin intact. A stale or rejected training conversation cannot turn the handed-in gold into a native gift. Gold interactions outside a training conversation retain their native behavior. Direct `Teach(..., apply:true)` is not a free skill-grant route under this feature: the validated gold hand-in owns the mutation.

[The practice catalogue](../data/Sanctuary/Balance/profession-practice.json) contains these **provisional practice-only** inputs:

| Craft | Exact ordinary input | Amounts at30/40/50/60 |
| --- | --- | --- |
| Alchemy | Ginseng | 2 / 4 / 6 / 8 |
| Blacksmithing | Iron Ingots | 2 / 4 / 6 / 8 |
| Bowcraft/Fletching | Logs | 2 / 4 / 6 / 8 |
| Carpentry | Boards | 2 / 4 / 6 / 8 |
| Cartography | Blank Scrolls | 2 / 4 / 6 / 8 |
| Cooking | Raw Fish Steaks | 2 / 4 / 6 / 8 |
| Imbuing | Magic Essence | 1 / 2 / 3 / 4 |
| Inscription | Blank Scrolls | 2 / 4 / 6 / 8 |
| Tailoring | Cloth | 2 / 4 / 6 / 8 |
| Tinkering | Iron Ingots | 2 / 4 / 6 / 8 |

Exact native types prevent practice consuming colored resources as ordinary inputs. A valid attempt consumes all stated inputs and one native tool use, produces no item, and rolls once for +0.1. Chance is `100 − (trained skill in tenths mod100)` within30.0–69.9:100% at each band's beginning,1% at its last tenth. The captured band, skill lock/cap, tool, ingredients and current action/cooldown are checked before consumption. Practice cannot continue through70.0. A one-second skill-action deadline and consumed gump response prevent immediate replay; future tool charge-saving/station rules remain T06.

The [instructor and Imbuing ladle types](../src/Sanctuary.Content/ProfessionTools.cs) support tests and later production placement/stock. The ladle currently uses native utensil art and the existing Imbuing interface; it does not establish the final cauldron/station transaction. No instructor, ladle stock, material source or earned wardrobe was installed into the world. Useful production recipe books and their skill/gain bands remain their later cards.

## Verification

The [server fixture](../tests/Sanctuary.Integration/ProfessionChecks.cs) passed all ten first choices, all thirteen retained trainer skills and all forty practice bands. It checks cancel/stale/replaced/missing-tool prompts, trainer confirmation, exact partial payment and excess return, inactive/unrelated training, shared-cap independence without lowering another skill, skill locks/caps, missing-input/cooldown/stale-band rejection and band-end/no-gain consumption. Existing craft progress42.7/cap110, Focus5.1 and Mining cap105 survived selection; native values/caps/locks and selected professions survived four disposable boots/saves/restarts. Empty old-selection data defaults to an open Alchemy choice without touching native skills.

Direct native calls to both Craft/CompleteCraft paths and gain-producing success checks could not craft/gain Tailoring while Alchemy was active. Inactive repair/enhancement and project Imbuing were rejected; board conversion and salvage remained available. The test instructor/ladle and saved native tools reloaded correctly. These are automated fixtures, not a completed production acquisition loop or a manual player balance pass.

The [actual-client exercise](../tests/check_professions_ui.py) displayed the initial Alchemy/open-choice status, cancelled a tool prompt, confirmed Tailoring through the trainer prompt, opened the tool panel and submitted its practice response. Server assertions verified0→30 training for300gold, then30.0→30.1 practice consuming2cloth/one tool use and producing nothing. Native gold hand-in is invoked by the fixture; the test does not claim mouse-driven coin dragging. The real gump replies and saved client character/tool/trainer/skill/gold/material state passed four boots. Confirmation and practice screenshots were visually inspected for legibility and layout.

With the feature left disabled, the existing four-boot regression passed creation/class names, origins, gear, T04 new/legacy state, economy, monster profiles/XP/revival claims, belt, pets, resurrection, monsters/combat and class-profile migrations. The T02 audit was refreshed from the final build:24 exact affix-pool queries/1,536 seeded rolls, unchanged effect pools. All12 Python checks passed. Server/content/integration builds passed with zero warnings/errors; the client build passed with59 existing warnings. Fresh/reverse/repeated patch reproduction passed: **15 ModernUO patches /35 paths;21 ClassicUO patches /35 paths**.

A disposable copy of the installed world, with professions enabled in that copy, passed two boots/save/restarts. Its ordinary class character received the unconfirmed Alchemy default; its existing staff character remained unrestricted. Existing profiles, allocations, all native skills/caps/locks, worn identities, gear rolls and origins were preserved. This was a copied-save upgrade, not a live migration.

## Reproduction and release boundary

Use the non-staging server/content/client build commands documented in [T04](t04-item-state.md) and [T03](t03-class-names.md), then build the integration assembly. From the Sanctuary root:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_PROFESSIONS=1 SANCTUARY_TEST_PROFESSIONS_UI=1 \
SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/t05-professions-20261019/ui-state.json" \
SANCTUARY_TEST_FIRST_LOOP=1 SANCTUARY_FIRST_LOOP_ROOT="$PWD" \
SANCTUARY_FIRST_LOOP_AUDIT="$PWD/tools/reference/first-loop-affix-audit.json" \
python3 -u tests/check_britain.py
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" SANCTUARY_TEST_PROFESSIONS=1 \
python3 tests/check_working_baseline.py --output "$PWD/.local/logs/t05-professions-20261019/copied-world"
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation --check
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

`SANCTUARY_TEST_PROFESSIONS=1` enables the feature **only in the disposable harness configuration**. It runs the profession fixture separately from the ordinary comparison fixtures, which intentionally assume all retained crafts are usable. For the normal comparison suite omit this switch and use the existing T04 flags; harnesses sharing `britain-baseline-check.log` must run sequentially.

The runtime setting **`sanctuary.professions.enabled` defaults to false**. Keep it false until T14/T15 supply recognition and the public craft-switch route. No live console command, deployment, account/progression change or preference change was made. All322 existing protected runtime/configuration/save/preference files recorded at entry remained byte-identical, and the same installed server process remained running.

Deployment later requires the matching cumulative server/content/client artifacts, save/backup/stop/install/restart checks and the release bundle's completed access route. Do not stage the integration assembly, fixtures or test kits. Disabling the feature suspends activation enforcement; rollback after real training needs the pre-upgrade save and matching prior binaries to restore the prior game state. Native skill caps/locks/values remain native throughout.

Supplementary local evidence is under `.local/logs/t05-professions-20261019/`: build and fixture logs, screenshots/UI state, copied-world snapshots, patch reproduction, Python results and protected before/after manifests. These logs supplement the versioned specification, source, tests and patches. [T06](t06-crafting.md) subsequently implements the first shared tool transactions and station rules; T07 is now the next recommended card.

Final build SHA-256 values:

- `Sanctuary.Content.dll`: `77f1474b5a744d992b0d2bd0e9edc5533cf77c89cc5d34d24da0d4de22d76958`
- `Server.dll`: `ff7ae9a6a92d0f848c470a623a95428594a8f995ae1b65cf1240f2ebaea48486`
- `UOContent.dll`: `62a6942457e44f8ca634d89e7421356c90a14534d4dacb1ef9fe8bc6af6a55d1`
- Client `cuo.dll`: `09a9e7b3984f331d3ad1b6dcd0eba11efc9ba32a329f4fda6b2aaf070894d08c`
