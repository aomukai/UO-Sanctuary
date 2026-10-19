# T09 — effective gear stats

Confirmed, implemented and verified 19 October 2026 (JST). **T09 complete for the scope below and deployed.** See the [T01–T09 deployment](deployment-t01-t09.md). T12 repair costs remain a separate card.

## Confirmed scope

- Ordinary cloth clothing, including cloth hats, is wearable but cosmetic: no defense, combat attributes, material effects or sockets. Belts/tabards are exceptions. Leather footwear retains its combat role. Existing saved items must remain loadable; their stored history is not deleted to make an effect inactive.
- Broken durable gear stays equipped and retains weight and existing armor/shield running penalties, but supplies no damage, defense, block, stats or material effects. Broken weapons use unarmed damage, timing and melee reach. Naturally non-wearing leather footwear remains functional; do not invent a durability maximum for it.
- Activate T08's saved resistance/material rolls; do not reroll or grant retroactive random rolls to legacy items. Retain the existing resistance cap. Weapon material damage uses the agreed 5–10% of unmodified reference damage before global bonuses, keeping physical damage intact.
- Cold weapon material damage also chills attacks and movement for a base 2 seconds. Poison applies its total material damage over 3 seconds, rather than applying that amount each second or also applying it immediately. Repeated poison applications refresh/replace one effect rather than accumulating independent stacks; weaker damage rates must not overwrite stronger poison.
- Verite's 1 life (armor/shield) or 1 mana (weapon) per 5 seconds stacks across working pieces, adds to base regeneration, works in combat, and respects existing regeneration eligibility. No offline catch-up or free equip tick.
- Enable numeric Faster Hit Recovery affix rolls through the existing recovery consumer. Keep unsupported effects and MF/GF reward properties gated. Keep the existing timing/breakpoint formulas and committed action deadlines.

## Diablo II evidence and adaptation

Blizzard's [resistance guide](https://classic.battle.net/diablo2exp/basics/resistances.shtml) describes cold slowing movement and attacks and poison draining life over time. Its [melee equipment guide](https://classic.battle.net/diablo2exp/classes/meleeequipment.shtml) explicitly confirms that even one point of cold damage can chill. Chill is distinct from immobilizing freeze.

