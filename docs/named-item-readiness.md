# Named-item runtime readiness and return list

Generated from the real native factory/eligibility fixture. This records staged acquisition eligibility; it does not certify deployment or every gameplay path. Every source definition appears in the runtime audit, and every deferred definition is listed below. Excluded charms remain in the source audit only. Rebuild with `python3 tools/build_named_readiness.py`; verify with `--check`.

Currently eligible: **245 Unique variants and 88 Set pieces**. **196 source-spawnable definitions remain held**; 29 other source records retain their source/quest/recipe restrictions. 15 charm definitions are intentionally excluded, with no implementation or class-return task.

Unavailable outcomes keep their original weight: empty drops or refunded gambles, without replacement rolls. A Set stays held until all its pieces and bonuses can work. The [spell/skill prerequisite list](deferred-named-skills.md) gives the exact source skills and delivery systems.

## Required revisit during class work

1. Implement the named spell/skill and its proc, aura, charge or cross-class delivery system; learning a spell alone does not complete these dependencies.
2. Review visual needs for every returning skill/item. Custom imagegen art is approved where needed; track icons, effects, directional frames, client resource integration and in-game verification.
3. Recheck every affected item and whole set below, including socketable jewels, dual wielding, transformations, source groups and non-spell effects.
4. Run the native named fixture, export a fresh runtime audit and regenerate both return lists. Stale input hashes fail verification.
5. Verify acquisition → identification → equip/bonuses → durability/socket changes → trade/wardrobe → death/recovery → save/reload before enabling the definition.

Class integration contract: consume `NamedSkills.Rank` and shared damage, defence, status and control APIs. New throwing/missile skills must spend quantity once at release and share the pierce budget; corpse skills must honor Rest in Peace. Recheck actual-life-loss damage-to-mana when Energy Shield or another mitigation layer is introduced.


## Dependency map

Counts overlap: one item may need several systems. Shared combat, maintenance and set visuals are implemented. The remaining work is grouped below; whole-set partner gates clear automatically when every piece and bonus works.

| Workstream | Held definitions | Remaining gates |
| --- | ---: | --- |
| Class skills and effect delivery | 168 | Implement each listed skill/effect and its proc, aura, charge or cross-class delivery. A rank bonus alone does not complete these paths. |
| Equipment, sockets and appearance | 21 | Rainbow Facet socket delivery; dual wielding; vampire form; held Warlock/quest gear representations. The item rows identify each dependency. |
| Source groups, restrictions and unresolved data | 13 | Saved random source-group choices, six quest-only acquisition paths, Warlock prerequisites and unresolved source data. Never invent missing definitions. |
| Whole-set dependency gate | 47 | These are dependent partner pieces, not separate mechanics. Release the whole set after all pieces and bonuses work. |

## Outstanding blocker inventory

A definition may have several blockers. These include shared item mechanics as well as class skills.

| Blocker | Definitions | Return requirement |
| --- | ---: | --- |
| Effect: att-skill | 4 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: aura | 1 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: charged | 30 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: death-skill | 5 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: explosivearrow | 5 | Implement the source exploding-arrow replacement attack, area targeting and damage rules. |
| Effect: gethit-skill | 37 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: hit-skill | 49 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: kill-skill | 1 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: levelup-skill | 4 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: magdam-rand | 1 | Audit the pinned property definition, arguments, units and all damage/skill consumers before enabling it. |
| Effect: magicarrow | 5 | Implement the source Magic Arrow replacement attack and its ammunition/damage rules. |
| Effect: oskill | 6 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: reanimate | 1 | Implement source monster template, on-kill ownership, AI, lifetime and persistence. |
| Effect: roguesbow-affix1 | 1 | Resolve the exact pinned property group and all argument ranges/delivery systems; never invent missing Gelid-Affix5. |
| Effect: skill | 43 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Effect: skill-rand | 1 | Persist the selected source skill and rank, then implement every possible skill effect. |
| Effect: skilltab-war | 1 | Implement the Warlock source tree, bindings and skill consumers. |
| Effect: war | 3 | Implement the source Warlock class and class skill bonuses. |
| No reviewed equipment appearance | 14 | Review representation, slot/activation rules and client art; jewels need their socket consumers too. |
| Quest-only base requires its quest acquisition path | 6 | Honor the stated class, quest, recipe or source-acquisition restriction; add its actual prerequisite before enabling it. |
| Set effect: gethit-skill | 3 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Set effect: oskill | 10 | Use the per-item skill list; implement both the skill and its exact trigger, aura, charges or granted-rank delivery. |
| Set effect: state | 5 | Implement the vampire transformation, class movement/casting/equipment rules and equip/break/unequip/save restoration; golden set auras already work. |
| Set effect: war | 5 | Implement the source Warlock class and class skill bonuses. |
| Set has unavailable pieces | 47 | Complete every held partner and bonus before enabling the whole set. |
| Set requires dual wielding | 2 | Implement both-hand equipment, attack/stat rules and save/wardrobe/death behavior. |
| Warlock class is not implemented | 5 | Honor the stated class, quest, recipe or source-acquisition restriction; add its actual prerequisite before enabling it. |

