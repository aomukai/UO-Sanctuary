# Three complete Sorceress staffs returned

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. The generic named-item grant path and existing class spell controllers are reused. Nine qualified direct grants expand the registry from 18 to 27; normal weighted generation now admits three complete originals:

| Original | Pinned identity | Effective grants with no hard points |
| --- | --- | --- |
| Lazarus Spire | unique/56, cst, nine properties | Chain Lightning 2, Lightning 3, Static Field 4 |
| The Salamander | unique/57, bst, six properties | Fire Wall 3, Fire Ball 4, Warmth 5 |
| The Iron Jang Bong | unique/58, wst, eight properties | Nova 4, Frost Nova 5, Blaze 4 |

General class/fire bonuses add to the specific grants without supplying hard synergy points. Warmth remains passive and cannot occupy an active binding. Its rank-five 78% recovery and Lazarus Spire's 43% item recovery use the real regeneration-rate consumer. Complete original properties, ordinary equip, unidentified/broken suppression and restoration, wrong-class trade, and original item identity/rolls/resources are exercised across two reloads. A full 573-definition availability comparison requires exactly these three admissions beyond the previously qualified Warpspear; Hellslayer remains gated.

**C6/I6/source6/run4 passes 66 native results, 24 actual ability inputs, 27 authenticated connections, three saves and two reloads.** [Receipt](../tools/reference/sorceress-staff-grants-native-evidence.json). Eight active spells use real right or quick input, ordinary windup and one fractional mana payment. Released missiles/fields retain their captured rank and damage after the staff is removed. Blaze uses actual movement to lay its trail; Static Field uses the ordinary quarter-life hit. Warmth is checked through its passive consumer. This qualifies these complete-item grants, without claiming an all-class or graphical certificate.

The cohort exposed a real equipment-refresh bug: removing and re-adding unchanged Energy modifiers temporarily lowered maximum mana, and the native stat-timer check clamped current mana. Lazarus Spire's +15 Energy consequently lost 30 mana before spell release. `ClassGear.Refresh` now retains an existing modifier when its name, stat type and value match. Changed or removed bonuses still follow ordinary replacement and resource caps. Each staff checks repeated refreshes at full and fractional mana; wire actors start below the unequipped maximum so removing a staff cannot masquerade as a second spell payment.

The first three runs and an isolated Chain Lightning diagnostic are preserved. Earlier fixture corrections separate the intentional regeneration freeze from a synchronous rate probe and use the real elemental weapon-range accessor. The fifth build failed on an unavailable native `StatMod.Duration` getter; equipment modifiers occupy reserved names and are permanent, so the fix uses the existing type/value getters. All failed sources, data, drivers, logs and binaries remain frozen. The accepted sixth build has zero warnings/errors; no assertions were removed.

Existing equipment art and clearly identified spell placeholders are reused. Effects/audio and talent/hotkey icons remain separately tracked; no extra item-specific icon is required. The native kernel, client, persistence schema, installed build, saves and configuration are unchanged. No proc, charged-skill or item-aura admission; no charms/runewords, newer D2R process/CASC read, imagegen or deployment. Whole Hellslayer delivery and other deferred item dependencies remain separate work.
