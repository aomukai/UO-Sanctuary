# Development checkpoint — 22 October 2026

This checkpoint preserves the completed work before the class and skill phase. GitHub contains the source, balance catalogues, native patches, tests and documentation. The verified runtime artifacts remain local. **Production still runs T45; the later work is isolated staging.**

The [verification record](checkpoint-2026-10-22-verification.json) records artifact hashes, native pins and the completed checks. Local installation details and character data remain in the ignored operational handoff.

## Completed scope

| Area | Checkpoint state | Details |
| --- | --- | --- |
| T45 gambling | Implemented and deployed | 36 tavern services, hidden offers, guarded purchases, real item generation and refunds for unavailable outcomes. [T45](t45-gambling.md) |
| Named definitions | Complete source catalogue | 433 Unique rows, 140 Set pieces and 35 sets, including variants, source restrictions, inactive rows and unresolved properties. [Catalogue](named-equipment-catalogue.md) |
| T49 shared integration | Implemented in staging; final T49 remains open | 245 Unique variants and 88 Set pieces are eligible. Identity/roll persistence, set activation, acquisition, gambling, insurance, repairs, salvage, sockets, wardrobe/trade/death paths and supported combat effects share the named-item systems. [Audit](set-unique-audit.md) |
| T46 mercenary preparation | Implemented in staging | Paid resurrection, retained actual gear, unlimited healing reserve, timed drinking, free living-companion town healing, retirement and native pet-cleanup protection. Recruitment, loadout interfaces and combat AI follow class skills. [T46 boundary and return checklist](t46-mercenary-preparation.md) |
| T47 professions | Implemented in staging | All 13 professions have routes from 0 to 120. Cartography boundary stalls and exhausted gathering gain windows are fixed; existing training, practice, skill locks and ordered Power Scroll caps remain. [Coverage matrix](t47-profession-coverage.md) |

All 573 named definitions are accounted for: 333 eligible, 196 source-spawnable definitions held, 29 other restricted records and 15 deliberately excluded charms. Held results retain their source weight: drops yield nothing and gambling refunds without another roll. Items become available only when every advertised effect works. Charms have no implementation or class-return tasks.

## Decisions carried forward

- Mercenary resurrection costs 1,000 gold per saved level, paid from the owner's bank. Gear and stored doses survive death. Free town healing applies to living owned pets and mercenaries outside combat; death remains a separate paid service.
- The reserve accepts healing potions only, with no gameplay capacity limit or withdrawal. Drink at 50% life or below, at least ten seconds apart, using healing over time. Warn at 9, 5 and 1 remaining. Retirement/replacement loses leftover doses and preserves recoverable gear.
- All professions can train to 120, retaining independent progress. Existing ordered caps 100 → 105 → 110 → 115 → 120 remain; T47 does not grant those caps automatically.
- Full mercenaries follow complete class skills. Final T48 balance outings wait for complete gameplay, classes, supported item effects and intended encounters. Autoattack damage cannot establish the final economic/combat balance.
- Custom generated art is permitted where needed. Retain prompts/source assets and verify transparency, scale, directional frames, timing and actual client mapping. Art generation and completed gameplay integration are separate checks.

## Verification

- Native UOContent, project content and integration fixtures build with zero warnings and errors.
- Cumulative and legacy regression worlds pass four boots, including saved identities, effects, equipment, progression and transaction checks.
- Care fixtures verify real death/resurrection, exact payment, insufficient funds, stale/range/ownership guards, timed doses, a 1,200,001-dose reserve, retirement and full-backpack gear recovery. A separate forced native abandonment check preserves registered mercenaries while retaining ordinary pet cleanup.
- The actual client verifies the resurrection quote, cancel, insufficient funds, payment and connected-owner automatic drinking with the real ten-second cooldown. Earlier T49 checks cover named appearances, tooltips and set fade behavior.
- Actual production transactions train all ten crafts from 119.9 to 120. Native gathering callbacks gain at 100, 110 and 119.9 and honor locks/caps. All thirteen skills preserve 120 through four boots. The generated matrix covers every crafting tenth from 70.0 through 119.9: 5,000 checks.
- A disposable live-save round trip preserves both existing players, class allocations, every native skill value/cap/lock and actual worn gear identities, rolls and origins.
- All 62 Python tests pass. The standalone named catalogue check covers 573 rows, 35 sets, 224 property definitions and 692 set subsets. Generated readiness/coverage/first-loop evidence checks pass.
- Native patch replay matches the working source exactly: ModernUO 48 patches across 99 paths; ClassicUO 33 patches across 41 paths. Native pins and protected installed binaries remain unchanged.

The native carriers in the care fixtures are test doubles for the future mercenary class. These results verify the preparation and functional systems, rather than mercenary combat balance or final class gameplay.

## Matching build components

ModernUO remains pinned to `eff9de028136b11e544fd81375d1e0e710e22dd6`; ClassicUO remains pinned to `ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93`. Their working differences are recorded in the root patch stacks. Keep the submodule pins intact; fresh checkouts apply the recorded patches through the [development workflow](../README.md#linux-setup).

An eventual deployment needs the matching patched UOContent and project content. It also needs the staged client with the named-set opacity protocol; shipping only the content assembly is insufficient. Integration assemblies and test worlds are verification artifacts. Deployment remains a separate operational action.

## Next phase and explicit returns

1. Turn the [agreed class sequence](../Britannia-Diablo-Project-Kickoff.md#14-agreed-implementation-sequence) into concrete class/skill work cards. The numbered foundation roadmap ends at T49 and is not a complete finished-game plan.
2. Implement complete class skills, using the shared rank, damage, status, resource and corpse APIs. Revisit the [named skill prerequisites](deferred-named-skills.md) and [complete runtime return list](named-item-readiness.md) as each prerequisite becomes functional.
3. Complete mercenary archetypes, recruitment, skills, XP/levels, gear/loadout interfaces, AI and lifecycle integration using the [T46 return checklist](t46-mercenary-preparation.md).
4. Complete remaining T49 equipment/form/socket delivery, source/quest paths and asset integration with those prerequisites. Preserve unresolved source gaps, including Gelid-Affix5, rather than inventing effects. Ten exotic equipment recipes remain explicitly excluded from the current recipe scope.
5. With complete gameplay, run T48 economic/combat outings and close final T49 integration. Keep remaining world/content and release work explicit when authoring the later cards.

The [roadmap](implementation-roadmap.md), [decision tracker](economy-open-questions.md) and [reconciliation](economy-reconciliation.md) retain the individual choices. The original questionnaire replies remain immutable.