The [gem table](https://classic.battle.net/diablo2exp/items/gems.shtml) assigns source-specific cold durations (1–3 seconds for sapphires), with cumulative cold lengths, and poison damage/duration pairs (3–7 seconds for emeralds). It represents poison internally as a fractional damage rate and duration. The [gameplay FAQ](https://classic.battle.net/diablo2exp/faq/gameplay.shtml) defines displayed poison as total damage over its duration and describes stronger poison rates replacing weaker ones. Combining item contributions to one attack differs from stacking successive poison applications on a victim.

The selected 2-second material chill and 3-second material poison are Sanctuary tuning values, not universal D2 constants. Target chill effectiveness is target-dependent in D2; for example Blizzard's [zombie table](https://classic.battle.net/diablo2exp/monsters/act1-zombie.shtml) lists 50/40/25 for Normal/Nightmare/Hell. Existing authored target traits, including zero effectiveness, must be respected. Broader freeze skills, corpse shattering, socket insertion and full affix libraries are outside this card.

## Acceptance checks

- Equip/unequip/break/repair/trade/relog project the same attributes, maxima, resistance, damage, block and speed; no duplicate stat modifiers or free resource refill.
- Broken ranged/two-handed weapons use unarmed reach and animation/timing. Breaking or swapping during a pending attack cannot refund its committed deadline or deliver a stale weapon hit.
- Each activated material property has a measured combat/resource effect. Fractional low-damage poison is retained; hit recovery is not repeatedly triggered by its ticks. Chill slows actual attacks/movement, expires cleanly, and respects target exceptions.
- Verite stacking, disabled/broken contributors, regeneration restrictions and no immediate equip tick are verified.
- FHR rolls affect the existing recovery frames; cosmetic clothing cannot acquire or contribute combat effects. Historical saved records still load.
- Verify native durability notifications, native wear without broken-item deletion, save/restart, native patch reproduction and the relevant legacy regressions in disposable worlds. Installed runtime/saves/configuration/preferences remain unchanged.

## Implementation

[ClassGear](../src/Sanctuary.Content/ClassGear.cs) owns effective contributors, including durability, cosmetic clothing, stored material resistances and FHR. Native durability remains authoritative. Existing cloth-hat base/affix/socket records are validated and preserved as inactive history; their display, requirements, new Imbuing and socket eligibility are cosmetic. Naturally non-wearing leather footwear stays active. The starter dagger's Teeth grant also requires a working equipped item.

[GearEffects](../src/Sanctuary.Content/GearEffects.cs) adds typed weapon damage without replacing the physical component. Each type uses its own resistance; direct components are summed before final integer truncation. Positive direct hits retain the existing minimum1 damage, while complete immunity yields0. Poison's fractional damage is accumulated separately, including sub-point remainder between applications, and never generates repeated weapon-hit recovery. Hit effects snapshot the landed damage; later equipment changes do not undo damage already applied. Cold reduces attack rate through the existing timing formula, movement through the native move clock, and respects authored chill effectiveness/zero-effect targets. Its2-second base length is reduced by cold resistance. Casting and existing committed deadlines are unchanged.

Verite adds its rate to the existing native regeneration rate; it does not replace base regeneration, grant resources on equip, or bypass native alive/poison eligibility. Item-granted resource maxima still clamp current resources downward without refilling them. Material regeneration and resistances require active, working contributors. At T09, MF/GF were explicitly labelled dormant in tooltips. [T10](t10-rewards.md) activates Gold Find and updates its tooltip; Magic Find stays dormant.

[Native patch0019](../patches/ModernUO/0019-effective-gear-and-wear-hooks.patch) adds durability notifications/clamping in UOContent weapon/armor/clothing/jewellery setters, preserves broken worn class items against further wear/destruction, exposes the existing weapon wear routine to class attacks, and supplies move/attack delay callbacks. Existing native wear chances are retained. No server engine or client production source changes are required.

Material chill/poison are live combat debuffs with bounded transient state; logout/restart does not bank offline effects. They do not substitute UO's unrelated tiered poison damage. General timed-effect persistence/stacking and the full potion/cure catalogue remain their later cards, as do freeze skills, corpse shattering, sockets, generic elemental/regen affixes and reward consumers.

## Verification

- Native, content and integration builds pass with zero warnings/errors.
- [EffectiveGearChecks](../tests/Sanctuary.Integration/EffectiveGearChecks.cs) passes four disposable boots/save/restarts: cloth/legacy socket history, natural0/0 footwear, resistance changes on break/repair, actual FHR rolls/recovery frames, every weapon material damage type against resistance, cold attack/move delays and expiry, fractional poison/stronger-rate protection/refresh/immunity, Verite stacked rate consumers, broken shield block, broken bow and halberd timing/reach/tooltips, interrupted attacks retaining deadlines, starter skills, legacy broken-item initialization, wear preservation and no refresh refill.
- The full603-recipe ×3-quality crafting suite passes alongside profession/tool/material processing regressions through four boots. The actual client tier/recipe/next/previous/full-pack/equip flow passes. Client fixtures grant materials/skills; this is not proof of complete production acquisition or player feedback about combat balance.
- Legacy creation,314 converted ordinary bases, v0/v1/new gear records (including old magical/socketed cloth hats), economy, class names,726 creature types, XP, belts, pets, resurrection, monsters and combat/timing/recovery checks pass through four boots.
- A disabled→enabled→enabled disposable copy of installed saves preserves both player profiles, allocations, native skills/caps/locks, worn identities, gear and existing material/bandage identities.
- The affix exporter passes24 exact pool queries and1,536 seeded rolls:32 accepted numeric codes,28 represented by complete spawnable affixes. Both catalogue reproduction checks and all12 Python checks pass.
- Exact fresh/reverse/repeated native patch reproduction passes:19 ModernUO patches/52 paths and22 ClassicUO patches/36 paths. All534 recorded installed runtime/save/configuration/preference files remain byte-identical. Original questionnaire replies remain unchanged.

Supporting logs/screenshots are under `.local/logs/t09/`: `final-effective-server.log`, `legacy-server.log`, `combined-server.log`, `combined-run.log`, `copied-world-final/`, `patch-reproduction.log`, build logs and Python results. Fixture sources and this contract are the versioned reproducible evidence; ignored logs are supplementary.

Run the non-staging builds documented in [T04](t04-item-state.md), then build the integration assembly and run:

```bash
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_PROFESSIONS=1 \
SANCTUARY_TEST_EFFECTIVE_GEAR=1 python3 tests/check_britain.py
```