## Source-spawnable items to complete

| ID | Item | Remaining prerequisites |
| --- | --- | --- |
| piece/70 | Immortal King's Will | Set has unavailable pieces |
| piece/71 | Immortal King's Soul Cage | Effect: gethit-skill; Set has unavailable pieces |
| piece/72 | Immortal King's Detail | Set has unavailable pieces |
| piece/73 | Immortal King's Forge | Effect: gethit-skill; Set has unavailable pieces |
| piece/74 | Immortal King's Pillar | Set has unavailable pieces |
| piece/75 | Immortal King's Stone Crusher | Set has unavailable pieces |
| piece/76 | Tal Rasha's Fine-Spun Cloth | Set has unavailable pieces |
| piece/77 | Tal Rasha's Adjudication | Set has unavailable pieces |
| piece/78 | Tal Rasha's Lidless Eye | Effect: skill; Set has unavailable pieces |
| piece/79 | Tal Rasha's Guardianship | Set has unavailable pieces |
| piece/80 | Tal Rasha's Horadric Crest | Set has unavailable pieces |
| piece/85 | Trang-Oul's Guise | Set effect: oskill; Set effect: state; Set has unavailable pieces |
| piece/86 | Trang-Oul's Scales | Set effect: oskill; Set effect: state; Set has unavailable pieces |
| piece/87 | Trang-Oul's Wing | Set effect: oskill; Set effect: state; Set has unavailable pieces |
| piece/88 | Trang-Oul's Claws | Set effect: oskill; Set effect: state; Set has unavailable pieces |
| piece/89 | Trang-Oul's Girth | Set effect: oskill; Set effect: state; Set has unavailable pieces |
| piece/90 | M'avina's True Sight | Set has unavailable pieces |
| piece/91 | M'avina's Embrace | Effect: gethit-skill; Set has unavailable pieces |
| piece/92 | M'avina's Icy Clutch | Set has unavailable pieces |
| piece/93 | M'avina's Tenet | Set has unavailable pieces |
| piece/94 | M'avina's Caster | Effect: hit-skill; Effect: magicarrow; Set has unavailable pieces |
| piece/95 | Telling of Beads | Set has unavailable pieces |
| piece/96 | Laying of Hands | Effect: hit-skill; Set has unavailable pieces |
| piece/97 | Rite of Passage | Set has unavailable pieces |
| piece/98 | Dark Adherent | Effect: gethit-skill; Set has unavailable pieces |
| piece/99 | Credendum | Set has unavailable pieces |
| piece/100 | Dangoon's Teaching | Effect: hit-skill; Set has unavailable pieces |
| piece/101 | Taebaek's Glory | Set has unavailable pieces |
| piece/102 | Haemosu's Adamant | Set has unavailable pieces |
| piece/103 | Ondal's Almighty | Effect: hit-skill; Set has unavailable pieces |
| piece/108 | Hwanin's Splendor | Set has unavailable pieces |
| piece/109 | Hwanin's Refuge | Effect: gethit-skill; Set has unavailable pieces |
| piece/110 | Hwanin's Blessing | Set has unavailable pieces |
| piece/111 | Hwanin's Justice | Effect: hit-skill; Set has unavailable pieces |
| piece/115 | Bul-Kathos' Sacred Charge | Set requires dual wielding; Set has unavailable pieces |
| piece/116 | Bul-Kathos' Tribal Guardian | Set requires dual wielding; Set has unavailable pieces |
| piece/117 | Cow King's Horns | Set effect: gethit-skill; Set has unavailable pieces |
| piece/118 | Cow King's Hide | Effect: gethit-skill; Set effect: gethit-skill; Set has unavailable pieces |
| piece/119 | Cow King's Hooves | Set effect: gethit-skill; Set has unavailable pieces |
| piece/120 | Naj's Puzzler | Effect: charged; Set has unavailable pieces |
| piece/121 | Naj's Light Plate | Set has unavailable pieces |
| piece/122 | Naj's Circlet | Effect: gethit-skill; Set has unavailable pieces |
| piece/135 | Horazon's Countenance | Effect: war; Set effect: oskill; Set effect: war; Set has unavailable pieces |
| piece/136 | Horazon's Dominion | Set effect: oskill; Set effect: war; Set has unavailable pieces |
| piece/137 | Horazon's Hold | Set effect: oskill; Set effect: war; Set has unavailable pieces |
| piece/138 | Horazon's Legacy | Set effect: oskill; Set effect: war; Set has unavailable pieces |
| piece/139 | Horazon's Secrets | No reviewed equipment appearance; Set effect: oskill; Set effect: war; Warlock class is not implemented; Set has unavailable pieces |
| unique/11 | Maelstrom | Effect: skill |
| unique/13 | Ume's Lament | Effect: skill |
| unique/16 | Rusthandle | Effect: skill |
| unique/17 | Stormeye | Effect: skill |
| unique/20 | Bloodrise | Effect: skill |
| unique/35 | Kinemil's Awl | Effect: skill |
| unique/54 | Bane Ash | Effect: skill |
| unique/56 | Spire of Lazarus | Effect: skill |
| unique/57 | The Salamander | Effect: skill |
| unique/58 | The Iron Jang Bong | Effect: skill |
| unique/60 | Witherstring | Effect: magicarrow |
| unique/61 | Raven Claw | Effect: explosivearrow |
| unique/62 | Rogue's Bow | Effect: roguesbow-affix1 |
| unique/64 | Wizendraw | Effect: magicarrow |
| unique/66 | Blastbark | Effect: skill |
| unique/69 | Hellcast | Effect: explosivearrow |
| unique/108 | Gorefoot | Effect: skill |
| unique/111 | Tearhaunch | Effect: skill |
| unique/123 | Amulet of the Viper | No reviewed equipment appearance; Quest-only base requires its quest acquisition path |
| unique/124 | Staff of Kings | Quest-only base requires its quest acquisition path |
| unique/125 | Horadric Staff | Quest-only base requires its quest acquisition path |
| unique/126 | Hell Forge Hammer | Quest-only base requires its quest acquisition path |
| unique/127 | Khalim's Flail | Quest-only base requires its quest acquisition path |
| unique/128 | Khalim's Will | Quest-only base requires its quest acquisition path |
| unique/129 | Coldkill | Effect: gethit-skill; Effect: hit-skill |
| unique/131 | Islestrike | Effect: skill |
| unique/132 | Pompeii's Wrath | Effect: hit-skill |
| unique/133 | Guardian Naga | Effect: hit-skill |
| unique/135 | Spellsteel | Effect: charged |
| unique/136 | Stormrider | Effect: gethit-skill; Effect: hit-skill |
| unique/137 | Boneslayer Blade | Effect: charged; Effect: gethit-skill |
| unique/141 | Arm of King Leoric | Effect: gethit-skill; Effect: skill |
| unique/142 | Blackhand Key | Effect: charged |
| unique/144 | Zakarum's Hand | Effect: hit-skill; Effect: skill |
| unique/145 | The Fetid Sprinkler | Effect: hit-skill |
| unique/146 | Hand of Blessed Light | Effect: hit-skill; Effect: skill |
| unique/148 | Sureshrill Frost | Effect: charged |
| unique/149 | Moonfall | Effect: charged; Effect: hit-skill |
| unique/150 | Baezil's Vortex | Effect: charged; Effect: hit-skill |
| unique/151 | Earthshaker | Effect: hit-skill |
| unique/152 | Bloodtree Stump | Effect: skill |
| unique/153 | The Gavel of Pain | Effect: charged; Effect: gethit-skill; Effect: hit-skill |
| unique/154 | Bloodletter | Effect: skill |
| unique/156 | Hexfire | Effect: charged |
| unique/160 | Plague Bearer | Effect: hit-skill; Effect: skill |
| unique/163 | Bing Sz Wang | Effect: hit-skill |
| unique/164 | The Vile Husk | Effect: hit-skill |
| unique/165 | Cloudcrack | Effect: hit-skill |
| unique/166 | Todesfaelle Flamme | Effect: att-skill; Effect: charged |
| unique/169 | Heart Carver | Effect: skill |
| unique/170 | Blackbog's Sharp | Effect: skill |
| unique/171 | Stormspike | Effect: gethit-skill |
| unique/172 | The Impaler | Effect: skill |
| unique/178 | Blackleach Blade | Effect: hit-skill |
| unique/185 | Chromatic Ire | Effect: skill |
| unique/186 | Warpspear | Effect: skill |
| unique/188 | Skystrike | Effect: hit-skill |
| unique/190 | Kuko Shakaku | Effect: explosivearrow; Effect: skill |
| unique/191 | Endlesshail | Effect: skill |
| unique/192 | Witchwild String | Effect: hit-skill; Effect: magicarrow |
| unique/194 | Magewrath | Effect: skill |
| unique/195 | Goldstrike Arch | Effect: hit-skill |
| unique/197 | Pus Spitter | Effect: gethit-skill; Effect: hit-skill |
| unique/199 | Demon Machine | Effect: explosivearrow |
| unique/204 | Darksight Helm | Effect: charged; Effect: gethit-skill |
| unique/222 | Corpsemourn | Effect: charged; Effect: gethit-skill |
| unique/226 | Stormchaser | Effect: gethit-skill |
| unique/227 | Tiamat's Rebuke | Effect: gethit-skill |
| unique/229 | Radament's Sphere | Effect: charged; Effect: gethit-skill |
| unique/235 | Lava Gout | Effect: hit-skill |
| unique/236 | Hellmouth | Effect: hit-skill |
| unique/237 | Infernostride | Effect: gethit-skill |
| unique/245 | Snowclash | Effect: gethit-skill; Effect: skill |
| unique/246 | Thundergod's Vigor | Effect: gethit-skill; Effect: skill |
| unique/252 | Blackoak Shield | Effect: gethit-skill |
| unique/254 | Hellslayer | Effect: att-skill |
| unique/257 | Schaefer's Hammer | Effect: hit-skill |
| unique/258 | The Cranium Basher | Effect: hit-skill |
| unique/259 | Lightsabre | Effect: att-skill |
| unique/260 | Doombringer | Effect: hit-skill |
| unique/264 | Stormspire | Effect: gethit-skill |
| unique/270 | The Rising Sun | Effect: gethit-skill |
| unique/273 | Atma's Scarab | Effect: hit-skill |
| unique/277 | Saracen's Chance | Effect: gethit-skill |
| unique/284 | The Oculus | Effect: gethit-skill |
| unique/289 | Bloodmoon | Effect: charged |
| unique/292 | Warshrike | Effect: hit-skill |
| unique/296 | Demon Limb | Effect: charged |
| unique/298 | Tomb Reaver | Effect: reanimate |
| unique/300 | Nature's Peace | Effect: charged |
| unique/301 | Azurewrath | Effect: aura |
| unique/306 | Horizon's Tornado | Effect: hit-skill |
| unique/310 | Cerebus' Bite | Effect: skill |
| unique/312 | Soul Drainer | Effect: hit-skill |
| unique/315 | Executioner's Justice | Effect: kill-skill |
| unique/319 | Wisp Projector | Effect: charged; Effect: hit-skill |
| unique/320 | Gargoyle's Bite | Effect: charged |
| unique/321 | Lacerator | Effect: hit-skill |
| unique/323 | Viperfork | Effect: hit-skill |
| unique/326 | The Reaper's Toll | Effect: hit-skill |
| unique/328 | Hellrack | Effect: charged |
| unique/331 | Widowmaker | Effect: magicarrow; Effect: oskill |
| unique/332 | Blood Raven's Charge | Effect: charged; Effect: explosivearrow |
| unique/334 | Shadow Killer | Effect: hit-skill |
| unique/337 | Windhammer | Effect: hit-skill |
| unique/338 | Thunderstroke | Effect: hit-skill; Effect: skill |
| unique/341 | Boneflame | Effect: gethit-skill |
| unique/345 | Andariel's Visage | Effect: charged; Effect: gethit-skill |
| unique/347 | Dragonscale | Effect: oskill |
| unique/348 | Steel Carapace | Effect: gethit-skill |
| unique/349 | Medusa's Gaze | Effect: death-skill; Effect: gethit-skill |
| unique/350 | Ravenlore | Effect: skill |
| unique/351 | Boneshade | Effect: skill |
| unique/353 | Flamebellow | Effect: hit-skill; Effect: oskill |
| unique/355 | Wolfhowl | Effect: charged; Effect: oskill |
| unique/356 | Spirit Ward | Effect: gethit-skill |
| unique/358 | Ormus' Robes | Effect: skill-rand |
| unique/360 | Stormlash | Effect: hit-skill |
| unique/361 | Halaberd's Reign | Effect: skill |
| unique/364 | Dracul's Grasp | Effect: hit-skill |
| unique/365 | Frostwind | Effect: oskill |
| unique/368 | Firelizard's Talons | Effect: skill |
| unique/370 | Marrowwalk | Effect: charged |
| unique/373 | Arachnid Mesh | Effect: charged |
| unique/375 | Metalgrid | Effect: charged |
| unique/378 | Carrion Wind | Effect: charged; Effect: gethit-skill; Effect: hit-skill |
| unique/383 | Cranebeak | Effect: charged |
| unique/384 | Nord's Tenderizer | Effect: charged |
| unique/385 | Earth Shifter | Effect: charged; Effect: hit-skill |
| unique/387 | Bonehew | Effect: charged; Effect: hit-skill |
| unique/389 | The Redeemer | Effect: skill |
| unique/392 | Rainbow Facet | Effect: death-skill; No reviewed equipment appearance |
| unique/393 | Rainbow Facet | Effect: death-skill; No reviewed equipment appearance |
| unique/394 | Rainbow Facet | Effect: death-skill; No reviewed equipment appearance |
| unique/395 | Rainbow Facet | Effect: death-skill; No reviewed equipment appearance |
| unique/396 | Rainbow Facet | Effect: levelup-skill; No reviewed equipment appearance |
| unique/397 | Rainbow Facet | Effect: levelup-skill; No reviewed equipment appearance |
| unique/398 | Rainbow Facet | Effect: levelup-skill; No reviewed equipment appearance |
| unique/399 | Rainbow Facet | Effect: levelup-skill; No reviewed equipment appearance |
| unique/408 | Ars Al'Diabolos | Effect: gethit-skill; Effect: skill; No reviewed equipment appearance; Warlock class is not implemented |
| unique/409 | Ars Tor'Baalos | Effect: gethit-skill; Effect: skill; No reviewed equipment appearance; Warlock class is not implemented |
| unique/410 | Ars Dul'Mephistos | Effect: gethit-skill; Effect: war; No reviewed equipment appearance; Warlock class is not implemented |
| unique/411 | Measured Wrath | Effect: gethit-skill; Effect: skill; Effect: war; No reviewed equipment appearance; Warlock class is not implemented |
| unique/412 | Dreadfang | Effect: hit-skill; Effect: skill |
| unique/413 | Wraithstep | Effect: skilltab-war |
| unique/414 | Bloodpact Shard | Effect: skill |
| unique/415 | Sling | Effect: oskill |
| unique/416 | Opalvein | Effect: att-skill; Effect: magdam-rand |
| unique/417 | Entropy Locket | Effect: hit-skill |

