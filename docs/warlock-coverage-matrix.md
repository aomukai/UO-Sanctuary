# Warlock/Witch acceptance inventory

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. **Charms and runewords are excluded.** Druid combined acceptance has passed; this is the next-class inventory and it enables no skill, performs no new native run and grants no class certificate.

Current status has **26 handlers and four pending talents**. Existing component/learned fixtures are listed below for reuse. A fixture filename is an evidence entry point, not a claim that every modern mechanic or the combined class has passed.

| Talent | Current route | Existing fixture entry point |
| --- | --- | --- |
| Demonic Mastery | Handler present; combined acceptance pending | [DemonicMasteryChecks.cs](../tests/Sanctuary.Integration/DemonicMasteryChecks.cs) |
| Summon Goatman | Handler present; combined acceptance pending | [GoatmanCastChecks.cs](../tests/Sanctuary.Integration/GoatmanCastChecks.cs) |
| Blood Oath | **Prepared only; frozen pending decision** | [BloodOathDamageChecks.cs](../tests/Sanctuary.Integration/BloodOathDamageChecks.cs) |
| Death Mark | Handler present; combined acceptance pending | [DeathMarkActionChecks.cs](../tests/Sanctuary.Integration/DeathMarkActionChecks.cs) |
| Summon Tainted | Handler present; combined acceptance pending | [TaintedCastChecks.cs](../tests/Sanctuary.Integration/TaintedCastChecks.cs) |
| Blood Boil | **Prepared only; frozen pending decision** | [BloodBoilWorldChecks.cs](../tests/Sanctuary.Integration/BloodBoilWorldChecks.cs) |
| Summon Defiler | Handler present; combined acceptance pending | [DefilerCastChecks.cs](../tests/Sanctuary.Integration/DefilerCastChecks.cs) |
| Engorge | **Prepared only; frozen pending decision** | [EngorgeWorldChecks.cs](../tests/Sanctuary.Integration/EngorgeWorldChecks.cs) |
| Consume | **Prepared only; frozen pending decision** | [ConsumeWorldChecks.cs](../tests/Sanctuary.Integration/ConsumeWorldChecks.cs) |
| Bind Demon | Handler present; combined acceptance pending | [BindDemonLearnedChecks.cs](../tests/Sanctuary.Integration/BindDemonLearnedChecks.cs) |
| Levitation Mastery | Handler present; combined acceptance pending | [LevitationChecks.cs](../tests/Sanctuary.Integration/LevitationChecks.cs) |
| Hex: Bane | Handler present; combined acceptance pending | [HexBaneCastChecks.cs](../tests/Sanctuary.Integration/HexBaneCastChecks.cs) |
| Cleave | Handler present; combined acceptance pending | [CleaveCastChecks.cs](../tests/Sanctuary.Integration/CleaveCastChecks.cs) |
| Echoing Strike | Handler present; combined acceptance pending | [EchoingStrikeCastChecks.cs](../tests/Sanctuary.Integration/EchoingStrikeCastChecks.cs) |
| Hex: Purge | Handler present; combined acceptance pending | [HexPurgeCastChecks.cs](../tests/Sanctuary.Integration/HexPurgeCastChecks.cs) |
| Blade Warp | Handler present; combined acceptance pending | [BladeWarpChecks.cs](../tests/Sanctuary.Integration/BladeWarpChecks.cs) |
| Psychic Ward | Handler present; combined acceptance pending | [PsychicWardCastChecks.cs](../tests/Sanctuary.Integration/PsychicWardCastChecks.cs) |
| Eldritch Blast | Handler present; combined acceptance pending | [EldritchBlastCastChecks.cs](../tests/Sanctuary.Integration/EldritchBlastCastChecks.cs) |
| Hex: Siphon | Handler present; combined acceptance pending | [HexSiphonCastChecks.cs](../tests/Sanctuary.Integration/HexSiphonCastChecks.cs) |
| Mirrored Blades | Handler present; combined acceptance pending | [MirroredCastChecks.cs](../tests/Sanctuary.Integration/MirroredCastChecks.cs) |
| Miasma Bolt | Handler present; combined acceptance pending | [MiasmaCastChecks.cs](../tests/Sanctuary.Integration/MiasmaCastChecks.cs) |
| Ring of Fire | Handler present; combined acceptance pending | [RingCastChecks.cs](../tests/Sanctuary.Integration/RingCastChecks.cs) |
| Sigil: Lethargy | Handler present; combined acceptance pending | [SigilCastChecks.cs](../tests/Sanctuary.Integration/SigilCastChecks.cs) |
| Sigil: Rancor | Handler present; combined acceptance pending | [RancorCastChecks.cs](../tests/Sanctuary.Integration/RancorCastChecks.cs) |
| Miasma Chain | Handler present; combined acceptance pending | [MiasmaChainCastChecks.cs](../tests/Sanctuary.Integration/MiasmaChainCastChecks.cs) |
| Flame Wave | Handler present; combined acceptance pending | [FlameWaveCastChecks.cs](../tests/Sanctuary.Integration/FlameWaveCastChecks.cs) |
| Sigil: Death | Handler present; combined acceptance pending | [SigilDeathChecks.cs](../tests/Sanctuary.Integration/SigilDeathChecks.cs) |
| Enhanced Entropy | Handler present; combined acceptance pending | [EntropyCloudChecks.cs](../tests/Sanctuary.Integration/EntropyCloudChecks.cs) |
| Apocalypse | Handler present; combined acceptance pending | [ApocalypseCastChecks.cs](../tests/Sanctuary.Integration/ApocalypseCastChecks.cs) |
| Abyss | Handler present; combined acceptance pending | [AbyssCastChecks.cs](../tests/Sanctuary.Integration/AbyssCastChecks.cs) |

The four frozen proposal pairs are `blood-oath-damage`, `engorge-world-rule`, `blood-boil-rule` and `consume-world-rule`. Do not infer approval, open their learned routes or reask solely for this inventory. Other existing routes can continue independently.

Retain the approved UO bodies: Goatman → Gargoyle, Tainted → Succubus, Defiler → Gazer. Defiler is a following/linking demon rather than a basic attacker. Reuse shared summon snapshots, original bodies/equipment, difficulty and percentage-life rescaling, safe-area parking without recovery/reset, existing allegiance and native path/damage services. Current caller, unsupported native species/elite/Sunder inputs, item callbacks, geometry, event ordering and exceptional recipient/PvP limits remain explicit in the component checkpoints.

Reuse the existing current-input and component drivers when assembling the class cohort. Keep connected setup/result ordering, independent synthetic clocks, retained wounds/resources, original-save identity, no input replay and exact marker inventories. Do not rerun unchanged reference compiler matrices. Current installed D2R is newer than the pinned reference; do not inspect that process or CASC.

Missing visual effects/audio stay in [skill-effects](skill-effects.md); tree/hotkey art stays in [skill-icons](skill-icons.md). Identified placeholders do not gate mechanics. No image generation or deployment is part of this inventory.
