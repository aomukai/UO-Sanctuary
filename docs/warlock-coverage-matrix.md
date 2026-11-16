# Warlock/Witch acceptance inventory

**Current four-talent review — 2026-11-16 JST.** The [consolidated release proposal](warlock-final-talents-release-proposal.md) binds the four original rules and their later qualified bound/numeric/aura extensions. The [current checkpoint](warlock-remaining-review-checkpoint.md) passes75 results/33connections/three saves/two reloads using exact reused C4/I4/native0075artifacts,with no C#/data/gate change. There are zero learned socket ability inputs in this review. Original frozen pairs/receipts are unchanged;this prepares a release decision rather than claiming completion.

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. **Charms and runewords are excluded.** Druid combined acceptance has passed; this inventory records scoped subgroup results and grants no whole-class certificate.

Current status has **27 handlers and three approved talents awaiting learned admission**. Existing component/learned fixtures are listed below for reuse. A fixture filename is an evidence entry point, not a claim that every modern mechanic or the combined class has passed.

| Talent | Current route | Existing fixture entry point |
| --- | --- | --- |
| Demonic Mastery | Demon/foundation group accepted (180 results) | [DemonicMasteryChecks.cs](../tests/Sanctuary.Integration/DemonicMasteryChecks.cs) |
| Summon Goatman | Demon/foundation group accepted (180 results) | [GoatmanCastChecks.cs](../tests/Sanctuary.Integration/GoatmanCastChecks.cs) |
| Blood Oath | Automatic learned original/bound passive accepted (27 results) | [BloodOathLearnedChecks.cs](../tests/Sanctuary.Integration/BloodOathLearnedChecks.cs), [checkpoint](blood-oath-learned-checkpoint.md) |
| Death Mark | Demon/foundation group accepted (180 results) | [DeathMarkActionChecks.cs](../tests/Sanctuary.Integration/DeathMarkActionChecks.cs) |
| Summon Tainted | Demon/foundation group accepted (180 results) | [TaintedCastChecks.cs](../tests/Sanctuary.Integration/TaintedCastChecks.cs) |
| Blood Boil | **Approved; learned admission next** | [BloodBoilWorldChecks.cs](../tests/Sanctuary.Integration/BloodBoilWorldChecks.cs) |
| Summon Defiler | Demon/foundation group accepted (180 results) | [DefilerCastChecks.cs](../tests/Sanctuary.Integration/DefilerCastChecks.cs) |
| Engorge | **Approved; learned admission next** | [EngorgeWorldChecks.cs](../tests/Sanctuary.Integration/EngorgeWorldChecks.cs) |
| Consume | **Approved; learned admission next** | [ConsumeWorldChecks.cs](../tests/Sanctuary.Integration/ConsumeWorldChecks.cs) |
| Bind Demon | Demon/foundation group accepted (180 results) | [BindDemonLearnedChecks.cs](../tests/Sanctuary.Integration/BindDemonLearnedChecks.cs) |
| Levitation Mastery | Demon/foundation group accepted (180 results) | [LevitationChecks.cs](../tests/Sanctuary.Integration/LevitationChecks.cs) |
| Hex: Bane | Weapon/Hex/Ward group accepted (240 results) | [HexBaneCastChecks.cs](../tests/Sanctuary.Integration/HexBaneCastChecks.cs) |
| Cleave | Weapon/Hex/Ward group accepted (240 results) | [CleaveCastChecks.cs](../tests/Sanctuary.Integration/CleaveCastChecks.cs) |
| Echoing Strike | Weapon/Hex/Ward group accepted (240 results) | [EchoingStrikeCastChecks.cs](../tests/Sanctuary.Integration/EchoingStrikeCastChecks.cs) |
| Hex: Purge | Weapon/Hex/Ward group accepted (240 results) | [HexPurgeCastChecks.cs](../tests/Sanctuary.Integration/HexPurgeCastChecks.cs) |
| Blade Warp | Weapon/Hex/Ward group accepted (240 results) | [BladeWarpChecks.cs](../tests/Sanctuary.Integration/BladeWarpChecks.cs) |
| Psychic Ward | Weapon/Hex/Ward group accepted (240 results) | [PsychicWardCastChecks.cs](../tests/Sanctuary.Integration/PsychicWardCastChecks.cs) |
| Eldritch Blast | Weapon/Hex/Ward group accepted (240 results) | [EldritchBlastCastChecks.cs](../tests/Sanctuary.Integration/EldritchBlastCastChecks.cs) |
| Hex: Siphon | Weapon/Hex/Ward group accepted (240 results) | [HexSiphonCastChecks.cs](../tests/Sanctuary.Integration/HexSiphonCastChecks.cs) |
| Mirrored Blades | Weapon/Hex/Ward group accepted (240 results) | [MirroredCastChecks.cs](../tests/Sanctuary.Integration/MirroredCastChecks.cs) |
| Miasma Bolt | Chaos/Sigil group accepted (315 results) | [MiasmaCastChecks.cs](../tests/Sanctuary.Integration/MiasmaCastChecks.cs) |
| Ring of Fire | Chaos/Sigil group accepted (315 results) | [RingCastChecks.cs](../tests/Sanctuary.Integration/RingCastChecks.cs) |
| Sigil: Lethargy | Chaos/Sigil group accepted (315 results) | [SigilCastChecks.cs](../tests/Sanctuary.Integration/SigilCastChecks.cs) |
| Sigil: Rancor | Chaos/Sigil group accepted (315 results) | [RancorCastChecks.cs](../tests/Sanctuary.Integration/RancorCastChecks.cs) |
| Miasma Chain | Chaos/Sigil group accepted (315 results) | [MiasmaChainCastChecks.cs](../tests/Sanctuary.Integration/MiasmaChainCastChecks.cs) |
| Flame Wave | Chaos/Sigil group accepted (315 results) | [FlameWaveCastChecks.cs](../tests/Sanctuary.Integration/FlameWaveCastChecks.cs) |
| Sigil: Death | Chaos/Sigil group accepted (315 results) | [SigilDeathChecks.cs](../tests/Sanctuary.Integration/SigilDeathChecks.cs) |
| Enhanced Entropy | Chaos/Sigil group accepted (315 results) | [EntropyCloudChecks.cs](../tests/Sanctuary.Integration/EntropyCloudChecks.cs) |
| Apocalypse | Chaos/Sigil group accepted (315 results) | [ApocalypseCastChecks.cs](../tests/Sanctuary.Integration/ApocalypseCastChecks.cs) |
| Abyss | Chaos/Sigil group accepted (315 results) | [AbyssCastChecks.cs](../tests/Sanctuary.Integration/AbyssCastChecks.cs) |