## Restricted or inactive source records

| ID | Item | Remaining prerequisites |
| --- | --- | --- |
| piece/127 | Warlord's Conquest | Set effect: aura; Source definition is not available to random acquisition; Set has unavailable pieces |
| piece/128 | Warlord's Lust | Set effect: aura; Source definition is not available to random acquisition; Set has unavailable pieces |
| piece/129 | Warlord's Mantle | Set effect: aura; Source definition is not available to random acquisition; Set has unavailable pieces |
| piece/130 | Warlord's Crushers | Set effect: aura; Source definition is not available to random acquisition; Set has unavailable pieces |
| piece/131 | Warlord's Authority | Set effect: aura; Source definition is not available to random acquisition; Set has unavailable pieces |
| unique/29 | Azurewrath | Source definition is not available to random acquisition |
| unique/263 | Constricting Ring | Source definition is not available to random acquisition |
| unique/295 | Gore Ripper | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/303 | Zakarum's Salvation | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/305 | Odium | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/318 | Larzuk's Champion | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/339 | Giant Maimer | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/346 | Darkfear | Source definition is not available to random acquisition |
| unique/352 | Nethercrow | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/362 | Warriv's Warder | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/372 | Merman's Sprocket | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/377 | Siggard's Stealth | No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/420 | Defender's Bile | Effect: gethit-skill; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/421 | Guardian's Thunder | Effect: gethit-skill; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/422 | Protector's Frost | Effect: gethit-skill; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/423 | Defender's Fire | Effect: gethit-skill; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/424 | Protector's Stone | Effect: gethit-skill; Effect: pierce-dmg; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/425 | Guardian's Light | Effect: gethit-skill; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/427 | Renewed Cold Rupture | Effect: Gelid-Affix1; Effect: Gelid-Affix2; Effect: Gelid-Affix3; Effect: Gelid-Affix4; Effect: Gelid-Affix5; Effect: Gelid-Affix6; Effect: pierce-immunity-cold; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/433 | Renewed Flame Rift | Effect: Incendiary-Affix1; Effect: Incendiary-Affix2; Effect: Incendiary-Affix3; Effect: Incendiary-Affix4; Effect: Incendiary-Affix6; Effect: pierce-immunity-fire; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/434 | Renewed Crack of the Heavens | Effect: Magnetic-Affix1; Effect: Magnetic-Affix2; Effect: Magnetic-Affix3; Effect: Magnetic-Affix4; Effect: Magnetic-Affix6; Effect: pierce-immunity-light; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/435 | Renewed Rotting Fissure | Effect: Virulent-Affix1; Effect: Virulent-Affix2; Effect: Virulent-Affix3; Effect: Virulent-Affix4; Effect: Virulent-Affix6; Effect: pierce-immunity-poison; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/436 | Renewed Bone Break | Effect: Breaching-Affix1; Effect: Breaching-Affix2; Effect: Breaching-Affix3; Effect: Breaching-Affix4; Effect: Breaching-Affix6; Effect: pierce-immunity-damage; No reviewed equipment appearance; Source definition is not available to random acquisition |
| unique/437 | Renewed Black Cleft | Effect: Mystical-Affix1; Effect: Mystical-Affix2; Effect: Mystical-Affix3; Effect: Mystical-Affix4; Effect: Mystical-Affix6; Effect: pierce-immunity-magic; No reviewed equipment appearance; Source definition is not available to random acquisition |

