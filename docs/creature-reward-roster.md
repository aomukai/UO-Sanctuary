# Creature reward roster

18 October 2026. Generated review view of the authored `creature-reward-rules.json` assignments and resolved `creature-rewards.json` values. All 730 common creature types have an explicit policy: **324 reward-bearing types and 406 exclusions**, including the four project types. [Runtime contract, sources and verification](creature-experience.md).

Values are base players-1 XP before player-level penalties. Normal / Nightmare / Hell columns are `level : XP`. Existing populations remain Normal; [persistent Nightmare/Hell variants](t19-difficulties.md) are available for explicit spawning, with world placement deferred. Counterparts and level adjustments are project balance choices, not a claim of identical abilities. Named advanced ordinary encounters have authored levels but no act-boss, unique or champion XP multiplier. Champion/peerless encounters are explicitly reserved below.

| UO types | D2 source ID | Normal | Nightmare | Hell | Reason |
| --- | --- | ---: | ---: | ---: | --- |
| GraveyardZombie, Zombie | `zombie1` | 1 : 33 | 36 : 2,696 | 67 : 28,069 | Slow early corpse walker. |
| GraveyardSkeleton, Skeleton | `skeleton1` | 2 : 34 | 37 : 2,333 | 68 : 23,878 | Early bone warrior; project skeleton already uses level 2. |
| BoneKnight, SkeletalKnight | `skeleton4` | 13 : 130 | 43 : 3,502 | 74 : 30,809 | Armoured skeletal melee warrior. |
| BoneMagi, SkeletalMage | `skmage_fire2` | 10 : 67 | 40 : 3,701 | 70 : 33,891 | Ranged elemental skeletal caster. |
| Cursed, Ghoul | `zombie3` | 12 : 152 | 43 : 4,327 | 73 : 36,631 | Faster, tougher corpse walker. |
| RottingCorpse | `zombie4` | 22 : 395 | 50 : 6,948 | 80 : 46,620 | Durable advanced corpse walker. |
| PatchworkSkeleton | `reanimatedhorde1` | 31 : 903 | 61 : 13,314 | 81 : 48,046 | Heavy reanimated warrior. |
| Mummy | `mummy2` | 15 : 161 | 44 : 4,409 | 79 : 43,041 | Durable desert undead. |
| UnfrozenMummy | `mummy5` | 32 : 671 | 62 : 13,442 | 85 : 51,195 | Advanced spellcasting mummy. |
| RestlessSoul | `wraith1` | 7 : 83 | 38 : 3,085 | 75 : 39,485 | Lesser immaterial undead. |
| Bogle, Shade, Spectre, Wraith | `wraith3` | 19 : 251 | 48 : 6,072 | 79 : 45,193 | Dangerous immaterial undead. |
| WailingBanshee | `wraith5` | 30 : 612 | 59 : 11,850 | 85 : 53,754 | Advanced spirit with a disruptive ranged attack. |
| KhaldunSummoner, Lich | `unraveler1` | 15 : 252 | 44 : 13,227 | 79 : 129,123 | Undead caster and corpse support. |
| LichLord | `unraveler4` | 23 : 542 | 52 : 22,518 | 84 : 149,508 | Stronger undead caster. |
| AncientLich | `unraveler5` | 40 : 2,736 | 64 : 45,312 | 85 : 153,585 | Late Normal undead caster. |
| GraveyardLich | `unraveler1` | 5 : 200 | 36 : 7,704 | 69 : 88,353 | Existing level-5 tutorial caster; retain its authored 200 XP comparison reward. |
| BoneDemon | `doomknight2` | 27 : 650 | 57 : 14,061 | 84 : 69,770 | Heavy advanced undead warrior. |
| KhaldunRevenant, ShadowKnight | `doomknight1` | 24 : 487 | 56 : 9,477 | 82 : 47,118 | Armoured undead/dark warrior. |
| SpectralArmour | `doomknight2` | 27 : 650 | 57 : 14,061 | 84 : 69,770 | Animated armoured undead. |
| CorruptedSoul, WandererOfTheVoid | `wraith5` | 28 : 535 | 57 : 10,546 | 85 : 53,754 | Dangerous spirit. |
| PestilentBandage | `swarm3` | 21 : 201 | 48 : 3,469 | 79 : 25,824 | Small pestilential nuisance; no boss premium. |
| GrimmochDrummel | `sk_archer5` | 18 : 223 | 47 : 5,131 | 78 : 39,597 | Named undead archer, ordinary advanced-monster reward. |
| LysanderGathenwale, TavaraSewel | `skmage_fire4` | 17 : 123 | 47 : 5,942 | 78 : 45,850 | Named undead caster, ordinary advanced-monster reward. |
| KhaldunZealot, MorgBergen | `skeleton5` | 14 : 143 | 44 : 3,747 | 78 : 35,429 | Advanced undead warrior. |
| GreaterMongbat, Mongbat | `foulcrow1` | 4 : 22 | 37 : 1,098 | 68 : 11,236 | Small flying harrier. |
| ShadowFiend, StrongMongbat, VampireBat | `foulcrow2` | 6 : 29 | 38 : 1,175 | 69 : 11,780 | Stronger flying harrier. |
| Gibberling, HordeMinion | `fallen1` | 1 : 18 | 36 : 1,669 | 67 : 17,376 | Weak, numerous melee minion. |
| Orc | `goatman2` | 6 : 72 | 38 : 2,645 | 69 : 26,505 | Early tribal melee fighter. |
| OrcCaptain, OrcishLord, SpawnedOrcishLord | `goatman5` | 10 : 109 | 40 : 3,028 | 70 : 27,729 | Veteran tribal melee fighter. |
| OrcBomber | `slinger1` | 14 : 113 | 43 : 3,296 | 75 : 30,084 | Ranged projectile specialist. |
| OrcishMage | `fallenshaman3` | 9 : 89 | 40 : 5,047 | 70 : 46,215 | Tribal support caster. |
| OrcBrute | `blunderbore3` | 25 : 554 | 55 : 11,624 | 83 : 63,020 | Heavy tribal brute. |
| Ratman | `fetish1` | 10 : 96 | 42 : 2,889 | 72 : 25,146 | Small fast tribal fighter. |
| RatmanArcher | `fetishblow1` | 9 : 71 | 39 : 2,829 | 72 : 30,175 | Small ranged tribal fighter. |
| RatmanMage | `fetishshaman1` | 11 : 100 | 39 : 8,174 | 72 : 87,172 | Small tribal caster. |
| Brigand, ElfBrigand | `corruptrogue2` | 5 : 54 | 38 : 2,792 | 68 : 26,687 | Early armed humanoid. |
| Executioner | `cr_lancer4` | 16 : 186 | 47 : 4,591 | 78 : 35,429 | Advanced armed humanoid. |
| Savage | `fetish3` | 22 : 286 | 50 : 4,963 | 80 : 33,300 | Fast tribal melee fighter. |
| SavageRider | `pantherwoman4` | 23 : 279 | 52 : 6,004 | 83 : 38,781 | Mounted tribal skirmisher. |
| SavageShaman | `fetishshaman3` | 23 : 303 | 50 : 17,206 | 81 : 118,973 | Advanced tribal caster. |
| EvilMage, GolemController, Spellbinder | `cantor1` | 12 : 147 | 40 : 4,374 | 70 : 40,053 | Humanoid spellcaster. |
| EvilMageLord | `cantor3` | 24 : 465 | 53 : 10,342 | 82 : 61,253 | Advanced humanoid spellcaster. |
| HeadlessOne | `brute1` | 4 : 72 | 38 : 3,526 | 70 : 36,972 | Early shambling brute. |
| Ettin | `brute3` | 9 : 133 | 40 : 4,038 | 70 : 36,972 | Mid-early heavy brute. |
| Ogre | `brute4` | 12 : 168 | 41 : 4,320 | 72 : 40,233 | Large slow brute. |
| Troll | `brute4` | 14 : 202 | 43 : 4,945 | 74 : 43,495 | Regenerating melee brute. |
| FrostTroll | `snowyeti1` | 19 : 540 | 40 : 3,701 | 67 : 29,406 | Cold-adapted melee brute. |
| OgreLord | `blunderbore3` | 25 : 554 | 55 : 11,624 | 83 : 63,020 | Advanced heavy melee brute. |
| ArcticOgreLord | `frozenhorror1` | 30 : 1,594 | 54 : 10,122 | 75 : 45,126 | Heavy cold brute. |
| Cyclops, Moloch | `blunderbore2` | 20 : 342 | 49 : 8,041 | 80 : 57,720 | Large heavy melee brute. |
| Titan | `overseer3` | 28 : 754 | 53 : 9,945 | 74 : 45,307 | Large magical bruiser. |
| Troglodyte | `brute4` | 19 : 327 | 48 : 6,939 | 79 : 51,649 | Cave-dwelling brute. |
| Minotaur, TormentedMinotaur | `bloodlord1` | 33 : 1,939 | 61 : 26,628 | 82 : 98,947 | Frenzied heavy melee demon analogue. |
| MinotaurScout | `bloodlord1` | 30 : 1,581 | 58 : 22,358 | 79 : 90,386 | Agile minotaur skirmisher. |
| MinotaurCaptain | `bloodlord3` | 36 : 2,574 | 61 : 26,628 | 82 : 98,947 | Veteran frenzied heavy melee fighter. |
| ChaosDragoon, JukaWarrior | `zealot1` | 20 : 300 | 50 : 5,294 | 80 : 35,520 | Organised armed warrior. |
| JukaMage | `cantor2` | 23 : 422 | 51 : 9,204 | 81 : 59,486 | Organised support caster. |
| ChaosDragoonElite, JukaLord | `overseer3` | 30 : 863 | 55 : 11,177 | 76 : 48,705 | Heavy veteran commander. |
| MeerWarrior | `zealot1` | 20 : 300 | 50 : 5,294 | 80 : 35,520 | Armed warrior; friendly alignment remains native. |
| MeerMage | `cantor2` | 23 : 422 | 51 : 9,204 | 81 : 59,486 | Caster; friendly alignment remains native. |
| MeerCaptain | `zealot3` | 24 : 439 | 53 : 6,364 | 82 : 37,694 | Veteran warrior; friendly alignment remains native. |
| MeerEternal | `cantor3` | 30 : 698 | 59 : 14,671 | 85 : 66,553 | Veteran caster; friendly alignment remains native. |
| Ronin | `cr_lancer5` | 24 : 395 | 53 : 6,762 | 84 : 42,360 | Advanced armed humanoid. |
| EliteNinja | `cr_lancer5` | 28 : 518 | 57 : 8,537 | 85 : 43,515 | Agile advanced armed humanoid. |
| FanDancer | `succubus1` | 32 : 913 | 61 : 11,412 | 82 : 42,406 | Fast melee/caster humanoid. |
| YomotsuWarrior | `zealot3` | 24 : 439 | 53 : 6,364 | 82 : 37,694 | Advanced tribal fighter. |
| YomotsuPriest | `cantor3` | 24 : 465 | 53 : 10,342 | 82 : 61,253 | Advanced tribal caster. |
| YomotsuElder | `overseer3` | 36 : 1,296 | 61 : 15,850 | 82 : 58,897 | Heavy tribal commander. |
| Imp | `fallen4` | 10 : 72 | 41 : 2,340 | 71 : 20,909 | Small dangerous demon. |
| ArcaneDaemon, ChaosDaemon, Daemon | `megademon1` | 24 : 566 | 55 : 11,624 | 82 : 61,253 | Large offensive demon. |
| Balron | `megademon3` | 35 : 1,406 | 62 : 17,474 | 85 : 66,553 | High-tier winged fire demon. |
| IceFiend | `frozenhorror3` | 39 : 3,102 | 61 : 15,216 | 84 : 59,803 | Large cold demon. |
| Succubus | `succubus3` | 35 : 1,211 | 62 : 12,097 | 83 : 43,629 | Dangerous caster demon. |
| AbysmalHorror | `doomknight3` | 38 : 1,484 | 66 : 35,523 | 85 : 107,509 | Late Normal curse/spell demon. |
| DemonKnight | `doomknight3` | 40 : 1,699 | 68 : 39,328 | 85 : 107,509 | Heavy advanced dark caster/warrior. |
| Ravager | `bloodlord4` | 40 : 3,774 | 63 : 29,922 | 84 : 104,655 | Extremely strong ordinary melee monster. |
| FleshRenderer, Impaler | `bloodlord2` | 35 : 2,340 | 61 : 26,628 | 82 : 98,947 | Strong aggressive melee monster. |
| FleshGolem | `blunderbore4` | 32 : 1,072 | 57 : 13,057 | 85 : 66,553 | Large durable construct. |
| GoreFiend | `vampire4` | 30 : 744 | 60 : 17,946 | 85 : 76,792 | Blood-themed advanced caster/melee monster. |
| Devourer | `regurgitator2` | 27 : 807 | 57 : 14,061 | 84 : 69,770 | Large corpse-consuming monster. |
| DarknightCreeper | `deathmauler2` | 35 : 1,535 | 59 : 11,850 | 81 : 48,046 | Large stalking melee monster. |
| MoundOfMaggots | `swarm3` | 21 : 201 | 48 : 3,469 | 79 : 25,824 | Small pest swarm. |
| GazerLarva | `bighead3` | 5 : 56 | 37 : 3,431 | 69 : 36,813 | Small ranged grotesque. |
| Gazer | `bighead1` | 12 : 113 | 43 : 5,151 | 73 : 43,608 | Ranged magical grotesque. |
| ElderGazer | `bighead5` | 27 : 473 | 57 : 12,555 | 84 : 62,295 | Advanced ranged magical grotesque. |
| EnslavedGargoyle, Gargoyle, StoneGargoyle | `clawviper2` | 15 : 145 | 45 : 4,718 | 76 : 38,964 | Tough humanoid monster; analogue is a mobile armored demon. |
| FireGargoyle | `megademon1` | 24 : 566 | 55 : 11,624 | 82 : 61,253 | Fire-oriented humanoid demon. |
| GargoyleDestroyer, GargoyleEnforcer | `blunderbore3` | 25 : 554 | 55 : 11,624 | 83 : 63,020 | Heavy armoured humanoid demon. |
| AirElemental, CrystalWisp, DarkWisp, ShadowWisp, Wisp | `willowisp1` | 21 : 244 | 50 : 7,941 | 80 : 53,280 | Mobile elemental/spirit attacker. |
| Efreet, FireElemental | `vampire2` | 23 : 438 | 54 : 12,652 | 82 : 70,677 | Fire-oriented ranged elemental analogue. |
| CorporealBrume, WaterElemental | `frogdemon1` | 21 : 363 | 49 : 5,567 | 79 : 38,736 | Ranged liquid elemental analogue. |
| EarthElemental | `thornhulk1` | 21 : 405 | 49 : 8,041 | 79 : 55,953 | Slow sturdy elemental brute. |
| SnowElemental | `snowyeti1` | 25 : 930 | 46 : 5,556 | 67 : 29,406 | Cold sturdy elemental brute. |
| IceElemental | `frozenhorror1` | 37 : 2,562 | 61 : 15,216 | 82 : 56,541 | Advanced cold elemental. |
| AcidElemental, FetidEssence | `regurgitator1` | 24 : 605 | 56 : 13,267 | 82 : 65,965 | Caustic elemental analogue. |
| BloodElemental | `vampire4` | 30 : 744 | 60 : 17,946 | 85 : 76,792 | Durable advanced blood elemental. |
| PoisonElemental | `clawviper5` | 32 : 588 | 62 : 13,442 | 85 : 51,195 | Deadly poison elemental analogue. |
| CrystalElemental, CrystalVortex | `frozenhorror2` | 32 : 1,879 | 55 : 10,730 | 77 : 48,387 | Advanced crystalline elemental. |
| CopperElemental, DullCopperElemental | `thornhulk1` | 23 : 490 | 51 : 9,204 | 81 : 59,486 | Ore-armoured sturdy elemental. |
| BronzeElemental, GoldenElemental, ShadowIronElemental | `thornhulk3` | 26 : 618 | 55 : 11,624 | 84 : 64,786 | Stronger ore-armoured elemental. |
| AgapiteElemental, VeriteElemental | `thornhulk4` | 30 : 876 | 60 : 15,553 | 85 : 66,553 | High-tier ore-armoured elemental. |
| ValoriteElemental | `thornhulk4` | 34 : 1,149 | 64 : 19,635 | 85 : 66,553 | Highest-tier ore-armoured elemental. |
| Drake | `megademon1` | 24 : 566 | 55 : 11,624 | 82 : 61,253 | Smaller fire-breathing dragon: Balrog-tier Megademon. |
| Dragon, SerpentineDragon | `megademon3` | 30 : 1,001 | 57 : 13,057 | 84 : 64,786 | Fire-breathing dragon: Venom Lord-tier Megademon. |
| GreaterDragon | `megademon3` | 36 : 1,504 | 63 : 18,523 | 85 : 66,553 | Stronger fire dragon, late Normal tier. |
| AncientWyrm | `megademon3` | 40 : 1,971 | 67 : 23,168 | 85 : 66,553 | Ancient fire dragon, end-Normal tier. |
| ShadowWyrm | `megademon3` | 43 : 2,415 | 68 : 24,346 | 85 : 66,553 | Top ordinary dragon tier; no act-boss multiplier. |
| WhiteWyrm | `frozenhorror3` | 36 : 2,534 | 58 : 12,776 | 81 : 54,910 | Cold dragon: Frozen Horror analogue. |
| SkeletalDragon | `doomknight3` | 38 : 1,484 | 66 : 35,523 | 85 : 107,509 | Undead dragon: heavy undead caster analogue. |
| Wyvern | `clawviper4` | 22 : 283 | 51 : 7,080 | 82 : 47,118 | Poisonous reptilian predator. |
| Reptalon | `megademon2` | 29 : 862 | 58 : 13,841 | 85 : 66,553 | Advanced reptile with elemental attacks. |
| SeaSerpent | `tentaclehead1` | 18 : 213 | 48 : 7,517 | 75 : 48,886 | Aquatic predator; reward follows the head, not spawned tentacles. |
| CrystalSeaSerpent, DeepSeaSerpent | `tentaclehead3` | 28 : 495 | 59 : 14,671 | 85 : 66,553 | Large advanced aquatic predator. |
| Kraken | `tentaclehead3` | 32 : 649 | 63 : 18,523 | 85 : 66,553 | Heavy aquatic predator. |
| Leviathan | `tentaclehead3` | 36 : 851 | 67 : 23,168 | 85 : 66,553 | Top ordinary aquatic predator; no act-boss multiplier. |
| Hydra | `clawviper5` | 32 : 588 | 62 : 13,442 | 85 : 51,195 | Large multi-headed reptilian predator. |
| Lizardman | `goatman3` | 7 : 81 | 38 : 2,645 | 75 : 33,844 | Early tribal reptile fighter. |
| OphidianWarrior | `clawviper3` | 18 : 193 | 46 : 5,051 | 77 : 40,323 | Advanced reptilian fighter. |
| OphidianKnight | `clawviper4` | 22 : 283 | 51 : 7,080 | 82 : 47,118 | Veteran reptilian fighter. |
| OphidianMage | `skmage_pois3` | 20 : 168 | 50 : 7,279 | 85 : 56,314 | Poison-themed reptilian caster. |
| OphidianArchmage | `skmage_pois4` | 25 : 258 | 55 : 9,836 | 85 : 56,314 | Veteran reptilian caster. |
| OphidianMatriarch | `clawviper5` | 28 : 449 | 58 : 10,647 | 85 : 51,195 | Large reptilian leader. |
| FrostSpider, GiantSpider | `arach1` | 11 : 148 | 42 : 4,237 | 73 : 38,375 | Web/poison spider. |
| GiantBlackWidow | `arach3` | 21 : 372 | 49 : 6,804 | 78 : 45,850 | Stronger venomous spider. |
| DreadSpider | `arach5` | 23 : 454 | 53 : 8,751 | 84 : 54,819 | High-tier venomous spider. |
| Scorpion | `scarab1` | 8 : 125 | 37 : 3,431 | 69 : 36,813 | Armoured poisonous arthropod. |
| TerathanDrone | `sandmaggot1` | 12 : 163 | 41 : 4,500 | 72 : 41,910 | Insectoid worker/fighter. |
| TerathanWarrior | `sandmaggot3` | 19 : 316 | 48 : 7,228 | 79 : 53,801 | Heavy insectoid warrior. |
| TerathanAvenger | `scarab5` | 24 : 548 | 54 : 10,543 | 85 : 63,993 | Advanced armoured insectoid. |
| TerathanMatriarch | `maggotqueen5` | 29 : 942 | 50 : 12,243 | 85 : 94,710 | Dangerous insectoid leader; ordinary-monster table only. |
| BlackSolenWorker, RedSolenWorker | `sandmaggot1` | 12 : 163 | 41 : 4,500 | 72 : 41,910 | Insectoid worker. |
| BlackSolenInfiltratorWarrior, BlackSolenWarrior, RedSolenInfiltratorWarrior, RedSolenWarrior | `sandmaggot3` | 19 : 316 | 48 : 7,228 | 79 : 53,801 | Insectoid warrior. |
| BlackSolenInfiltratorQueen, BlackSolenQueen, RedSolenInfiltratorQueen, RedSolenQueen | `maggotqueen4` | 21 : 442 | 49 : 11,444 | 84 : 92,196 | Insectoid queen; ordinary-monster table, not Coldworm superunique. |
| AntLion | `sandmaggot3` | 19 : 316 | 48 : 7,228 | 79 : 53,801 | Large burrowing insect. |
| DeathwatchBeetleHatchling | `maggotbaby2` | 17 : 123 | 45 : 1,415 | 84 : 14,950 | Small beetle brood. |
| DeathwatchBeetle | `scarab3` | 17 : 281 | 45 : 5,897 | 84 : 62,295 | Armoured adult beetle. |
| RuneBeetle | `scarab5` | 28 : 720 | 58 : 13,308 | 85 : 63,993 | Advanced magical beetle. |
| SkitteringHopper | `sandleaper1` | 14 : 158 | 43 : 3,914 | 75 : 35,724 | Small agile leaping insect. |
| GiantIceWorm | `sandmaggot4` | 21 : 386 | 50 : 8,272 | 79 : 53,801 | Large cold burrowing worm. |
| Reaper, Treefellow | `thornhulk1` | 21 : 405 | 49 : 8,041 | 79 : 55,953 | Large animated plant. |
| FeralTreefellow | `thornhulk4` | 27 : 715 | 57 : 13,057 | 84 : 64,786 | Aggressive advanced animated tree. |
| Bogling | `vilechild1` | 7 : 68 | 39 : 1,257 | 67 : 10,693 | Small swamp plant. |
| BogThing, Quagmire | `thornhulk2` | 22 : 446 | 50 : 8,603 | 80 : 57,720 | Large swamp plant brute. |
| Corpser | `tentaclehead1` | 10 : 103 | 40 : 4,374 | 67 : 34,752 | Rooted plant ambusher. |
| SwampTentacle, WhippingVine | `tentacle1` | 15 : 199 | 45 : 3,538 | 72 : 25,146 | Plant tendril attacker. |
| FrostOoze, Jwilson, Slime | `swarm1` | 5 : 42 | 36 : 1,540 | 67 : 16,039 | Small amorphous nuisance. |
| CorrosiveSlime | `swarm4` | 18 : 151 | 48 : 3,469 | 77 : 24,193 | Advanced caustic nuisance. |
| PlagueBeast | `vilemother2` | 27 : 828 | 57 : 13,057 | 84 : 64,786 | Large diseased spawn-producing brute. |
| PlagueBeastLord | `vilemother3` | 30 : 1,107 | 57 : 13,057 | 85 : 66,553 | Stronger diseased spawn-producing brute. |
| PlagueSpawn | `vilechild2` | 27 : 441 | 57 : 4,017 | 84 : 19,934 | Spawned lesser plague creature; low child reward. |
| Golem | `blunderbore1` | 18 : 282 | 47 : 7,022 | 78 : 54,186 | Slow durable construct. |
| Juggernaut | `siegebeast1` | 37 : 3,039 | 59 : 29,343 | 81 : 118,973 | Large siege construct. |
| ExodusMinion | `minion3` | 35 : 1,720 | 60 : 9,571 | 80 : 35,520 | Advanced constructed fighter. |
| Betrayer, ExodusOverseer | `overseer4` | 37 : 1,429 | 62 : 16,802 | 83 : 60,596 | Heavy advanced construct. |
| SandVortex | `willowisp1` | 21 : 244 | 50 : 7,941 | 80 : 53,280 | Roaming hostile elemental. |
| AnimatedWeapon | `flyingscimitar` | 12 : 116 | 45 : 6,605 | 78 : 58,354 | Wild animated weapon; summoned instances are excluded at death. |
| Centaur | `cr_archer5` | 24 : 399 | 54 : 8,435 | 85 : 51,195 | Mobile ranged/melee fighter. |
| Harpy | `vulture2` | 15 : 154 | 44 : 3,527 | 76 : 31,171 | Winged melee attacker. |
| StoneHarpy | `vulture4` | 23 : 331 | 50 : 5,294 | 81 : 36,607 | Armoured winged attacker. |
| Pixie | `willowisp1` | 15 : 137 | 44 : 5,290 | 74 : 43,495 | Small ranged magical creature. |
| EtherealWarrior | `zealot3` | 28 : 576 | 57 : 8,035 | 85 : 40,956 | Advanced magical warrior. |
| InsaneDryad, MLDryad | `cantor2` | 23 : 422 | 51 : 9,204 | 81 : 59,486 | Plant-associated caster. |
| EnslavedSatyr, Satyr | `goatman4` | 19 : 245 | 48 : 5,204 | 79 : 38,736 | Musical humanoid melee/caster analogue. |
| Changeling, Doppleganger | `zealot2` | 22 : 363 | 51 : 5,664 | 81 : 36,607 | Shapeshifting humanoid; no reward copied from its disguise. |
| CrystalDaemon | `megademon2` | 27 : 753 | 56 : 12,320 | 83 : 63,020 | Advanced crystalline demon. |
| CrystalLatticeSeeker | `scarab5` | 30 : 823 | 60 : 14,955 | 85 : 63,993 | Armoured crystalline hunter. |
| Protector | `doomknight1` | 30 : 731 | 62 : 13,442 | 85 : 51,195 | Armoured crystalline guardian. |
| MantraEffervescence | `willowisp4` | 28 : 472 | 58 : 12,776 | 85 : 61,434 | Advanced ranged crystal spirit. |
| LadyOfTheSnow | `frozenhorror1` | 30 : 1,594 | 54 : 10,122 | 75 : 45,126 | Cold caster spirit. |
| Oni | `blunderbore3` | 30 : 777 | 60 : 15,553 | 85 : 66,553 | Large aggressive demon. |
| Kappa | `frogdemon1` | 16 : 225 | 44 : 3,968 | 74 : 32,621 | Aquatic leaping demon. |
| KazeKemono | `sandleaper4` | 22 : 341 | 52 : 7,130 | 80 : 42,180 | Agile magical predator. |
| RaiJu | `willowisp2` | 25 : 347 | 55 : 10,730 | 83 : 58,172 | Lightning-oriented magical predator. |
| TsukiWolf | `bloodlord1` | 33 : 1,939 | 61 : 26,628 | 82 : 98,947 | Large aggressive wolf-demon. |
| Yamandon | `clawviper5` | 36 : 772 | 66 : 16,916 | 85 : 51,195 | Large high-tier venomous reptile. |
| BakeKitsune | `succubuswitch1` | 28 : 702 | 56 : 13,741 | 77 : 58,468 | Magical trickster/caster predator. |
| RevenantLion | `reanimatedhorde2` | 33 : 1,090 | 62 : 14,114 | 82 : 49,473 | Strong undead predator. |
| HellCat, HellHound, PredatorHellCat | `vampire1` | 18 : 272 | 48 : 8,674 | 79 : 64,561 | Fire-oriented hunting beast. |
| GiantRat | `quillrat1` | 1 : 21 | 36 : 1,797 | 67 : 18,713 | Early hostile rodent. |
| IceSnake, LavaSnake, Snake | `quillrat1` | 3 : 35 | 38 : 2,057 | 69 : 20,615 | Small venomous hazard. |
| GiantSerpent, IceSerpent, LavaSerpent, SilverSerpent | `clawviper1` | 11 : 101 | 43 : 4,121 | 75 : 37,605 | Dangerous larger venomous reptile. |
| LavaLizard | `clawviper2` | 12 : 109 | 42 : 3,852 | 73 : 34,887 | Fire-adapted reptile. |
| Alligator | `clawviper1` | 6 : 62 | 38 : 2,939 | 70 : 30,810 | Large reptilian predator. |
| Raptor | `sandleaper3` | 17 : 211 | 46 : 4,798 | 77 : 38,306 | Fast reptilian predator. |
| Slith | `clawviper2` | 15 : 145 | 45 : 4,718 | 76 : 38,964 | Reptilian predator. |
| StoneSlith | `clawviper4` | 22 : 283 | 51 : 7,080 | 82 : 47,118 | Armoured reptilian predator. |
| ToxicSlith | `clawviper5` | 24 : 342 | 54 : 8,435 | 85 : 51,195 | Advanced venomous reptilian predator. |
| BullFrog, GiantToad | `frogdemon1` | 5 : 77 | 36 : 2,311 | 67 : 24,059 | Amphibian predator. |
| BlackBear | `brute1` | 5 : 84 | 39 : 3,772 | 71 : 38,602 | Wild bear; slow early brute analogue. |
| BrownBear | `brute2` | 7 : 108 | 40 : 4,038 | 71 : 38,602 | Wild bear; heavier brute analogue. |
| GrizzlyBear | `brute3` | 9 : 133 | 40 : 4,038 | 70 : 36,972 | Large wild bear. |
| PolarBear | `snowyeti1` | 12 : 277 | 36 : 2,824 | 67 : 29,406 | Cold-adapted wild bear. |
| RagingGrizzlyBear | `brute4` | 19 : 327 | 48 : 6,939 | 79 : 51,649 | Aggressive advanced bear. |
| GreyWolf, TimberWolf | `quillrat2` | 4 : 42 | 37 : 1,921 | 67 : 18,713 | Early wild predator. |
| WhiteWolf | `quillrat3` | 6 : 56 | 37 : 1,921 | 67 : 18,713 | Stronger wild predator. |
| DireWolf | `brute3` | 8 : 121 | 39 : 3,772 | 69 : 35,341 | Large aggressive pack predator. |
| Cougar, Panther, SnowLeopard | `sandleaper1` | 7 : 84 | 36 : 2,439 | 68 : 26,687 | Agile wild feline. |
| Gorilla | `baboon1` | 10 : 115 | 39 : 3,458 | 70 : 33,891 | Large wild primate. |
| Gaman | `brute3` | 10 : 145 | 41 : 4,320 | 71 : 38,602 | Large sturdy wild animal. |
| Phoenix | `vampire2` | 26 : 553 | 57 : 15,066 | 85 : 76,792 | Powerful fire creature. |
| VorpalBunny | `sandleaper5` | 26 : 497 | 56 : 9,003 | 82 : 44,762 | Magical aggressive rabbit; ordinary high-tier skirmisher. |
| CuSidhe | `bloodlord1` | 32 : 1,812 | 60 : 25,124 | 81 : 96,093 | Powerful magical hunting beast. |
| Hiryu | `megademon2` | 32 : 1,057 | 61 : 16,484 | 85 : 66,553 | Large magical flying reptile. |
| LesserHiryu | `megademon1` | 24 : 566 | 55 : 11,624 | 82 : 61,253 | Lesser magical flying reptile. |
| FireSteed, HellSteed, Nightmare | `megademon1` | 24 : 566 | 55 : 11,624 | 82 : 61,253 | Dangerous fire-breathing equine. |
| Kirin, Unicorn | `cantor3` | 24 : 465 | 53 : 10,342 | 82 : 61,253 | Powerful magical beast; native friendly alignment retained. |
| ScaledSwampDragon, SwampDragon | `clawviper3` | 18 : 193 | 46 : 5,051 | 77 : 40,323 | Heavy armoured swamp reptile. |
| Beetle, FireBeetle | `scarab3` | 17 : 281 | 45 : 5,897 | 84 : 62,295 | Large wild beetle; tame/summoned victims excluded. |
| Ridgeback, SavageRidgeback | `sandleaper1` | 10 : 112 | 39 : 2,986 | 71 : 30,560 | Small reptilian predator. |
| FrenziedOstard | `sandleaper2` | 15 : 174 | 44 : 4,188 | 76 : 37,015 | Aggressive flightless hunting bird. |
| SkeletalMount | `skeleton4` | 13 : 130 | 43 : 3,502 | 74 : 30,809 | Undead mount; owned instances excluded. |
| DarkGuardian | `doomknight1` | 24 : 487 | 56 : 9,477 | 82 : 47,118 | Hostile armored guardian. |
| ServantOfSemidar | `megademon2` | 27 : 753 | 56 : 12,320 | 83 : 63,020 | Ordinary demon servant; no act-boss reward. |
| PumpkinHead | `reanimatedhorde2` | 33 : 1,090 | 62 : 14,114 | 82 : 49,473 | Hostile seasonal monster, ordinary advanced undead tier. |
| Lurg, MougGuur | `blunderbore3` | 28 : 679 | 58 : 13,841 | 85 : 66,553 | Named ordinary heavy brute. |
| Grobu | `brute5` | 25 : 568 | 55 : 10,730 | 85 : 61,434 | Named ordinary bear. |
| Rend | `megademon3` | 38 : 1,722 | 65 : 20,813 | 85 : 66,553 | Named advanced reptalon. |
| Pyre | `vampire4` | 34 : 976 | 64 : 22,656 | 85 : 76,792 | Named ordinary fire bird. |
| Miasma | `scarab5` | 35 : 1,156 | 65 : 20,012 | 85 : 63,993 | Named ordinary venomous arthropod. |
| LadyJennifyr, LadyMarai, RedDeath, SirPatrick | `reanimatedhorde4` | 40 : 2,127 | 64 : 15,859 | 84 : 52,327 | Named advanced undead, no boss multiplier. |
| MasterJonath, MasterMikael | `doomknight3` | 38 : 1,484 | 66 : 35,523 | 85 : 107,509 | Named advanced undead caster. |
| MasterTheophilus | `cantor3` | 36 : 1,049 | 65 : 20,813 | 85 : 66,553 | Named advanced humanoid caster. |
| Abscess | `clawviper5` | 38 : 883 | 68 : 18,728 | 85 : 51,195 | Named advanced multi-headed reptile. |
| Saliva | `vulture4` | 28 : 478 | 55 : 7,153 | 85 : 40,956 | Named advanced winged attacker. |
| Putrefier | `megademon3` | 40 : 1,971 | 67 : 23,168 | 85 : 66,553 | Named advanced demon. |
| Coil, Thrasher | `clawviper5` | 34 : 673 | 64 : 15,104 | 85 : 51,195 | Named venomous reptile. |
| Gnaw | `bloodlord1` | 32 : 1,812 | 60 : 25,124 | 81 : 96,093 | Named advanced pack predator. |
| LadyLissith, LadySabrix, Silk | `arach5` | 30 : 751 | 60 : 13,160 | 85 : 56,314 | Named advanced venomous spider. |
| Malefic, Virulent | `arach5` | 34 : 984 | 64 : 16,614 | 85 : 56,314 | Named advanced venomous spider. |
| Guile, Irk, Spite | `succubuswitch3` | 35 : 1,230 | 62 : 19,490 | 83 : 70,291 | Named hostile shapeshifting caster. |
| Swoop | `vulture4` | 30 : 546 | 57 : 8,035 | 85 : 40,956 | Named dangerous bird. |
| Chiikkaha | `fetishshaman3` | 28 : 437 | 55 : 23,249 | 85 : 133,107 | Named advanced tribal caster. |
| Szavetra | `succubus3` | 34 : 1,131 | 61 : 11,412 | 82 : 42,406 | Named advanced demon caster. |

