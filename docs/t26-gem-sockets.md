# T26 — Permanent gems and working slot effects

Double-click a socket gem, target identified equipment with an empty socket in your backpack, review its effects for that host, then confirm. One gem is consumed from the selected stack. Installation costs no gold, tool use, skill or station. It is permanent: no extraction or replacement. Salvage destroys contents without increasing yield. Legacy decorative gems cannot be inserted.

Gems and runes share capacity and level requirements. A helmet, chest, weapon or shield can hold any mix within its existing socket count, at most five. One through five filled sockets require level30/40/50/60/70, combined with existing base/affix level and attribute requirements. Empty sockets add no requirement; gem identity adds none. Cancellation, stale requests, changed stacks/hosts, foreign or protected items and invalid player state consume nothing. Gem and rune confirmations share a session, so opening one invalidates an older request for the other.

Each installed gem saves its family, quality and complete frozen weapon/shield/armour payload. The host determines which group applies; helmets and chests use armour effects. All qualities use the [T24 catalogue](t24-gems.md). The original seven mapped families follow [Blizzard's gem table](https://classic.battle.net/diablo2exp/items/gems.shtml); Amber supplies universal flat Attacker Takes Damage4–20 and Star Sapphire supplies the confirmed universal Magic Damage Reduced1–5. Insertion does not reroll values, alter rarity or enable additional random affix pools.

| UO family | Weapon | Shield | Helmet/chest |
| --- | --- | --- | --- |
| Amethyst | Attack rating | Flat defense | Strength |
| Diamond | Physical damage against undead | Four elemental resistances | Attack rating |
| Emerald | Poison damage over time | Poison resistance | Dexterity |
| Ruby | Fire damage | Fire resistance | Maximum life |
| Sapphire | Cold damage and chill | Cold resistance | Maximum mana |
| Citrine | Lightning damage | Lightning resistance | Magic Find |
| Tourmaline | Physical-hit life/mana steal | Flat attacker damage | Life replenishment and mana regeneration |
| Amber | Flat attacker damage | Flat attacker damage | Flat attacker damage |
| Star Sapphire | Flat magic reduction | Flat magic reduction | Flat magic reduction |

Stats, resource maxima, defense, attack rating, resistance and MF enter the existing gear consumers once. Broken, unequipped, hidden or inactive hosts contribute nothing. Copy, trade, native repair and reload retain identity and totals. Existing native sockets may still survive Imbuing with their contents; tinkerer-added sockets retain their prior Imbuing prohibition.

Elemental weapon damage joins the existing material damage channels before resistance. Diamond adds its percentage to the physical attack's existing off-weapon/stat bonus only against a target classified as undead by its creature profile, the project's undead classes or native Silver slayer classification. It does not multiply elemental damage or spells. Cold durations from equipped weapon gems add, alongside any material chill duration, then use the existing resistance and monster chill-effectiveness rules. Emerald poison rates add and source durations average; repeated identical Emeralds increase total damage without multiplying length. Material poison remains a three-second source. The existing stronger-rate replacement, fractional damage and periodic-hit rules remain in force. Timed-effect persistence is now provided by [T27](t27-timed-effects.md).

Life/mana steal uses actual physical weapon damage, capped by health actually lost, and never elemental damage, poison ticks, spells or reflected damage. Monster drain effectiveness applies, including zero for skeletons; Normal/Nightmare/Hell divide recovery by1/2/3. Recovery rounds down per hit and native resource caps apply. [Blizzard's suffix reference](https://classic.battle.net/diablo2exp/items/magic/suf.shtml) describes physical-hit stealing, drain exclusions and the scope of magic reduction.

Attacker Takes Damage triggers once after a damaging melee hit, including native creature melee, and applies the attacker's physical resistance. Ranged attacks, spells, misses and blocked/no-damage hits do not trigger it. Reflected damage cannot recurse into another weapon strike, leech or on-hit effect. Flat Magic Damage Reduced subtracts from each fire/cold/lightning or supported pure-magic component before resistance, to a minimum of zero; it does not reduce physical or poison damage, or spill excess reduction into another channel. Native AOS damage and the project's weapon/Teeth paths use this rule. Native attacks explicitly bypassing resistance retain their existing bypass. The reduction order follows [Blizzard's resistance reference](https://classic.battle.net/diablo2exp/basics/resistances.shtml).

Tourmaline regeneration extends existing project rates rather than replacing the class baseline. Each Replenish Life point adds25/256 life per second. Mana regeneration increases the baseline mana rate by the summed percentage; Verite's flat per-five-second contribution stays additive. Native eligibility, resource caps and tick scheduling remain in force, with no equip tick or offline catch-up.

Gear persistence advances from version4 to5, adding frozen gem records after rune records. Older saves load empty gem arrays; existing rune identities and every prior field remain intact. Copy clones gem effect arrays. The source/installed checks cover180 family/quality/host cases,360 saved originals/copies, mixed capacity/requirements, actual stat/combat/regen consumers, native spell reduction and melee reflection, leech difficulties, repair/salvage, cancellation/replay and old-version migration. The actual client exercises gem double-click, target, cancel, permanent confirmation, mixed rune/gem tooltip, level40 and weapon damage. Disposable fixture seeds remain outside production.

Completed and deployed on20 October2026(JST). Native/content/integration builds have zero warnings or errors; source and installed cumulative suites, separate legacy migrations,26 Python checks and both saved live boots pass. The complete runtime backup and final copied-world comparison preserve all prior character, gear, material, gold and configuration data. Production contains no test assembly or fixture world. Engine, application, client and world populations are unchanged.
