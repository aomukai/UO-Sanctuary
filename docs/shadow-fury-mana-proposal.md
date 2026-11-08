# Shadow Blade Fury startup mana

2026-11-08 JST. **Approved: “Approve the Shadow Fury mana exception.”** Reference: D2R Reign of the Warlock 3.3.93847; approved Sanctuary deviations first.

Skill 266 selects NPC mode 7 / SC despite its separate monster slot-2 sequence link. Its table has `startmana=6`, `usemanaondo=1`, `Param4=5`, source damage 96 and missile 506 (`bladefragment1`). The complete older `SKILLS_SrvSt26_BladeFury` checks the six-mana threshold without a player-kind guard on fresh startup. The shared ordinary NPC resource wrappers otherwise do not charge player mana. Original Shadow resource bases in the saved native fixture contain no mana base, and the live checks establish zero mana as a supported Shadow context. Blindly importing this old threshold would make Fury unavailable there.

Approved adaptation: **bypass only the six-mana startup availability threshold for intrinsic Shadow Warrior/Master Blade Fury; spend neither pet nor owner mana.** Preserve the separately qualified NPC startup/state/callback behavior and existing original equipment/projectile mechanics. This does not approve copying the player held-input clock, adding an invented burst, changing damage, or granting a player resource exception.

Current D2R NPC behavior is not established by the older source. The [player approval](blade-fury-approved-input-rule.md) covers player held input and is not taken as NPC approval. Damage collection, projectile work and other intrinsic skills can proceed independently while the remaining NPC timing and impact work proceeds.
