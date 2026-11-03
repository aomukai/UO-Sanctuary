# Approved Warlock summon models

The user approved these UO presentations on2026-11-03 and clarified that “Beholder” means the basic **Gazer**:

| D2R summon | UO model | Body | Basic sound |
| --- | --- | --- | --- |
| Goatman | Gargoyle |4/0x04|372/0x174|
| Defiler | Gazer |22/0x16|377/0x179|
| Tainted | Succubus |149/0x95|1200/0x4B0|

The [appearance reference](../tools/reference/warlock-demon-appearance-reference.json) hashes the local ModernUO constructors that supply these values. This is an approved presentation deviation; the mechanical reference remains **D2R Reign of the Warlock3.3.93847**. `WarlockDemonAppearance` is shared by the prepared Goatman and the future demon bodies. Goatman's visible name remains “Summoned Goatman”; talent names are unchanged.

The prepared Goatman uses Gargoyle now. Its load hook replaces the old0x11model/basic-sound placeholder while retaining birth, serial, owner, equipment, order and fractional wounds. It does not reconstruct a summon or recover health. Defiler and Tainted do not yet have mechanical bodies; their presentation choices are registered, not functioning learned summons. These models do not import UO creature classes, Magery, spells, flight, combat stats, loot, AI or source animation clocks.

The Inferno stream acceptance batch includes actual prepared Warlock and Witch bodies through3boots/2reloads. One original saved body deliberately starts with the old placeholder; both reloads verify the approved body plus the original snapshot, fractional health, owner/helm/order and unavailable learned gate. Source species remains wargoatman/Demon/size2. See the [combined native receipt](../tools/reference/inferno-stream-native-evidence.json). No deployment or installed-save change is claimed.

These native models/basic sounds supersede the earlier body0x11placeholder notes. Special summon/teleport/skill/combat VFX and custom audio, full animation acceptance and talent/hotkey icons remain separately tracked. No image generation is required to establish the chosen native body models.
