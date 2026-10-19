# T08 — ordinary white equipment and tools

Approved and implemented 19 October 2026 (JST). **Agreed standard-material scope complete and tested; not deployed.** The profession release flag stays disabled by default; installed runtime/configuration/saves/preferences remain untouched. The user confirmed completion of the standard-material recipe book, using the native/analogue baseline and deferring exotic materials.

## Approved scope

The [complete recipe book](t08-recipe-book.md) covers201 ordinary equipment identities across Normal/Exceptional/Elite:603 recipes. This includes all mapped standard-material weapons, shields, helmets, bodies, gloves, boots and belts, plus the five existing potion tabards. Material routing is98 Blacksmithing,26 Fletching and77 Tailoring identities. Cloth, leather and bone use Tailoring; wood uses Fletching; metal uses Blacksmithing. BoneHarvester uses the explicitly authored bone recipe. Potion tabards remain physical cloth and retain their existing row counts independently of equipment tier.

The scope accounts for all206 ordinary combat-equipment mappings plus five potion tabards. Ten glass, stone and dragon-scale identities have explicit exclusions and are deferred by user decision. Jewellery remains T17; cosmetic layers receive no invented combat tiers; named rewards/variants do not enter the ordinary book. The ten Tinkering tool recipes retain T06's contract. Native race/gender/class and other equipment restrictions remain in effect.

The original eighteen recipes remain unchanged. New Normal recipes use native quantities and upward-rounded skill minima capped at80, or the explicit analogue/authored baseline in the recipe table. Native logs become processed boards; secondary components, including gems, bark and feathers, remain explicit. Supply for those ingredients stays with its acquisition cards; fixtures do not demonstrate a complete economy.

Retain UO item classes/art and label the base tier clearly. Select the matching D2 reference row per saved item, without reclassifying existing items of that UO type. Use the already-pinned local source tables, not a new external import:

| UO identity | Normal → Exceptional → Elite reference codes |
| --- | --- |
| Longsword | lsd → 9ls → 7ls (Long Sword / Rune Sword / Cryptic Sword) |
| WoodenShield | sml → xml → uml (Small Shield / Round Shield / Luna) |
| LeatherCap / SkullCap | cap → xap → uap (Cap / War Hat / Shako) |
| RingmailChest / BoneChest | rng → xng → ung (Ring Mail / Linked Mail / Diamond Mail) |

Adopt those references' damage/defense, attribute requirements, block and speed fields for the selected variants. Crafted item levels stay35/70/99 and equip-level floors0/60/80. Other affix/socket requirements can raise the final requirement. BoneChest remains physically bone and Heavy, made by Tailoring, despite sharing the Ring Mail numerical analogue. New tier identities must survive copy/trade/restart/Imbuing without changing old Normal identities or rerolling saved values.

## Recipe rules — B01

One successful attempt produces one white item. The first six examples below use processed materials;9 boards replace the native shield recipe's9 logs. BoneChest uses ordinary leather as a secondary ingredient, with bone as its physical identity and no leather-material bonus.

| Product | Craft | Normal / Exceptional / Elite required skill | Normal ingredients |
| --- | --- | --- | --- |
| SkullCap | Tailoring | 0 / 80 / 110 | 2 cloth |
| LeatherCap | Tailoring | 10 / 84 / 112 | 2 leather |
| Longsword | Blacksmithing | 30 / 92 / 114 | 12 ingots |
| RingmailChest | Blacksmithing | 30 / 92 / 114 | 18 ingots |
| WoodenShield | Bowcraft/Fletching | 50 / 99 / 117 | 9 boards |
| BoneChest | Tailoring | 70 / 107 / 119 | 10 bone + 12 ordinary leather |

For a Normal requirement `n`, the higher requirements are `80 + ceil(30n/80)` and `110 + ceil(10n/80)`. Exceptional quantities are2× Normal, Elite3× Normal, including each component of mixed recipes. Quantities and skill requirements belong in versioned recipe data for later tuning.

Retain native-style success/failure and skill checks, exact selected material/hue, failure losses and per-skill caps/locks. Extend the guarded T06 transaction to these outputs. Corresponding profession/tool and public facility remain required, with zero station fee. One committed attempt spends one tool use, subject to T06's crafted-tool saving; cancelled, stale or capacity-rejected work spends nothing and gains nothing. Preflight delivery and all inputs before any charge or roll, including mixed-component recipes and full packs.

