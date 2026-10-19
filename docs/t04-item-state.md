# T04 — persisted item state

## Bounded scope and acceptance checks

Started 19 October 2026 (JST). T02 is complete. Extend the existing serial-keyed `GearProfiles` persistence; retain real native items and their origin tags. Deployment is separate.

- Reuse native resource subtype, creator name and current/maximum durability as their authoritative saved values. Provide a common read projection for the eight T02 bases, including cloth and bone, without interpreting hue as material or changing native durability.
- Version the project gear record for Normal base tier, Average/High Quality/Superior crafting quality, separate native and tinkerer-added empty socket counts, optional identified sale offer and identification state. No recipes, quality/socket/quote generation or identification UI are enabled here. Higher base mappings, durability perks/rounding and jewellery progression remain later-card work.
- Migrate version 0 deterministically: Normal, Average, no sockets, no offer and identified (the existing visible-stat behavior). Preserve defense, affixes, levels, starter flags and native fields exactly. Never call a random generator during migration, copy or reload.
- Preserve metadata and origins through native property copy, duplication, stack splitting and transfers. Converted equipment cannot merge into stacks; ordinary resources retain origin-matched native stacking. Copying a previously implicit white base must freeze its existing serial-derived defense rather than derive another value from the new serial.
- Reject invalid socket slots/counts/provenance, starter sockets and invalid quotes; a tinkerer-added socket must block the existing Imbuing transaction. Imbuing must preserve all other metadata.
- Test version-0/version-1 buffers, malformed records, the eight-base native save graph, copy/split/transfer paths and repeated world save/restart. Exercise the existing client economy flow against the matching test build. Preserve installed binaries, accounts, saved progression, configuration and client preferences.

Status: **implemented, verified and deployed**, with subsequent T08/T09 tier/material consumers. See the [T01–T09 deployment](deployment-t01-t09.md). [T11](t11-vendors.md) advances gear persistence to version3 for original vendor purchase prices and activates saved sale quotes; versions0–2 remain readable.

## State ownership and legacy defaults

`SanctuaryEquipment` advances from version **0** to **1**. Its previous record prefix, affix IDs/values and starter-migration set remain intact. Each version-1 record appends:

| Field | Representation | Version-0 default / boundary |
| --- | --- | --- |
| Base tier | Byte, `GearTier` | Normal. Only Normal mappings are currently authored; Exceptional/Elite are rejected until their base catalogues exist. This is distinct from native UO Exceptional quality. |
| Crafting quality | Byte, `CraftQuality` | Average. Native Low/Regular/Exceptional fields are preserved, without claiming they were produced by the new three-outcome quality roll. |
| Native / tinkerer sockets | Two integers | 0 / 0. Native sockets are zero or the quality's 1/2 count; total capacity is five. Only weapon/shield/head/body stat slots qualify. Starter/newbie equipment cannot have sockets. |
| Identification | Boolean | True, preserving the existing converted-item visible-stat behavior. Native weapon/armour identification flags remain unchanged; this project field is the contract for future converted-item identification. No hidden-stat behavior is enabled here. |
| Identified sale offer | Integer; zero means absent | Absent. Stored offers currently accept the Normal 100–500 band for non-starter, non-vendor equipment. No vendor quote is generated or paid from this field yet. |

[GearDetails](../src/Sanctuary.Content/GearDetails.cs) reads the native `Resource`, `Crafter`, `HitPoints` and `MaxHitPoints`. Bone/cloth have explicit physical identities rather than inheriting their native fallback resource labels; wood-family weapons with native Iron defaults project ordinary wood. Real colored resource subtypes remain intact. Hue is not used to determine material. Native jewellery has durability/resource fields but no crafter-name field; the read projection returns no creator for jewellery. A jewellery creator extension belongs with its authored production route, rather than inventing an old maker.

There is no duplicate durability, material or maker save. Crafting durability perks/rounding, material bonuses, recipes and quality generation remain T06–T09/T17 work. Socket filling and bonuses remain T22–T26 work; these counts represent empty capacity only. Jewellery's five-step progression remains gated by E01. No speculative filled-socket structure, new origin enum, gem stat or rare base is added.

## Copy, stack and transformation contract

[Patch 0014](../patches/ModernUO/0014-item-state-copy-hooks.patch) adds optional content callbacks at native property copy, split and stack validation. They carry the existing serial-keyed Sanctuary record through real native item creation; origin serialization stays at Item version **12**. The source's affix values are copied with separate mutable arrays. A white item without a record gets its existing serial-derived defense frozen on copy; neither the source nor its copy receives a fresh random defense roll.

Native resources, maker, native quality and wear are explicitly copied for the split path, which otherwise copies only common Item fields. Ordinary resource stacks keep their native behavior and same-origin requirement. Converted wearable equipment is rejected by both stack admission and merging, even if a staff operation forces its `Stackable` flag. Trade continues to transfer the actual Item and its serial.

The existing Imbuing quote and preparation paths both reject any tinkerer-added socket. Native sockets remain eligible. A successful transformation replaces only the roll and retains quality, socket provenance, identification and an existing sale offer. Preparing a roll makes no saved-state change when payment fails. No socket-creation or sale UI is introduced.

## Reproduction

Build directly into the upstream build output and `.local/content`, without staging into the installed runtime:

