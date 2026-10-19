# T01 working baseline — 19 October 2026

## Scope and acceptance checks

T01 re-establishes the comparison build before further implementation. It changes no gameplay, installs nothing, and leaves the live server running. Existing dirty project and upstream work is intentional and must be preserved.

Acceptance:

- Record root/upstream revisions, patch reproduction, runtime artifact hashes and running processes.
- Inspect saved character/equipment/binding versions, retained native skills, item origins and spent reward-life state.
- Run the existing Python checks, content/integration builds and relevant isolated server/client checks, including a disposable save/restart round trip.
- Compare live configuration, accounts, progression and client preferences before/after; explain any observed changes and limitations.
- Record actual differences from the roadmap's comparison table, distinguishing source inspection, automated client interaction and player feedback.

Local evidence is retained in `.local/logs/t01-baseline-20261019/`. The completed findings below will carry the essential cold-start evidence in the repository; ignored logs are supplementary.

Status: **complete**. No gameplay change, deployment or save migration was performed.

## Source, patches and installed runtime

The root revision is `7b022527a7dbcde6eab349dfc57fa15a28f89d72`, with substantial pre-existing modified and untracked project work. This commit alone does not identify the comparison build. Both upstream checkouts are also intentionally dirty:

| Component | Pinned revision | Patch verification |
| --- | --- | --- |
| ModernUO | `eff9de028136b11e544fd81375d1e0e710e22dd6` | All 13 patches reproduce all 29 affected paths from HEAD exactly. |
| ClassicUO | `ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93` | All 19 patches reproduce all 34 affected paths from HEAD exactly. |

Patch verification used disposable `/dev/shm` trees, followed by a reverse/reapply round trip. It did not apply patches to either working checkout. The initial source/data/patch file hashes and all three Git status listings are recorded locally. For identifying this dirty baseline, the SHA-256 of the sorted JSON path-to-SHA-256 manifest for all 32 patch files is `55f1cedfe7dd53a5914a0c697d2c471901e401dab910f73ceadcbe386dabd177`.

Installed artifact SHA-256 values:

| Installed artifact | SHA-256 |
| --- | --- |
| `.local/server/ModernUO.dll` | `2951bd048001a0304e31e05871dbed032f779a14f00d16e7372f7f82996ddb31` |
| `.local/server/Server.dll` | `30c5a67dc71251df32b3af552d1abda2b0d399b2d90a782d45757002ae9160e2` |
| `.local/server/Assemblies/UOContent.dll` | `51fa0e6f16ddf8a9459cc4c1a264079e403513a5c8c3e2748d7ba0613369d131` |
| `.local/server/Assemblies/Sanctuary.Content.dll` | `0737f5a4adbbb26a5901bfcc0722da6d998983377373c0c0df9690f31803103e` |
| `.local/client/cuo.dll` | `100cfcf2f80ff4e7441c6b20ded423b83639d98bdfa3d5daa3ef51317fe9d795` |

Installed Server/UOContent match `upstream/ModernUO/Distribution`; installed project content matches `.local/content`, including after the content rebuild; installed client matches `upstream/ClassicUO/bin/Debug/net10.0`. All 23 installed Sanctuary data files match repository data. These are artifact comparisons, not claims that native/client sources were freshly rebuilt in T01.

At entry, the live server was PID **348733**, started **18 October 2026, 18:53:50 JST**, running the local .NET host with `.local/server/ModernUO.dll` and working directory `.local/server`. Its memory maps point to the installed application, Server, UOContent and project content paths above. The desktop client was closed. No integration assembly was present in the live runtime or its assembly list.

## Persisted state and migration boundaries

The installed save was copied only after before/copy/after hashes agreed, then loaded with the installed binaries and data in a disposable runtime. Inspection ran on the server loop without invoking fixture creation commands. Names and account credentials are omitted from its JSON inspection output.

| State | Observed contract |
| --- | --- |
| Character profiles | Actual `SanctuaryCharacters.bin` header: version **2**, one profile. Reader supports versions 0/1/2; v1 adds talents, v2 adds allocations. One class per serial; no class-history collection or selected-craft state. |
| Equipment / action bindings | Actual save headers: version **0**, one record each. Gear persistence stores the rolled affixes and numerical values. |
| Native item / mobile / skills | Source writers: Item **12**, Mobile **38**, Skills **3**; native entity index **5**. These are source-format observations, not header versions of the whole Items/Mobiles payload. |
| Belt | Generated tabard, restoration potion and belt-holder serializers use version **0**. Real stored items use the ordinary native save graph; occupied-cell persistence is covered by disposable fixtures. |
| Origins | Native Item v12 stores Drop/Vendor/Crafted; older items default to Drop. The copied installed world contained only Drop-tagged items (510 after load), including its one converted dagger. Vendor/Crafted persistence and split/merge/copy behavior are exercised by separate fixtures. |
| Reward life | Corpse flag `SanctuaryNoRewards` is `0x00008000`, stored with corpse flags (corpse generated version 19). XP claims the life before evaluating recipient/XP eligibility; revival carries spent state and sets `NoKillAwards`. The copied installed save had no corpse or raised-undead instance, so persistence evidence for those flags comes from death/revival/restart fixtures. |