The ordinary recipe book is available by skill without a new recipe-scroll requirement. The actual acquisition of105–120 caps and later rare120 recipes remains T36; test fixtures do not satisfy that production acquisition. Native recipes for these same products must not bypass their approved maker/tier/quality contract.

Recipes retain native chance-window widths. A prepared recipe requiring120 skill has a minimum chance of `max(native minimum, 1%)`: native Blacksmithing's zero chance at its exact minimum would otherwise make those recipes impossible at the120 cap. A119.9 base skill still fails the requirement. This exception applies only to the prepared T08 recipes at120 and does not change the original eighteen recipes.

## Approved coloured-material arithmetic — B04

Keep the already-agreed material identities from crafting.md. A colour does not change rarity or add ordinary random affixes to a white base. No native UO exceptional/resource stat or durability bonus should silently stack with the explicit Sanctuary model.

- Roll an inclusive uniform integer1–5 **percentage points for each listed resistance**, independently, once for an armour/shield item. Add these to ordinary resistance bonuses before the existing resistance cap. Two-resistance hides give two bonuses (2–10 points combined), and both named identities always contribute.
- Roll an inclusive uniform5–10% once for a weapon. Add typed damage based on that weapon's unmodified reference base damage, before Strength/Dexterity, skill, quality or affix increases. This adds damage; it does not convert existing physical damage. Apply the percentage in the hit calculation before final integer rounding, so low-damage bases are not permanently rounded to zero in their stored ranges. Shadow/Bloodwood use added physical damage.
- Random element pool: Fire, Cold, Lightning, Poison. Roll once and persist the choice; exclude Poison from Barbed leather's random choice because its other resistance is already Poison. Spined uses Cold/Lightning; Horned uses Fire/Physical; Barbed uses Poison/random.
- Gold: persist a uniform1–2% MF (armour/shield) or GF (weapon) roll. Verite: retain the agreed fixed1 life (armour/shield) or mana (weapon) per5 seconds. No bonus is invented for plain materials, cloth or bone.
- T08 persists the selected resource and these rolls. T09 activates and measures supported stat/damage/regeneration consumers; MF/GF stay gated until their actual reward consumers exist. Dormant bonuses must not be advertised as active effects. The overall profession release stays disabled, as already required by the roadmap.

The user confirmed these formerly open arithmetic choices for T08; they supplement the unchanged original questionnaire replies.

## Approved durability rounding — B06

At the producing craft's base skill100+, independently roll an integer bonus from10 through20 percent. Start with the item's ordinary native durability roll, excluding native exceptional/coloured-material bonuses, and set both maximum and current durability to `ceil(base maximum × (100 + bonus)/100)`. The new item begins fully repaired. Apply this once, then let existing native wear fields own the resulting values; copies/restarts/Imbuing must not apply it again. Record the maker's name at the same threshold, regardless of quality. Below100 there is no extra durability or maker mark.

Already-agreed quality remains89% Average /10% High Quality /1% Superior after success once eligible at `min(required skill+10,120)`. At100+ crafting skill, socketable Average/HQ/Superior bases start with0/1/2 native sockets; no tinker provenance is invented. Quality does not add a separate damage/defense boost. Empty sockets must be visible; insertion remains its own card.

## Acceptance

- All603 approved recipes produce usable white gear with exact maker/material/quantity/tier/item level/attributes/equip floor; tools still follow T06. Existing Normal gear retains its identity and stats.
- Quality probabilities/eligibility,100-skill sockets/maker/durability boundaries and one-time durability rounding pass deterministic tests. Bone's Heavy behavior is measured in the client/stat projection.
- Exact multi-component input selection, success/failure, depleted/stale/moved tools, recipe bypasses, duplicate responses, full item/weight capacity and disconnect are atomic and charge once at most. No costs/gains occur on rejection.
- Native resource/maker/wear fields and one project gear state remain authoritative; required metadata migration preserves old records and saved affixes. Copy/trade/Imbuing/save/restart retain tiers, quality, sockets and material rolls.
- Exercise the actual client recipe choice→craft→tooltip→equip loop, and record which acquisition is fixture-supplied. Test a disposable copy of installed saves, comparison behavior with the flag off, regression suites and exact native patch reproduction.
- Keep installed files and original questionnaire replies unchanged. Record the agreed exotic exclusions and gated material/reward consumers explicitly. Deployment and GitHub push require separate instructions.

