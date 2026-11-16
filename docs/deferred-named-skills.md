# Deferred named-item spells and skills

User decision: withhold items whose spell/skill effects cannot work yet, and return to them when their prerequisites exist. Charms are excluded by project policy and have no return tasks. Keep their acquisition weight unchanged: unavailable drops yield nothing, and unavailable gambles refund the stake without rerolling.

Art decision (22 October 2026): custom imagegen assets may supply missing equipment art, class/skill icons and spell effects. For each returning skill or item, review existing art, record any required new assets, retain the prompt and source image in the project, then verify transparency, scale, directional frames/timing and the actual client resource mapping. Generation, client integration and gameplay verification are separate completion checks.

Pinned source: `fa1286949266498b294c40c2942521a7c0d7207a`. Reproduce with `python3 tools/build_named_skill_backlog.py`; verify with `--check`. The machine-readable companion is `data/Sanctuary/Balance/named-skill-backlog.json`.

240 class talent routes are mechanically implemented;the separate direct-grant registry still contains18talents. A working spell alone does not supply a proc, aura, charge or cross-class grant system. Generic class/tree/all-skill and source-tagged fire-skill bonuses raise learned or directly granted skills without spending points or unlocking every skill. Item freeze, blind and flee are implemented shared effects; they do not require the full player class spells to exist.

164 source-spawnable item definitions have direct or set-wide spell/skill dependencies. 19 additional pieces are held so their sets remain completable. These counts describe skill dependencies, not total runtime availability; other source, appearance or combat-effect gates may also apply.

## Source-spawnable items to revisit

