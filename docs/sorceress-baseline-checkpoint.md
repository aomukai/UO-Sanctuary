# Sorceress existing mechanics/save baseline

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This starts the next class after preserving Necromancer's full acceptance and annotation checkpoints. Sorceress has29registered handlers; Inferno remains individually deferred. No class is certified complete.

The baseline passes **88 native markers across three boots, two actual reloads and three saves** in disposable worlds. It reuses11existing saved fixture families and five shared checks: armor/shield/Enchant, Blaze/Fire Wall/Meteor, Blizzard/Frozen Orb/Hydra, chain/storm/teleport and historical Inferno primitives, plus Charged Bolt/Lightning and prepared channel/sequence/rate controls. The [receipt](../tools/reference/sorceress-baseline-native-evidence.json) binds exact fixtures, probes, inputs, logs and unchanged106installed artifacts.

All five assemblies are the exact accepted build10binaries. C#/project inputs are identical; current copied data matches the accepted [annotation audit](necromancer-roadmap-metadata-checkpoint.md). No compilation or gameplay modification was needed for the new Python coordinator. Existing887/21audit fingerprints remain current with unchanged decisions. This baseline adds no graphical-client acceptance or real right/quick input action; those gaps remain explicit rather than inferred from direct release calls.

## Basic projectile reuse review

`SorceressCombat.Missile` still uses a private unsaved `SkillField`. Its50-frame Fire Bolt uses the correct existing velocity conversion, but also stops at `CombatProtocol.TeethRange`. The retained `firebolt` row instead supplies Range50/Vel20. Shared factory velocity, Q12 vectors, source-step history, basic do1 update, source-size contact, FP256 rolls and prepared mitigation already exist in the Necromancer spell/Mage and Druid projectile services. Compare their contracts before adding another implementation.

`SkillMechanics.Values` already captures both effective and hard skill-rank dictionaries at release. Do not redo that work. The generic basic impact path still rolls its damage at impact and lets `SkillCombat` read enhancement then; item mastery capture and the T29 impact-time potion rule need to be kept distinct. Existing Firestorm endpoint/modifier and prepared-packet helpers are reuse candidates. Preserve the approved noncompounding four-component potion rule instead of silently choosing a new basis or snapshot time.

The NPC Mage controller requires its own actor/owner/AI context and cannot simply be called with a player. The Necromancer controller has class-specific poison/guidance/lease/save contracts. Reuse compatible kernels while retaining explicit player flags, collision termination, ReturnFire/armor reaction, ownership, timing, mitigation and save/lifecycle differences. Preserve existing persistence identities; do not reinterpret historical saved flights as newly verified trajectories.

Next mechanical increment: Fire Bolt's actual player creation/travel/impact path and dedicated input/rank/gear/payment/cancellation/lifecycle controls, beginning with the unrelated Teeth range cutoff. Keep remaining basic spells, Nova/Frost Nova/Static Field and passive consumers in the [class coverage plan](sorceress-acceptance-preflight.md). The [Inferno preflight](sorceress-inferno-preflight.md) records its separate modern input/payment/reference gap; a passing historical primitive or FCR fallback does not enable that talent.

Final effects/audio and talent/left/right/quick icons remain separate identified placeholders. No new asset is added by this baseline. The working installed build and saves remain untouched.