The two existing characters in the copied save were:

- An Owner character with no class profile, native skills all at **100.0**, per-skill caps **100.0**, locks Up and native total cap **700.0**. Preserve this legacy staff state; do not retrofit a class or normalize its skill total.
- A Player Necromancer (class ID **103**), level **3**, **2,567 XP**, **10 Vitality** allocated and no other stat allocation. Focus (skill ID 50) is **5.1**, all other native skills **0.0**; all per-skill caps are **100.0**, locks Up and total cap **700.0**. The retained Focus value predates the current gain restriction. Its presence does not mean Focus gains or class regeneration through Focus remain enabled.

All native skill values/caps/locks, profile/allocations, worn identities and converted gear rolls/origins agreed across the copied-world restart. Source `ClassSkills.CanGain` currently allows all 13 retained trades for class characters, without a selected-profession gate; Cartography is still listed in the treasure-hunting group. The ten-craft selection/default/first-confirmation design is future work. Existing-character craft migration remains undecided; T01 changes none of these values.

## Comparison-table qualifications

No contradiction of the roadmap's comparison table has been established within the inspected/tested subset. The distinctions relevant to subsequent cards are:

| Area | Current behavior / boundary |
| --- | --- |
| Movement/combat | Existing bounded steering, pursuit/reach, action deadlines and recovery remain the comparison target. Tile movement and provisional combat tuning remain limitations. |
| Progression/rewards | Normal common-profile XP and pet-owner credit exist. Claiming a spent life is separate from XP actually increasing; gold still has its own payout path. No level-99 dungeon cohort is supplied by this audit. |
| Gear | Converted normal bases and persistent random numerical affixes exist. The implemented-stat allowlist does not imply MF, leech, binary or other reference effects have consumers. |
| Economy | The actual prototype still charges **100 gold / 5 essence**, with no Imbuing skill requirement. Resale remains **10%** of native with **10/25/50** normal/magic/rare caps and native buyback. Kill gold is still collected automatically when eligible, with overflow left on the corpse. The newer manual-gold, tier-quote, no-buyback and profession-gated rules are future cards. |
| Consumables | Persistent tabard belt and restorative use exist; GM test supply is not production acquisition. New recipes/drop supply and buff catalogue remain pending. |
| Careers/world | Existing single-class progression and native trades remain. No first-craft lock, earned public wardrobe switch, multiclass history, complete world population or new death economy was introduced. |

The nine Python checks, content build and integration build passed; both builds reported zero warnings/errors. The first combined server/economy-client pass completed four boots, explicit saves and clean shutdowns, preserving all eight doors, three town spawners and six resident identities. It passed class/gear/origin checks, v1/v0 character migration, common creature profiles and XP/revival claims, combat, pets, normal/project monsters, resurrection, belt and economy reload checks.

The actual SDL client passed class/appearance/Back/reselect/world entry and economy gump cancel/confirm, salvage, white-base imbuing and vendor-origin rejection. The captured imbuing confirmation was visually inspected and shows the existing 100-gold/5-essence charge. Economy targets are provided through the normal server target callback; this does not establish pixel-based backpack targeting. These are automated client observations; no new player feedback or manual live play is claimed.

The separate full client regression passed pixel/fallback hostile picking, stationary Shift attacks and release, pursuit/repeat, continuous movement/release, move-to-cast stopping, held Teeth/cadence/release, dagger/halberd timing, equipment IAS/FCR, nearby corpse-grid transfer/harvesting and saved Run/Walk preference. Its four save/restart boots also passed belt persistence and loot reload checks. Dedicated belt-key/chat, sustained geometry/CPU, approach and recovery UI suites were not rerun; their prior evidence remains in the corresponding implementation reports. Server recovery checks were included in the combined combat/monster suite. This is a bounded baseline, not a fresh manual test of every encounter or feature.

## Reproduction and durable outcomes

From the repository root:

```bash
python3 -m unittest discover -s tests -p 'test_*.py' -v
./dev build content
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
python3 tests/check_working_baseline.py --output "$PWD/.local/logs/t01-baseline-20261019"
```

