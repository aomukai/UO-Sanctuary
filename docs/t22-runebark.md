# T22 — Runebark harvesting and resale

T22 adds eight persistent, stackable Runebark tiers. Rune crafting remains T23.

Each successful wood extraction makes one 5% roll. A find awards one piece, with equal odds among tiers unlocked by trained **Base Lumberjacking** at the start of the extraction check. Skill bonuses, active craft and the amount of wood harvested do not change the chance or unlocks. Failed checks, invalid targets, exhausted trees and special harvest paths do not award bark. A successful extraction still qualifies if native wood delivery subsequently fails because the backpack is full.

| Tier | Runebark | Base Lumberjacking | Vendor pays per piece |
| --- | --- | ---: | ---: |
| 1 | Crude | 45 | 10 gold |
| 2 | Rough | 50 | 20 gold |
| 3 | Shaped | 55 | 30 gold |
| 4 | Etched | 60 | 40 gold |
| 5 | Refined | 65 | 50 gold |
| 6 | Polished | 70 | 60 gold |
| 7 | Flawless | 75 | 70 gold |
| 8 | Perfect | 80 | 80 gold |

A find message names the bark. It goes into the backpack, merging with a compatible stack when possible. If it cannot fit, it appears at the character's feet with an explicit message. Each piece weighs 0.1 stone. Identity and tier survive native duplication, stack splitting, trading and world reload; different tiers cannot stack even if their displayed name and hue are made identical.

Supported town merchants buy whole or partial stacks for the listed prices using the existing guarded sale transaction. Sold bark is consumed and never enters shop stock. There is no NPC or monster supply and no bark upgrade recipe. Native ML harvesting extras and existing gems remain unchanged. Higher bark is suitable for lower rune recipes; T23 will implement that ingredient substitution without raising the output tier.

The single native content hook runs after primary extraction, once per successful action, independently of native bonus harvesting. Mining and Fishing do not produce Runebark. The existing profession release flag controls natural Runebark supply; gathering does not require selecting or switching to a production craft.

Balance rows are in `data/Sanctuary/Balance/runebark.json`; production code is `RunebarkHarvesting.cs` and `Runebark.cs`. Native changes are recorded in `patches/ModernUO/0027-successful-harvest-hook.patch`.

Validation covers exact chance tickets, every trained-skill boundary, all unlocked-tier selections, modified-skill rejection, native extraction success/failure/full-pack callbacks, backpack and ground delivery, copy/split/trade/reload identity, prices, partial sales, sale replay and absent buyback stock. The actual client fixture uses native lumberjacking with a seeded rare byproduct ticket, then checks the visible find/tooltip/quote and a mouse-driven sale. It adds no production test setting.

Completed and deployed on 20 October 2026 (JST). Native/content/integration builds pass with zero warnings or errors. Source and installed four-boot cumulative checks, separate legacy migration, all24 Python checks and native patch round trips pass. The installed actual-client sale and copied-world checks pass; live save/restart preservation is recorded in the local deployment manifest.
