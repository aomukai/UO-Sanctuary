# Assassin acceptance map

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. Charms and runewords are excluded. All 30 talents have handlers; each fixture below proves only its named scope. Current combined acceptance is pending, and no full class certificate is claimed.

| Talent | Mechanical scope | Existing fixture |
| --- | --- | --- |
| Tiger Strike | Independent saved charges, paid builder, physical finisher contribution and cancellation | `tiger-strike` |
| Dragon Talon | Approved kick count/clock, one payment, locked target and charge consumption per successful contact | `dragon-talon`, `dragon-talon-clock` |
| Dragon Claw | Approved distinct dual-claw clock, once-only payment, separate contacts/avoidance and charge release | `dragon-claw`, `dragon-claw-preparation` |
| Fists of Fire | Claw builder, elemental conversion and captured fire release | `fists-fire`, `fists-claw` |
| Cobra Strike | Independent saved charges and physical-contact life/mana recovery | `cobra-strike` |
| Dragon Tail | Approved charged contact/payment, kick packet and independently mitigated fire burst | `dragon-tail`, `dragon-tail-preparation`, `dragon-kick`, `kick-packet` |
| Claws of Thunder | Selected claw builder, charges, lightning/charged/area release | `thunder-claw`, `thunder-release` |
| Blades of Ice | Selected claw builder, charges, cold/freeze release and saved effects | `ice-claw`, `ice-release` |
| Dragon Flight | Approved teleport/kick/end events, validated landing payment, interruption and charge release | `dragon-flight`, `dragon-flight-sequence`, `dragon-flight-mode`, `dragon-flight-landing` |
| Phoenix Strike | Single-contact builder, independent charges and meteor/chain/cold release | `phoenix-builder`, `phoenix-release` |
| Claw Mastery | Eligible claw AR/damage/critical, real gear, passive and saved state | `claw-mastery` |
| Psychic Hammer | Paid SC/FCR targeting leeway, physical/magic damage and knockback | `psychic-hammer` |
| Burst of Speed | Paid SC/FCR self-buff, movement/attack speed, Fade replacement and expiry | `burst-speed` |
| Cloak of Shadows | Paid SC/FCR blindness/defense, active living-recipient recast lock and expiry | `cloak` |
| Weapon Block | Dual-claw eligibility, approved chance and shared stationary avoidance | `weapon-block` |
| Fade | Paid SC/FCR self-buff, resistance/physical reduction, curse duration and replacement | `fade` |
| Shadow Warrior | Paid original creator/own skill book/controller, one shared Shadow slot, parking and saved resources | `shadow-cast`, `shadow-master-world` |
| Mind Blast | Paid area damage, conversion versus stun/knockback, allegiance and saved effects | `mind-blast` |
| Venom | Paid captured poison weapon buff, short poison delivery, expiry and original saved state | `venom` |
| Shadow Master | Paid original creator/own AI/equipment/skill use, one shared Shadow slot, parking and saved resources | `shadow-cast`, `shadow-master-world`, `shadow-fury-cast` |
| Fire Blast | Paid S2/IAS ground lob, retained landing point, explosion and saved stages | `fire-blast` |
| Shock Web | Paid S2/IAS scatter, distinct fields and per-throw delay identity | `shock-web` |
| Blade Sentinel | Paid S2/IAS cast, original actor/following blade, shared trap cap, movement and lifecycle | `blade-cast`, `blade-controller`, `blade-missile`, `blade-lifecycle` |
| Charged Bolt Sentry | Paid placement, original S1 actor, firing budget/bolts, shared cap and retirement | `sentry-learned`, `sentry-actor`, `sentry-bolt`, `sentry-retirement` |
| Wake of Fire | Approved shared-per-firing delay, paired waves, actor retirement and learned placement | `wake-learned`, `wake-fire-automatic`, `wake-fire-missile` |
| Blade Fury | Approved held input, fresh windup, per-blade resources, release, captured flights and no saved intent replay | `blade-fury-held-input`, existing held-wire callback |
| Lightning Sentry | Paid placement, original firing actor and travelling lightning | `lightning-learned`, `lightning-sentry` |
| Wake of Inferno | Paid placement, distinct NPC channel/fire beam, firing budget and retirement | `inferno-learned`, `inferno-sentry` |
| Death Sentry | Paid placement, distinct AI/corpse search, lightning/explosion and saved state | `death-learned`, `death-control`, `death-explosion` |
| Blade Shield | Start payment, SC/FCR activation, source weapon fill, natural pulses and saved state | `blade-shield`, `assassin-cast-clock` |

## Approved behavior retained

The individual Talon, Claw, Tail and Flight action rules remain authoritative; they are explicit Sanctuary adaptations rather than claims of recovered modern input edges. Blade Fury uses its independently approved held-input rule and separate startup/per-blade payment. Wake's two waves share the delay within one firing while separate firings/traps remain independent. Occupied stationary-sentry placement uses the approved bounded native search.

Cloak uses the user's corrected observation: recasting is locked while living affected recipients remain, until they die or their effects expire. Mind Blast conversion retains damage but omits conversion-success stun/knockback. Shadow intrinsic Flight/Fury, dual-claw Weapon Block, unsigned missile critical, independent Hydra expiry and Master Sentinel range retain their own approvals and dedicated receipts. Original summons/traps use the established snapshot, difficulty, parking/offline and expiry policies; a class composition does not silently redefine them.

## Remaining work

Exact current executable timing/RNG/stat/event/geometry/owner/PvP equivalence, unsupported original-item callbacks, exceptional terrain/target/recipient cases and graphical-client acceptance remain bounded per skill. Some component fixtures intentionally use supplied states or prepared actors; their titles and raw row counts do not certify learned world/input behavior. Learned Shadow casting and selected automatic AI controls supplement, rather than replace, the dedicated intrinsic proofs. See the [status ledger](../data/Sanctuary/Balance/class-skill-status.json) and individual checkpoints.

Final [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separately tracked with identified placeholders. This mechanical work includes no artwork, charm/runeword substitute or deployment. Installed build, saves, configuration and client protection remain required.