```bash
# From upstream/ModernUO; use the absolute project path for the override and SDK.
../../.tools/dotnet/dotnet build Projects/Application/Application.csproj -c Debug --nologo \
  -p:CustomAfterMicrosoftCommonTargets=/media/aomukai/FILES/Sanctuary/build/ModernUO.targets \
  -p:NBGV_GitEngine=Disabled -p:WarningsNotAsErrors=NBGV1001 -p:PublicRelease=false

# From the Sanctuary root:
./dev build content
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_ITEM_STATE=1 SANCTUARY_TEST_ECONOMY=1 \
SANCTUARY_TEST_CLASS_NAMES=1 SANCTUARY_TEST_FIRST_LOOP=1 \
SANCTUARY_FIRST_LOOP_ROOT="$PWD" \
SANCTUARY_FIRST_LOOP_AUDIT="$PWD/tools/reference/first-loop-affix-audit.json" \
SANCTUARY_TEST_UI=1 SANCTUARY_TEST_COMBAT_UI=1 SANCTUARY_TEST_ECONOMY_UI=1 \
SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/t04-item-state-20261019/ui-state.json" \
python3 -u tests/check_britain.py
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
python3 tests/check_working_baseline.py --output "$PWD/.local/logs/t04-item-state-20261019/copied-world-final"
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation --check
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

The fixture-only `item-state-write-legacy` command emits the original version-0 layout after the second disposable save. The third boot migrates it and writes version 1; the fourth reloads that migrated save. This intentionally strips the fixture's new metadata to test defaults. It is not a production downgrade utility. Existing accounts/saves are only copied by the baseline harness; `SANCTUARY_TEST_SERVER` selects the tested binaries while the installed world's saves remain the read-only copy source.

Do not use `./dev build server` for a non-deployment test: that command stages binaries. A later deployment needs the matching server/content build, a save/backup and the earlier T03 matching client changes. Old content cannot read version-1 gear records; rollback after a deployment requires the pre-upgrade save and matching older binaries. Never install the integration assembly.

## Verification and preservation

The [item-state fixture](../tests/Sanctuary.Integration/ItemStateChecks.cs) passed all eight T02 bases and **32 saved identities**: original, direct property copy, native Dupe and split for each base. It checked native colored metal/wood/leather subtypes, cloth/bone identity, maker names and 17/73 current/maximum durability. Accepted and cancelled native secure trades transferred the same real Item/serial and metadata. Forced equipment stacking was rejected; the existing origin suite also passed real vendor/craft creation, ordinary resource split/merge and mixed-origin rejection. These are server fixtures, not a two-player trade GUI play test.

Version-1 records survived save/restart with quality, socket provenance, identification and offers intact. Version-0 records retained defense, affixes, item/affix/required levels, starter data and native fields with the documented new defaults. The migrated version-1 records survived another restart. Invalid versions, tiers, qualities, socket slots/counts/provenance and sale offers were rejected. Implicit white defense survived copy; failed-payment Imbuing left an implicit item without a new record; valid Imbuing retained native sockets/quality/offer. Starter/vendor copy restrictions passed. No random values are regenerated during these paths.

The final combined harness completed **four disposable boots**, saves and clean shutdowns, including the existing creation/origin/gear/economy/class-name checks and class-profile v1/v0 migration. The T02 audit was refreshed against the final build: **24 exact affix-pool queries and 1,536 validated seeded rolls** passed, with unchanged pools/effects/material observations. All **12 Python checks** passed. Fresh/reverse/repeated patch reproduction passed for **14 ModernUO patches / 30 source paths** and the unchanged **20 ClassicUO patches / 35 source paths**. Server, content and integration builds passed with **zero warnings/errors**; no client rebuild was needed.

The real SDL client passed creation/world entry, the native economy menu, destructive cancellation/confirmation, salvage, Imbuing and vendor-origin rejection. Its fixture white dagger carried High Quality, one native socket and an offer of 321; server assertions after the actual client confirmation verified all three survived Imbuing. The Imbuing confirmation screenshot was visually checked. Target selection uses the established normal server Target callback fixture; this does not establish new pixel-based item targeting, a metadata tooltip, a socket UI or a revised economy balance. These are automated client observations, not new player feedback. The first GUI invocation omitted its required state-file environment variable; it was corrected and the final combined run passed.

A disposable copy of the **installed version-0 world** loaded with the final server/content build and completed two save/restart boots. Both existing players retained profiles, allocations, all native skill values/caps/locks, worn identities, gear rolls and origins. Its 510 items were all Drop-origin; richer Vendor/Crafted/socket states are established by fixtures. The baseline harness now accepts `SANCTUARY_TEST_SERVER` so an upgrade can be tested against copied live saves without staging binaries.

All **262 existing protected files** recorded at entry stayed byte-identical, including installed DLLs, native saved `.bin` payloads, runtime configuration and client settings/profiles. The installed server remained running; no live console mutation, deployment, account change, progression change or ClassicUO source change was made. Source/build output differs from the installed comparison runtime.

Final build SHA-256 values:

- `Sanctuary.Content.dll`: `eeab5ecdda3ddfe336dfc52c35d72edc739845c5553862a8060b21ad9bb5a285`
- `Server.dll`: `ff7ae9a6a92d0f848c470a623a95428594a8f995ae1b65cf1240f2ebaea48486`
- `UOContent.dll`: `ee57a0c84310a4218d58acde8a53ee96a0066a24624f95dbfff862b0597a3081`

Local supporting evidence is under `.local/logs/t04-item-state-20261019/`: build logs, `final-summary.log`, `server.log`, `ui-client.log`, economy screenshots/state, `copied-world-final/`, Python results and before/after protection manifests. The specification, migration rules, fixture sources and reproducible patch are versioned here; ignored logs are supplementary evidence. [T05](t05-professions.md) is also complete with its six decisions confirmed and its activation feature disabled until earned switching exists. T06 is next.
