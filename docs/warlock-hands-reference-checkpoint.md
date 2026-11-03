# Warlock innate weapon reference checkpoint

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. This establishes owned equipment metadata and the concrete implementation dependencies. It enables no skill or equipment exception and certifies no class.

The local installation's `charstats.txt`, `charstats.bin`, `itemtypes.txt` and `itemtypes.bin` were extracted read-only with the previously hash-pinned strict CASC reader. The executable and build manifest were checked before and after extraction. No process memory was read, no game input was sent, and no game or installed server file was written.

The owned class table contains eight records of 212 bytes. All eight class names, 128 ordinary starting/movement/resource stat fields and 16 new hand-rule fields agree with the owned text. Warlock's `TwoHandedOffHandRestrictItemType` is **Grimoire**, with `TwoHandedDamageAsOneHanded` unset. The independently pinned reference JSON agrees with these fields for all eight classes.

The owned item-type table contains 109 records of 232 bytes. All 109 packed codes and 218 parent indices agree with the owned text. Grimoire is runtime index **106**, with parents `shld` and `warl`, class `war`, and staff-mod class `war`. The `Expansion` line is a loader marker; the four other empty-code lines remain real records. Dropping every empty-code row gives an incorrect index. The cached JSON's physical line number is not the compiled item-type index.

[Blizzard's introduction](https://news.blizzard.com/en-us/article/24243863/rain-annihilation-in-reign-of-the-warlock) describes the innate ability to levitate a two-handed weapon while using an offhand. For the pinned version, use the owned Grimoire restriction above rather than treating that launch description as permission to use an ordinary shield. These table matches establish metadata, **not execution of the current equip/damage selectors**. Weapon-family exceptions, animation handling and the actual runtime selector remain separate reference limits.

Current Sanctuary dependencies are concrete:

- The full pinned armor table has **15 Grimoire bases across five families**, all with automatic-prefix group306. The named-item base catalogue contains only five of those bases and none of the five normal-tier bases. It cannot serve as the ordinary Grimoire catalogue. Source the automatic-prefix and Warlock staff-mod behavior before factory rollout rather than silently omitting inherent stats.
- `EquipmentBalance.AddNamedBase` excludes Warlock-specific bases, and `NamedGear.Load` blocks their named items. Normal Grimoire runtime ancestry and appearance/factory support must exist before the hand rule can be exercised by players. Do not unblock named effects merely to add an ordinary Grimoire.
- `ClassGear.CanEquip` rejects the two-handed weapon/offhand combination, and `ClassGear.Active` deactivates an offhand next to a true two-handed weapon. An isolated equip exemption would therefore leave the offhand's stats inactive.
- Reuse `DualEquip`'s atomic layer transaction for both equip orders, failure rollback and canonical layer restoration. Preserve existing Barbarian/Assassin behavior and source two-handed damage. This is a separate innate rule; do not require Levitation Mastery points.
- Verify original Warlock and Witch items through two actual reloads, zero-mastery and trained-mastery cases, both equip orders, ordinary-shield rejection, removal and rollback. Keep fractional resources, attributes and saves intact. The Grimoire's final art is a separate placeholder requirement; this introduces no talent or skill icon.

The [reproducible source evidence](../tools/reference/warlock-hands-reference-evidence.json) binds all owned files, the extraction reader and current dependency anchors. Run `python3 tools/probe_warlock_hands_reference.py --check` for the offline comparison and unchanged-build protection checks.

Production remains `524328631d6b8eae7e2e040660bf0dfc34ffa11e`, with the separately accepted **393 broader / 61 focused** native checks. No new native acceptance is claimed here. Production C#, runtime data and all five accepted assemblies match that baseline; **106 installed artifacts, 38 saves and 20 configuration files are unchanged**. There remain 191 handlers and no completed class. Defiler's dynamic formula execution and the Necromancer's individually documented barrier/reference gaps remain open. Skill effects/audio and talent/hotkey icons remain in their separate registers.
