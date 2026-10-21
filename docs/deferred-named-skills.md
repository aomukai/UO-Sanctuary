# Deferred named-item spells and skills

User decision: withhold items whose spell/skill effects cannot work yet, and return to them when their prerequisites exist. Charms are excluded by project policy and have no return tasks. Keep their acquisition weight unchanged: unavailable drops yield nothing, and unavailable gambles refund the stake without rerolling.

Art decision (22 October 2026): custom imagegen assets may supply missing equipment art, class/skill icons and spell effects. For each returning skill or item, review existing art, record any required new assets, retain the prompt and source image in the project, then verify transparency, scale, directional frames/timing and the actual client resource mapping. Generation, client integration and gameplay verification are separate completion checks.

Pinned source: `fa1286949266498b294c40c2942521a7c0d7207a`. Reproduce with `python3 tools/build_named_skill_backlog.py`; verify with `--check`. The machine-readable companion is `data/Sanctuary/Balance/named-skill-backlog.json`.

Direct bonuses currently work for Teeth, Raise Skeleton, Skeleton Mastery and Teleport. A working spell alone does not supply a proc, aura, charge or cross-class grant system. Generic class/tree/all-skill and source-tagged fire-skill bonuses raise learned or directly granted skills without spending points or unlocking every skill. Item freeze, blind and flee are implemented shared effects; they do not require the full player class spells to exist.

169 source-spawnable item definitions have direct or set-wide spell/skill dependencies. 19 additional pieces are held so their sets remain completable. These counts describe skill dependencies, not total runtime availability; other source, appearance or combat-effect gates may also apply.

## Source-spawnable items to revisit

