# Blade Shield event follow-up

Pinned reference: **D2R Reign of the Warlock3.3.93847**, with the older engine used only where its scope is identified.

The complete historical do142 callback allocates successful weapon contacts and calls DrainItemDurability. Result32 suppresses damage/kill events in ExecuteEvents and the earlier fill callback, but DrainItemDurability later dispatches DOMELEEATTACK/ATTACKEDINMELEE and thorns before its return phase. Its NPC attacker branch does not wear the weapon. The new intrinsic Shadow path reuses that complete existing NPC contact ordering. Its focused test requires a real original on-attack property and observes the named attack-attempt phase without source damage/kill phases.

The existing player `BladeShields.Pulse` instead applies its prepared damage under `CombatItemEvents.Suppress`, performs leech/poison/chill/recovery and wear, and does not explicitly run that later attack-attempt/thorns stage. This is an unresolved player parity gap, not an approved new deviation. Current-executable behavior has not been independently observed, so historical source is not labelled current confirmation.

Before Assassin completion: compare the current event path where possible; reuse the established player contact/event/thorns services for the supported branch, with Result32 and copied HitFlags32 at the correct stages; verify actual on-attack, attacked-in-melee, damage/kill suppression, thorns, poison/leech, death and durability ordering. Preserve existing input/payment/cancellation/save behavior. If current D2R differs, record evidence or obtain a specific Sanctuary adaptation. This follow-up does not block independent Shadow/Warlock consumers or asset work.