## Implementation and evidence

[equipment-crafting-scope.json](../data/Sanctuary/Balance/equipment-crafting-scope.json) records every ordinary mapping as craftable or explicitly deferred. Startup validates complete coverage, all three tiers, physical maker routing, distinct ingredient types and canonical primary materials. [build_equipment_catalogue.py](../tools/build_equipment_catalogue.py) reproduces all603 tier rows and156 higher reference rows from authored Normal recipes and hash-verified local sources; it leaves the original Normal catalogue untouched.

[EquipmentCrafting](../src/Sanctuary.Content/EquipmentCrafting.cs) authors cached native `CraftItem` recipes from [equipment-recipes.json](../data/Sanctuary/Balance/equipment-recipes.json). [EquipmentCraftGump](../src/Sanctuary.Content/EquipmentCraftGump.cs) exposes the three tier tabs from the smithing hammer, fletching tools and tailoring kit. Its menu displays three recipes per page, next/previous navigation and all three tier tabs; up to four ingredient components fit in two lines. Choosing a recipe targets the first processed ingredient, preserving its canonical subtype and hue. Native material skill thresholds still apply. Bone recipes require bone plus ordinary leather; cloth recipes require processed cloth. Recipes are skill-known without a new scroll requirement.

The native craft timer enters the extended [CraftTransactions](../src/Sanctuary.Content/CraftTransactions.cs) guarded completion. Equipment captures exact input stacks, counts, hue, resource, origin, weight and parent, plus tool uses/parent and connection. Commit rechecks profession, skill, native equipment requirements and public facility. Complete delivery preflight accounts for freed input slots and integer pile weights; full packs can reuse consumed slots. Failure retains native per-component loss rules and crafted-tool charge saving. Direct old native recipe entry/completion cannot bypass the cohort's maker, tier or transaction contract. Native alternative recipes for ordinary/exotic/named combat equipment are blocked for affected players, preventing bypasses of the white recipe book. Jewellery and cosmetic production retain their existing behavior pending their own cards.

[Native patch0018](../patches/ModernUO/0018-equipment-crafting-hooks.patch) adds a full-recipe selection guard, a pure native durability-bonus callback a crafted-cohort enhancement guard, a narrowly scoped minimum-chance callback and explicit crafted-clothing resource persistence in UOContent. Native enhancement rejects the new crafted cohort before charging materials or tool uses, preserving its fixed resource and saved material rolls. Legacy equipment keeps its existing enhancement and resource-serialization behavior. Crafted clothing writes its native resource even when it equals its constructor default, preserving plain leather boots on restart. No second resource authority is introduced. No engine or client production files change. New cohort output uses native Regular quality and suppresses native exceptional/coloured durability bonuses; project quality and the one-time approved perk remain separate. The custom completion message uses Average/High Quality/Superior without calling a Superior item native “exceptional quality.”

[GearProfiles](../src/Sanctuary.Content/GearProfiles.cs) now writes version2, adding the crafted-equipment marker, one-time durability percentage and bounded material-roll records. Native fields remain the only authority for resource, maker and current/maximum wear. Version0/1 records receive deterministic defaults without retroactive perks. Tier/base mappings and role-specific material rolls are validated. Copy clones roll arrays; accepted/cancelled secure trades and Imbuing preserve identity, origin and wear. Imbuing retains the tier's base code and equip floor.

[equipment-tier-bases.json](../data/Sanctuary/Balance/equipment-tier-bases.json) contains the156 required higher reference rows from the existing pinned extraction. The173 Normal rows and UO type mappings stay unchanged. The pinned links preserve claws' h2h→h2h2 subtype transition and Circlet's shared Tiara/Diadem upgrades. `ClassGear.Base` selects the saved per-item reference, so damage/defense, Strength/Dexterity, block, speed and affix eligibility use the proper tier. `ClassGear.RequiredLevel` uses the stored floor plus affix requirements. Newly crafted BoneChest has Heavy movement classification; old comparison BoneChest retains its previous Medium classification. Tooltips show tier, quality, empty sockets, wear and maker; stored coloured effects explicitly remain inactive. No random affixes are granted by crafting whites.

