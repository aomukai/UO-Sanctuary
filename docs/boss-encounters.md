# Campaign boss encounters

The working lineup is King Leoric or another Normal candidate, Archbishop Lazarus in Nightmare, and Diablo in Hell. The Butcher is the other Normal candidate. The campaign uses separate, one-clear Shame 5 story encounters. Each fight should give unfamiliar players a chance to learn, with paths for sustain, single-target, and crowd-clearing builds. Timings, numbers, and art treatments below are playtest proposals. [The invasion campaign](invasion-campaign.md)

## King Leoric

Split a hit directed at Leoric evenly among him and his living linked skeletons. A 100-damage hit would deliver 20 to each recipient with four skeletons, 10 with nine, and the full 100 with none. A hit directed at a skeleton damages that skeleton alone. Fragile skeletons still threaten the player, so clearing them reduces incoming pressure and leaves Leoric more exposed. Attacking Leoric can wear down several skeletons together.

Run summons and healing on an encounter cadence, with time left for Leoric to swing his sword. One action summons skeletons. Another fully heals a randomly chosen living skeleton, using *In Vas Mani* and the familiar UO Greater Heal swirl as presentation. The patient approach is to thin the army between casts. An aggressive player can press Leoric while managing the extra enemies. An imposing armoured skeleton with a large sword is the art direction to explore.

An optional variation gives Leoric a brief, visibly signalled window in which the last qualifying direct player hit selects the next skeleton type. Fire, cold, lightning, and poison each suggest a matching caster, with cues such as a red glow for fire or blue for cold. Physical suggests an archer. A window with only pet attacks can use a random type. Melee skeletons may also be present. An early pet-scaling experiment would add roughly half the player’s active pet count per summon action, subject to population limits and playtesting.

Resolve how the incoming hit is split around mitigation, rounding, mixed elements, and damage over time. The server should process one hit with shared damage recipients and account for effects such as life leech and on-hit procs once. Check that summon and heal actions stay exclusive, target loss is handled cleanly, and pet-heavy builds face the same action cadence.

## Archbishop Lazarus

Lazarus teleports through five elemental pentagrams in the same order, covering fire, cold, lightning, poison, and physical. Immediately after landing he shoots one fireball at the player from the new position. The shot reveals his location. The fixed order lets players anticipate where he will go. He summons another demon of that circle’s element during the stop.

Lazarus is immune to the circle’s damage type while he stands inside it and one or more demons linked to that element are alive. Leaving the circle or killing its last guardian drops the ward immediately. Other elements remain available at that stop. On the first cold stop the player has a window before the Ice Fiend spawns. If it survives, the ward is active on arrival at the cold circle on the second circuit. At the start of the third circuit, two surviving demons per element would give every pentagram a ward source.

Single-target builds can kill each summon and spend the rest of the stop attacking Lazarus. Crowd-clearing builds can endure a pack and then clear it together. Clearing a whole element reopens that circle’s window until its next summon, and clearing the pack restores the opening windows across all five stops. Track all live guardian identities for each element and reconcile the ward on movement, death, and reset.

Consider dodgeable volleys or temporary firewalls and an explicit recovery window for melee players. Tune summon timing, demon strength, projectile geometry, and damage in the client. Larger shots are an option if their visible shape still matches the danger area. The fireball immediately after teleport is the location cue.

## Diablo

Diablo uses ordinary melee and spell attacks alongside brief disruptions that respond to a character’s playstyle. Give each disruption a cue, a way to recover, and time to resume the build’s normal play. Sparse lines can show Diablo understands what the player relies on.

- **Ranged.** A draft version blinds the player after roughly three seconds in Diablo’s effective ranged zone, for about ten seconds. Blindness affects targeting. The player can leave the zone and reposition or get close enough to target him in melee. Exact duration and reset rules are open.
- **Summoners.** Diablo temporarily turns active summons against their owner, with about five seconds as a starting point. Survivors go dormant afterward. The owner reactivates them by coming within roughly five UO tiles with line of sight. Diablo can use a telegraphed charge to threaten the approach. A successful return to control lets the summoner reposition normally. The server saves ownership and handles resets, death, and logout without corrupting the real summons.
- **Melee.** A trap, knockback, or brief fear can force a fighter to reposition. Give the player a readable cue and recovery opportunity before follow-up attacks. Redirected Assassin traps, terrain that changes Paladin positioning, and interruptions to Druid shapeshifting are examples to evaluate.

Use server tile geometry for proximity and line of sight. Test pure and mixed builds and decide how disruptions select targets in mixed parties.

## Failure and traces in the world

One proposal sends a defeated Avatar back to Normal with their carried gear, as if waking from a dream. They have to make the trip again and the encounter resets. Decide whether this applies to Lazarus only or to all three, and reconcile it with death and recovery rules. [Travel, shrines, and recovery](travel-and-recovery.md)

Teach mechanics in smaller encounters where suitable. A lone ritual circle can introduce a demon-sustained ward. Liches can raise ordinary undead, and Lich Lords can heal followers in the style of Leoric’s army. Umbra can give that tradition a home. Blood’s cult can try to restore Lazarus, and cultists in Doom can invoke Diablo. These places can reuse selected mechanics and reveal what the invasion left behind. [Worldbuilding](worldbuilding.md) · [Social encounters](social-encounters.md)