## Eligible definitions

| ID | Item | Rarity |
| --- | --- | --- |
| piece/0 | Civerb's Ward | Set |
| piece/1 | Civerb's Icon | Set |
| piece/2 | Civerb's Cudgel | Set |
| piece/3 | Hsarus' Iron Heel | Set |
| piece/4 | Hsarus' Iron Fist | Set |
| piece/5 | Hsarus' Iron Stay | Set |
| piece/6 | Cleglaw's Tooth | Set |
| piece/7 | Cleglaw's Claw | Set |
| piece/8 | Cleglaw's Pincers | Set |
| piece/9 | Iratha's Collar | Set |
| piece/10 | Iratha's Cuff | Set |
| piece/11 | Iratha's Coil | Set |
| piece/12 | Iratha's Cord | Set |
| piece/13 | Isenhart's Lightbrand | Set |
| piece/14 | Isenhart's Parry | Set |
| piece/15 | Isenhart's Case | Set |
| piece/16 | Isenhart's Horns | Set |
| piece/17 | Vidala's Barb | Set |
| piece/18 | Vidala's Fetlock | Set |
| piece/19 | Vidala's Ambush | Set |
| piece/20 | Vidala's Snare | Set |
| piece/21 | Milabrega's Orb | Set |
| piece/22 | Milabrega's Rod | Set |
| piece/23 | Milabrega's Diadem | Set |
| piece/24 | Milabrega's Robe | Set |
| piece/25 | Cathan's Rule | Set |
| piece/26 | Cathan's Mesh | Set |
| piece/27 | Cathan's Visage | Set |
| piece/28 | Cathan's Sigil | Set |
| piece/29 | Cathan's Seal | Set |
| piece/30 | Tancred's Crowbill | Set |
| piece/31 | Tancred's Spine | Set |
| piece/32 | Tancred's Hobnails | Set |
| piece/33 | Tancred's Weird | Set |
| piece/34 | Tancred's Skull | Set |
| piece/35 | Sigon's Gage | Set |
| piece/36 | Sigon's Visor | Set |
| piece/37 | Sigon's Shelter | Set |
| piece/38 | Sigon's Sabot | Set |
| piece/39 | Sigon's Wrap | Set |
| piece/40 | Sigon's Guard | Set |
| piece/41 | Infernal Cranium | Set |
| piece/42 | Infernal Torch | Set |
| piece/43 | Infernal Sign | Set |
| piece/44 | Berserker's Headgear | Set |
| piece/45 | Berserker's Hauberk | Set |
| piece/46 | Berserker's Hatchet | Set |
| piece/47 | Death's Hand | Set |
| piece/48 | Death's Guard | Set |
| piece/49 | Death's Touch | Set |
| piece/50 | Angelic Sickle | Set |
| piece/51 | Angelic Mantle | Set |
| piece/52 | Angelic Halo | Set |
| piece/53 | Angelic Wings | Set |
| piece/54 | Arctic Horn | Set |
| piece/55 | Arctic Furs | Set |
| piece/56 | Arctic Binding | Set |
| piece/57 | Arctic Mitts | Set |
| piece/58 | Arcanna's Sign | Set |
| piece/59 | Arcanna's Deathwand | Set |
| piece/60 | Arcanna's Head | Set |
| piece/61 | Arcanna's Flesh | Set |
| piece/62 | Natalya's Totem | Set |
| piece/63 | Natalya's Mark | Set |
| piece/64 | Natalya's Shadow | Set |
| piece/65 | Natalya's Soul | Set |
| piece/66 | Aldur's Stony Gaze | Set |
| piece/67 | Aldur's Deception | Set |
| piece/68 | Aldur's Rhythm | Set |
| piece/69 | Aldur's Advance | Set |
| piece/81 | Griswold's Valor | Set |
| piece/82 | Griswold's Heart | Set |
| piece/83 | Griswold's Redemption | Set |
| piece/84 | Griswold's Honor | Set |
| piece/104 | Guillaume's Face | Set |
| piece/105 | Wilhelm's Pride | Set |
| piece/106 | Magnus' Skin | Set |
| piece/107 | Whitstan's Guard | Set |
| piece/112 | Sazabi's Cobalt Redeemer | Set |
| piece/113 | Sazabi's Ghost Liberator | Set |
| piece/114 | Sazabi's Mental Sheath | Set |
| piece/123 | Sander's Paragon | Set |
| piece/124 | Sander's Riprap | Set |
| piece/125 | Sander's Taboo | Set |
| piece/126 | Sander's Superstition | Set |
| piece/132 | Bane's Oathmaker | Set |
| piece/133 | Bane's Wraithskin | Set |
| piece/134 | Bane's Authority | Set |
| unique/0 | The Gnasher | Unique |
| unique/1 | Deathspade | Unique |
| unique/2 | Bladebone | Unique |
| unique/3 | Skull Splitter | Unique |
| unique/4 | Rakescar | Unique |
| unique/5 | Axe of Fechmar | Unique |
| unique/6 | Goreshovel | Unique |
| unique/7 | The Chieftain | Unique |
| unique/8 | Brainhew | Unique |
| unique/9 | Humongous | Unique |
| unique/10 | Torch of Iro | Unique |
| unique/12 | Gravenspine | Unique |
| unique/14 | Felloak | Unique |
| unique/15 | Knell Striker | Unique |
| unique/18 | Stoutnail | Unique |
| unique/19 | Crushflange | Unique |
| unique/21 | The General's Tan Do Li Ga | Unique |
| unique/22 | Ironstone | Unique |
| unique/23 | Bonesnap | Unique |
| unique/24 | Steeldriver | Unique |
| unique/25 | Rixot's Keen | Unique |
| unique/26 | Blood Crescent | Unique |
| unique/27 | Skewer of Krintiz | Unique |
| unique/28 | Gleamscythe | Unique |
| unique/30 | Griswold's Edge | Unique |
| unique/31 | Hellplague | Unique |
| unique/32 | Culwen's Point | Unique |
| unique/33 | Shadowfang | Unique |
| unique/34 | Soulflay | Unique |
| unique/36 | Blacktongue | Unique |
| unique/37 | Ripsaw | Unique |
| unique/38 | The Patriarch | Unique |
| unique/39 | Gull | Unique |
| unique/40 | The Diggler | Unique |
| unique/41 | The Jade Tan Do | Unique |
| unique/42 | Spectral Shard | Unique |
| unique/43 | The Dragon Chang | Unique |
| unique/44 | Razortine | Unique |
| unique/45 | Bloodthief | Unique |
| unique/46 | Lance of Yaggai | Unique |
| unique/47 | The Tannr Gorerod | Unique |
| unique/48 | Dimoak's Hew | Unique |
| unique/49 | Steelgoad | Unique |
| unique/50 | Soul Harvest | Unique |
| unique/51 | The Battlebranch | Unique |
| unique/52 | Woestave | Unique |
| unique/53 | The Grim Reaper | Unique |
| unique/55 | Serpent Lord | Unique |
| unique/59 | Pluckeye | Unique |
| unique/63 | Stormstrike | Unique |
| unique/65 | Hellclap | Unique |
| unique/67 | Leadcrow | Unique |
| unique/68 | Ichorsting | Unique |
| unique/70 | Doomslinger | Unique |
| unique/71 | Biggin's Bonnet | Unique |
| unique/72 | Tarnhelm | Unique |
| unique/73 | Coif of Glory | Unique |
| unique/74 | Duskdeep | Unique |
| unique/75 | Wormskull | Unique |
| unique/76 | Howltusk | Unique |
| unique/77 | Undead Crown | Unique |
| unique/78 | The Face of Horror | Unique |
| unique/79 | Greyform | Unique |
| unique/80 | Blinkbat's Form | Unique |
| unique/81 | The Centurion | Unique |
| unique/82 | Twitchthroe | Unique |
| unique/83 | Darkglow | Unique |
| unique/84 | Hawkmail | Unique |
| unique/85 | Sparking Mail | Unique |
| unique/86 | Venom Ward | Unique |
| unique/87 | Iceblink | Unique |
| unique/88 | Boneflesh | Unique |
| unique/89 | Rockfleece | Unique |
| unique/90 | Rattlecage | Unique |
| unique/91 | Goldskin | Unique |
| unique/92 | Silks of the Victor | Unique |
| unique/93 | Heavenly Garb | Unique |
| unique/94 | Pelta Lunata | Unique |
| unique/95 | Umbral Disk | Unique |
| unique/96 | Stormguild | Unique |
| unique/97 | Wall of the Eyeless | Unique |
| unique/98 | Swordback Hold | Unique |
| unique/99 | Steelclash | Unique |
| unique/100 | Bverrit Keep | Unique |
| unique/101 | The Ward | Unique |
| unique/102 | The Hand of Broc | Unique |
| unique/103 | Bloodfist | Unique |
| unique/104 | Chance Guards | Unique |
| unique/105 | Magefist | Unique |
| unique/106 | Frostburn | Unique |
| unique/107 | Hotspur | Unique |
| unique/109 | Treads of Cthon | Unique |
| unique/110 | Goblin Toe | Unique |
| unique/112 | Lenymo | Unique |
| unique/113 | Snakecord | Unique |
| unique/114 | Nightsmoke | Unique |
| unique/115 | Goldwrap | Unique |
| unique/116 | Bladebuckle | Unique |
| unique/117 | Nokozan Relic | Unique |
| unique/118 | The Eye of Etlich | Unique |
| unique/119 | The Mahim-Oak Curio | Unique |
| unique/120 | Nagelring | Unique |
| unique/121 | Manald Heal | Unique |
| unique/122 | The Stone of Jordan | Unique |
| unique/130 | Butcher's Pupil | Unique |
| unique/134 | Warlord's Trust | Unique |
| unique/138 | The Minotaur | Unique |
| unique/139 | Suicide Branch | Unique |
| unique/140 | Carin Shard | Unique |
| unique/143 | Dark Clan Crusher | Unique |
| unique/147 | Fleshrender | Unique |
| unique/155 | Coldsteel Eye | Unique |
| unique/157 | Blade of Ali Baba | Unique |
| unique/158 | Ginther's Rift | Unique |
| unique/159 | Headstriker | Unique |
| unique/161 | The Atlantean | Unique |
| unique/162 | Crainte Vomir | Unique |
| unique/167 | Swordguard | Unique |
| unique/168 | Spineripper | Unique |
| unique/173 | Kelpie Snare | Unique |
| unique/174 | Soulfeast Tine | Unique |
| unique/175 | Hone Sundan | Unique |
| unique/176 | Spire of Honor | Unique |
| unique/177 | The Meat Scraper | Unique |
| unique/179 | Athena's Wrath | Unique |
| unique/180 | Pierre Tombale Couant | Unique |
| unique/181 | Husoldal Evo | Unique |
| unique/182 | Grim's Burning Dead | Unique |
| unique/183 | Razorswitch | Unique |
| unique/184 | Ribcracker | Unique |
| unique/187 | Skull Collector | Unique |
| unique/189 | Riphook | Unique |
| unique/193 | Cliffkiller | Unique |
| unique/196 | Langer Briser | Unique |
| unique/198 | Buriza-Do Kyanon | Unique |
| unique/201 | Peasant Crown | Unique |
| unique/202 | Rockstopper | Unique |
| unique/203 | Stealskull | Unique |
| unique/205 | Valkyrie Wing | Unique |
| unique/206 | Crown of Thieves | Unique |
| unique/207 | Blackhorn's Face | Unique |
| unique/208 | Vampire Gaze | Unique |
| unique/209 | The Spirit Shroud | Unique |
| unique/210 | Skin of the Vipermagi | Unique |
| unique/211 | Skin of the Flayed One | Unique |
| unique/212 | Iron Pelt | Unique |
| unique/213 | Spirit Forge | Unique |
| unique/214 | Crow Caw | Unique |
| unique/215 | Shaftstop | Unique |
| unique/216 | Duriel's Shell | Unique |
| unique/217 | Skullder's Ire | Unique |
| unique/218 | Guardian Angel | Unique |
| unique/219 | Toothrow | Unique |
| unique/220 | Atma's Wail | Unique |
| unique/221 | Black Hades | Unique |
| unique/223 | Que-Hegan's Wisdom | Unique |
| unique/224 | Visceratuant | Unique |
| unique/225 | Moser's Blessed Circle | Unique |
| unique/228 | Gerke's Sanctuary | Unique |
| unique/230 | Lidless Wall | Unique |
| unique/231 | Lance Guard | Unique |
| unique/232 | Venom Grip | Unique |
| unique/233 | Gravepalm | Unique |
| unique/234 | Ghoulhide | Unique |
| unique/238 | Waterwalk | Unique |
| unique/239 | Silkweave | Unique |
| unique/240 | War Traveler | Unique |
| unique/241 | Gore Rider | Unique |
| unique/242 | String of Ears | Unique |
| unique/243 | Razortail | Unique |
| unique/244 | Gloom's Trap | Unique |
| unique/248 | Harlequin Crest | Unique |
| unique/249 | Veil of Steel | Unique |
| unique/250 | The Gladiator's Bane | Unique |
| unique/251 | Arkaine's Valor | Unique |
| unique/253 | Stormshield | Unique |
| unique/255 | Messerschmidt's Reaver | Unique |
| unique/256 | Baranar's Star | Unique |
| unique/261 | The Grandfather | Unique |
| unique/262 | Wizardspike | Unique |
| unique/265 | Eaglehorn | Unique |
| unique/266 | Windforce | Unique |
| unique/268 | Bul-Kathos' Wedding Band | Unique |
| unique/269 | The Cat's Eye | Unique |
| unique/271 | Crescent Moon | Unique |
| unique/272 | Mara's Kaleidoscope | Unique |
| unique/274 | Dwarf Star | Unique |
| unique/275 | Raven Frost | Unique |
| unique/276 | Highlord's Wrath | Unique |
| unique/279 | Arreat's Face | Unique |
| unique/280 | Homunculus | Unique |
| unique/281 | Titan's Revenge | Unique |
| unique/282 | Lycander's Aim | Unique |
| unique/283 | Lycander's Flank | Unique |
| unique/285 | Herald of Zakarum | Unique |
| unique/286 | Bartuc's Cut-Throat | Unique |
| unique/287 | Jalal's Mane | Unique |
| unique/288 | The Scalper | Unique |
| unique/290 | Djinn Slayer | Unique |
| unique/291 | Deathbit | Unique |
| unique/293 | Gut Siphon | Unique |
| unique/294 | Razor's Edge | Unique |
| unique/297 | Steel Shade | Unique |
| unique/299 | Death's Web | Unique |
| unique/302 | Seraph's Hymn | Unique |
| unique/304 | Fleshripper | Unique |
| unique/307 | Stone Crusher | Unique |
| unique/308 | Jade Talon | Unique |
| unique/309 | Shadow Dancer | Unique |
| unique/311 | Tyrael's Might | Unique |
| unique/313 | Rune Master | Unique |
| unique/314 | Death Cleaver | Unique |
| unique/316 | Stoneraven | Unique |
| unique/317 | Leviathan | Unique |
| unique/322 | Mang Song's Lesson | Unique |
| unique/324 | Ethereal Edge | Unique |
| unique/325 | Demonhorn's Edge | Unique |
| unique/327 | Spirit Keeper | Unique |
| unique/329 | Alma Negra | Unique |
| unique/330 | Darkforce Spawn | Unique |
| unique/333 | Ghostflame | Unique |
| unique/335 | Gimmershred | Unique |
| unique/336 | Griffon's Eye | Unique |
| unique/340 | Demon's Arch | Unique |
| unique/342 | Steel Pillar | Unique |
| unique/343 | Nightwing's Veil | Unique |
| unique/344 | Crown of Ages | Unique |
| unique/354 | Death's Fathom | Unique |
| unique/357 | Kira's Guardian | Unique |
| unique/363 | Spike Thorn | Unique |
| unique/366 | Templar's Might | Unique |
| unique/367 | Eschuta's Temper | Unique |
| unique/369 | Sandstorm Trek | Unique |
| unique/371 | Heaven's Light | Unique |
| unique/374 | Nosferatu's Coil | Unique |
| unique/376 | Verdungo's Hearty Cord | Unique |
| unique/379 | Giant Skull | Unique |
| unique/380 | Astreon's Iron Ward | Unique |
| unique/382 | Arioc's Needle | Unique |
| unique/386 | Wraith Flight | Unique |
| unique/388 | Ondal's Wisdom | Unique |
| unique/390 | Head Hunter's Glory | Unique |
| unique/391 | Steelrend | Unique |
| unique/418 | Gheed's Wager | Unique |
| unique/419 | Hellwarden's Will | Unique |