| ID | Item | Prerequisite spells/skills and systems |
| --- | --- | --- |
| unique/16 | Rusthandle | own `skill`: Vengeance (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Thorns (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/17 | Stormeye | own `skill`: Resist Lightning (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Holy Shock (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Fist of the Heavens (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/20 | Bloodrise | own `skill`: Sacrifice (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/35 | Kinemil's Awl | own `skill`: Holy Fire (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/54 | Bane Ash | own `skill`: Fire Bolt (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Warmth (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/56 | Spire of Lazarus | own `skill`: Chain Lightning (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Lightning (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Static Field (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/57 | The Salamander | own `skill`: Fire Wall (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Fire Ball (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Warmth (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/58 | The Iron Jang Bong | own `skill`: Nova (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Blaze (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Frost Nova (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/60 | Witherstring | own `magicarrow`: Magic Arrow (mechanical route implemented) — Inherent magic-arrow attack and projectile delivery |
| unique/61 | Raven Claw | own `explosivearrow`: Exploding Arrow (mechanical route implemented) — Inherent exploding-arrow attack and area damage |
| unique/62 | Rogue's Bow | own `skill` via roguesbow-affix1: Fire Arrow (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill` via roguesbow-affix1: Cold Arrow (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/64 | Wizendraw | own `magicarrow`: Magic Arrow (mechanical route implemented) — Inherent magic-arrow attack and projectile delivery |
| unique/66 | Blastbark | own `skill`: Exploding Arrow (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/69 | Hellcast | own `explosivearrow`: Exploding Arrow (mechanical route implemented) — Inherent exploding-arrow attack and area damage |
| unique/108 | Gorefoot | own `skill`: Leap (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/111 | Tearhaunch | own `skill`: Vigor (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/129 | Coldkill | own `hit-skill`: Ice Blast (mechanical route implemented) — on-hit proc dispatcher<br>own `gethit-skill`: Frost Nova (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/131 | Islestrike | own `skill`: Maul (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Fury (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/132 | Pompeii's Wrath | own `hit-skill`: Volcano (mechanical route implemented) — on-hit proc dispatcher |
| unique/133 | Guardian Naga | own `hit-skill`: Poison Nova (mechanical route implemented) — on-hit proc dispatcher |
| unique/135 | Spellsteel | own `charged`: Teleport (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Decrepify (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Holy Bolt (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Firestorm (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/136 | Stormrider | own `hit-skill`: Chain Lightning (mechanical route implemented) — on-hit proc dispatcher<br>own `hit-skill`: Charged Bolt (mechanical route implemented) — on-hit proc dispatcher<br>own `gethit-skill`: Charged Bolt (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/137 | Boneslayer Blade | own `charged`: Holy Bolt (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `gethit-skill`: Holy Bolt (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/141 | Arm of King Leoric | own `gethit-skill`: Bone Spirit (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `gethit-skill`: Bone Prison (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `skill`: Raise Skeletal Mage (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/142 | Blackhand Key | own `charged`: Grim Ward (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/144 | Zakarum's Hand | own `hit-skill`: Blizzard (mechanical route implemented) — on-hit proc dispatcher<br>own `skill`: Holy Freeze (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Holy Shock (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/145 | The Fetid Sprinkler | own `hit-skill`: Decrepify (mechanical route implemented) — on-hit proc dispatcher<br>own `hit-skill`: Confuse (mechanical route implemented) — on-hit proc dispatcher |
| unique/146 | Hand of Blessed Light | own `skill`: Holy Bolt (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Fist of the Heavens (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `hit-skill`: Fist of the Heavens (mechanical route implemented) — on-hit proc dispatcher |
| unique/148 | Sureshrill Frost | own `charged`: Frozen Orb (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/149 | Moonfall | own `hit-skill`: Meteor (mechanical route implemented) — on-hit proc dispatcher<br>own `charged`: Meteor (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/150 | Baezil's Vortex | own `hit-skill`: Nova (mechanical route implemented) — on-hit proc dispatcher<br>own `charged`: Nova (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/151 | Earthshaker | own `hit-skill`: Fissure (mechanical route implemented) — on-hit proc dispatcher |
| unique/152 | Bloodtree Stump | own `skill`: Mace Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/153 | The Gavel of Pain | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher<br>own `gethit-skill`: Iron Maiden (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `charged`: Amplify Damage (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/154 | Bloodletter | own `skill`: Blade Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Whirlwind (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/156 | Hexfire | own `charged`: Hydra (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/160 | Plague Bearer | own `hit-skill`: Poison Nova (mechanical route implemented) — on-hit proc dispatcher<br>own `skill`: Rabies (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/163 | Bing Sz Wang | own `hit-skill`: Frozen Orb (mechanical route implemented) — on-hit proc dispatcher |
| unique/164 | The Vile Husk | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher |
| unique/165 | Cloudcrack | own `hit-skill`: Fist of the Heavens (mechanical route implemented) — on-hit proc dispatcher |
| unique/166 | Todesfaelle Flamme | own `att-skill`: Fire Ball (mechanical route implemented) — on-attack proc dispatcher<br>own `charged`: Enchant (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Fire Wall (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/169 | Heart Carver | own `skill`: Find Potion (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Find Item (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Grim Ward (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/171 | Stormspike | own `gethit-skill`: Charged Bolt (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/172 | The Impaler | own `skill`: Impale (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Power Strike (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/178 | Blackleach Blade | own `hit-skill`: Weaken (mechanical route implemented) — on-hit proc dispatcher |
| unique/185 | Chromatic Ire | own `skill`: Fire Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Lightning Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Cold Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/188 | Skystrike | own `hit-skill`: Meteor (mechanical route implemented) — on-hit proc dispatcher |
| unique/190 | Kuko Shakaku | own `skill`: Immolation Arrow (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `explosivearrow`: Exploding Arrow (mechanical route implemented) — Inherent exploding-arrow attack and area damage |
| unique/191 | Endlesshail | own `skill`: Strafe (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/192 | Witchwild String | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher<br>own `magicarrow`: Magic Arrow (mechanical route implemented) — Inherent magic-arrow attack and projectile delivery |
| unique/194 | Magewrath | own `skill`: Guided Arrow (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/195 | Goldstrike Arch | own `hit-skill`: Fist of the Heavens (mechanical route implemented) — on-hit proc dispatcher |
| unique/197 | Pus Spitter | own `hit-skill`: Lower Resist (mechanical route implemented) — on-hit proc dispatcher<br>own `gethit-skill`: Poison Nova (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/199 | Demon Machine | own `explosivearrow`: Exploding Arrow (mechanical route implemented) — Inherent exploding-arrow attack and area damage |
| unique/204 | Darksight Helm | own `gethit-skill`: Dim Vision (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `charged`: Cloak of Shadows (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/222 | Corpsemourn | own `gethit-skill`: Iron Maiden (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `charged`: Corpse Explosion (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/226 | Stormchaser | own `gethit-skill`: Blizzard (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `gethit-skill`: Tornado (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/227 | Tiamat's Rebuke | own `gethit-skill`: Frost Nova (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `gethit-skill`: Nova (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `gethit-skill`: Hydra (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/229 | Radament's Sphere | own `gethit-skill`: Poison Nova (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `charged`: Poison Explosion (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/235 | Lava Gout | own `hit-skill`: Enchant (mechanical route implemented) — on-hit proc dispatcher |
| unique/236 | Hellmouth | own `hit-skill`: Meteor (mechanical route implemented) — on-hit proc dispatcher<br>own `hit-skill`: Firestorm (mechanical route implemented) — on-hit proc dispatcher |
| unique/237 | Infernostride | own `gethit-skill`: Blaze (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/245 | Snowclash | own `gethit-skill`: Blizzard (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `skill`: Blizzard (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Glacial Spike (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Chilling Armor (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/246 | Thundergod's Vigor | own `gethit-skill`: Fist of the Heavens (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `skill`: Lightning Strike (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Lightning Fury (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/252 | Blackoak Shield | own `gethit-skill`: Weaken (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/254 | Hellslayer | own `att-skill`: Fire Ball (mechanical route implemented) — on-attack proc dispatcher |
| unique/257 | Schaefer's Hammer | own `hit-skill`: Static Field (mechanical route implemented) — on-hit proc dispatcher |
| unique/258 | The Cranium Basher | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher |
| unique/259 | Lightsabre | own `att-skill`: Chain Lightning (mechanical route implemented) — on-attack proc dispatcher |
| unique/260 | Doombringer | own `hit-skill`: Weaken (mechanical route implemented) — on-hit proc dispatcher |
| unique/264 | Stormspire | own `gethit-skill`: Chain Lightning (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `gethit-skill`: Charged Bolt (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/270 | The Rising Sun | own `gethit-skill`: Meteor (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/273 | Atma's Scarab | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher |
| unique/277 | Saracen's Chance | own `gethit-skill`: Iron Maiden (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/284 | The Oculus | own `gethit-skill`: Teleport (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/289 | Bloodmoon | own `charged`: Blood Golem (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/292 | Warshrike | own `hit-skill`: Nova (mechanical route implemented) — on-hit proc dispatcher |
| unique/296 | Demon Limb | own `charged`: Enchant (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/298 | Tomb Reaver | own `reanimate` — On-kill reanimation, source monster template, ownership, AI and lifetime |
| unique/300 | Nature's Peace | own `charged`: Oak Sage (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/301 | Azurewrath | own `aura`: Sanctuary (mechanical route implemented) — equipped aura lifecycle, radius and stacking |
| unique/306 | Horizon's Tornado | own `hit-skill`: Tornado (mechanical route implemented) — on-hit proc dispatcher |
| unique/310 | Cerebus' Bite | own `skill`: Feral Rage (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/312 | Soul Drainer | own `hit-skill`: Weaken (mechanical route implemented) — on-hit proc dispatcher |
| unique/315 | Executioner's Justice | own `kill-skill`: Decrepify (mechanical route implemented) — on-kill proc dispatcher |
| unique/319 | Wisp Projector | own `hit-skill`: Lightning (mechanical route implemented) — on-hit proc dispatcher<br>own `charged`: Oak Sage (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Heart of Wolverine (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Spirit of Barbs (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/320 | Gargoyle's Bite | own `charged`: Plague Javelin (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/321 | Lacerator | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher |
| unique/323 | Viperfork | own `hit-skill`: Poison Explosion (mechanical route implemented) — on-hit proc dispatcher |
| unique/326 | The Reaper's Toll | own `hit-skill`: Decrepify (mechanical route implemented) — on-hit proc dispatcher |
| unique/328 | Hellrack | own `charged`: Immolation Arrow (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/331 | Widowmaker | own `magicarrow`: Magic Arrow (mechanical route implemented) — Inherent magic-arrow attack and projectile delivery<br>own `oskill`: Guided Arrow (mechanical route implemented) — cross-class skill grants and casting |
| unique/332 | Blood Raven's Charge | own `explosivearrow`: Exploding Arrow (mechanical route implemented) — Inherent exploding-arrow attack and area damage<br>own `charged`: Revive (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/334 | Shadow Killer | own `hit-skill`: Frost Nova (mechanical route implemented) — on-hit proc dispatcher |
| unique/337 | Windhammer | own `hit-skill`: Twister (mechanical route implemented) — on-hit proc dispatcher |
| unique/338 | Thunderstroke | own `hit-skill`: Lightning (mechanical route implemented) — on-hit proc dispatcher<br>own `skill`: Lightning Bolt (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/341 | Boneflame | own `gethit-skill`: Terror (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/345 | Andariel's Visage | own `gethit-skill`: Poison Nova (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `charged`: Venom (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/347 | Dragonscale | own `oskill`: Hydra (mechanical route implemented) — cross-class skill grants and casting |
| unique/348 | Steel Carapace | own `gethit-skill`: Iron Maiden (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/349 | Medusa's Gaze | own `gethit-skill`: Lower Resist (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `death-skill`: Nova (mechanical route implemented) — on-death proc dispatcher |
| unique/350 | Ravenlore | own `skill`: Raven (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/353 | Flamebellow | own `hit-skill`: Firestorm (mechanical route implemented) — on-hit proc dispatcher<br>own `oskill`: Inferno (mechanical route implemented) — cross-class skill grants and casting |
| unique/355 | Wolfhowl | own `oskill`: Werewolf (mechanical route implemented) — cross-class skill grants and casting<br>own `charged`: Summon Dire Wolf (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `oskill`: Lycanthropy (mechanical route implemented) — cross-class skill grants and casting<br>own `oskill`: Feral Rage (mechanical route implemented) — cross-class skill grants and casting |
| unique/356 | Spirit Ward | own `gethit-skill`: Fade (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/358 | Ormus' Robes | own `skill-rand`: Fire Bolt (mechanical route implemented), Warmth (mechanical route implemented), Charged Bolt (mechanical route implemented), Ice Bolt (mechanical route implemented), Frozen Armor (mechanical route implemented), Inferno (mechanical route implemented), Static Field (mechanical route implemented), Telekinesis (mechanical route implemented), Frost Nova (mechanical route implemented), Ice Blast (mechanical route implemented), Blaze (mechanical route implemented), Fire Ball (mechanical route implemented), Nova (mechanical route implemented), Lightning (mechanical route implemented), Shiver Armor (mechanical route implemented), Fire Wall (mechanical route implemented), Enchant (mechanical route implemented), Chain Lightning (mechanical route implemented), Teleport (mechanical route implemented), Glacial Spike (mechanical route implemented), Meteor (mechanical route implemented), Thunder Storm (mechanical route implemented), Energy Shield (mechanical route implemented), Blizzard (mechanical route implemented), Chilling Armor (mechanical route implemented) — Random skill selection and saved skill identity; Qualify direct +skill item grants for the selected class consumers |
| unique/360 | Stormlash | own `hit-skill`: Static Field (mechanical route implemented) — on-hit proc dispatcher<br>own `hit-skill`: Tornado (mechanical route implemented) — on-hit proc dispatcher |
| unique/361 | Halaberd's Reign | own `skill`: Battle Orders (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Battle Command (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/364 | Dracul's Grasp | own `hit-skill`: Life Tap (mechanical route implemented) — on-hit proc dispatcher |
| unique/365 | Frostwind | own `oskill`: Arctic Blast (mechanical route implemented) — cross-class skill grants and casting |
| unique/368 | Firelizard's Talons | own `skill`: Wake of Fire (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Wake of Inferno (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/370 | Marrowwalk | own `charged`: Bone Prison (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Life Tap (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/373 | Arachnid Mesh | own `charged`: Venom (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/375 | Metalgrid | own `charged`: Iron Golem (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Iron Maiden (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/378 | Carrion Wind | own `gethit-skill`: Poison Nova (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `charged`: Poison Creeper (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `hit-skill`: Twister (mechanical route implemented) — on-hit proc dispatcher |
| unique/383 | Cranebeak | own `charged`: Raven (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/384 | Nord's Tenderizer | own `charged`: Blizzard (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/385 | Earth Shifter | own `hit-skill`: Fissure (mechanical route implemented) — on-hit proc dispatcher<br>own `charged`: Volcano (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| unique/387 | Bonehew | own `charged`: Corpse Explosion (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `hit-skill`: Bone Spear (mechanical route implemented) — on-hit proc dispatcher |
| unique/389 | The Redeemer | own `skill`: Redemption (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Holy Bolt (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/392 | Rainbow Facet | own `death-skill`: Chain Lightning (mechanical route implemented) — on-death proc dispatcher |
| unique/393 | Rainbow Facet | own `death-skill`: Blizzard (mechanical route implemented) — on-death proc dispatcher |
| unique/394 | Rainbow Facet | own `death-skill`: Meteor (mechanical route implemented) — on-death proc dispatcher |
| unique/395 | Rainbow Facet | own `death-skill`: Poison Nova (mechanical route implemented) — on-death proc dispatcher |
| unique/396 | Rainbow Facet | own `levelup-skill`: Nova (mechanical route implemented) — on-level-up proc dispatcher |
| unique/397 | Rainbow Facet | own `levelup-skill`: Frost Nova (mechanical route implemented) — on-level-up proc dispatcher |
| unique/398 | Rainbow Facet | own `levelup-skill`: Blaze (mechanical route implemented) — on-level-up proc dispatcher |
| unique/399 | Rainbow Facet | own `levelup-skill`: Venom (mechanical route implemented) — on-level-up proc dispatcher |
| unique/408 | Ars Al'Diabolos | own `skilltab` — Warlock skill-tree rank bonuses<br>own `gethit-skill`: Terror (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `skill`: Apocalypse (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/409 | Ars Tor'Baalos | own `skilltab` — Warlock skill-tree rank bonuses<br>own `gethit-skill`: Decrepify (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `skill`: Demonic Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Blood Boil (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Engorge (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Consume (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/410 | Ars Dul'Mephistos | own `war` — Warlock class skill ranks<br>own `gethit-skill`: Blizzard (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/411 | Measured Wrath | own `war` — Warlock class skill ranks<br>own `gethit-skill`: Ring of Fire (mechanical route implemented) — on-taking-hit proc dispatcher<br>own `skill`: Summon Tainted (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Ring of Fire (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Flame Wave (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/412 | Dreadfang | own `hit-skill`: Amplify Damage (mechanical route implemented) — on-hit proc dispatcher<br>own `skill`: Mirrored Blades (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/413 | Wraithstep | own `skilltab` via skilltab-war — Warlock skill-tree rank bonuses<br>own `skilltab` via skilltab-war — Warlock skill-tree rank bonuses |
| unique/414 | Bloodpact Shard | own `skill`: Blood Oath (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Blood Boil (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Bind Demon (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| unique/415 | Sling | own `oskill`: Town Portal — Implement spell/skill: Town Portal; cross-class skill grants and casting |
| unique/416 | Opalvein | own `att-skill`: Flame Wave (mechanical route implemented) — on-attack proc dispatcher |
| unique/417 | Entropy Locket | own `hit-skill`: Miasma Chain (mechanical route implemented) — on-hit proc dispatcher |
| piece/71 | Immortal King's Soul Cage | own `gethit-skill`: Enchant (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/73 | Immortal King's Forge | own `gethit-skill`: Charged Bolt (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/78 | Tal Rasha's Lidless Eye | own `skill`: Fire Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Lightning Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer<br>own `skill`: Cold Mastery (mechanical route implemented) — Qualify direct +skill item grant for this class consumer |
| piece/85 | Trang-Oul's Guise | partial `oskill`: Fire Ball (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Fire Wall (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Meteor (mechanical route implemented) — cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery (mechanical route implemented) — cross-class skill grants and casting |
| piece/86 | Trang-Oul's Scales | partial `oskill`: Fire Ball (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Fire Wall (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Meteor (mechanical route implemented) — cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery (mechanical route implemented) — cross-class skill grants and casting |
| piece/87 | Trang-Oul's Wing | partial `oskill`: Fire Ball (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Fire Wall (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Meteor (mechanical route implemented) — cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery (mechanical route implemented) — cross-class skill grants and casting |
| piece/88 | Trang-Oul's Claws | partial `oskill`: Fire Ball (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Fire Wall (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Meteor (mechanical route implemented) — cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery (mechanical route implemented) — cross-class skill grants and casting |
| piece/89 | Trang-Oul's Girth | partial `oskill`: Fire Ball (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Fire Wall (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Meteor (mechanical route implemented) — cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery (mechanical route implemented) — cross-class skill grants and casting |
| piece/91 | M'avina's Embrace | own `gethit-skill`: Glacial Spike (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/94 | M'avina's Caster | own `magicarrow`: Magic Arrow (mechanical route implemented) — Inherent magic-arrow attack and projectile delivery<br>piece bonus `hit-skill`: Nova (mechanical route implemented) — on-hit proc dispatcher |
| piece/96 | Laying of Hands | own `hit-skill`: Holy Bolt (mechanical route implemented) — on-hit proc dispatcher |
| piece/98 | Dark Adherent | own `gethit-skill`: Nova (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/100 | Dangoon's Teaching | own `hit-skill`: Frost Nova (mechanical route implemented) — on-hit proc dispatcher |
| piece/103 | Ondal's Almighty | own `hit-skill`: Weaken (mechanical route implemented) — on-hit proc dispatcher |
| piece/109 | Hwanin's Refuge | own `gethit-skill`: Static Field (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/111 | Hwanin's Justice | own `hit-skill`: Ice Blast (mechanical route implemented) — on-hit proc dispatcher |
| piece/117 | Cow King's Horns | full `gethit-skill`: Static Field (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/118 | Cow King's Hide | own `gethit-skill`: Chain Lightning (mechanical route implemented) — on-taking-hit proc dispatcher<br>full `gethit-skill`: Static Field (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/119 | Cow King's Hooves | full `gethit-skill`: Static Field (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/120 | Naj's Puzzler | own `charged`: Teleport (mechanical route implemented) — charged-skill casting, targeting, charge ledger and recharge |
| piece/122 | Naj's Circlet | own `gethit-skill`: Chain Lightning (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/135 | Horazon's Countenance | own `war` — Warlock class skill ranks<br>full `war` — Warlock class skill ranks<br>full `oskill`: Enchant (mechanical route implemented) — cross-class skill grants and casting |
| piece/136 | Horazon's Dominion | own `skilltab` — Warlock skill-tree rank bonuses<br>full `war` — Warlock class skill ranks<br>full `oskill`: Enchant (mechanical route implemented) — cross-class skill grants and casting |
| piece/137 | Horazon's Hold | full `war` — Warlock class skill ranks<br>full `oskill`: Enchant (mechanical route implemented) — cross-class skill grants and casting |
| piece/138 | Horazon's Legacy | full `war` — Warlock class skill ranks<br>full `oskill`: Enchant (mechanical route implemented) — cross-class skill grants and casting |
| piece/139 | Horazon's Secrets | own `skilltab` — Warlock skill-tree rank bonuses<br>full `war` — Warlock class skill ranks<br>full `oskill`: Enchant (mechanical route implemented) — cross-class skill grants and casting |

## Source-inactive or recipe-only definitions

| ID | Item | Prerequisite spells/skills and systems |
| --- | --- | --- |
| unique/420 | Defender's Bile | own `gethit-skill`: Bone Armor (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/421 | Guardian's Thunder | own `gethit-skill`: Cyclone Armor (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/422 | Protector's Frost | own `gethit-skill`: Frozen Armor (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/423 | Defender's Fire | own `gethit-skill`: Blaze (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/424 | Protector's Stone | own `gethit-skill`: Fade (mechanical route implemented) — on-taking-hit proc dispatcher |
| unique/425 | Guardian's Light | own `gethit-skill`: Psychic Ward (mechanical route implemented) — on-taking-hit proc dispatcher |
| piece/127 | Warlord's Conquest | full `aura`: Meditation (mechanical route implemented) — equipped aura lifecycle, radius and stacking |
| piece/128 | Warlord's Lust | full `aura`: Meditation (mechanical route implemented) — equipped aura lifecycle, radius and stacking |
| piece/129 | Warlord's Mantle | full `aura`: Meditation (mechanical route implemented) — equipped aura lifecycle, radius and stacking |
| piece/130 | Warlord's Crushers | full `aura`: Meditation (mechanical route implemented) — equipped aura lifecycle, radius and stacking |
| piece/131 | Warlord's Authority | full `aura`: Meditation (mechanical route implemented) — equipped aura lifecycle, radius and stacking |

## Set-wide dependencies

| Set | Pieces withheld | Partial/full prerequisites |
| --- | --- | --- |
| Trang-Oul's Avatar | piece/85, piece/86, piece/87, piece/88, piece/89 | partial `oskill`: Fire Ball (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Fire Wall (mechanical route implemented) — cross-class skill grants and casting<br>partial `oskill`: Meteor (mechanical route implemented) — cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery (mechanical route implemented) — cross-class skill grants and casting |
| Cow King's Leathers | piece/117, piece/118, piece/119 | full `gethit-skill`: Static Field (mechanical route implemented) — on-taking-hit proc dispatcher |
| Warlord's Glory | piece/127, piece/128, piece/129, piece/130, piece/131 | full `aura`: Meditation (mechanical route implemented) — equipped aura lifecycle, radius and stacking |
| Horazon's Splendor | piece/135, piece/136, piece/137, piece/138, piece/139 | full `war` — Warlock class skill ranks<br>full `oskill`: Enchant (mechanical route implemented) — cross-class skill grants and casting |

## Other pieces held with their set

| ID | Item | Set ID |
| --- | --- | --- |
| piece/70 | Immortal King's Will | set/19 |
| piece/72 | Immortal King's Detail | set/19 |
| piece/74 | Immortal King's Pillar | set/19 |
| piece/75 | Immortal King's Stone Crusher | set/19 |
| piece/76 | Tal Rasha's Fine-Spun Cloth | set/20 |
| piece/77 | Tal Rasha's Adjudication | set/20 |
| piece/79 | Tal Rasha's Guardianship | set/20 |
| piece/80 | Tal Rasha's Horadric Crest | set/20 |
| piece/90 | M'avina's True Sight | set/23 |
| piece/92 | M'avina's Icy Clutch | set/23 |
| piece/93 | M'avina's Tenet | set/23 |
| piece/95 | Telling of Beads | set/24 |
| piece/97 | Rite of Passage | set/24 |
| piece/99 | Credendum | set/24 |
| piece/101 | Taebaek's Glory | set/25 |
| piece/102 | Haemosu's Adamant | set/25 |
| piece/108 | Hwanin's Splendor | set/27 |
| piece/110 | Hwanin's Blessing | set/27 |
| piece/121 | Naj's Light Plate | set/31 |

Before releasing a row, implement and test every listed skill and its delivery mechanism, then review all remaining runtime gates. Test real acquisition, saved rolls/charges, equip/unequip, broken/unidentified suppression, death, trading and save/reload. Release complete sets together. Source-disabled definitions stay disabled. Renewed Cold Rupture also has the separately tracked missing source property `Gelid-Affix5`; no effect has been invented.
