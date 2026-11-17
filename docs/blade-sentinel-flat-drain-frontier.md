# Blade Sentinel flat-drain boundary

**Reachable, unresolved — 2026-11-17 JST.** This is a current production-data audit, not a new mechanical approval or a native cast receipt. The mechanical reference remains D2R 3.3.93847; the newer installed client/CASC was not read.

The player-stage collector in `BladeSentinelLaunch.Capture` calls `BowLaunch.WeaponElements`. That collector includes the player's `life-steal%` and stealing-potion bonus in `MissileSourceStats.LifeDrain`. `BladeSentinelPackets.FromSource` and `BowLaunch.FromSource` then apply the pinned SrcDam value, including the existing two-handed division. `MissileSourceStats.Scale` scales the drain field as well as the damage fields.

This is reachable from planned gear. The current readiness audit admits **Stealskull (`unique/203`)**, whose table supplies exactly 5% life steal. Sentinel's pinned SrcDam is 96. With no other leech bonuses, a one-handed weapon captures `floor(5 × 96 / 128) = 3`; the two-handed route captures `floor(5 × 48 / 128) = 1`. Neither requires an excluded charm or runeword. The audit establishes the data/control path and arithmetic; it does not claim an actual Stealskull cast was newly run.

At impact, the current receiver adds that positive field separately to the settled total, in FP256 units, following its documented older NPC-drain path. It also retains its existing NPC leech service. The extra field has no established physical/magic/fire/cold/lightning channel. A positive value therefore keeps the scalar health call and the explicit Health Link missing-components fault. Ordinary zero-drain packets already have typed coverage; the earlier positive synthetic control remains a refusal rather than a claim that this path cannot occur.

Do not silently map the extra amount to an element, discard its existing primary damage, or label this boundary finished. A follow-up should exercise actual admitted gear through capture, learned casting and a linked impact, then settle the extra lane through pinned evidence or a tested, explicitly approved Sanctuary rule. Existing ordinary NPC typed packets with separate flat-drain fields are the first reuse point to inspect. Keep the captured damage, NPC leech, ownership, timers and original-save behavior separately attributable.

This does not block the independent Necromancer combined audit. No gameplay/data/asset change is made by this report. Effects/audio and icons remain separate inventories.

[Machine-readable source audit](../tools/reference/blade-sentinel-flat-drain-frontier.json), [earlier receiver checkpoint](blade-link-checkpoint.md).