The new [inspection command](../tests/Sanctuary.Integration/WorkingBaselineChecks.cs) belongs only to the integration assembly. The [copied-save harness](../tests/check_working_baseline.py) loads that assembly into a temporary `/dev/shm` runtime with copied accounts/saves/configuration, installed binaries/data, autosave/account creation disabled and separate loopback ports. It compares all player skills/caps/locks, profiles/allocations, worn identities and gear state after restart, and checks that the serialized class/talent/allocation history agrees across the two saves. The copied save had no spent corpse to inspect; the separate generated-world reward-life fixtures cover that case. Two boots, explicit saves and clean shutdowns passed. An earlier two-boot copy inspection also passed before adding the serialized-history comparison.

Both existing `tests/check_britain.py` runs used these common environment values:

```bash
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution"
SANCTUARY_TEST_CLIENT_DLL="$PWD/.local/client/cuo.dll"
SANCTUARY_TEST_CREATION=1
SANCTUARY_TEST_UI=1
SANCTUARY_TEST_COMBAT_UI=1
```

Pass the common values plus the following as environment variables to `python3 -u tests/check_britain.py`; run sessions sequentially and copy its shared server/client logs after each run:

| Run | Additional environment values | Outcome / retained local evidence |
| --- | --- | --- |
| Combined server + economy client | `SANCTUARY_TEST_ECONOMY=1`, `SANCTUARY_TEST_CREATURE_XP=1`, `SANCTUARY_TEST_CREATURE_PROFILES=1`, `SANCTUARY_TEST_COMBAT=1`, `SANCTUARY_TEST_PETS=1`, `SANCTUARY_TEST_MONSTERS=1`, `SANCTUARY_TEST_RESURRECTION=1`, `SANCTUARY_TEST_BELT=1`, `SANCTUARY_TEST_ECONOMY_UI=1`; UI state at absolute `$PWD/.local/logs/t01-baseline-20261019/economy-ui-state.json` via `SANCTUARY_TEST_UI_STATE` | Passed four boots. `server-economy-ui-summary.log`, `server-economy-ui.log`, `economy-client.log`, `economy-*.png`. |
| Full combat/timing/loot client | `SANCTUARY_TEST_BELT=1`; UI state at absolute `$PWD/.local/logs/t01-baseline-20261019/combat-ui-state.json` via `SANCTUARY_TEST_UI_STATE` | Passed four boots. `combat-ui-summary.log`, `combat-ui-server.log`, `combat-ui-client.log`. |

Other local evidence under the audit directory: `python-tests.log` (9 passed), `content-build.log`, `integration-build.log`, `inspector-build.log` (all builds zero warnings/errors), `patch-reproduction.log`, `copied-world-summary.log`, `copied-world-server.log`, `copied-world-1.json`, `copied-world-2.json`, `copied-save-hashes.json` and before/after artifact/protected manifests. No native/client rebuild, staging command, `./dev setup`, `./dev server` or live-console mutation was used.

## Live preservation and completion record

All **54** protected live files were checked: save/configuration payloads, client settings and the **12** production client profile files under `.local/client/Data/Profiles`. Test clients explicitly used temporary profile paths. During the audit the live server continued its normal five-minute autosaves. Six native index files (Accounts, BOBEntries, Ethics, Guilds, Items, Mobiles) changed only their v5 save-anchor timestamp at byte offsets 4–11; matching original hashes were located in automatic backups and the remaining bytes compared equal. All other protected files, including every `.bin` payload and the class/equipment/binding saves, remained byte-identical. No live account was created and no player progression, control preference or item origin was changed by T01.

Gameplay source, shared protocols, balance data and upstream patches remain unchanged from entry. The content rebuild produced the identical content DLL. The integration output changed solely to add the inspection command and was never installed. The same live PID remained running; the desktop client was left closed.

- **Card / cohort:** T01, comparison-build inventory and isolated persistence baseline.
- **Prerequisite:** T00 verified; no gameplay design decision or new mechanic was needed.
- **Changed files:** this report, roadmap/README/baseline-audit links, and the two inspection helpers linked above. No production source/data/patch change.
- **Migration / rollback:** no save version/default changed, no migration or deployment; live installation requires no rollback.
- **Automated / client evidence:** commands, outcomes and limitations above. No new player feedback.
- **Remaining:** first-profession legacy migration and other open decisions remain gated in their own cards. Manual bank transfers/original GM inventory interaction and broad species/balance play are not newly certified by this audit.
- **Next recommended card:** **T02**, one bounded reference/catalogue cohort. T03 is also dependency-ready; no later gameplay card has been started here.
