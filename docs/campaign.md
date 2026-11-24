# Britannia campaign

**Contains story and encounter spoilers.** The three chapters take place on the original playable Britannia map (Trammel). Chapter difficulty governs the authored encounters. Alternate facets are post-campaign access.

Leoric wields a two-handed sword in the ruined court. Lazarus lifts off the floor as he releases a spirit in the mausoleum. Diablo wields a whip, and flames continue to flicker on his armour and wings after he falls. The four lava channels around his platform brighten and fade independently. [Boss sprites and in-game previews](../art/campaign-bosses/README.md)

## Progression and arena access

| Dungeon | Ordinary population band | Campaign difficulty | Arena and GM destination |
| --- | --- | --- | --- |
| Despise and Covetous | Up to 30 | Normal | No campaign arena |
| Hythloth | Up to 40 | Normal | King Leoric, level 40 · `[go Leoric` |
| Deceit | 35–55 | Nightmare | Lazarus, level 55 · `[go Lazarus` |
| Wrong | 50–60 | Optional route | No campaign arena |
| Destard | 55–65 | Hell resistance rules | Diablo, level 65 · `[go Diablo` |

The bands are authoring targets. The [Hythloth roster](worldbuilding.md#hythloth-spawners) is available for GM placement. The regular spawn and elite placement pass for Hythloth, Deceit, Wrong, and Destard is tracked in the [roadmap](roadmap.md#campaign-and-world). Destard’s campaign difficulty uses high resistances with an 85% ceiling and without dual immunities. Arena definitions contain the effective boss levels, fixed layouts, and arrival points. [Arena code](../src/Sanctuary.Content/CampaignArenas.cs) · [Dungeon routes](../src/Sanctuary.Content/CampaignDungeonRoutes.cs)

Double-click a red gate at the bottom of the corresponding dungeon. Lazarus requires Leoric’s defeat. Diablo requires both previous defeats. A completed boss’s gate disappears for that Avatar. A party member eligible to open a gate can bring already-cleared members. A player at least ten levels below a boss receives a recommended-level confirmation and can enter anyway. The gates are one-way. Use the runebook to leave.

A solo player defeated inside wakes unharmed from a dream, with their belongings and progress, at the Britain inn. During the ghost-town chapter the destination is the Bountiful Harvest Inn in New Haven. A group uses the usual defeat and recovery rules. Boss victories and their prerequisites determine progress, allowing a party to go ahead of the level-30 story introduction. [Campaign state](../src/Sanctuary.Content/BritanniaCampaign.cs)

### Campaign boss loot

| Boss | Regular act-boss loot table | Item level |
| --- | --- | ---: |
| King Leoric | Normal Diablo | 40 |
| Lazarus | Nightmare Mephisto | 55 |
| Diablo | Hell Diablo | 65 |

All campaign fights use players 5 for life, experience, and loot, including solo kills. Each act-boss table attempts seven rolls and can produce up to six results. The credited player’s Magic Find and Gold Find apply. Equipment pools select from bases with a Sanctuary item mapping. A gear base uses rare or magic quality when the selected set or unique item is unavailable. Other results require an existing Sanctuary mapping, so a corpse can have fewer items than the table rolled.

## Fire Island

From level 30, if the Avatar has yet to defeat Leoric, Geoffrey intercepts them on entering Britain. He has been searching for the Avatar on Lord British’s orders. Lord British explains that Mariah and the mages at the Lycaeum have traced destabilising energy across Fire Island to Hythloth. Accepting passage grants permanent access to the Fire Island ferry through **Elowen Marr**, the Britain harbormaster. Her regular conversation covers ships, trade, and other harbours. [Campaign actors](../src/Sanctuary.Content/CampaignActors.cs)

### The ruined throne room below Hythloth

The red gate at **6104, 33 (27)** on Hythloth Level 4 brings the party to the southern vestibule of Leoric’s 31 × 39 tile hall. The GM destination is `[go Leoric` at **6905, 3775 (0)** in Trammel. A torn red runner leads past the side galleries to a lion throne. Its right armrest lies broken on the base, and the velvet seat sags toward the break. Five tattered oxblood standards hang from iron rods along the throne wall and side gallery. Their faded gold lions echo the runner. Leoric begins in front of the throne. Broken piers and fallen furniture give the side aisles a different shape on each side. The vestibule is sheltered from his floor strike. [Throne room layout](../src/Sanctuary.Content/LeoricThroneRoom.cs) · [Entrance](../art/world/leoric-throne/entrance.jpg) · [The king’s end of the hall](../art/world/leoric-throne/court.jpg)

## Dagger Isle

If the Avatar accepted the Fire Island audience and defeated Leoric, Geoffrey intercepts them on their next arrival in Britain at any level. He looks worn down. Lord British has fallen ill, guards are missing or reporting drunk, and understaffing makes it hard to remove corrupt guards. Blackthorn waits at his keep north of Britain.

Blackthorn suspects an anti-virtue dungeon and has no clear lead. Shamino suggests checking Deceit on Dagger Isle.

> “I have no proof. But we found one evil nesting on Fire Isle. I would see what another island conceals before dismissing it.”

Blackthorn calls the deduction childish but provides passage if the Avatar wants to follow it. The subsequent Geoffrey summons and ghost-town conversation depend on the first audience. Boss progression is tracked separately.

### The cathedral below Deceit

The red gate at **5276, 667 (6)** in Deceit Level 4 leads to **6976, 3746 (0)** in the 89 × 89 tile mausoleum. The GM command `[go Lazarus` uses the same arrival tile. Three shallow limestone steps lead from the nave to an east–west chancel with a broad sacrificial altar centered parallel to the long steps. Lazarus stands at its back edge, with Red Vex and Blackjade holding the two ends until the player closes his greeting. Both guards can move freely during the fight. A hidden GM can dismiss the greeting and inspect the court in peace. UO braziers light the burial niches, and four symmetrically placed masonry piers provide cover with walking room on every side. The five pentagrams lie around the perimeter. Each is outside the altar view and the view from its preceding stop at standard zoom. [Entrance and fire circle in ClassicUO](../art/world/lazarus-mausoleum/README.md)

Lazarus greets the Avatar from behind the altar.

> “Abandon your foolish quest! All that awaits you is the wrath of my master! You have come too late, Avatar. I lacked only an offering, and you have walked willingly to my altar.”

Dismiss his portrait dialogue to engage Red Vex and Blackjade. Red Vex fires red bolts and Blackjade fires blue bolts. Duck behind a pier to draw a bodyguard out of position. Lazarus becomes targetable when both bodyguards are dead.

Lazarus visits the fire, cold, lightning, poison and physical pentagrams in that order. Each arrival launches a pale Bone Spirit toward the Avatar. It flies straight briefly, turns to follow its target and bursts against solid cover. Follow its approach to find his circle. He summons a guardian four seconds after landing and moves again after eighteen seconds.

A surviving guardian protects Lazarus against its element while he stands in the matching circle, including on later circuits. Killing the last matching guardian drops the ward immediately. Guardians from previous stops can accumulate if the party concentrates on the boss. [Original encounter notes](https://github.com/aomukai/UO-Sanctuary/blob/528dd686e310f2885504ad2e6ca4ab47ba254ef5/docs/boss-encounters.md#archbishop-lazarus) · [Encounter implementation](../src/Sanctuary.Content/ArchbishopLazarus.cs)

## Ghost towns

Defeating both Leoric and Lazarus starts the chapter immediately. Mainland town NPC visibility changes for that Avatar. Other players see the chapter appropriate to them. Geoffrey remains alone at the Britain bridge guard tower. He looks aged and exhausted. Lord British’s condition has worsened, Blackthorn is ill, and Geoffrey suspects another dungeon but has no lead.

New Haven, Moonglow, and Jhelom remain accessible by moongate. Companions are around New Haven during the chapter. At the Bountiful Harvest Inn, Jaana suspects Shame, Iolo suggests Wrong, and Dupre enters to suggest Destard because of its dragons and wyrms. Jaana agrees Destard is worth investigating.

## Destard and the dragons

The Diablo arena is an 81×81 volcanic chamber in Trammel. The red gate at **5190, 1010 (0)** in Destard leads to **6941, 3932 (0)** by a short bridge. The GM command `[go Diablo` uses the same arrival tile. Diablo spawns at **6976, 3932 (0)** on a broad basalt platform with blood painted across it. Lava surrounds the platform and fills its two vents. Four causeways connect the platform to the cave. Four rock formations beyond the lava break line of sight, with room to run around each one. [Cave layout](../src/Sanctuary.Content/DiabloCave.cs) · [Isometric floor art](../art/world/diablo-ritual/README.md)

Vaelith, the dragon queen, pays **100 gold per daemon horn** without a quest announcement. She tells the Avatar their cumulative horn count with each payment. Lesser daemons, daemons, and archdaemons each have a **90%** horn drop chance. Rhazek, her consort, says the wyverns and wyrms chose betrayal while the drakes and dragons were enslaved. After Diablo's defeat, Vaelith thanks the Avatar for freeing their home and still pays the bounty. Rhazek grudgingly admits he misjudged the Avatar.

Two royal dragon guards stand at **5307, 969 (0)** and **5311, 956 (0)**. They greet each character according to how many horns that character has handed to Vaelith. At **20**, they thank the Avatar for standing with them. At **50**, they greet the Avatar as a trusted ally. Turn-ins after Diablo count toward both greetings. The friendly royal servant at **5318, 980 (0)** speaks of Vaelith, Rhazek, the invasion, the traitors, the daemons, and the enslaved dragons and young. The journal records each topic as it is heard, including the red gate in the old nursery in Destard's far quarters. After Diablo's defeat, the servant acknowledges that the voice below has fallen silent. [Court NPCs](../src/Sanctuary.Content/DestardCourtAttendants.cs)

Each character gets two hidden thresholds. The first is drawn uniformly from **10–19** horns and the second from **20–29**. Each horn through a band increases the chance of earning that visit by ten percentage points. The same thresholds apply to single and bulk hand-ins. Rhazek reveals his help when he arrives during the fight.

- **First pledge.** At or below 75% Diablo health, Rhazek takes his attention for one minute.
- **Second pledge.** At or below 25% health, Rhazek returns for the rest of the fight. If he is still on his first visit when Diablo crosses 25%, he returns as soon as that visit ends.

Rhazek answers a nearby eligible Avatar and helps the whole party. He holds Diablo’s attention when players attack, uses dragon melee animations, and deals zero damage. He stays at 1 HP once Diablo has worn him down and delivers an irritated line. Each visit can happen once per encounter. A fresh attempt can call on the pledges again. [GM placements](worldbuilding.md#destard-spawners) · [Bounty and dialogue](../src/Sanctuary.Content/DestardCourt.cs) · [Arena aid](../src/Sanctuary.Content/DestardAid.cs)

## Homecoming

Diablo’s defeat restores the towns and cancels earlier live story summons and triggers. On returning to Britain, Geoffrey greets the Avatar with a smile and sends them to Lord British even if they skipped the earlier audiences. Lord British proclaims the Avatar **Hero of the People** and awards campaign recognition with wardrobe access. The independent crafting recognition path remains available. Campaign completion opens ordinary boat access and the post-campaign facets.

Record learned leads, travel permissions, boss victories, and the proclamation in the per-character journal. Completed and skipped interactions can be distinguished through individual flags. See the [dialogue authoring workflow](worldbuilding.md#dialogue-and-journal) and the [roadmap](roadmap.md#campaign-and-world).