| ID | Item | Prerequisite spells/skills and systems |
| --- | --- | --- |
| unique/11 | Maelstrom | own `skill`: Corpse Explosion — Implement spell/skill: Corpse Explosion<br>own `skill`: Terror — Implement spell/skill: Terror<br>own `skill`: Amplify Damage — Implement spell/skill: Amplify Damage<br>own `skill`: Iron Maiden — Implement spell/skill: Iron Maiden |
| unique/13 | Ume's Lament | own `skill`: Terror — Implement spell/skill: Terror<br>own `skill`: Decrepify — Implement spell/skill: Decrepify |
| unique/16 | Rusthandle | own `skill`: Vengeance — Implement spell/skill: Vengeance<br>own `skill`: Thorns — Implement spell/skill: Thorns |
| unique/17 | Stormeye | own `skill`: Resist Lightning — Implement spell/skill: Resist Lightning<br>own `skill`: Holy Shock — Implement spell/skill: Holy Shock<br>own `skill`: Fist of the Heavens — Implement spell/skill: Fist of the Heavens |
| unique/20 | Bloodrise | own `skill`: Sacrifice — Implement spell/skill: Sacrifice |
| unique/35 | Kinemil's Awl | own `skill`: Holy Fire — Implement spell/skill: Holy Fire |
| unique/54 | Bane Ash | own `skill`: Fire Bolt — Implement spell/skill: Fire Bolt<br>own `skill`: Warmth — Implement spell/skill: Warmth |
| unique/56 | Spire of Lazarus | own `skill`: Chain Lightning — Implement spell/skill: Chain Lightning<br>own `skill`: Lightning — Implement spell/skill: Lightning<br>own `skill`: Static Field — Implement spell/skill: Static Field |
| unique/57 | The Salamander | own `skill`: Fire Wall — Implement spell/skill: Fire Wall<br>own `skill`: Fire Ball — Implement spell/skill: Fire Ball<br>own `skill`: Warmth — Implement spell/skill: Warmth |
| unique/58 | The Iron Jang Bong | own `skill`: Nova — Implement spell/skill: Nova<br>own `skill`: Blaze — Implement spell/skill: Blaze<br>own `skill`: Frost Nova — Implement spell/skill: Frost Nova |
| unique/60 | Witherstring | own `magicarrow`: Magic Arrow — Implement spell/skill: Magic Arrow; Inherent magic-arrow attack and projectile delivery |
| unique/61 | Raven Claw | own `explosivearrow`: Exploding Arrow — Implement spell/skill: Exploding Arrow; Inherent exploding-arrow attack and area damage |
| unique/62 | Rogue's Bow | own `skill` via roguesbow-affix1: Fire Arrow — Implement spell/skill: Fire Arrow<br>own `skill` via roguesbow-affix1: Cold Arrow — Implement spell/skill: Cold Arrow |
| unique/64 | Wizendraw | own `magicarrow`: Magic Arrow — Implement spell/skill: Magic Arrow; Inherent magic-arrow attack and projectile delivery |
| unique/66 | Blastbark | own `skill`: Exploding Arrow — Implement spell/skill: Exploding Arrow |
| unique/69 | Hellcast | own `explosivearrow`: Exploding Arrow — Implement spell/skill: Exploding Arrow; Inherent exploding-arrow attack and area damage |
| unique/108 | Gorefoot | own `skill`: Leap — Implement spell/skill: Leap |
| unique/111 | Tearhaunch | own `skill`: Vigor — Implement spell/skill: Vigor |
| unique/129 | Coldkill | own `hit-skill`: Ice Blast — Implement spell/skill: Ice Blast; on-hit proc dispatcher<br>own `gethit-skill`: Frost Nova — Implement spell/skill: Frost Nova; on-taking-hit proc dispatcher |
| unique/131 | Islestrike | own `skill`: Maul — Implement spell/skill: Maul<br>own `skill`: Fury — Implement spell/skill: Fury |
| unique/132 | Pompeii's Wrath | own `hit-skill`: Volcano — Implement spell/skill: Volcano; on-hit proc dispatcher |
| unique/133 | Guardian Naga | own `hit-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-hit proc dispatcher |
| unique/135 | Spellsteel | own `charged`: Teleport (spell implemented) — charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Decrepify — Implement spell/skill: Decrepify; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Holy Bolt — Implement spell/skill: Holy Bolt; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Firestorm — Implement spell/skill: Firestorm; charged-skill casting, targeting, charge ledger and recharge |
| unique/136 | Stormrider | own `hit-skill`: Chain Lightning — Implement spell/skill: Chain Lightning; on-hit proc dispatcher<br>own `hit-skill`: Charged Bolt — Implement spell/skill: Charged Bolt; on-hit proc dispatcher<br>own `gethit-skill`: Charged Bolt — Implement spell/skill: Charged Bolt; on-taking-hit proc dispatcher |
| unique/137 | Boneslayer Blade | own `charged`: Holy Bolt — Implement spell/skill: Holy Bolt; charged-skill casting, targeting, charge ledger and recharge<br>own `gethit-skill`: Holy Bolt — Implement spell/skill: Holy Bolt; on-taking-hit proc dispatcher |
| unique/141 | Arm of King Leoric | own `gethit-skill`: Bone Spirit — Implement spell/skill: Bone Spirit; on-taking-hit proc dispatcher<br>own `gethit-skill`: Bone Prison — Implement spell/skill: Bone Prison; on-taking-hit proc dispatcher<br>own `skill`: Terror — Implement spell/skill: Terror<br>own `skill`: Raise Skeletal Mage — Implement spell/skill: Raise Skeletal Mage |
| unique/142 | Blackhand Key | own `charged`: Grim Ward — Implement spell/skill: Grim Ward; charged-skill casting, targeting, charge ledger and recharge |
| unique/144 | Zakarum's Hand | own `hit-skill`: Blizzard — Implement spell/skill: Blizzard; on-hit proc dispatcher<br>own `skill`: Holy Freeze — Implement spell/skill: Holy Freeze<br>own `skill`: Holy Shock — Implement spell/skill: Holy Shock |
| unique/145 | The Fetid Sprinkler | own `hit-skill`: Decrepify — Implement spell/skill: Decrepify; on-hit proc dispatcher<br>own `hit-skill`: Confuse — Implement spell/skill: Confuse; on-hit proc dispatcher |
| unique/146 | Hand of Blessed Light | own `skill`: Holy Bolt — Implement spell/skill: Holy Bolt<br>own `skill`: Fist of the Heavens — Implement spell/skill: Fist of the Heavens<br>own `hit-skill`: Fist of the Heavens — Implement spell/skill: Fist of the Heavens; on-hit proc dispatcher |
| unique/148 | Sureshrill Frost | own `charged`: Frozen Orb — Implement spell/skill: Frozen Orb; charged-skill casting, targeting, charge ledger and recharge |
| unique/149 | Moonfall | own `hit-skill`: Meteor — Implement spell/skill: Meteor; on-hit proc dispatcher<br>own `charged`: Meteor — Implement spell/skill: Meteor; charged-skill casting, targeting, charge ledger and recharge |
| unique/150 | Baezil's Vortex | own `hit-skill`: Nova — Implement spell/skill: Nova; on-hit proc dispatcher<br>own `charged`: Nova — Implement spell/skill: Nova; charged-skill casting, targeting, charge ledger and recharge |
| unique/151 | Earthshaker | own `hit-skill`: Fissure — Implement spell/skill: Fissure; on-hit proc dispatcher |
| unique/152 | Bloodtree Stump | own `skill`: Mace Mastery — Implement spell/skill: Mace Mastery |
| unique/153 | The Gavel of Pain | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher<br>own `gethit-skill`: Iron Maiden — Implement spell/skill: Iron Maiden; on-taking-hit proc dispatcher<br>own `charged`: Amplify Damage — Implement spell/skill: Amplify Damage; charged-skill casting, targeting, charge ledger and recharge |
| unique/154 | Bloodletter | own `skill`: Blade Mastery — Implement spell/skill: Blade Mastery<br>own `skill`: Whirlwind — Implement spell/skill: Whirlwind |
| unique/156 | Hexfire | own `charged`: Hydra — Implement spell/skill: Hydra; charged-skill casting, targeting, charge ledger and recharge |
| unique/160 | Plague Bearer | own `hit-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-hit proc dispatcher<br>own `skill`: Rabies — Implement spell/skill: Rabies |
| unique/163 | Bing Sz Wang | own `hit-skill`: Frozen Orb — Implement spell/skill: Frozen Orb; on-hit proc dispatcher |
| unique/164 | The Vile Husk | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher |
| unique/165 | Cloudcrack | own `hit-skill`: Fist of the Heavens — Implement spell/skill: Fist of the Heavens; on-hit proc dispatcher |
| unique/166 | Todesfaelle Flamme | own `att-skill`: Fire Ball — Implement spell/skill: Fire Ball; on-attack proc dispatcher<br>own `charged`: Enchant — Implement spell/skill: Enchant; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Fire Wall — Implement spell/skill: Fire Wall; charged-skill casting, targeting, charge ledger and recharge |
| unique/169 | Heart Carver | own `skill`: Find Potion — Implement spell/skill: Find Potion<br>own `skill`: Find Item — Implement spell/skill: Find Item<br>own `skill`: Grim Ward — Implement spell/skill: Grim Ward |
| unique/170 | Blackbog's Sharp | own `skill`: Poison Dagger — Implement spell/skill: Poison Dagger<br>own `skill`: Poison Explosion — Implement spell/skill: Poison Explosion<br>own `skill`: Poison Nova — Implement spell/skill: Poison Nova |
| unique/171 | Stormspike | own `gethit-skill`: Charged Bolt — Implement spell/skill: Charged Bolt; on-taking-hit proc dispatcher |
| unique/172 | The Impaler | own `skill`: Impale — Implement spell/skill: Impale<br>own `skill`: Power Strike — Implement spell/skill: Power Strike |
| unique/178 | Blackleach Blade | own `hit-skill`: Weaken — Implement spell/skill: Weaken; on-hit proc dispatcher |
| unique/185 | Chromatic Ire | own `skill`: Fire Mastery — Implement spell/skill: Fire Mastery<br>own `skill`: Lightning Mastery — Implement spell/skill: Lightning Mastery<br>own `skill`: Cold Mastery — Implement spell/skill: Cold Mastery |
| unique/186 | Warpspear | own `skill`: Telekinesis — Implement spell/skill: Telekinesis<br>own `skill`: Energy Shield — Implement spell/skill: Energy Shield |
| unique/188 | Skystrike | own `hit-skill`: Meteor — Implement spell/skill: Meteor; on-hit proc dispatcher |
| unique/190 | Kuko Shakaku | own `skill`: Immolation Arrow — Implement spell/skill: Immolation Arrow<br>own `explosivearrow`: Exploding Arrow — Implement spell/skill: Exploding Arrow; Inherent exploding-arrow attack and area damage |
| unique/191 | Endlesshail | own `skill`: Strafe — Implement spell/skill: Strafe |
| unique/192 | Witchwild String | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher<br>own `magicarrow`: Magic Arrow — Implement spell/skill: Magic Arrow; Inherent magic-arrow attack and projectile delivery |
| unique/194 | Magewrath | own `skill`: Guided Arrow — Implement spell/skill: Guided Arrow |
| unique/195 | Goldstrike Arch | own `hit-skill`: Fist of the Heavens — Implement spell/skill: Fist of the Heavens; on-hit proc dispatcher |
| unique/197 | Pus Spitter | own `hit-skill`: Lower Resist — Implement spell/skill: Lower Resist; on-hit proc dispatcher<br>own `gethit-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-taking-hit proc dispatcher |
| unique/199 | Demon Machine | own `explosivearrow`: Exploding Arrow — Implement spell/skill: Exploding Arrow; Inherent exploding-arrow attack and area damage |
| unique/204 | Darksight Helm | own `gethit-skill`: Dim Vision — Implement spell/skill: Dim Vision; on-taking-hit proc dispatcher<br>own `charged`: Cloak of Shadows — Implement spell/skill: Cloak of Shadows; charged-skill casting, targeting, charge ledger and recharge |
| unique/222 | Corpsemourn | own `gethit-skill`: Iron Maiden — Implement spell/skill: Iron Maiden; on-taking-hit proc dispatcher<br>own `charged`: Corpse Explosion — Implement spell/skill: Corpse Explosion; charged-skill casting, targeting, charge ledger and recharge |
| unique/226 | Stormchaser | own `gethit-skill`: Blizzard — Implement spell/skill: Blizzard; on-taking-hit proc dispatcher<br>own `gethit-skill`: Tornado — Implement spell/skill: Tornado; on-taking-hit proc dispatcher |
| unique/227 | Tiamat's Rebuke | own `gethit-skill`: Frost Nova — Implement spell/skill: Frost Nova; on-taking-hit proc dispatcher<br>own `gethit-skill`: Nova — Implement spell/skill: Nova; on-taking-hit proc dispatcher<br>own `gethit-skill`: Hydra — Implement spell/skill: Hydra; on-taking-hit proc dispatcher |
| unique/229 | Radament's Sphere | own `gethit-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-taking-hit proc dispatcher<br>own `charged`: Poison Explosion — Implement spell/skill: Poison Explosion; charged-skill casting, targeting, charge ledger and recharge |
| unique/235 | Lava Gout | own `hit-skill`: Enchant — Implement spell/skill: Enchant; on-hit proc dispatcher |
| unique/236 | Hellmouth | own `hit-skill`: Meteor — Implement spell/skill: Meteor; on-hit proc dispatcher<br>own `hit-skill`: Firestorm — Implement spell/skill: Firestorm; on-hit proc dispatcher |
| unique/237 | Infernostride | own `gethit-skill`: Blaze — Implement spell/skill: Blaze; on-taking-hit proc dispatcher |
| unique/245 | Snowclash | own `gethit-skill`: Blizzard — Implement spell/skill: Blizzard; on-taking-hit proc dispatcher<br>own `skill`: Blizzard — Implement spell/skill: Blizzard<br>own `skill`: Glacial Spike — Implement spell/skill: Glacial Spike<br>own `skill`: Chilling Armor — Implement spell/skill: Chilling Armor |
| unique/246 | Thundergod's Vigor | own `gethit-skill`: Fist of the Heavens — Implement spell/skill: Fist of the Heavens; on-taking-hit proc dispatcher<br>own `skill`: Lightning Strike — Implement spell/skill: Lightning Strike<br>own `skill`: Lightning Fury — Implement spell/skill: Lightning Fury |
| unique/252 | Blackoak Shield | own `gethit-skill`: Weaken — Implement spell/skill: Weaken; on-taking-hit proc dispatcher |
| unique/254 | Hellslayer | own `att-skill`: Fire Ball — Implement spell/skill: Fire Ball; on-attack proc dispatcher |
| unique/257 | Schaefer's Hammer | own `hit-skill`: Static Field — Implement spell/skill: Static Field; on-hit proc dispatcher |
| unique/258 | The Cranium Basher | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher |
| unique/259 | Lightsabre | own `att-skill`: Chain Lightning — Implement spell/skill: Chain Lightning; on-attack proc dispatcher |
| unique/260 | Doombringer | own `hit-skill`: Weaken — Implement spell/skill: Weaken; on-hit proc dispatcher |
| unique/264 | Stormspire | own `gethit-skill`: Chain Lightning — Implement spell/skill: Chain Lightning; on-taking-hit proc dispatcher<br>own `gethit-skill`: Charged Bolt — Implement spell/skill: Charged Bolt; on-taking-hit proc dispatcher |
| unique/270 | The Rising Sun | own `gethit-skill`: Meteor — Implement spell/skill: Meteor; on-taking-hit proc dispatcher |
| unique/273 | Atma's Scarab | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher |
| unique/277 | Saracen's Chance | own `gethit-skill`: Iron Maiden — Implement spell/skill: Iron Maiden; on-taking-hit proc dispatcher |
| unique/284 | The Oculus | own `gethit-skill`: Teleport (spell implemented) — on-taking-hit proc dispatcher |
| unique/289 | Bloodmoon | own `charged`: Blood Golem — Implement spell/skill: Blood Golem; charged-skill casting, targeting, charge ledger and recharge |
| unique/292 | Warshrike | own `hit-skill`: Nova — Implement spell/skill: Nova; on-hit proc dispatcher |
| unique/296 | Demon Limb | own `charged`: Enchant — Implement spell/skill: Enchant; charged-skill casting, targeting, charge ledger and recharge |
| unique/298 | Tomb Reaver | own `reanimate` — On-kill reanimation, source monster template, ownership, AI and lifetime |
| unique/300 | Nature's Peace | own `charged`: Oak Sage — Implement spell/skill: Oak Sage; charged-skill casting, targeting, charge ledger and recharge |
| unique/301 | Azurewrath | own `aura`: Sanctuary — Implement spell/skill: Sanctuary; equipped aura lifecycle, radius and stacking |
| unique/306 | Horizon's Tornado | own `hit-skill`: Tornado — Implement spell/skill: Tornado; on-hit proc dispatcher |
| unique/310 | Cerebus' Bite | own `skill`: Feral Rage — Implement spell/skill: Feral Rage |
| unique/312 | Soul Drainer | own `hit-skill`: Weaken — Implement spell/skill: Weaken; on-hit proc dispatcher |
| unique/315 | Executioner's Justice | own `kill-skill`: Decrepify — Implement spell/skill: Decrepify; on-kill proc dispatcher |
| unique/319 | Wisp Projector | own `hit-skill`: Lightning — Implement spell/skill: Lightning; on-hit proc dispatcher<br>own `charged`: Oak Sage — Implement spell/skill: Oak Sage; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Heart of Wolverine — Implement spell/skill: Heart of Wolverine; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Spirit of Barbs — Implement spell/skill: Spirit of Barbs; charged-skill casting, targeting, charge ledger and recharge |
| unique/320 | Gargoyle's Bite | own `charged`: Plague Javelin — Implement spell/skill: Plague Javelin; charged-skill casting, targeting, charge ledger and recharge |
| unique/321 | Lacerator | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher |
| unique/323 | Viperfork | own `hit-skill`: Poison Explosion — Implement spell/skill: Poison Explosion; on-hit proc dispatcher |
| unique/326 | The Reaper's Toll | own `hit-skill`: Decrepify — Implement spell/skill: Decrepify; on-hit proc dispatcher |
| unique/328 | Hellrack | own `charged`: Immolation Arrow — Implement spell/skill: Immolation Arrow; charged-skill casting, targeting, charge ledger and recharge |
| unique/331 | Widowmaker | own `magicarrow`: Magic Arrow — Implement spell/skill: Magic Arrow; Inherent magic-arrow attack and projectile delivery<br>own `oskill`: Guided Arrow — Implement spell/skill: Guided Arrow; cross-class skill grants and casting |
| unique/332 | Blood Raven's Charge | own `explosivearrow`: Exploding Arrow — Implement spell/skill: Exploding Arrow; Inherent exploding-arrow attack and area damage<br>own `charged`: Revive — Implement spell/skill: Revive; charged-skill casting, targeting, charge ledger and recharge |
| unique/334 | Shadow Killer | own `hit-skill`: Frost Nova — Implement spell/skill: Frost Nova; on-hit proc dispatcher |
| unique/337 | Windhammer | own `hit-skill`: Twister — Implement spell/skill: Twister; on-hit proc dispatcher |
| unique/338 | Thunderstroke | own `hit-skill`: Lightning — Implement spell/skill: Lightning; on-hit proc dispatcher<br>own `skill`: Lightning Bolt — Implement spell/skill: Lightning Bolt |
| unique/341 | Boneflame | own `gethit-skill`: Terror — Implement spell/skill: Terror; on-taking-hit proc dispatcher |
| unique/345 | Andariel's Visage | own `gethit-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-taking-hit proc dispatcher<br>own `charged`: Venom — Implement spell/skill: Venom; charged-skill casting, targeting, charge ledger and recharge |
| unique/347 | Dragonscale | own `oskill`: Hydra — Implement spell/skill: Hydra; cross-class skill grants and casting |
| unique/348 | Steel Carapace | own `gethit-skill`: Iron Maiden — Implement spell/skill: Iron Maiden; on-taking-hit proc dispatcher |
| unique/349 | Medusa's Gaze | own `gethit-skill`: Lower Resist — Implement spell/skill: Lower Resist; on-taking-hit proc dispatcher<br>own `death-skill`: Nova — Implement spell/skill: Nova; on-death proc dispatcher |
| unique/350 | Ravenlore | own `skill`: Raven — Implement spell/skill: Raven |
| unique/351 | Boneshade | own `skill`: Bone Armor — Implement spell/skill: Bone Armor<br>own `skill`: Bone Spear — Implement spell/skill: Bone Spear<br>own `skill`: Bone Spirit — Implement spell/skill: Bone Spirit<br>own `skill`: Bone Wall — Implement spell/skill: Bone Wall |
| unique/353 | Flamebellow | own `hit-skill`: Firestorm — Implement spell/skill: Firestorm; on-hit proc dispatcher<br>own `oskill`: Inferno — Implement spell/skill: Inferno; cross-class skill grants and casting |
| unique/355 | Wolfhowl | own `oskill`: Werewolf — Implement spell/skill: Werewolf; cross-class skill grants and casting<br>own `charged`: Summon Dire Wolf — Implement spell/skill: Summon Dire Wolf; charged-skill casting, targeting, charge ledger and recharge<br>own `oskill`: Lycanthropy — Implement spell/skill: Lycanthropy; cross-class skill grants and casting<br>own `oskill`: Feral Rage — Implement spell/skill: Feral Rage; cross-class skill grants and casting |
| unique/356 | Spirit Ward | own `gethit-skill`: Fade — Implement spell/skill: Fade; on-taking-hit proc dispatcher |
| unique/358 | Ormus' Robes | own `skill-rand`: Fire Bolt, Warmth, Charged Bolt, Ice Bolt, Frozen Armor, Inferno, Static Field, Telekinesis, Frost Nova, Ice Blast, Blaze, Fire Ball, Nova, Lightning, Shiver Armor, Fire Wall, Enchant, Chain Lightning, Teleport (spell implemented), Glacial Spike, Meteor, Thunder Storm, Energy Shield, Blizzard, Chilling Armor — Random skill selection and saved skill identity; Implement remaining skills in the source ID range |
| unique/360 | Stormlash | own `hit-skill`: Static Field — Implement spell/skill: Static Field; on-hit proc dispatcher<br>own `hit-skill`: Tornado — Implement spell/skill: Tornado; on-hit proc dispatcher |
| unique/361 | Halaberd's Reign | own `skill`: Battle Orders — Implement spell/skill: Battle Orders<br>own `skill`: Battle Command — Implement spell/skill: Battle Command |
| unique/364 | Dracul's Grasp | own `hit-skill`: Life Tap — Implement spell/skill: Life Tap; on-hit proc dispatcher |
| unique/365 | Frostwind | own `oskill`: Arctic Blast — Implement spell/skill: Arctic Blast; cross-class skill grants and casting |
| unique/368 | Firelizard's Talons | own `skill`: Wake of Fire — Implement spell/skill: Wake of Fire<br>own `skill`: Wake of Inferno — Implement spell/skill: Wake of Inferno |
| unique/370 | Marrowwalk | own `charged`: Bone Prison — Implement spell/skill: Bone Prison; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Life Tap — Implement spell/skill: Life Tap; charged-skill casting, targeting, charge ledger and recharge |
| unique/373 | Arachnid Mesh | own `charged`: Venom — Implement spell/skill: Venom; charged-skill casting, targeting, charge ledger and recharge |
| unique/375 | Metalgrid | own `charged`: Iron Golem — Implement spell/skill: Iron Golem; charged-skill casting, targeting, charge ledger and recharge<br>own `charged`: Iron Maiden — Implement spell/skill: Iron Maiden; charged-skill casting, targeting, charge ledger and recharge |
| unique/378 | Carrion Wind | own `gethit-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-taking-hit proc dispatcher<br>own `charged`: Poison Creeper — Implement spell/skill: Poison Creeper; charged-skill casting, targeting, charge ledger and recharge<br>own `hit-skill`: Twister — Implement spell/skill: Twister; on-hit proc dispatcher |
| unique/383 | Cranebeak | own `charged`: Raven — Implement spell/skill: Raven; charged-skill casting, targeting, charge ledger and recharge |
| unique/384 | Nord's Tenderizer | own `charged`: Blizzard — Implement spell/skill: Blizzard; charged-skill casting, targeting, charge ledger and recharge |
| unique/385 | Earth Shifter | own `hit-skill`: Fissure — Implement spell/skill: Fissure; on-hit proc dispatcher<br>own `charged`: Volcano — Implement spell/skill: Volcano; charged-skill casting, targeting, charge ledger and recharge |
| unique/387 | Bonehew | own `charged`: Corpse Explosion — Implement spell/skill: Corpse Explosion; charged-skill casting, targeting, charge ledger and recharge<br>own `hit-skill`: Bone Spear — Implement spell/skill: Bone Spear; on-hit proc dispatcher |
| unique/389 | The Redeemer | own `skill`: Redemption — Implement spell/skill: Redemption<br>own `skill`: Holy Bolt — Implement spell/skill: Holy Bolt |
| unique/392 | Rainbow Facet | own `death-skill`: Chain Lightning — Implement spell/skill: Chain Lightning; on-death proc dispatcher |
| unique/393 | Rainbow Facet | own `death-skill`: Blizzard — Implement spell/skill: Blizzard; on-death proc dispatcher |
| unique/394 | Rainbow Facet | own `death-skill`: Meteor — Implement spell/skill: Meteor; on-death proc dispatcher |
| unique/395 | Rainbow Facet | own `death-skill`: Poison Nova — Implement spell/skill: Poison Nova; on-death proc dispatcher |
| unique/396 | Rainbow Facet | own `levelup-skill`: Nova — Implement spell/skill: Nova; on-level-up proc dispatcher |
| unique/397 | Rainbow Facet | own `levelup-skill`: Frost Nova — Implement spell/skill: Frost Nova; on-level-up proc dispatcher |
| unique/398 | Rainbow Facet | own `levelup-skill`: Blaze — Implement spell/skill: Blaze; on-level-up proc dispatcher |
| unique/399 | Rainbow Facet | own `levelup-skill`: Venom — Implement spell/skill: Venom; on-level-up proc dispatcher |
| unique/408 | Ars Al'Diabolos | own `skilltab` — Warlock skill-tree rank bonuses<br>own `gethit-skill`: Terror — Implement spell/skill: Terror; on-taking-hit proc dispatcher<br>own `skill`: Apocalypse — Implement spell/skill: Apocalypse |
| unique/409 | Ars Tor'Baalos | own `skilltab` — Warlock skill-tree rank bonuses<br>own `gethit-skill`: Decrepify — Implement spell/skill: Decrepify; on-taking-hit proc dispatcher<br>own `skill`: Demonic Mastery — Implement spell/skill: Demonic Mastery<br>own `skill`: Blood Boil — Implement spell/skill: Blood Boil<br>own `skill`: Engorge — Implement spell/skill: Engorge<br>own `skill`: Consume — Implement spell/skill: Consume |
| unique/410 | Ars Dul'Mephistos | own `war` — Warlock class skill ranks<br>own `gethit-skill`: Blizzard — Implement spell/skill: Blizzard; on-taking-hit proc dispatcher |
| unique/411 | Measured Wrath | own `war` — Warlock class skill ranks<br>own `gethit-skill`: Ring of Fire — Implement spell/skill: Ring of Fire; on-taking-hit proc dispatcher<br>own `skill`: Summon Tainted — Implement spell/skill: Summon Tainted<br>own `skill`: Ring of Fire — Implement spell/skill: Ring of Fire<br>own `skill`: Flame Wave — Implement spell/skill: Flame Wave |
| unique/412 | Dreadfang | own `hit-skill`: Amplify Damage — Implement spell/skill: Amplify Damage; on-hit proc dispatcher<br>own `skill`: Mirrored Blades — Implement spell/skill: Mirrored Blades |
| unique/413 | Wraithstep | own `skilltab` via skilltab-war — Warlock skill-tree rank bonuses<br>own `skilltab` via skilltab-war — Warlock skill-tree rank bonuses |
| unique/414 | Bloodpact Shard | own `skill`: Blood Oath — Implement spell/skill: Blood Oath<br>own `skill`: Blood Boil — Implement spell/skill: Blood Boil<br>own `skill`: Bind Demon — Implement spell/skill: Bind Demon |
| unique/415 | Sling | own `oskill`: Town Portal — Implement spell/skill: Town Portal; cross-class skill grants and casting |
| unique/416 | Opalvein | own `att-skill`: Flame Wave — Implement spell/skill: Flame Wave; on-attack proc dispatcher |
| unique/417 | Entropy Locket | own `hit-skill`: Miasma Chain — Implement spell/skill: Miasma Chain; on-hit proc dispatcher |
| piece/71 | Immortal King's Soul Cage | own `gethit-skill`: Enchant — Implement spell/skill: Enchant; on-taking-hit proc dispatcher |
| piece/73 | Immortal King's Forge | own `gethit-skill`: Charged Bolt — Implement spell/skill: Charged Bolt; on-taking-hit proc dispatcher |
| piece/78 | Tal Rasha's Lidless Eye | own `skill`: Fire Mastery — Implement spell/skill: Fire Mastery<br>own `skill`: Lightning Mastery — Implement spell/skill: Lightning Mastery<br>own `skill`: Cold Mastery — Implement spell/skill: Cold Mastery |
| piece/85 | Trang-Oul's Guise | partial `oskill`: Fire Ball — Implement spell/skill: Fire Ball; cross-class skill grants and casting<br>partial `oskill`: Fire Wall — Implement spell/skill: Fire Wall; cross-class skill grants and casting<br>partial `oskill`: Meteor — Implement spell/skill: Meteor; cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery — Implement spell/skill: Fire Mastery; cross-class skill grants and casting |
| piece/86 | Trang-Oul's Scales | partial `oskill`: Fire Ball — Implement spell/skill: Fire Ball; cross-class skill grants and casting<br>partial `oskill`: Fire Wall — Implement spell/skill: Fire Wall; cross-class skill grants and casting<br>partial `oskill`: Meteor — Implement spell/skill: Meteor; cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery — Implement spell/skill: Fire Mastery; cross-class skill grants and casting |
| piece/87 | Trang-Oul's Wing | partial `oskill`: Fire Ball — Implement spell/skill: Fire Ball; cross-class skill grants and casting<br>partial `oskill`: Fire Wall — Implement spell/skill: Fire Wall; cross-class skill grants and casting<br>partial `oskill`: Meteor — Implement spell/skill: Meteor; cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery — Implement spell/skill: Fire Mastery; cross-class skill grants and casting |
| piece/88 | Trang-Oul's Claws | partial `oskill`: Fire Ball — Implement spell/skill: Fire Ball; cross-class skill grants and casting<br>partial `oskill`: Fire Wall — Implement spell/skill: Fire Wall; cross-class skill grants and casting<br>partial `oskill`: Meteor — Implement spell/skill: Meteor; cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery — Implement spell/skill: Fire Mastery; cross-class skill grants and casting |
| piece/89 | Trang-Oul's Girth | partial `oskill`: Fire Ball — Implement spell/skill: Fire Ball; cross-class skill grants and casting<br>partial `oskill`: Fire Wall — Implement spell/skill: Fire Wall; cross-class skill grants and casting<br>partial `oskill`: Meteor — Implement spell/skill: Meteor; cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery — Implement spell/skill: Fire Mastery; cross-class skill grants and casting |
| piece/91 | M'avina's Embrace | own `gethit-skill`: Glacial Spike — Implement spell/skill: Glacial Spike; on-taking-hit proc dispatcher |
| piece/94 | M'avina's Caster | own `magicarrow`: Magic Arrow — Implement spell/skill: Magic Arrow; Inherent magic-arrow attack and projectile delivery<br>piece bonus `hit-skill`: Nova — Implement spell/skill: Nova; on-hit proc dispatcher |
| piece/96 | Laying of Hands | own `hit-skill`: Holy Bolt — Implement spell/skill: Holy Bolt; on-hit proc dispatcher |
| piece/98 | Dark Adherent | own `gethit-skill`: Nova — Implement spell/skill: Nova; on-taking-hit proc dispatcher |
| piece/100 | Dangoon's Teaching | own `hit-skill`: Frost Nova — Implement spell/skill: Frost Nova; on-hit proc dispatcher |
| piece/103 | Ondal's Almighty | own `hit-skill`: Weaken — Implement spell/skill: Weaken; on-hit proc dispatcher |
| piece/109 | Hwanin's Refuge | own `gethit-skill`: Static Field — Implement spell/skill: Static Field; on-taking-hit proc dispatcher |
| piece/111 | Hwanin's Justice | own `hit-skill`: Ice Blast — Implement spell/skill: Ice Blast; on-hit proc dispatcher |
| piece/117 | Cow King's Horns | full `gethit-skill`: Static Field — Implement spell/skill: Static Field; on-taking-hit proc dispatcher |
| piece/118 | Cow King's Hide | own `gethit-skill`: Chain Lightning — Implement spell/skill: Chain Lightning; on-taking-hit proc dispatcher<br>full `gethit-skill`: Static Field — Implement spell/skill: Static Field; on-taking-hit proc dispatcher |
| piece/119 | Cow King's Hooves | full `gethit-skill`: Static Field — Implement spell/skill: Static Field; on-taking-hit proc dispatcher |
| piece/120 | Naj's Puzzler | own `charged`: Teleport (spell implemented) — charged-skill casting, targeting, charge ledger and recharge |
| piece/122 | Naj's Circlet | own `gethit-skill`: Chain Lightning — Implement spell/skill: Chain Lightning; on-taking-hit proc dispatcher |
| piece/135 | Horazon's Countenance | own `war` — Warlock class skill ranks<br>full `war` — Warlock class skill ranks<br>full `oskill`: Enchant — Implement spell/skill: Enchant; cross-class skill grants and casting |
| piece/136 | Horazon's Dominion | own `skilltab` — Warlock skill-tree rank bonuses<br>full `war` — Warlock class skill ranks<br>full `oskill`: Enchant — Implement spell/skill: Enchant; cross-class skill grants and casting |
| piece/137 | Horazon's Hold | full `war` — Warlock class skill ranks<br>full `oskill`: Enchant — Implement spell/skill: Enchant; cross-class skill grants and casting |
| piece/138 | Horazon's Legacy | full `war` — Warlock class skill ranks<br>full `oskill`: Enchant — Implement spell/skill: Enchant; cross-class skill grants and casting |
| piece/139 | Horazon's Secrets | own `skilltab` — Warlock skill-tree rank bonuses<br>full `war` — Warlock class skill ranks<br>full `oskill`: Enchant — Implement spell/skill: Enchant; cross-class skill grants and casting |

## Source-inactive or recipe-only definitions

| ID | Item | Prerequisite spells/skills and systems |
| --- | --- | --- |
| unique/420 | Defender's Bile | own `gethit-skill`: Bone Armor — Implement spell/skill: Bone Armor; on-taking-hit proc dispatcher |
| unique/421 | Guardian's Thunder | own `gethit-skill`: Cyclone Armor — Implement spell/skill: Cyclone Armor; on-taking-hit proc dispatcher |
| unique/422 | Protector's Frost | own `gethit-skill`: Frozen Armor — Implement spell/skill: Frozen Armor; on-taking-hit proc dispatcher |
| unique/423 | Defender's Fire | own `gethit-skill`: Blaze — Implement spell/skill: Blaze; on-taking-hit proc dispatcher |
| unique/424 | Protector's Stone | own `gethit-skill`: Fade — Implement spell/skill: Fade; on-taking-hit proc dispatcher |
| unique/425 | Guardian's Light | own `gethit-skill`: Psychic Ward — Implement spell/skill: Psychic Ward; on-taking-hit proc dispatcher |
| piece/127 | Warlord's Conquest | full `aura`: Meditation — Implement spell/skill: Meditation; equipped aura lifecycle, radius and stacking |
| piece/128 | Warlord's Lust | full `aura`: Meditation — Implement spell/skill: Meditation; equipped aura lifecycle, radius and stacking |
| piece/129 | Warlord's Mantle | full `aura`: Meditation — Implement spell/skill: Meditation; equipped aura lifecycle, radius and stacking |
| piece/130 | Warlord's Crushers | full `aura`: Meditation — Implement spell/skill: Meditation; equipped aura lifecycle, radius and stacking |
| piece/131 | Warlord's Authority | full `aura`: Meditation — Implement spell/skill: Meditation; equipped aura lifecycle, radius and stacking |

## Set-wide dependencies

| Set | Pieces withheld | Partial/full prerequisites |
| --- | --- | --- |
| Trang-Oul's Avatar | piece/85, piece/86, piece/87, piece/88, piece/89 | partial `oskill`: Fire Ball — Implement spell/skill: Fire Ball; cross-class skill grants and casting<br>partial `oskill`: Fire Wall — Implement spell/skill: Fire Wall; cross-class skill grants and casting<br>partial `oskill`: Meteor — Implement spell/skill: Meteor; cross-class skill grants and casting<br>full `state` — Vampire transformation with movement, casting, equipment and restoration lifecycle<br>full `oskill`: Fire Mastery — Implement spell/skill: Fire Mastery; cross-class skill grants and casting |
| Cow King's Leathers | piece/117, piece/118, piece/119 | full `gethit-skill`: Static Field — Implement spell/skill: Static Field; on-taking-hit proc dispatcher |
| Warlord's Glory | piece/127, piece/128, piece/129, piece/130, piece/131 | full `aura`: Meditation — Implement spell/skill: Meditation; equipped aura lifecycle, radius and stacking |
| Horazon's Splendor | piece/135, piece/136, piece/137, piece/138, piece/139 | full `war` — Warlock class skill ranks<br>full `oskill`: Enchant — Implement spell/skill: Enchant; cross-class skill grants and casting |

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
