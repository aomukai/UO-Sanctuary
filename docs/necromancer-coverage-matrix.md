# Necromancer acceptance map

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. Sanctuary's approved pet, targeting and roster changes override the reference. This map identifies the existing execution paths and tests; handler presence or a passing subset is not a class-completion certificate. The detailed unresolved limits remain in `data/Sanctuary/Balance/class-skill-status.json` and the linked checkpoints.

`NecromancerCombat` supplies the learned begin/release routes, with `ClassCombat` dispatching them before its historical fallback. The three passive talents feed summon creation rather than appearing as active casts. Existing shared flight, poison, curse, prepared-contact and pet controllers remain the implementations of those mechanics.

The current combined driver is `tests/check_necromancer_current_acceptance.py`. Family names below identify its native checks; separately accepted newer scopes are explicitly distinguished afterward.

| Talent | Mechanical execution | Included native families / checks |
| --- | --- | --- |
| Raise Skeleton | Corpse selection, paid summon, saved world AI and prepared melee | `skeleton-birth`, `skeleton-world`, `skeleton-contact`, `skeleton-combat`, `skeleton-roster`, `skeleton-upgrades` |
| Skeleton Mastery | Captured life/damage supplier for Skeletons, Mages and Revives | `skeleton-birth`, `mage-birth`, `mage-basics`, `revive-cast` |
| Clay Golem | Paid summon, world controller, contact and incoming slow | `clay-golem-birth`, `clay-golem-world`, `clay-golem-combat`, `clay-golem-contact`, `clay-golem-slow`, `clay-upgrades` |
| Golem Mastery | Captured life, attack rating, velocity and approved damage supplier | Four golem birth families; dedicated learned Mastery acceptance is a separate newer scope |
| Raise Skeletal Mage | Paid three-element summon; saved bolts, unlocks, buffs and migration | `mage-birth`, `mage-world`, `mage-basics`, `mage-missile`, `mage-elements`, `mage-historical`, `mage-meteor`, `mage-glacial`, `mage-chain`, `mage-static`, `mage-pet-buff`, `necromancer-army` |
| Blood Golem | Paid summon, world melee and fractional life transfer | `blood-golem-birth`, `blood-golem-world`, `blood-golem-combat`, `blood-golem-contact`, `blood-damage`, `blood-life-tap` |
| Summon Resist | Captured passive resistance supplier | `summon-resist`, golem and Skeleton/Mage birth checks |
| Iron Golem | Original item admission, paid birth, saved item graph, world AI and ordered contact | `iron-golem-cast`, `iron-golem-birth-graph`, `iron-golem-body`, `iron-golem-world`, `iron-golem-damage`, `iron-golem-retaliation`, `iron-golem-strong`, `iron-golem-spectral`, shared item/contact checks |
| Fire Golem | Paid summon, world melee, Holy Fire, absorption and retirement | `fire-golem-birth`, `fire-golem-world`, `fire-golem-combat`, `fire-golem-absorption`, `fire-golem-aura`, `fire-golem-death`, `fire-upgrades` |
| Revive | Original corpse actor activation, captured combat effects, persistent roster | `revive-cast`, `revive-native-activation`, `revive-facet`, `necromancer-army` |
| Teeth | Shared integer fan into saved source-flight missiles | `teeth` |
| Bone Armor | Captured fractional physical pool and incoming shield consumer | `bone-armor`, `damage-rounding`, `class-skills` |
| Poison Dagger | Prepared weapon start, ordered contact and combined poison | `weapon-start`, `poison-dagger-attack`, `poison-dagger`, shared prepared/contact checks |
| Corpse Explosion | Paid corpse release into physical/fire damage | `corpse-cast`; newer ground/town acceptance is separate |
| Bone Wall | Paid barrier creation, independent makers, resources and expiry clock | `bone-barrier-cast`, `bone-wall-maker`, `bone-barrier-birth`, `bone-barrier-clock`, `bone-barriers` |
| Poison Explosion | Paid corpse release into eight saved moving clouds | `poison-explosion`, `spell-poison-ticks`; newer ground/town acceptance is separate |
| Bone Spear | Saved piercing magic source-flight missiles | `bone-spear` |
| Bone Prison | Paid barrier allocation and saved anchors/links/expiry | Shared barrier families; newer ground acquisition acceptance is separate |
| Poison Nova | Saved ring flights and strongest-poison tick consumer | `poison-nova`, `spell-poison-ticks` |
| Bone Spirit | Saved guided magic source-flight missile | `bone-spirit` |
| Amplify Damage | Paid curse area, rank arbitration and physical resistance | `amplify-damage` |
| Dim Vision | Paid curse area and saved blind AI controller | `dim-vision-cast`, `blind-world` |
| Weaken | Paid curse area and ordinary weapon damage-percent pool | `weaken` |
| Iron Maiden | Paid curse and ordered fractional melee reflection | `iron-maiden`, shared prepared/contact checks |
| Terror | Paid curse and fear/return-to-combat AI transition | `terror-cast`, `blind-world`, `howl` |
| Confuse | Paid curse, neutral allegiance and independent saved target override | `confuse-cast`, `confuse-resume`, connected reload checks |
| Life Tap | Paid curse and fractional physical-hit healing | `life-tap-cast`, `life-tap` |
| Attract | Paid selected-centered curse and neighbor target leases | `attract-cast`, `blind-world` |
| Decrepify | Paid curse, physical resistance/damage and motion/attack slowing | `decrepify` |
| Lower Resist | Paid four-channel resistance curse | `lower-resist` |

## Newer evidence outside the original combined cohort

The combined driver adds the approved army-limit and original Revive activation families to the original 113-family cohort. It does **not** silently claim to rerun every later checkpoint. Dedicated current receipts cover learned Golem Mastery, explosion ground targeting and corpse-location town rejection, Raise Skeleton/Mage corpse-town rejection, Bone Prison targeting, elite Amplify/Lower Resist integration, the expanded ordinary blind/fear AI catalogue, and the later full NPC damage/status/item callbacks. See their named entries in the status ledger and resume checkpoint. Their historical binary identities and limits stay attached to those receipts.

## Remaining mechanical acceptance

- Bone Wall/Prison's exact modern footprint, owner/minion graph, DEATH/DT/hidedead scheduling and collision removal ordering remain unresolved. The pinned older spawn stub and inaccessible current initializer do not establish a replacement implementation. Existing immediate cleanup is not certified as the exact D2R death sequence.
- Iron Golem and Revive admit supported item/stat/event projections. Explicit unsupported projections remain refusals before consumption; wider original-monster AI/species, abilities and current-engine events still need acceptance. UO caster conversion to D2 spells is a separate world task.
- Full modern geometry, room/candidate ordering, RNG/event ordering, PvP/special recipients and some breaking-hit shield/poison details remain bounded individually. The combined cohort validates its actual native adapters; it does not establish every current executable path.
- Actual graphical-client combat and final assets remain separate. [Effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) retain identified placeholders rather than blocking mechanical work.

Continue source-backed, unaffected skills when one of these is blocked. Do not retry inaccessible reference pages without an external change or silently convert uncertainty into an approved rule.