[EquipmentCraftingChecks](../tests/Sanctuary.Integration/EquipmentCraftingChecks.cs) passed all603 recipes ×3 qualities (1,809 craft commits), exact costs, crafted origin, item/equip levels, tier stat/attribute projections, Bone's10% Heavy penalty, both100-skill boundaries, ceil rounding, native bonus suppression, coloured metals/woods/leathers, independent dual-resistance ranges and dormant effects. Tests also cover copy arrays/native fields, accepted/cancelled trade, higher-tier Imbuing, exact hue selection, mixed-ingredient failure including four components, changed fourth components, complete roster/exotic/named-output bypass coverage, skill119.9 rejection and120 success eligibility, no sockets on gloves/boots/belts, native expanded equips, coloured footwear and bone weapons, potion row counts, changed/missing/moved sources and tools, tool-use changes, material skill requirements, full item/weight capacity, freed source slots, native recipe/enhancement bypasses, one-shot responses, charge saving on success/failure and the89/10/1 quality distribution. Encoded property lists are checked for tier/quality/empty sockets.

[The client test](../tests/check_equipment_crafting_ui.py) sends actual mouse replies through the tool's equipment button, tier tabs and Elite Leather Cap recipe. Material hand-in and level/equip checks use native server APIs supplied by the fixture; a fixed successful Superior roll makes this UI test reproducible. It consumes6 leather/one use for Elite LeatherCap, then navigates next→previous→next and crafts a Normal Bandana using2 cloth/one use. It rejects a full-pack repeat intact, rejects an old disconnected request and opens the equipped paperdoll and item tooltip. Test skill120, materials and level80 are fixture grants, not production acquisition. Saved gear, material rolls, maker and wear passed four disposable boots/save/restarts. Existing T05–T07 server regressions pass in the same world.

The disabled comparison passes four boots covering creation/class names, version0/1/new gear state, native processing comparison, economy, monster profiles/XP/revival, belts, pets, resurrection and combat. A three-boot disabled→enabled→enabled copy of installed saves preserves two characters' profiles/allocations/skills/caps/locks/worn identities, the existing gear record and bandage stack. The copy gains the eleven configured public town objects; original installed files remain unchanged. First-loop evidence keeps24 exact pools/1536 seeded rolls; only implementation provenance changes. Server/content/integration builds have zero warnings/errors. Exact fresh/reverse/repeated patch reproduction passes18 ModernUO patches/51 paths and22 ClassicUO patches/36 paths. All12 Python checks pass after refreshing implementation provenance. All217 protected installed runtime/save/configuration/preference files and the original questionnaire replies retain their entry hashes. Final isolated evidence is `final-equipment-server.log`/`final-equipment-run.log` and `final-comparison-server.log`/`final-comparison-run.log` under `.local/logs/t08-complete/`; actual-client equipped-item and pagination screenshots are beside them. The harness accepts `SANCTUARY_TEST_LOG` to keep independent runs separate.

## Reproduction and deferred work

Use the non-staging native/content builds from T04/T07, then:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_PROFESSIONS=1 SANCTUARY_TEST_PROCESSING=1 \
SANCTUARY_TEST_CRAFTING=1 SANCTUARY_TEST_EQUIPMENT_CRAFTING=1 \
SANCTUARY_TEST_EQUIPMENT_CRAFTING_UI=1 \
SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/t08-complete/ui-state.json" \
python3 tests/check_britain.py
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_PROFESSIONS=1 SANCTUARY_TEST_PROCESSING=1 \
python3 tests/check_working_baseline.py --output "$PWD/.local/logs/t08-complete/copied-world"
python3 tools/build_equipment_catalogue.py --source-dir .local/research/foundation --check
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation --check
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Deferred work is explicit:

- The ten glass, stone and dragon-scale recipes listed in the recipe book remain deferred by user decision; they are blocked through native recipe alternatives as well.
- T09 owns measured material-stat/damage/regeneration activation and effective-stat consistency. MF/GF stay gated until their reward consumers exist.
- T17 owns jewellery's separate five-step ladder. T36 owns real105–120 cap/rare120-recipe acquisition. Gem/bark/other ingredient acquisition stays with its supply cards. Native race/gender/class restrictions have not been removed.
- Empty sockets do not provide insertion or gem effects; those remain their own cards. Native zero-durability clothing keeps its native baseline; the percentage perk does not invent a positive maximum.

This completes the user-approved standard-material T08 scope. Local supplementary evidence is under `.local/logs/t08-complete/`. No staging, deployment, commit or GitHub push was performed.
