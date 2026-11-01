# Enchant acceptance preflight

Follow-up: the [casting checkpoint](sorceress-enchant-cast-checkpoint.md) records the implemented source-list corrections and ordinary player/native verification. This preflight remains the original plan; its modern NPC/item/event/client gaps still apply.

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This read-only follow-up does not claim new Enchant acceptance or class completion.

Reuse `SorceressEffects.CastEnchant`, `ApplyEnchant`, the independent `EnchantState`, `EnchantFire`, current learned/gear rank suppliers and the existing native/class/minion weapon consumers. `EnchantChecks` already covers ally/pet targeting, snapshots, AR, weaker/equal/stronger replacement, native/class/ranged delivery, wielder mastery, expiry and saved state. Retain that coverage rather than copying the Mage's approved automatic hard-point unlock into the player's cast path. The latter is a distinct actor policy.

The pinned row is source52/start0/do25, mana25+1per effective rank beyond1, and duration`ln12`. It writes `firemindam=enma`, `firemaxdam=exma` and `item_tohit_percent=toht`; AR is20+9per rank beyond1. Complete older do25 falls back to the caster when no target or an unaligned target is selected, constructs the curse/stat list, refuses allocation failure, evaluates/writes up to six valid nonzero stat slots and sets the target graphics flag. Current `SorceressCombat` has the same hostile-target self fallback and uses existing ally admission/range/LOS. Do not infer that the shared curse/list helper, modern state replacement or every weapon event has been executed merely by reading this wrapper.

Next: review existing curse/list replacement and element/mastery helpers, then compile/reuse complete bounded source evidence and add ordinary player right/quick acceptance for self/ally/hostile fallback, payment at release, cancellation, insufficient mana, ranks/hard Warmth/mastery/real gear, independent recipient state and two actual reloads. Keep captured caster enhancement distinct from wielding-recipient enhancement and current weapon/event eligibility. A held inspection deadline is not natural expiry proof. Source event/RNG/order, special/PvP/charged/oskill and actual graphical targeting remain explicit until tested.

Effects/audio/glow and talent/hotkey icons remain separately pending with identified UO/text placeholders. Inferno's unresolved modern accepted-start/payment/reentry chronology remains individually deferred; its compatible stream binding can still advance independently.