## Explicit zero rewards

These policies exist on all three difficulty previews. Controlled, summoned, animated and `NoKillAwards` victims are also excluded by the death service regardless of their underlying wild-species reward.

| Policy | Types | Reason |
| --- | --- | --- |
| boss-reserved | Barracoon, Harrower, HarrowerTentacles, Ilhenir, InterredGrizzle, LordOaks, Mephitis, Meraktus, Neira, Rikktor, Semidar, Serado, Silvani, Tangle, Twaulo | Champion/peerless/boss encounter reserved for a manual dungeon-boss profile; no imported act-boss reward. |
| summon-helper | SkeletonGuardian, SummonedPaladin, ArcaneFey, ArcaneFiend, BaseEnraged, BaseTalismanSummon, BladeSpirits, CapturedHordeMinion, Clone, DarkWolfFamiliar, DeathAdder, EnergyVortex, EnragedBlackBear, EnragedEagle, EnragedHart, EnragedHind, EnragedRabbit, HolyFamiliar, HolySteed, HordeMinionFamiliar, NatureFury, Revenant, ShadowWispFamiliar, UnholyFamiliar, UnholySteed, VampireBatFamiliar | Summon, pet implementation or scripted helper, never a reward target. |
| quest-trial | MilitiaFighter, Henchman, CursedSoul, DeadlyImp, DiseasedCat, FierceDragon, InjuredWolf, YoungNinja, YoungRonin | Scripted training/quest encounter; not an ordinary reward-bearing spawn. |
| event-copy | NaughtyTwin, ZombieSkeleton | Player-copy/event helper; avoid repeatable player-derived reward farming. |
| town-guard | FactionBerserker, FactionDeathKnight, FactionDragoon, FactionHenchman, FactionKnight, FactionMercenary, FactionNecromancer, FactionPaladin, FactionSorceress, FactionWarHorse, FactionWizard, ChaosGuard, Guardian, OrderGuard | Town/faction guard or faction mount; not a PvE XP target. |
| domestic-wildlife | Bird, Boar, Bull, Cat, Chicken, CoMWarHorse, Cow, Crane, DesertOstard, Dog, Dolphin, Eagle, Ferret, ForestOstard, Goat, GreatHart, GrizzledMare, Hind, Horse, JackRabbit, Llama, MinaxWarHorse, MountainGoat, PackHorse, PackLlama, Parrot, Pig, Rabbit, Rat, RidableLlama, SLWarHorse, SeaHorse, SewerRat, Sheep, SilverSteed, Squirrel, TBWarHorse, TropicalBird, Walrus | Domestic, ambient or passive wildlife; no hostile D2 counterpart or livestock XP loop. |
| vendors | TournamentRegistrar, Aelorn, AmeliaYoungstone, GeorgeHephaestus, Hamato, Morganna, Mulcivikh, Robyn, Ryuichi, SirHelper, BlackAmbitiousSolenQueen, RedAmbitiousSolenQueen, AlbertaGiacco, ElwoodMcCarrin, GabrielPiete, Impresario, TomasONeerlan, Chyloth, Victoria, Blackheart, Grizelda, Zeefzorpul, Dryad, MansionGuard, MilitiaCanoneer, Schmendrick, Uzeraan, BlackSolenMatriarch, RedSolenMatriarch, Naturalist, Horus, Maabus, Mardoth, Emino, EnshroudedFigure, HiddenFigure, JedahEntille, Zoel, Haochi, HaochisGuardsman, Relnia, AnsellaGryen, FactionBoardVendor, FactionBottleVendor, FactionHorseVendor, FactionOreVendor, FactionReagentVendor, Alchemist, AnimalTrainer, Architect, Armorer, Baker, Banker, Bard, BardGuildmaster, Barkeeper, Beekeeper, Blacksmith, BlacksmithGuildmaster, Bowyer, Butcher, Carpenter, Cobbler, Cook, CustomHairstylist, EvilHealer, EvilWanderingHealer, Farmer, FisherGuildmaster, Fisherman, FortuneTeller, Furtrader, Glassblower, GolemCrafter, GypsyAnimalTrainer, GypsyBanker, GypsyMaiden, HairStylist, Healer, HealerGuildmaster, Herbalist, HolyMage, IharaSoko, InnKeeper, IronWorker, Jeweler, KeeperOfChivalry, LeatherWorker, Mage, MageGuildmaster, Mapmaker, MerchantGuildmaster, Miller, Miner, MinerGuildmaster, Minter, Monk, PlayerBarkeeper, PricedHealer, Provisioner, Rancher, Ranger, RangerGuildmaster, RealEstateBroker, Scribe, Shipwright, StoneCrafter, Tailor, TailorGuildmaster, Tanner, TavernKeeper, Thief, ThiefGuildmaster, Tinker, TinkerGuildmaster, Vagabond, VarietyDealer, Veterinarian, Waiter, WanderingHealer, WarriorGuildmaster, Weaponsmith, Weaver | Vendor/service NPC; never a reward target. |
| quest-npcs | Aeluva, Aernya, Ahie, AldenArmstrong, Alefian, Alejaha, Alelle, Aluniol, AndreasVesalius, Andric, Andros, Aniel, Anolly, Arielle, Asandos, Athailon, Aulan, Aurelia, Avicenna, Belulah, Ben, Beotham, Bolaevin, Braen, Bravehorn, BravehornsMate, Brinnae, Caelas, Cailla, Canir, Chiyo, Churchill, Ciala, Cillitha, Clairesse, Clehin, Cloorne, Daelas, Dallid, Danoel, Dimethro, Drithen, ElderAbbein, ElderAcob, ElderAlethanian, ElderBrae, ElderJothan, ElderMallew, ElderOnallan, ElderTaellia, ElderVicaie, Emerillo, Emilio, Enigma, Evan, Frederic, Gervis, Gorrow, GrandpaCharley, Gustar, Hargrove, Huntsman, Iosep, JacobWaltz, Jamal, Jelrice, Jillian, Jockles, Jun, Jusae, Kaelynna, Kashiel, Kia, Koole, Landy, Leon, Lissbet, Lohn, LorekeeperAneen, LorekeeperBroolol, LorekeeperCalendor, LorekeeperNillaen, LorekeeperOolua, LorekeeperRollarn, LorekeeperRyal, LorekeeperSiarra, Lowel, Lyle, MasterGnosos, MaulTheBear, Mielan, Mithneral, Mugg, Nibbet, Norton, Nythalia, Olaeni, Olla, Patricus, Rebinil, Recaro, Regina, Sadrah, Salaenih, Saril, SarsmeaSmythe, SkeletonOfSzandor, Sledge, Sleen, Strongroot, Synaeva, Tallinin, Tamm, Thalia, Thallary, Tholef, Tiana, Tillanil, Tyeelor, TylAriadne, Unoelil, Vilo, Waelian, Walker, Yellienir, Yorus, Darius, Nedrick | Quest giver, escort or invulnerable quest helper; never an ordinary monster reward. |
| townfolk | Actor, Artist, BaseEscortable, BrideGroom, EscortableMage, Gypsy, HarborMaster, Merchant, Messenger, Ninja, Noble, Peasant, Samurai, Sculptor, SeekerOfAdventure, Prisoner | Townfolk, escort or service NPC; not an ordinary monster reward. |
| hirelings | BaseHire, HireBard, HireBardArcher, HireBeggar, HireFighter, HireMage, HirePaladin, HirePeasant, HireRanger, HireRangerArcher, HireSailor, HireThief | Hireling implementation; not a reward target. |
| summoned-types | SummonedAirElemental, SummonedAntLion, SummonedArcticOgreLord, SummonedBakeKitsune, SummonedBogling, SummonedBullFrog, SummonedChicken, SummonedCow, SummonedDaemon, SummonedDoppleganger, SummonedEarthElemental, SummonedFireElemental, SummonedFrostSpider, SummonedGreatHart, SummonedLavaSerpent, SummonedOrcBrute, SummonedPanther, SummonedSheep, SummonedSkeletalKnight, SummonedVorpalBunny, SummonedWailingBanshee, SummonedWaterElemental | Summon implementation, not an ordinary wild spawn. |
