# Paladin mechanical acceptance map

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. Charms and runewords are excluded. This identifies actual checks in `tests/check_paladin_planned_acceptance.py` and the existing dedicated evidence; the current [combined checkpoint](paladin-planned-acceptance-checkpoint.md) passes 601 exact results across three boots and two reloads. Handler presence and declared coverage do not certify the class.

| Talent | Mechanical scope | Included native families |
| --- | --- | --- |
| Prayer | Healing eligibility, fractional mana payment and owner/ally recovery | `paladin-resource` |
| Resist Fire | Active fire resistance and hard-rank passive maximum resistance | `paladin-resistance` |
| Resist Cold | Active cold resistance and hard-rank passive maximum resistance | `paladin-resistance` |
| Resist Lightning | Active lightning resistance and hard-rank passive maximum resistance | `paladin-resistance` |
| Defiance | Defense aura, range/linger, owner/pet effects and source priority | `paladin-aura` |
| Cleansing | Poison/curse duration reduction and inherited Prayer healing | `paladin-resource` |
| Vigor | Movement, maximum stamina/recovery, pet modifiers and no free refill | `paladin-travel` |
| Meditation | Mana recovery and inherited Prayer healing | `paladin-resource` |
| Redemption | Periodic corpse recovery, chance, one-use consumption and retained loot | `paladin-travel` |
| Salvation | Shared elemental resistance aura and priority | `paladin-resistance` |
| Might | Physical damage aura, real owner/pet consumers and saved effects | `might-aura`, `paladin-aura` |
| Holy Fire | Fire pulse and weapon contribution with captured aura values | `paladin-element` |
| Thorns | Physical melee return, exclusions and saved aura state | `paladin-thorns` |
| Blessed Aim | Active attack rating and hard-rank passive contribution | `paladin-aura`, `amazon-penetrate` |
| Concentration | Damage contribution, interruption modifier and aura ownership | `paladin-offense` |
| Holy Freeze | Cold pulse/weapon contribution, independent slow and corpse controls | `holy-freeze`, `paladin-element` |
| Holy Shock | Lightning pulse and weapon contribution | `paladin-element` |
| Sanctuary | Undead pulse/knockback, zero-mana operation and saved source effects | `sanctuary-aura` |
| Fanaticism | Attack speed/rating, separate personal/party damage and saved aura state | `paladin-offense` |
| Conviction | Defense/resistance reduction, immunity interaction and source priority | `paladin-conviction` |
| Sacrifice | Prepared weapon contact, self-cost and ordered damage allocation | `sacrifice` |
| Smite | Shield-based contact, knockback/stun and supported weapon events | `smite` |
| Holy Bolt | Travelling ally healing/eligible magic damage, piercing and saved flight | `holy-bolt-flight`, `holy-bolt-damage`, `holy-bolt` |
| Zeal | Source-timed series, target handling, payment and low-mana basic attack | `zeal` |
| Charge | Movement/contact phases, near-target payment and cancellation | `charge` |
| Vengeance | Prepared weapon and elemental contact, hard synergies and payment | `vengeance` |
| Blessed Hammer | Saved spiral movement, collision, magic damage and aura modifiers | `blessed-hammer-flight`, `blessed-hammer-damage`, `blessed-hammer` |
| Conversion | Successful no-damage conversion, allegiance, target AI and expiry | `conversion` |
| Holy Shield | Paid start, shield buffs/blocking, rank replacement and saved actions | `holy-shield-cast`, `holy-shield` |
| Fist of the Heavens | Cursor-assisted target, direct lightning and independent Holy Bolts | `heaven-fist-damage`, `heaven-bolt`, `heaven-fist` |

The `paladin-binding` family covers all 30 pinned mouse policies, quick slots and saved normalization. `paladin-cast-clock-check` retains ordinary SC timing and separate aura/melee controllers. The Holy Shield save coordinator also stages the existing Teleport fixture, checking that unpaid transient work does not replay. Dedicated checks retain the current immutable/atomic aura and knockback persistence contracts.

User-confirmed behavior remains explicit: Charge spends nine mana with or without a target; successful Conversion does no weapon damage and converted targets are friendly; ordinary auras have no mana cost except Prayer when healing; Sanctuary can push undead at zero mana. Fist of the Heavens ground acquisition follows the click direction and chooses near the cursor. The approved underfunded Zeal fallback is a basic attack. The proposed broader empty-swing/free-mana melee convenience pass remains deferred.

## Explicit remaining limits

- At exactly zero mana, the older Fanaticism callback currently supplies its party damage bonus to the caster instead of the full personal bonus. Modern equivalence is unverified; the no-mana-cost observation does not establish the correct damage rule. A [prepared mana-neutral option](fanaticism-mana-proposal.md) now passes47 checks/twelve existing aura inputs/two reloads while remaining disabled; adoption and enabled learned-input qualification are separate. Existing competing-source packet rules remain unqualified and unchanged.
- Sanctuary's broader undead-bypass consumers, exceptional Holy Freeze modes and other skill-specific supplied services retain their documented boundaries.
- Blessed Hammer retains the documented older-source motion/guard conflict; source comparison does not establish modern factory, room or global chronology parity.
- Unsupported native species, recipient/item projections, special/PvP routes, exact geometry/event/RNG ordering, historical binary upgrades and graphical-client behavior remain bounded by their dedicated receipts.
- Final effects/audio and talent/hotkey icons stay in [separate](skill-effects.md) [registers](skill-icons.md), with identified placeholders. Existing wardrobe save-pruning warnings remain a separate concern.

The [status ledger](../data/Sanctuary/Balance/class-skill-status.json) and dedicated checkpoints retain the detailed limits. Continue unaffected mechanics rather than inventing unresolved behavior or treating a passing subset as complete D2R parity.
