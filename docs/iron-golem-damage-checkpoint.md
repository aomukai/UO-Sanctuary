# Iron Golem shares Clay’s captured damage curve

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This increment follows `29cf4ad`. It implements Iron’s approved Clay damage baseline while preserving original-item composition. **Hard-10 Extra Strong and hard-20 Spectral Hit are still pending. No class is complete; the candidate is not deployed.**

## Related mechanics reviewed and reused

Before implementation, reviewed `GolemPetDamage`, `SkeletonStats.WeaponBase`, Clay/Blood birth snapshots, prepared Iron’s saved original-item recipe, `NpcEquipmentDamage.Capture`, ordinary NPC contact and Iron’s world controller. Iron already has the captured effective-rank/Golem Mastery data needed by the existing curve; it does not need a second formula or a new persistence field.

`GolemPetDamage` now accepts a captured `SkillValues` context and difficulty for prepared Iron. Both it and the ordinary Clay/Blood wrapper delegate to `SkeletonStats.WeaponBase`. Prepared Iron supplies `BirthValues` from its original recipe. The original plain `NecromancerMinion` Iron type supplies its own existing captured snapshot and ratio 100. The previous innate Iron damage/Fire Golem synergy is replaced. Golem Mastery replaces Skeleton Mastery for this approved damage baseline; unrelated birth life, attack rating, defense, item graph writes and controller timing stay on their existing paths.

The innate contribution enters the existing original-item capture. Armor and one-handed items retain that contribution; two-handed weapons replace it under the current item rules. Item-local enhanced damage stays local, and existing actor/aura/debuff percentages still compose through the shared NPC physical consumer. This change does not force an innate contribution back into a two-handed binding or add item damage twice.

Existing pets keep their captured birth ranks when the owner trains or changes gear. Difficulty still follows the owner’s configured facet and preserves current life percentage. Original item identity/ownership, roster order, random stream and wounded health are preserved. No mobile field, JSON record layout or persistence version changes.

## Verification

**Accepted build 2 / run 2 / audit 2:** both rebuilds have zero warnings/errors. The native suite passes **364 markers across three boots, two reloads and three saves**, retaining all 358 predecessor marker scopes. Three real item controls, three actual facet transitions, both saved actor types and three connected learned Iron damage sessions pass. Four runtime audit markers, **71 Python tests, 573 named definitions and eight classes/240 skills** pass. Audit decisions remain unchanged with 881/21 fresh fingerprints. Frozen pre-build/native/audit source/data and tested DLL inventories match; runtime data, staged kernel and **106 installed hashes** remain unchanged. No deployment/save reset or class completion.

Three independent Normal goldens distinguish the replacement curve: Iron rank 1/Golem Mastery 0 gives endpoints 1–2 and 0% skill damage; rank 4/mastery 0 gives 1–2 and 7%; rank 20/mastery 3 gives 23–24 and 119%. These are intrinsic endpoints before existing original-item and actor percentages.

The new suite uses real learned Broadsword, The Battlebranch and Boneflesh originals. It checks one-handed/armor addition, two-handed replacement, item-local enhanced damage, exactly-once payment and retained identity. A real soft rank distinguishes effective Iron/Golem Mastery from unrelated Skeleton Mastery, while later Iron/Golem Mastery/Fire training must not recapture an existing pet’s damage, resources, recipe, order or seed. Three actual owner facet transitions check current difficulty and proportional wounds. Prepared and historical types are saved and reloaded without replacing either actor.

Iron retains **release-time allocation**, unlike the immutable start packet used by the reviewed unarmed bodies. The connected test predicts an unchanged-target/no-absorption packet during windup, cancels that prediction and restores the exact actor stream; the later real contact must produce that HP loss. The test does not introduce a new production packet snapshot or certify arbitrary mutations during windup.

The first fixture used the display shorthand “Battlebranch” for a catalog lookup whose source key is “The Battlebranch.” That test lookup now shares the existing optional-“The” matching rule; production item eligibility was unchanged. The failed run and its inventories remain archived.

See the [native receipt](../tools/reference/iron-golem-damage-native-evidence.json) and [validator](../tools/record_iron_golem_damage.py).

## Affix reuse findings and next implementation

The [static affix reuse review](../tools/reference/iron-affix-reuse-review.json) pins current consumers and complete older functions. The approved instruction to reuse existing affixes takes precedence over differences from older engine code. That older Spectral producer uses level-table damage, a different channel ordering and stat-list writes with added duration; it is reference evidence, not permission to replace the current native affix globally.

The existing encounter Extra Strong implementation has two explicit difficulty-dependent branches. Direct affix damage/attack-rating bonuses are Normal 135%/90%, Nightmare 112%/75%, Hell 99%/66%. Inherited encounter-minion bonuses are 67%/45%, 56%/37%, 49%/33%. A summoned pet is not automatically `EncounterGrade.Minion`: that branch represents inheritance from a boss. The direct pet-affix context and interaction with original-item flat/percentage AR must be explicit when extracting/reusing the helper. Keep wild grades and existing composition unchanged; do not quietly select the weaker branch because the actor is called a minion.

`EncounterProfiles.Resistances` already supplies Spectral Hit’s cold/fire/lightning additions, under-75 checks, saved modifier order and two-immunity cap. **That is only its defensive portion.** `EliteCombat.Hit` also rolls an amount from 66% of unmodified base minimum through 100% of base maximum, then chooses one of five channels: magic, fire, lightning, cold or poison. Cold has a 1,600 ms minimum effect. The wild route adds these components after its incoming physical/base roll and applies poison for 1,600 ms after direct damage, only if the target survives.

The prepared NPC item path has different contracts: it can double typed channels with intrinsic critical, and its poison rate contributes once to the initial HP total before periodic state delivery. Blindly putting Spectral Hit in `NpcEquipmentElements` or item poison would therefore change its critical, initial-hit and poison timing behavior. Next extract the existing affix producer with declared basis/amount/type RNG, then supply an explicit prepared-hit/poison adapter and regression controls. Do not enable only resistances and call the affix finished.

Remaining reference/acceptance limits include native fallback and historical actor controllers, unsupported original properties/procs, PvP/source room/global seed/event/protection/client equivalence, installed old-DLL upgrades and concurrent/unheld saves. Held saved observation fixtures are distinct from natural live sessions. Other golem/Mage/barrier/parking/Druid/class work remains on the roadmap; coverage stays **186 handlers / Necromancer 30, no completed classes**.

Iron body `14`, hue `0x835`, summon particles `0x376A`, contact overlay `0x37B9`/hue `0x481` and sound `0x1E5` remain identified placeholders. Final summon/contact, Extra Strong and five-channel Spectral feedback/audio remain separately pending in [skill effects](skill-effects.md). Iron’s talent-tree/right/quick-hotkey artwork remains in [skill icons](skill-icons.md); this automatic damage change adds no action or extra hotkey icon.