The user [approved all four reviewed rules](../tools/reference/warlock-final-talents-approval.json), including their qualified-bound and Consume-aura extensions. The original proposals/receipts remain historical and unchanged. Blood Oath is admitted; enable and verify Engorge, Blood Boil and Consume one at a time without repeating the approved decisions. [Release scope](warlock-final-talents-release-proposal.md).

Retain the approved UO bodies: Goatman → Gargoyle, Tainted → Succubus, Defiler → Gazer. Defiler is a following/linking demon rather than a basic attacker. Reuse shared summon snapshots, original bodies/equipment, difficulty and percentage-life rescaling, safe-area parking without recovery/reset, existing allegiance and native path/damage services. Current caller, unsupported native species/elite/Sunder inputs, item callbacks, geometry, event ordering and exceptional recipient/PvP limits remain explicit in the component checkpoints.

Reuse the existing current-input and component drivers when assembling the class cohort. Keep connected setup/result ordering, independent synthetic clocks, retained wounds/resources, original-save identity, no input replay and exact marker inventories. Do not rerun unchanged reference compiler matrices. Current installed D2R is newer than the pinned reference; do not inspect that process or CASC.

Missing visual effects/audio stay in [skill-effects](skill-effects.md); tree/hotkey art stays in [skill-icons](skill-icons.md). Identified placeholders do not gate mechanics. No image generation or deployment is part of this inventory.

Accepted groups: [demons/foundations](warlock-demons-acceptance-checkpoint.md) cover seven routes (180 results); [weapons/Hex/Ward](warlock-weapons-acceptance-checkpoint.md) cover nine (240 results); [Chaos/Sigils](warlock-chaos-acceptance-checkpoint.md) cover ten (315 results). Each retains three saves/two reloads and its own exact artifact/source inventory. Those groups cover the26 previously implemented routes. The separate [Blood Oath checkpoint](blood-oath-learned-checkpoint.md) adds the27th; three approved active talents remain to admit. No full modern parity is claimed. Bane's previous three-life loss and the first Chaos run's Abyss input timeout remain explicitly unattributed; the accepted reruns retain the original assertions and added diagnostics.
