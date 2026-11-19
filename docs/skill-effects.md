# Skill effects

The register covers all eight classes and records the UO baseline as `native` where one shipped. Werewolf and Werebear use the Timber Wolf and Grizzly Bear sprites at 120% visual scale. Both keep the player's existing collision footprint. Sorceress Fireball keeps its existing presentation.

The [per-skill asset register](../data/Sanctuary/Balance/skill-effects.json) tracks custom art and audio independently. In ClassicUO, a learned Bone Spear pierced two targets with a small contact flash and shatter at the end of its flight. Meteor has a ground warning, falling stone, impact, and fire on its active field tiles. The first Sonniss edits cover Bone Spear and the red and blue flares. Their listening pass is queued. The [cue recipes](../art/spell-effects/sonniss-cues.json) record the GDC 2019 archive parts and source filenames. `python3 tools/install_spell_audio.py` converts the downloaded WAVs into ClassicUO's local Sounds directory.

Blizzard shows frost beneath its falling shards, an impact when they land, and a small flash when they damage a target. Frozen Orb rotates as it travels and emits directional bolts, with a burst at the existing terminal nova. Both were verified in ClassicUO with audio disabled. [Blizzard sprite source](../art/spell-effects/blizzard/sheet.png) · [Frozen Orb sprite source](../art/spell-effects/frozen-orb/sheet.png)

Teeth fans out directional bone shards that chip on contact. Bone Spirit uses a trailing skull and a burst on damage. Abyss opens as a ground tear, holds a vortex through its pull pulses, then collapses with the terminal hit. These effects were checked in ClassicUO with audio disabled. [Teeth sprite source](../art/spell-effects/teeth/sheet.png) · [Bone Spirit sprite source](../art/spell-effects/bone-spirit/sheet.png) · [Abyss sprite source](../art/spell-effects/abyss/sheet.png)

All ten Necromancer curses share a violet release ring and have distinct animated marks on affected targets. The mark follows its target until the curse is replaced or clears. Iron Maiden flashes on the attacker when damage is returned. Life Tap shows rising red ribbons on the attacker when it restores life. [ImageGen sprite sheet](../art/spell-effects/necromancer-curses/sheet.png) · [Prompt](../art/spell-effects/necromancer-curses/prompt.txt)

Muted ClassicUO checks covered each learned curse, an empty cast, multiple recipients, replacement, movement and natural Amplify Damage expiry. Attract marked the selected enemy and preserved its neighbor's existing curse. Native melee damage exercised Iron Maiden reflection and Life Tap healing. The client decoded the four Sonniss cues at release, expiry, reflection and healing. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Bone Armor orbits the caster while its physical absorption pool is active. It sparks when it catches a hit and shatters when the pool runs out. Corpse Explosion erupts at the consumed corpse. Bone Wall grows in raised ivory ribs, and Bone Prison shows hooked ribs at its twelve anchors plus four low connector sprites. Both barriers throw off chips on a surviving hit and show rubble during their existing retirement. The sprites follow the same blocking tiles and selection continues to use the visible live ribs. [Armor and explosion sheet](../art/spell-effects/bone-armor-explosion/sheet.png) · [Armor and explosion prompt](../art/spell-effects/bone-armor-explosion/prompt.txt) · [Barrier sheet](../art/spell-effects/bone-barriers/sheet.png) · [Barrier prompt](../art/spell-effects/bone-barriers/prompt.txt)

Muted ClassicUO checks covered partial absorption and depletion, one-time corpse consumption with loot retained, and the existing explosion damage radius. A growing Bone Wall blocked movement, intercepted a projectile route, and remained selectable. Wall damage and natural Prison expiry cleared collision and retired all affected art. The client decoded six Sonniss cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Poison Dagger uses the native knife pose and an acid-green contact slash. Poison Explosion blooms at the consumed corpse and sends eight clouds along their existing paths. It reuses the custom poison javelin mist. Poison Nova expands in directional wisps tied to the surviving rays. Both spells place their traveling art at fractional tile positions. Active Sanctuary poison adds rising droplets that follow the affected actor and dissipate at expiry or cleanse. [ImageGen sheet](../art/spell-effects/necromancer-poison/sheet.png) · [Prompt](../art/spell-effects/necromancer-poison/prompt.txt) · [Shared mist source](../art/spell-effects/javelin-venom/sheet.png)

Muted ClassicUO checked knife hits and misses, poison immunity, movement and natural expiry. Nova emitted 64 rays and cleared its poison overlay on cleanse. Poison Explosion retained loot, emitted eight clouds, poisoned a target in their path, and retired the clouds and status at their existing limits. The client decoded five new Sonniss edits and two shared poison cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Raise Skeleton forms a ring of rising bones, Skeletal Mage arrives through violet trails, and Revive sends an emerald spirit up from the corpse. The summons use their native bodies and animations. Revive keeps the original creature’s body and hue. The three rituals share an ash fade on departure and play the arrival effect again when a parked summon returns. [ImageGen sheet](../art/spell-effects/undead-summons/sheet.png) · [Prompt](../art/spell-effects/undead-summons/prompt.txt)

Muted ClassicUO checks covered the Skeleton Warrior’s landed melee, parking, return and dismissal. All three Mage variants used their elemental volleys. Their Glacial Spike, Meteor, Chain Lightning, Static Field, Frozen Armor and Enchant use the shared Sorceress effects. The Mage and revived Graveyard Lich faded on death. A revived Graveyard Skeleton kept its original body and flashed on a landed melee hit. Each ritual paid mana once and retained the corpse loot. The client decoded four new Sonniss cues at the corresponding events. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

The four golems keep their native bodies and have separate arrival effects in clay, scarlet ribbons, iron slivers and embers. Clay’s slow follows the target. Blood’s healing flare appears on each recipient that gains life, and high-rank Blood adds the shared Life Tap effects and an Open Wounds overlay. Iron throws sparks on strikes and retaliation, and uses the matching elemental effect on Spectral hits. Fire’s Holy Fire ring marks the pulse, and its eruption plays at the existing delayed death burst. The golems share rubble on ordinary departure. [Arrival sheet and prompt](../art/spell-effects/golem-arrivals) · [Combat sheet and prompt](../art/spell-effects/golem-combat) · [Fire sheet and prompt](../art/spell-effects/golem-fire) · [Open Wounds sheet and prompt](../art/spell-effects/open-wounds)

Muted ClassicUO checks covered all four arrivals, Clay’s slow in both directions and its return after parking, Blood’s two-recipient healing and rank-20 Life Tap and Open Wounds, Iron’s returned damage and five Spectral channels, and Fire’s pulse and delayed burst. Wounds expired independently of Life Tap. Each cast paid mana once and Iron retained its source item until replacement. ClassicUO loaded ten new Sonniss cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Werewolf transitions use silver-blue wisps. Werebear brings up amber leaves and earth, which settle on return. The forms use their native animations and draw the transformation art around the current body. Maul scatters stones on contact and leaves an amber orb. Feral Rage rakes with red-orange trails and leaves a red ember. The orbs pass in front of and behind the forms and grow across the first three charges. [Transition sheet and prompt](../art/spell-effects/druid-form-transitions) · [Charge sheet and prompt](../art/spell-effects/druid-form-charges)

Muted ClassicUO checks rendered both transformations and returns on both genders of Human, Elf and Gargoyle. Entry charged mana once, return was free, and the server kept the same body and tile fit. A moving caster canceled a pending shift, and a wolf that reached its saved deadline returned with the same effect. Feral Rage’s ember followed movement and cleared on unshift. Maul charged only on successful hits and its orb cleared at the real 20-second expiry. ClassicUO loaded eight new Sonniss cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Fire Claws leaves a gold-orange rake on a landed contact. Hunger closes a pair of crescents at the target and shows a separate red flare and blue ribbon when it restores life and mana. Fury uses a brief silver slash at the recipient of each landed strike. The attacks use their native form animations. [ImageGen sheet](../art/spell-effects/druid-melee/sheet.png) · [Prompt](../art/spell-effects/druid-melee/prompt.txt)

Muted ClassicUO checks covered Fire Claws and Hunger in wolf and bear form. The Fire Claws fixture isolated fire damage and exercised an accuracy miss and a real shield block. Hunger showed both recovery effects, either one alone, and the bite alone with full pools. Rank-four Fury landed five alternating strikes on two targets over about one second from first contact to last. Misses played the swing cue. Movement after the first strike ended the series. ClassicUO loaded seven new Sonniss cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Rabies shows an olive bite flash and a small flare when it spreads to a new carrier. The carrier’s chartreuse motes follow the independent infection timer, including through a poison cleanse, and disperse at its own expiry. Shock Wave breaks into ground pressure crests along the five existing paths, with chips on contact and a shared overhead stun marker. [Rabies sheet and prompt](../art/spell-effects/druid-rabies) · [Shock Wave sheet and prompt](../art/spell-effects/druid-shock-wave)

Muted ClassicUO checks covered a missed Rabies start, a paid bite, natural spread to a second target, movement and poison cleanse, and both infection expiries. Shock Wave rendered its fan in two directions with fractional tile positions. Its waves stopped at terrain, and a real shield block kept the native block reaction. The stun marker followed the target, appeared on a physically immune target that was stunned, and retired at the actual deadline. ClassicUO loaded six new Sonniss cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Firestorm runs three directional tongues of flame along the existing paths and leaves low fire on the active cells. Molten Boulder forms in sparks, sheds chips on a continuing hit, and shatters once at the end of its roll. The ball's visible center sits behind its collision point, and the fire trail can outlast the ball. Fissure opens at the accepted aim point, then scattered cracks glow while their hazards are active. All three share a small fire flash on damaging ground contact. [Firestorm and Fissure sheet and prompt](../art/spell-effects/druid-ground-fire) · [Molten Boulder sheet and prompt](../art/spell-effects/druid-boulder)

Muted ClassicUO checks covered a damaging Firestorm with three emitters, a continuing Boulder hit and separate wall impact, and damage from a live Fissure crack. Each cast paid mana once and its fields retired on their existing timers. ClassicUO decoded six new Sonniss edits and two shared fire cues. The boulder's rolling cue uses a trimmed thunder rumble pending the listening pass. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Twister sends three low silver eddies along the existing paths. It marks committed contact and shares the overhead stun effect. Tornado is a taller blue-gray funnel with a short ground sweep on the recurring area pulse. Both shrink into drifting dust when their missiles retire. Hurricane opens in frost, then keeps a thin ring at the caster's feet while icy wisps circle the active storm. Cold contact flashes at the target and shares the existing chill overlay. [Twister and Tornado sheets and prompt](../art/spell-effects/druid-wind-missiles) · [Hurricane sheet and prompt](../art/spell-effects/druid-hurricane)

Muted ClassicUO checks rendered all three Twister paths, landed damage and stun, and dissipation where a wall stopped the eddies. Tornado showed direct damage, recurring pulse effects, and natural dissipation. Hurricane moved with the caster and damaged and chilled eligible targets at the two locations. The core cleared at the source deadline and the wisps stopped. Each cast paid mana once. ClassicUO decoded seven new Sonniss edits and the shared ice contact cue. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Arctic Blast opens with a narrow frost gust and draws a continuous stream from its directional segments. It sparks with ice chips on damaging contact and shares the existing chill overlay. Each segment retains its trajectory through an aim change and disperses into snow when it ends. Cyclone Armor wraps the caster in an open wind shell. The rim flashes when it absorbs elemental damage and unravels at depletion. Both use the existing player poses. [Arctic Blast sheet and prompt](../art/spell-effects/druid-arctic-blast) · [Cyclone Armor sheet and prompt](../art/spell-effects/druid-cyclone-armor)

Muted ClassicUO checks held Arctic Blast with a quick-slot key, aimed it across two lanes, and chilled both targets. Key release ended new emissions and mana charges while existing segments finished. Cyclone Armor followed caster movement, passed physical damage without spending absorption, caught a fire hit, and depleted on cold and lightning damage. Excess damage reached the caster and the shield cleared at depletion. Recasting restored the shell, and explicit state cleanup removed it. ClassicUO decoded all six new Sonniss cues. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Volcano rises from the accepted tile and cools when the vent retires. Its glowing rocks arc along the existing airborne paths and leave small fire beds where they land. Armageddon puts a ring of cinders around the caster while it selects new landings. A heat ripple marks each accepted cell before a rock falls. Each impact uses a scaled version of the custom Meteor burst and lights the live ground fire. [Volcano sheet and prompt](../art/spell-effects/druid-volcano) · [Armageddon sheet and prompt](../art/spell-effects/druid-armageddon)

Muted ClassicUO checks covered Volcano rejecting a wall placement, its vent and landed fire separately dealing damage, and natural cooling. Armageddon warned and landed at the committed locations. Its terminal area and ground fire dealt damage separately. The cinder ring followed the caster and cleared at the weather deadline while already released rocks and fires finished. Both spells paid mana once. ClassicUO decoded six new Sonniss edits and two shared fire cues. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

The animal summons use their native bodies. Raven arrives through violet feathers and leaves a dedicated mark when it blinds. Spirit Wolf rises in frost and shows icy jaws on a landed bite, with the shared chill marker. Dire Wolf arrives through copper leaves and gains a warm aura when a corpse meal activates rage. Grizzly rises through earth and dust. Its ordinary swipe flashes gold, and Bear Smite uses the shared heavy stone impact. [Animal arrival sheet and prompt](../art/spell-effects/druid-animal-arrivals) · [Combat sheet and prompt](../art/spell-effects/druid-animal-contacts)

Muted ClassicUO checks covered all four learned arrivals and contacts. Raven's mark cleared with the blindness state. Spirit Wolf faded on parking and used its arrival again when the same body returned. Dire Wolf preserved corpse loot, hit during rage, and lost its aura at the real deadline. A replacement Grizzly retired the previous bear, landed Bear Smite with knockback, and faded on death. ClassicUO decoded ten new Sonniss edits and the shared Maul impact cue. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

The three spirits keep their native wisp bodies. Oak Sage raises green leaves and marks each recipient of its maximum-life bonus. Heart of Wolverine arrives in copper wisps and marks recipients of its attack rating and damage bonuses. Spirit of Barbs raises violet thorns, then marks recipients with an angular ring and flashes at the attacker on returned damage. Each spirit dissipates into silver wisps on departure. [Spirit arrivals and thorn contact](../art/spell-effects/druid-spirit-arrivals) · [Aura and departure sprites](../art/spell-effects/druid-spirit-auras)

Muted ClassicUO checks covered all three learned arrivals, native aura bonuses on the caster, the spirit and an allied wolf, and replacement across the three skills. The wolf lost Oak's life aura outside its range and regained it upon returning. Heart's ring followed caster movement. Barbs flashed on melee return damage, with no flash for ranged damage or physical immunity. The same spirit returned after parking and the last recipient markers cleared on dismissal. ClassicUO decoded five new Sonniss cues. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

The three vines use the native corpser body with distinct arrival and departure effects. Poison Creeper sends directional thorns along the existing subtiles, flashes on committed poison, and leaves mats that wither on their own timers. Carrion Vine and Solar Creeper mark the reserved corpse, which remains lootable. Carrion flashes red when life is restored, and Solar flashes blue when mana is restored. [Vine arrivals and thorns](../art/spell-effects/druid-vine-arrivals) · [Ground mats and recovery](../art/spell-effects/druid-vine-ground)

Muted ClassicUO checks covered all three learned arrivals, Poison Creeper's live damage and trail fade, and replacement across the three skills. The venom trail continued after its vine was replaced. Both recyclers reserved a real corpse and applied their own resource gain after the existing delay. A second Solar recovery reached a player who had already refilled mana and emitted only the corpse effect. The last vine faded on dismissal. ClassicUO decoded all eight new Sonniss edits and the shared departure cue. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Holy Fire, Holy Freeze, and Holy Shock keep gold, icy blue, and violet rings at the caster's feet for the active aura lease. On each scheduled pulse, a ring expands to the live damage radius. Pulse damage and landed aura-imbued weapon hits show short elemental flashes. Holy Freeze adds its own frost seal when it applies a slow and uses the shared cold burst for committed corpse shatters. [Holy aura sheet and prompt](../art/spell-effects/paladin-holy-auras) · [Contact sheet and prompt](../art/spell-effects/paladin-holy-contacts)

Muted ClassicUO checks covered the first pulse delay, caster movement, switching and disabling auras, pulse immunity and range, and all three landed weapon accents. Holy Freeze marked a cold-immune target when the slow applied, then cleared the frost seal at expiry. A separate death triggered the shared corpse shatter. Holy Shock reached three tiles at rank one and six and a half at rank eight. ClassicUO decoded the six edited Sonniss cues and the shared shatter. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Holy Bolt follows the existing piercing path with an ivory-gold core. Real healing raises green wisps and damage throws a small star. Blessed Hammer spins on its existing curve and sheds gold chips on damaging contact. Both have a separate terminal effect. Fist of the Heavens warns at the target's feet and drops lightning at the target's live position. The saved position flashes and emits silver-blue bolts, which share Holy Bolt's damage and fade effects. [Holy Bolt and secondary bolt sheet and prompt](../art/spell-effects/paladin-holy-bolt) · [Hammer and sky strike sheet and prompt](../art/spell-effects/paladin-hammer-heavens)

Muted ClassicUO checks covered Holy Bolt healing an ally and piercing two undead, the full-health case, and both wall and natural expiry for the two projectile types. Blessed Hammer dealt damage twice along the same flight before it dissipated. Fist of the Heavens followed a moving primary target while its bolts started at the saved position. When the primary died during the warning, the corpse kept the secondary burst live. Each cast paid mana once. ClassicUO decoded ten new Sonniss cues. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Resist Fire circles the recipient in copper shields, Resist Cold uses ice shields, and Resist Lightning uses gold forks with violet sparks. Salvation forms a wider ivory ring with jade edging. The four ring sizes let them appear together when their resistance bonuses stack. Each ring follows its recipient for the applied lease. Cues play on new applications, with one nearby cue per source on simultaneous applications. Ordinary renewals are quiet. [Shield-ring sprites and prompt](../art/spell-effects/paladin-resistance-wards)

Muted ClassicUO checks covered all four wards on player and pet recipients, native fire mitigation, out-of-range expiry, and all four rings active at once. A weaker caster left the stronger Fire lease in place. An NPC Fire aura replaced the Paladin ward on the pet, and the next valid Paladin pulse restored it. A timed Salvation provider cleared its own ward on removal, and the issued ally wards lasted through their scheduled deadline. ClassicUO decoded all four new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Prayer gives active recipients a small copper loop and raises amber ribbons on actual life gain. Cleansing uses violet filaments when it shortens poison or a curable curse. Cleansing and Meditation share Prayer's ribbons for their healing synergy. Meditation's blue ring follows player recipients, and a blue spark marks a real native mana gain. Redemption circles its caster in ochre, then flashes at each claimed corpse and shows a brief jade thread to the caster when life or mana is restored. Separate red and blue flashes show which resources rose. [Recovery aura sheet and prompt](../art/spell-effects/paladin-recovery-auras) · [Redemption sheet and prompt](../art/spell-effects/paladin-redemption)

Muted ClassicUO checks covered Prayer's first-pulse delay, caster, ally and pet healing, a single payment, and quiet pulses at full life or with insufficient mana. Cleansing shortened both poison and a curable curse on the affected ally. Meditation applied to players and marked two real mana gains each on the caster and ally. Redemption kept the original chance and corpse eligibility checks, retained loot, gave a corpse-only flash with full pools, and gave just a blue receipt for mana-only recovery. ClassicUO decoded all five new Sonniss cues. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Might raises copper pennants. Blessed Aim marks its active accuracy aura with teal arrowheads. Concentration uses amethyst standards and flashes a separate brace on successful interruption prevention. Fanaticism moves its crimson pennants faster. The four designs follow affected players and pets and use distinct widths so their circles can stack. [Aura sprites and prompt](../art/spell-effects/paladin-offense-auras) · [Concentration guard sprites and prompt](../art/spell-effects/paladin-concentration)

Muted ClassicUO checks covered pet weapon damage under Might, attack rating under Blessed Aim and its inactive passive, Concentration's Hammer bonus and both sides of its interruption roll, and Fanaticism's zero-mana caster and party bonuses. A pending attack kept its original timing through Fanaticism activation. The recipient effects followed movement, expired after a range exit, and rendered together under four providers. A bound demon granted the same Concentration banners and stopped renewing them when parked. ClassicUO decoded all five new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Defiance circles active recipients with steel-blue crenellations, Vigor moves a loop of mint leaves, and Thorns uses plum hooks. The Thorns burst rises at the attacker when returned damage lands. Holy Shield gathers gold light into paired braces, flashes on a successful block, and breaks apart at timed expiry. The braces disappear while the shield is stowed and return on re-equip for the remaining duration. [Defensive aura sprites and prompt](../art/spell-effects/paladin-defense-auras) · [Holy Shield sprites and prompt](../art/spell-effects/holy-shield)

Muted ClassicUO checks covered all 32 frames, allied range expiry, Defiance's defense bonus, and Vigor's stamina and pet speed bonuses. Thorns showed the original first-pulse delay and the flat damage on a native melee miss. A successful physical attack returned both the flat and percentage damage. Ranged attacks and physical immunity left the return burst quiet. Holy Shield paid mana once, flashed on a successful shield roll, and kept the original buff deadline through stowing and re-equipping. ClassicUO decoded seven new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Sanctuary circles the Paladin in ivory lanterns and sends a blue-white wave out to the active pulse radius. A small star strikes undead when they take damage or receive a knockback. Landed undead weapon hits share the star with their own cue. Conviction circles the caster in garnet and brass. When it applies or replaces a weaker debuff, a ruby flash settles into an open mark that follows the affected enemy for its remaining duration. [Sanctuary sprites and prompt](../art/spell-effects/paladin-sanctuary) · [Conviction sprites and prompt](../art/spell-effects/paladin-conviction)

Muted ClassicUO checks covered all 24 frames, the delayed first pulses, an actual 2.5-tile Sanctuary wave, zero-mana undead damage and knockback, and physical-immunity bypass on a landed weapon hit. The pulse left allies, living monsters and a magic-immune target without a knockback rule unaffected. Conviction reduced enemy defense and resistance, rejected a lower-rank application, and flashed again when a stronger pulse replaced the debuff. The marks kept their scheduled expiry through movement and aura deselection. ClassicUO decoded all five Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Sacrifice uses a garnet swing arc, red ribbons for the actual life payment, and a separate slash at the target. Smite uses an ivory shield sweep and a steel-and-gold impact. Its existing stun marker follows the target through the timed knockback. Vengeance opens with a braided swing, then splits into amber fire, blue frost and violet lightning on contact. Applied cold uses the shared chill marker. [Sacrifice sprites and prompt](../art/spell-effects/paladin-sacrifice) · [Smite sprites and prompt](../art/spell-effects/paladin-smite) · [Vengeance sprites and prompt](../art/spell-effects/paladin-vengeance)

Muted ClassicUO checks covered all 28 new frames. Sacrifice paid fractional life on a damaging hit and omitted the payment effect on a miss or against physical immunity. A lethal payment consumed the action before the blow. Smite stunned a physically immune target, and its marker followed the native knockback before expiring. Vengeance applied three elemental damage components and cold with one mana debit. Both Sacrifice and Vengeance played only their swing on a miss. ClassicUO decoded seven Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Zeal uses a short bronze arc for each swing and a separate spark at each landed target. Charge flares teal on launch and leaves an afterimage where a completed step started. Landing a hit or triggering a mapped knockback produces the impact. Conversion begins with a jade swing. A successful conversion ties a knot on the creature, and the knot breaks when allegiance returns to normal. A failed conversion uses the shared bronze spark if its ordinary blow lands. [Zeal sprites and prompt](../art/spell-effects/paladin-zeal) · [Charge sprites and prompt](../art/spell-effects/paladin-charge) · [Conversion sprites and prompt](../art/spell-effects/paladin-conversion)

Muted ClassicUO checks covered all 36 frames, five Zeal swings split across two targets, one miss, and the original single mana payment. Charge stopped at an intervening obstacle and left afterimages only on completed steps. A Charge miss still flashed when it displaced a knockback-eligible skeleton. Low-mana Zeal and adjacent Charge used their ordinary attack presentation. Conversion succeeded despite a missed weapon roll, preserved the creature's life, and moved its mark with the creature until the natural allegiance expiry. Its failed conversion delivered the ordinary contact. ClassicUO decoded all eight Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Howl sends a steel-blue wave and marks enemies while they flee. Taunt opens with a copper flare and keeps a rose mark on the enemy while its penalties remain. Battle Cry follows the live rays with amber wave fronts and marks enemies with a broken shield until their penalties expire. War Cry uses an ivory-blue wave and a separate impact at each qualified contact. Its stun uses the shared attached mark. [Howl sprites and prompt](../art/spell-effects/barbarian-howl) · [Taunt sprites and prompt](../art/spell-effects/barbarian-taunt) · [Battle Cry sprites and prompt](../art/spell-effects/barbarian-battle-cry) · [War Cry sprites and prompt](../art/spell-effects/barbarian-war-cry)

Muted ClassicUO checks covered all 128 frames, retreat, approach, Taunt replacement, and natural expiry for Howl and Battle Cry. Howl's wave stopped at the test wall. War Cry still stunned through physical immunity, and a successful shield block prevented damage and stun while showing the existing block effect. ClassicUO decoded twelve Sonniss edits for male and female casts and applied effects. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Shout uses steel-blue shield braces for defense. Battle Orders uses red, blue, and green lights for life, mana, and stamina. Battle Command uses gold and lilac chevrons for the skill-rank bonus. Each release sends a wave along the live rays. The buff effects can stack on the caster, allies, and pets, and each fades when its own buff ends. [Shout sprites and prompt](../art/spell-effects/barbarian-shout) · [Battle Orders sprites and prompt](../art/spell-effects/barbarian-battle-orders) · [Battle Command sprites and prompt](../art/spell-effects/barbarian-battle-command)

Muted ClassicUO checks covered all 132 frames, an immediate self-buff, travelling applications to an ally and pet, one mana debit per cast, and the original defense, resource, and effective-rank changes. The three effects followed the ally and faded independently as each buff expired. A test wall blocked Shout's wave and its ally application. ClassicUO decoded ten Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Find Potion uses a green search sweep and flashes a vial when a potion is added. Find Item uses an amber sweep and flashes a gold gleam when loot is added. Empty searches leave a short dust effect. Grim Ward raises one of three sizes of bone totem, sends a violet pulse across the active area, and marks affected enemies with thorns. At expiry the totem falls, and the curse marker clears when its own effect ends. [Find Potion sprites and prompt](../art/spell-effects/barbarian-find-potion) · [Find Item sprites and prompt](../art/spell-effects/barbarian-find-item) · [Grim Ward sprites and prompts](../art/spell-effects/barbarian-grim-ward)

Muted ClassicUO checks covered all 76 new frames, successful and empty searches, interrupted casts, retained existing loot, and one mana payment per attempt. All three totem sizes rose and collapsed at the ward's accepted site. The native marker kept its original art ID and collision. Grim Ward's mark followed an affected enemy and cleared separately from the totem. ClassicUO decoded nine new Sonniss edits and the shared curse fade cue. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Bash has an amber swing and impact, with a dust skid at completed knockback steps. Stun has a steel-blue impact and a spiral when the target is stunned, followed by the shared stun marker for the status duration. Concentrate frames the caster with jade braces for its defense window. Berserk surrounds the caster with a violet ring for its zero-defense state, bursts into shards on magic contact, and breaks away when defense returns. [Bash sprites and prompt](../art/spell-effects/barbarian-bash) · [Stun sprites and prompt](../art/spell-effects/barbarian-stun) · [Concentrate sprites and prompt](../art/spell-effects/barbarian-concentrate) · [Berserk sprites and prompt](../art/spell-effects/barbarian-berserk)

Muted ClassicUO checks covered 52 new frames, Bash against open ground and a wall, Stun on a landed hit and a shield block, and Stun's low-mana fallback. Concentrate cleared on recovery and invalidation, and its native poses and effects rendered on male and female Humans, Elves, and flying Gargoyles. Berserk applied zero defense and retained its deadline after a miss. ClassicUO decoded twelve new Sonniss edits and the shared fade cue. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Leap throws sand at takeoff, traces the flight in eight directions, and spreads a dust ring over the landing's knockback radius. Interrupted flight uses a small dust settle. Leap Attack adds a copper landing ring, debris at enemies hit by the area damage, and a separate sweep and spark for the later weapon strike. Whirlwind's blue ring follows the caster, leaving a wake at completed steps and sparks on landed hits. The last reached tile gets a short fade. [Leap sprites and prompt](../art/spell-effects/barbarian-leap) · [Leap Attack sprites and prompt](../art/spell-effects/barbarian-leap-attack) · [Whirlwind sprites and prompt](../art/spell-effects/barbarian-whirlwind)

Muted ClassicUO checks covered 76 frames, every Leap heading, traversal over a walking obstacle, zero-damage knockback, and an interrupted flight. Leap Attack rendered the 3.5-tile landing area and the later weapon swing, with separate hit and miss cases. Whirlwind hit multiple enemies and stopped its wake and spin at a wall. Server checks also passed across two reloads. ClassicUO decoded ten new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Double Swing has opposing gold and steel sweeps and a separate contact flash for each hand. Frenzy has copper sweeps and sparks. Its speed charges light a low ring that follows the caster and fades when the speed bonus expires. Double Throw shows the weapon launched by each hand. Knives and axes spin, while the three polearm silhouettes point along the flight. A small spark marks the actual target or terrain contact. [Double Swing sprites and prompt](../art/spell-effects/barbarian-double-swing) · [Frenzy sprites and prompt](../art/spell-effects/barbarian-frenzy) · [Double Throw sprites and prompts](../art/spell-effects/barbarian-double-throw)

Muted ClassicUO checks covered 152 frames, two Double Swing targets, misses, the ordinary low-mana fallback, and both genders across Human, Elf, and Gargoyle bodies. Frenzy's ring moved with the caster and faded as the speed bonus ended. Equipped knives, axes, and glaives rendered through the learned Double Throw, including direct contact and terrain obstruction. The javelin and pilum source rows were exercised as captured flights because their ordinary Barbarian equipment paths are unavailable. All three polearm silhouettes rendered in eight directions. Gameplay checks passed across two server reloads. ClassicUO decoded thirteen new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Tiger Strike, Cobra Strike, and the four elemental builders have distinct claw sweeps, contact flashes, and orbiting motes. The motes show the actual number of charges and follow the caster until spent or expired. Cobra Strike shares the recovery ribbons when the common finisher restores life or mana. Fists of Fire opens a flame ring and lights its accepted scatter tiles. Claws of Thunder pairs its nova ring with moving rays and charged bolts. Blades of Ice scatters crystals along the accepted ground cells. Phoenix Strike spends its first tier on a meteor, second on chain lightning, and third on chaos ice. Its meteor reuses the existing warning, descent, and impact graphics. [ImageGen sprite sheets and prompts](../art/spell-effects/assassin-martial-arts)

Muted ClassicUO checks covered 126 frames, all three charge counts for every family, actual misses, both claw releases, a Dragon Talon charge expenditure, and the active motes following movement and clearing on expiry. The learned builders were exercised across male and female Human, Elf, and Gargoyle bodies. Normal weapon finishers exercised the area, field, crystal, projectile, and recovery branches, including all three Phoenix Strike tiers. Native mechanics checks for the builders, their payloads, and all four Dragon finishers passed across two reloads. ClassicUO decoded seventeen new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Dragon Talon has amber kicks, contact sparks, and dust on completed final knockback. Dragon Claw uses lilac and blue sweeps for its two hands, with separate flashes on landed strikes. Dragon Tail gives the direct kick a copper sweep and marks the actual fire radius with an expanding ring. Embers mark affected enemies and follow completed displacement. Dragon Flight closes indigo ribbons at departure and opens a blue ring at the accepted landing. The kick follows on its own event. [Dragon Talon sprites and prompt](../art/spell-effects/assassin-dragon-talon) · [Dragon Claw sprites and prompt](../art/spell-effects/assassin-dragon-claw) · [Dragon Tail sprites and prompt](../art/spell-effects/assassin-dragon-tail) · [Dragon Flight sprites and prompt](../art/spell-effects/assassin-dragon-flight)

Muted ClassicUO checks covered all 204 frames, all eight directions, both genders on Human, Elf and Gargoyle bodies, four kicks against one target, and shield deflection. Talon's dust stopped at blocked terrain. Tail marked the live fire radius and affected enemies. Flight showed the accepted destination before the kick, with separate miss and interrupted-transfer cases. The two Claw hands spent their individual charges and rendered actual Cobra recovery. Mechanics and save checks passed across two server reloads. ClassicUO decoded sixteen new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Blade Sentinel emerges as a teal steel construct and spins at the retained fractional position of its summon. It flashes on a landed cut and breaks into flecks when it retires. Blade Fury glints at each emitted shot, tumbles in the actual direction of travel, and finishes with a small contact flash, shield deflection, or spent fragment. Blade Shield raises a sparse silver orbit when it activates. Small slashes mark confirmed periodic hits and the orbit disperses when the state expires. The Shadow variants share the same art and cues. [Blade Sentinel art and prompt](../art/spell-effects/assassin-blade-sentinel) · [Blade Fury art and prompt](../art/spell-effects/assassin-blade-fury) · [Blade Shield art and prompt](../art/spell-effects/assassin-blade-shield)

Muted ClassicUO checks covered 76 frames, paid Sentinel emergence and movement through its original collision path, a landed cut and natural retirement, and cancellation before a summon. Blade Fury rendered in all eight directions and preserved the three-shot cadence, with separate hit, miss, block, range expiry and early release cases. Shield attachment and refresh were checked on both genders across Human, Elf and Gargoyle bodies. The tests also exercised periodic damage, deflection, cancellation and expiration. The native mechanics and save/reload checks covered the learned skills and the Shadow Fury and Shield routes. ClassicUO decoded nine new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Burst of Speed uses low copper wisps. Fade uses a violet veil and the existing translucent body, and replaces the speed wisps when it takes over. Venom adds jade curls and a small coating mark at hand height. Delivered poison shares the existing poison mark and clear effects. Cloak of Shadows sends a dark wave across its configured radius and marks the caster's mantle, blinded enemies, and defense-only recipients. Psychic Hammer flashes at release and lands a vertical violet strike. Mind Blast shows its ground area, contacts, and the shared stun or allegiance effects for the actual outcome. [Speed and Fade art and prompts](../art/spell-effects/assassin-speed-fade) · [Venom art and prompt](../art/spell-effects/assassin-venom) · [Cloak art and prompt](../art/spell-effects/assassin-cloak-shadows) · [Psychic force art and prompt](../art/spell-effects/assassin-psychic-force)

Muted ClassicUO checks covered the 76 new frames, speed wisps following accepted movement, Fade replacing Speed and restoring opacity on expiry, and both genders across Human, Elf and Gargoyle bodies. Venom coated the caster and a real weapon hit poisoned its target. Cloak showed separate blindness and defense-only marks, excluded an out-of-range target, rejected a recast while targets remained marked, and cleared its marks at expiry. Hammer used the contact tile and displaced its victim on a committed cast. Mind Blast rendered on empty ground and showed separate area stun and conversion branches. Native gameplay and reload checks covered all six learned skills. ClassicUO decoded twelve additional Sonniss edits with playback disabled. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Fire Blast follows the original lob with a small tumbling ember. The fuse glows only after a valid landing and bursts over the actual 2.5-tile damage radius. Damaged targets share the existing Fire Bolt sparks. Shock Web throws blue shards and draws a separate lattice at each native half-tile center. Accepted hits send a short upward arc. Spent and newly obstructed nodes unravel where they stood. Both Shadow routes use the same art and cues. [Fire Blast art and prompt](../art/spell-effects/assassin-fire-blast) · [Shock Web art and prompt](../art/spell-effects/assassin-shock-web)

Muted ClassicUO checks rendered all 36 new frames and decoded seven Sonniss edits. Fire Blast retained its original fuse, hit the selected inner targets, spared the target beyond radius, and stopped at terrain. A cancelled throw produced no effects. The common throw presentation was checked on both genders across Human, Elf and Gargoyle bodies. The Shock Web check showed two simultaneous nodes whose half-tile centers round to the same UO tile, confirmed the one-second hit delay, and exercised natural expiry and selective retirement when a wall obstructed one tile. Native checks covered both learned and Shadow skills across a save and reload. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Wake of Fire and Wake of Inferno emerge as a low bronze brazier and a taller iron burner. One spreads fire across the ground, and the other shoots narrow bursts. Charged Bolt Sentry sends blue and gold forks along jagged paths. Lightning Sentry and Death Sentry share the original Lightning streak and have distinct models. Death Sentry's ground ring expands from the consumed corpse to the real blast radius. The five devices collapse on their existing tiles. Shadow casts share these effects. [Fire sentry art and prompts](../art/spell-effects/assassin-fire-sentries) · [Lightning sentry art and prompts](../art/spell-effects/assassin-lightning-sentries) · [Sentry attack art and prompts](../art/spell-effects/assassin-sentry-attacks)

Muted ClassicUO checks exercised learned placement, automatic attacks, contact, both Death Sentry decisions, and the replacement collapse in all five skills. Each cast debited mana once. The actors stayed on the same tiles and remained passable. ClassicUO decoded eight new Sonniss edits with playback disabled. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Shadow Warrior arrives in blue-violet wisps. Shadow Master emerges through a magenta threshold with gold sparks. Their veils follow the native humanoid bodies, and the departure effects play on replacement, parking, or death. A Shadow returning from a protected area plays its arrival once at the accepted return tile. The modifier death nova shares the existing Nova graphics and cue at its delayed release. [Shadow Warrior art and prompt](../art/spell-effects/shadow-warrior) · [Shadow Master art and prompt](../art/spell-effects/shadow-master)

Muted ClassicUO checks rendered all 32 new frames and decoded six new Sonniss edits. The tests covered the accepted placements, follow movement, a protected-area return on the same actor, replacement, cancelled casting, and death nova delay and damage on a supported target. Both spells used their native casting poses on male and female Human, Elf, and Gargoyle bodies. Native save and reload checks exercised the learned summon transaction and pending and emitted death novas. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

The Warlock's Goatman rises from rust-red rubble, throws sparks on melee contact, and shares the existing Berserk effect while that state is active. Tainted keeps its approved Succubus sprites and red flare. Embers mark its arrival, its fire resistance aura, and its departure. The Defiler retains its native Gazer and marks each accepted Health Link recipient. A separate impact flashes when damage transfers, and the marks clear when the link ends. [Goatman art and prompt](../art/spell-effects/warlock-goatman) · [Tainted art and prompt](../art/spell-effects/warlock-tainted) · [Defiler art and prompt](../art/spell-effects/warlock-defiler)

Muted ClassicUO checks rendered all 52 new frames and decoded twelve Sonniss edits. The tests covered one-time mana payment, shared-cap replacement, a protected-area return with the original Tainted, Goatman melee and Berserk, the Tainted's red flare and resistance lease, typed damage sharing between two Defiler marks, and marker cleanup after the Defiler died. All three casts were checked on male and female Humans, Elves, and Gargoyles. Native summon and Health Link checks passed across a save and reload. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Death Mark opens violet tears where demons depart and land, and marks the target for the duration of the debuff. Blood Boil blooms around each donor and draws a ring at the actual damage radius. Engorge draws crimson threads from the corpse to each affected demon and leaves a ward while the buff lasts. Consume dissolves the selected demon and transfers a ward to the caster until the sacrifice slot clears. Bind Demon sends indigo chains on paid attempts and seals the original creature on success. [Death Mark](../art/spell-effects/warlock-death-mark) · [Blood Boil](../art/spell-effects/warlock-blood-boil) · [Engorge](../art/spell-effects/warlock-engorge) · [Consume](../art/spell-effects/warlock-consume) · [Bind Demon](../art/spell-effects/warlock-bind-demon)

Muted ClassicUO checks covered Death Mark's relocation and expiry, Blood Boil's two donor centers and damage radius, Engorge healing two wounded demons from one corpse, and Consume clearing the ward when the shared cap replaces its slot. Bind Demon exercised a free interruption, a paid failure, and a later successful bind on the same creature. Separate native save-and-reload checks passed for all five skills, including Bind Demon holds through the right mouse and quick-slot routes. ClassicUO decoded fifteen new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Ring of Fire opens in a low ember halo and spreads along up to sixteen of its native rays. Each sampled ray blooms where it bursts. Flame Wave reveals its width, rolls along clear lanes, and leaves low flames in actual burn cells. A small flash marks each damaging front contact. Apocalypse marks its damage radius for two seconds, then erupts from the warning center. Cinders flash on damaged enemies. [Ring of Fire sheet and prompt](../art/spell-effects/warlock-ring-of-fire) · [Flame Wave sheet and prompt](../art/spell-effects/warlock-flame-wave) · [Apocalypse sheet and prompt](../art/spell-effects/warlock-apocalypse)

Muted ClassicUO checks drew all 44 new frames and decoded eight Sonniss edits. The tests covered a cancelled Ring, fractional ray positions, Flame Wave's 680 ms launch delay, one blocked lane with the other twelve still active, lingering fire expiry, and Apocalypse's two-second warning at its 6.5-tile test radius. Each cast paid mana once and spared an outside target. Native save-and-reload checks passed for all three skills. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Lethargy draws a teal boundary and attaches an hourglass to each slowed target. Rancor traces its radius in bronze and adds a triangle to accepted recipients. A separate gold curl flashes when Rancor's confusion takes effect. Death uses magenta fangs and a bone-colored mark. The burst erupts from the consumed corpse, spans the native four-tile radius, and flashes where it damages an enemy. [Lethargy sheet and prompt](../art/spell-effects/warlock-sigil-lethargy) · [Rancor sheet and prompt](../art/spell-effects/warlock-sigil-rancor) · [Death sheet and prompt](../art/spell-effects/warlock-sigil-death)

Muted ClassicUO checks covered all 64 frames, all three ground radii, a moving Lethargy mark and its expiry after leaving the area, shared-slot replacement, Rancor's primary effect and confusion precedence, and Death's corpse consumption with retained loot. The final sigil and its target marks expired on their native clocks. Native save-and-reload checks passed for all three sigils. ClassicUO decoded eight new Sonniss cues. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Miasma Bolt follows its fractional path in indigo and bursts at the native contact point. Low violet wisps mark the resulting magic clouds. Clouds at the same half-tile can share one sprite. Miasma Chain sends out a clasp and draws bright links along each accepted half-second span. Softer ground wisps remain along the frozen paths until their clouds expire. The spells share a small flash where an enemy loses life. [Miasma Bolt sheet and prompt](../art/spell-effects/warlock-miasma-bolt) · [Miasma Chain sheet and prompt](../art/spell-effects/warlock-miasma-chain)

Muted ClassicUO checks covered a cancelled Bolt, direct and cloud damage, cloud expiry, and a wall impact that spared the enemy behind it. Chain remained attached to the original enemy as both endpoints moved. A later wall cut the link, and its previous clouds stayed on their original paths and expired independently. Native tests passed for both skills across a save and reload. ClassicUO decoded seven new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Bane, Purge, and Siphon coat the equipped weapon in violet thorns, copper crescents, and teal leaves. The glows follow the weapon's native animated sprite. Bane and Siphon still mark targets on eligible melee misses. Bane adds a separate flash on landed contact. Purge's charged triangle clears at depletion and its blast shows the actual two-tile radius. Siphon's mint life ribbon and blue mana ribbon appear independently when a kill restores those resources. [Bane sheet and prompt](../art/spell-effects/warlock-hex-bane) · [Purge sheet and prompt](../art/spell-effects/warlock-hex-purge) · [Siphon sheet and prompt](../art/spell-effects/warlock-hex-siphon)

Muted ClassicUO drew all 72 frames and checked the weapon glow on male and female Humans, Elves, and flying Gargoyles. The coat cleared when unequipped and reappeared on a qualifying two-handed weapon. The tests covered shared-group replacement, Bane's miss and landed branches, successful and failed Purge rolls and charge depletion, and all four combinations of full or missing life and mana for Siphon. All three coats cleared at the shortened deadlines used in the test shard. Native save-and-reload checks passed for all three skills and their shared group. ClassicUO decoded ten new Sonniss edits. Listening is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Charged Bolt skitters along its existing paths, Lightning travels in a straight streak, and Chain Lightning sends violet arcs between targets. Static Field expands along the ground and Thunder Storm flashes above the caster at release and on its recurring strike schedule. The player casts were checked in ClassicUO with audio disabled. Nine Sonniss edits cover their cast and impact cues. Each cue has passed format and level checks and awaits a listening pass. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Ice Bolt travels as a thin shard, Ice Blast as a compact comet, and Glacial Spike as a heavier crystal with an area burst. Frost Nova expands across the ground. The shared cold overlays follow the affected target and clear with the game status. Thaw and cold corpse shatter reuse the Ice Blast fragments. The player effects were checked in ClassicUO with audio disabled. Eight Sonniss edits cover releases, impacts, and the shared cold corpse shatter. Their listening pass is queued. [Cold status sprite source](../art/spell-effects/cold-status/sheet.png) · [Cue recipes](../art/spell-effects/sonniss-cues.json)

Fire Bolt is a narrow ember dart, and Inferno is a flame stream that tracks held aim. Blaze puts low flames behind accepted movement, while Fire Wall rises and spreads from its center. The fields animate on their existing burn tiles and fade at expiry. The muted ClassicUO checks covered Fire Bolt contact, Inferno steering and release, and field damage and expiry. Six Sonniss edits passed format and level checks and await listening. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Frozen Armor, Shiver Armor, and Chilling Armor use blue, violet, and teal wards. Energy Shield adds a separate amber shell that sparks on the side taking damage. The wards follow the active buffs and fade at expiry. Chilling Armor shares the Ice Bolt flight and contact art. Muted ClassicUO checks covered reactions, expiry, and overlapping wards on male and female Human, Elf, and Gargoyle bodies, including Gargoyle flight. Nine Sonniss edits passed format and level checks and await listening. [ImageGen sheets and prompts](../art/spell-effects) · [Cue recipes](../art/spell-effects/sonniss-cues.json)

Nova sends an electric ring across the floor and flashes on damaging contact. Telekinesis uses a caster flash and shows violet force around enemies or on the tile of a remote interaction. Teleport leaves a closing crease at the origin and opens a matching one where the caster lands. Muted ClassicUO checks covered Nova's reach, Telekinesis damage and displacement, remote gold pickup and door use, and both learned and scroll Teleport. Four Sonniss edits passed format and level checks and await listening. [Nova](../art/spell-effects/nova/sheet.png) · [Telekinesis](../art/spell-effects/telekinesis/sheet.png) · [Teleport](../art/spell-effects/teleport/sheet.png)

Enchant uses copper runes at application, a pair of active embers, and a brief slash of sparks on landed contact. Hydra's three heads emerge, aim, and fire on their existing clocks. The head sprites stay distinct even when the emitters occupy the same native tile. Muted ClassicUO checks covered self and ally Enchant, its bonus damage and expiry, and Hydra's three firing heads, hit damage, nonblocking tiles, and retirement. Six Sonniss edits passed format and level checks and await listening. [Enchant](../art/spell-effects/enchant/sheet.png) · [Hydra](../art/spell-effects/hydra/sheet.png) · [Hydra bolt](../art/spell-effects/hydra-bolt/sheet.png)

The muted client decoded the new Sonniss cues for Blizzard, Frozen Orb, Meteor, Teeth, Bone Spirit, and Abyss at their existing events. The Meteor descent cue starts with the falling rock, about 400 ms before impact. The Fire Mage shares Meteor's falling stone, impact, and ground fire. Frozen Orb shares the Ice Bolt contact cue, and Teeth shares the bone-chip impact. The listening pass is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

For the later GM test arena, Green Acres is available on Trammel and Felucca at `5445, 1153, 0`. The requested setup gives the GM every class at max level with all spells available, unlimited mana, and targets with unlimited health and AI disabled. Test characters and targets should be isolated from the normal world. [Trammel location](../upstream/ModernUO/Distribution/Data/Locations/trammel.json#L189)

### Player animation coverage

Verify every custom player-body animation in ClassicUO on all six living player bodies. This includes passive reactions such as Amazon Dodge and Avoid. Check the directions the action supports and confirm that equipment stays aligned. Check airborne Gargoyles for actions they can use in flight. Keep the existing mechanical timing and collision for each action.

Magic Arrow uses violet flight and contact effects, and Guided Arrow uses gold sprites that turn with the projectile. Multiple Shot shares Strafe’s directional arrows, bolts, and physical contact burst. All four use native bow and crossbow poses. [Arcane arrow sprite source](../art/spell-effects/amazon-arcane-arrows/sheet.png) · [Contact sprite source](../art/spell-effects/amazon-arrow-impact/sheet.png) · [Strafe sprite source](../art/spell-effects/strafe/sheet.png)

Muted ClassicUO tests covered both weapons, distinct arrow and bolt art, the nine-projectile rank 8 Multiple Shot fan, and Guided Arrow turning to hit a moving target. Rank 12 Strafe turned between two targets and released five shots over 0.48 seconds with a bow and 1.00 second with a heavy crossbow. The client decoded all seven Sonniss cues at the release or contact events. The listening pass is queued. [Strafe firing sequence](../src/Sanctuary.Content/AmazonStrafe.cs) · [Cue recipes](../art/spell-effects/sonniss-cues.json)

The Amazon’s six elemental shots have distinct bow and crossbow flight sprites. Fire Arrow and Cold Arrow flash on a landed contact. Ice Arrow freezes a single target, while Freezing Arrow bursts and freezes targets in its existing area. Exploding Arrow bursts at its area damage event. Immolation Arrow ignites its existing ground cells and fades when they expire. The custom fire and ice cues use the Sonniss recordings, with two new edits for the fire bursts. [Fire flight](../art/spell-effects/amazon-fire-flight/sheet.png) · [Fire impact](../art/spell-effects/amazon-fire-impact/sheet.png) · [Frost flight](../art/spell-effects/amazon-cold-flight/sheet.png) · [Frost impact](../art/spell-effects/amazon-cold-impact/sheet.png) · [Cue recipes](../art/spell-effects/sonniss-cues.json)

Muted ClassicUO checks covered all six elemental shots on a bow and heavy crossbow. They verified chill, freeze and thaw, and all 29 Immolation damage cells through ground fire expiry. Exploding, Freezing, and Immolation Arrow also displayed their area effects when the parent shot missed its attack roll. The client decoded the release, impact, and field cues. Listening remains queued.

Lightning Bolt and Lightning Fury use distinct electrified javelins. Fury flashes where the secondary bolts originate and shares Sanctuary’s small charged bolt sprites and impact sparks. Poison Javelin leaves a green trail along the throw. Plague Javelin bursts into an ochre cloud that follows its moving damage cells. Both clouds clear when their cells expire. The native throwing pose remains in use. [Javelin ImageGen sheet](../art/spell-effects/javelin-flight/sheet.png) · [Venom ImageGen sheet](../art/spell-effects/javelin-venom/sheet.png)

In muted ClassicUO, Lightning Bolt hit its selected target and Fury sent secondary bolts through three targets. Poison Javelin’s trail poisoned a target that entered after the javelin landed. All 23 Plague Javelin cloud emitters spawned and expired on both a direct hit and a missed attack roll. The client decoded the throw, impact, burst, and cloud cues at their associated effects. The listening pass is queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Jab flashes on each landed thrust and Fend shows a silver crescent at each successive target. Impale uses a heavier amber impact and a copper marker that follows its target for the slow’s 12-second duration. Power Strike has a cobalt impact. Charged Strike and Lightning Strike share the Sorceress’s bolt and chain art and cue their secondary release where it begins. These skills use their existing weapon poses. [Thrust ImageGen sheet](../art/spell-effects/amazon-thrust-effects/sheet.png) · [Impale slow ImageGen sheet](../art/spell-effects/impale-slow/sheet.png)

Muted ClassicUO checks covered all six melee skills with a spear and a javelin. Jab played three thrust cues on a missed sequence and only showed impact flashes on landed thrusts. Fend struck three nearby targets. Impale’s marker followed a moving target and cleared at natural expiry. Charged Strike’s bolts continued after a lethal opening thrust, and Lightning Strike reached nearby targets and gave no chain release cue in a single-target fight. Four new Sonniss thrust and contact edits loaded in the client. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

Inner Sight casts a gold ring and marks affected targets with an eye. Slow Missiles casts a violet ring and marks affected targets with three violet streaks. The two marks can appear together and each clears on its own expiry. Decoy emerges in blue mirror facets, shimmers during its stationary lifetime, and dissolves at expiry. Valkyrie has gold emergence, a persistent aura on her native body, shared spear contact effects, and a dissolve on replacement or death. [Debuff ImageGen sheet](../art/spell-effects/amazon-marks/sheet.png) · [Decoy ImageGen sheet](../art/spell-effects/decoy-mirror/sheet.png) · [Valkyrie ImageGen sheet](../art/spell-effects/valkyrie-gold/sheet.png)

Muted ClassicUO checks covered overlapping marks on a moving enemy and their separate natural expiry at ranks with eight- and twelve-second durations. A newly fired hostile bolt traveled at 68% of its normal speed after Slow Missiles. The Decoy dissolved at its ten-second expiry. The Valkyrie’s aura followed her into combat and retired when she was replaced or killed. The client decoded the six new Sonniss cues. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

| Race | Male body | Female body |
| --- | --- | --- |
| Human | 400 | 401 |
| Elf | 605 | 606 |
| Gargoyle | 666 | 667 |

Dodge shows an amber sweep with a brief backward recoil. Avoid shows a cyan streak with a brief sidestep. The client shifts the native body and its equipment together and uses the same offset for mouse selection. Mounted actors use the flash and cue. Evade shows an olive trail during the native walk cycle. These effects leave collision, movement, recovery and attack timing in the existing gameplay code. [Reaction sprite sheet](../art/spell-effects/amazon-avoidance/sheet.png) · [Amazon avoidance implementation](../src/Sanctuary.Content/AmazonAvoidance.cs)

Muted ClassicUO checks rendered all four frames of each reaction on both genders of Human, Elf and Gargoyle, including both Gargoyles in flight. Standing checks covered all eight facings across the body matrix. Evade used accepted client movement and the failed avoidance rolls produced no reaction. Three Sonniss cues loaded with sound and music disabled. Listening remains queued. [Cue recipes](../art/spell-effects/sonniss-cues.json)

**Warlock combined integration — 2026-11-18 JST.** The [906-result checkpoint](warlock-lifecycle-acceptance-checkpoint.md) adds no final effects/audio or new asset identities. Existing missing assets and identified placeholders remain in this separate register; mechanical qualification does not imply graphical acceptance.

**Druid/Assassin current integration — 2026-11-18 JST.** [Druid](druid-lifecycle-acceptance-checkpoint.md) and [Assassin](assassin-lifecycle-acceptance-checkpoint.md) qualify existing melee,spell,pet,trap,weather,channel and saved-effect mechanics without creating artwork/audio. Their named mechanical placeholders retain the separately listed missing effects and sounds. No imagegen.

**Amazon integration/approved Impale — 2026-11-17 JST.** [Checkpoint](amazon-lifecycle-acceptance-checkpoint.md) adds no artwork/audio. Existing thrust,slow,arrow/javelin travel,poison/lightning/cold/burning fields,Decoy/Valkyrie and shatter effects remain identified mechanical placeholders. Final effects/audio stay separately missing;no imagegen.

**Barbarian lifecycle/input qualification — 2026-11-17 JST.** [Checkpoint](barbarian-lifecycle-acceptance-checkpoint.md) creates no artwork/audio. Shout waves,curse overlays,corpse searches,Leap/Whirlwind movement,throwing projectiles and melee impacts keep identified mechanical placeholders. Final effects/audio remain separately missing;no imagegen.

**Paladin lifecycle/input qualification — 2026-11-17 JST.** [Checkpoint](paladin-lifecycle-acceptance-checkpoint.md) creates no artwork/audio. Verified aura pulses,recipient overlays,projectiles,shields and corpse recovery still use identified mechanical placeholders. Final effects/audio remain separately missing;no imagegen.

**Sorceress lifecycle/input requalification — 2026-11-17 JST.** [Checkpoint](sorceress-lifecycle-acceptance-checkpoint.md) creates no artwork/audio. Existing spell travel,armor/reaction,ground fields,Hydra,Inferno,terrain/contact and shatter placeholders remain mechanically usable;final effects/audio stay separately missing. No imagegen.

**Necromancer lifecycle/input acceptance — 2026-11-17 JST.** [Checkpoint](necromancer-lifecycle-acceptance-checkpoint.md) adds no visual/audio asset. All27 active input routes and three passive mechanical scopes remain separate from final art. Existing summon/projectile/curse/shatter effects and audio are still identified placeholders;no imagegen.

**Approved ordinary-chill shatter rule — 2026-11-17 JST.** [Activation checkpoint](ordinary-cold-corpse-adoption-checkpoint.md). Final shatter visuals/audio remain separately missing;existing corpse body/flag presentation is an identified mechanical placeholder. Loot remains intact. No artwork/imagegen is created.

**Ordinary cold shatter candidate — 2026-11-17 JST.** The [proposed corpse rule](ordinary-cold-corpse-proposal.md) creates no artwork. Shatter visuals/audio remain missing, with existing body/flag presentation an identified mechanical placeholder. Candidate verification is separate from activation and final graphics. No imagegen.

**Mage post-death lifetime — 2026-11-17 JST.** Existing bolt, impact, cold/lightning, Meteor warning and burning-ground effects continue to their original mechanical expiry after Mage death. Final artwork/audio remains missing and the existing visuals are identified placeholders; this lifetime checkpoint creates no assets or imagegen work. [Mechanical evidence](mage-projectile-lifetime-checkpoint.md).

**Saved player proc host — 2026-11-16 JST.** Saving proc providers/streams and changing equipment creates no new visual effect. The existing item-cast sound and Fire Ball travel/explosion placeholders remain on their separate effect/audio entries. [Checkpoint](player-item-proc-host-checkpoint.md).

**Player proc frames — 2026-11-16 JST.** The protected player item frame now emits the shared 0x1E5 item-cast audio placeholder after an accepted source execution. Fire Ball travel/explosion placeholders are reused. Final effects/audio remain in their existing entries; no generated asset is added. [Checkpoint](player-item-proc-frames-checkpoint.md).

**Prepared player item Fire Ball — 2026-11-16 JST.** The prepared cross-class item producer reuses existing Fire Ball travel/explosion placeholders. Item-effect notification/audio remains with the future protected item-cast host; no final asset is added. [Checkpoint](player-item-fireball-checkpoint.md).

**Fire Bolt/mastery originals returned — 2026-11-16 JST.** Bane Ash Fire Bolt reuses the existing projectile/impact placeholder. The three masteries have no active cast; Tal Rasha set visuals retain their existing placeholder mapping. Final effects/audio remain in the separate skill entries; no new artwork is generated. [Checkpoint](sorceress-mastery-grants-checkpoint.md).

**Three Sorceress staffs returned — 2026-11-16 JST.** The three returned Sorceress staffs reuse the existing Nova, Frost Nova, Blaze, Fire Wall, Fire Ball, Chain Lightning, Lightning and Static Field placeholders; Warmth has no new active cast. Final effects/audio remain in their existing skill entries. No new item-specific effect is required. [Checkpoint](sorceress-staff-grants-checkpoint.md).

**Warpspear direct grants — 2026-11-16 JST.** The admitted original staff reuses Telekinesis’s instant-hit/motion and Energy Shield’s cast/buff/absorption placeholders. No distinct item-only effect or new artwork is required by this return;their final visuals/audio remain in the existing spell entries. [Checkpoint](warpspear-grants-checkpoint.md).

**Final four Warlock talents admitted — 2026-11-16 JST.** Blood Oath,Engorge,Blood Boil and Consume now use their approved mechanics. Final Oath transfer cues,Engorge corpse/heal/buff visuals,Blood Boil explosions,and Consume retirement/owner-buff effects and audio remain missing. Existing shared heal/explosion/buff/cast presentation is explicitly a placeholder;mechanical admission does not supply final art. [Current mechanics](warlock-final-talents-checkpoint.md).

**Raised helpers — 2026-11-16 JST.** [Ownership checkpoint](raised-minions-checkpoint.md) reuses the existing Lich raise and undead summon placeholders. Final effects/audio remain in the existing register; this ownership graph requires no distinct new artwork.

**Native corpse consumers — 2026-11-16 JST.** [Native Lich corpse support](native-corpse-checkpoint.md) reuses identified existing corpse/explosion/summon placeholders. Existing final CE/Poison Explosion and summon effects/audio requirements remain; this classification adds no distinct visual asset. Icons remain separately tracked.

**Approved barrier-collapse placeholder — 2026-11-16 JST.** Normal learned Wall/Prison collapse now uses the identified native action4 marker for600ms. Final cast, growth, hit, collapse/removal animations and audio remain needed; collision already releases mechanically at death entry. [Learned checkpoint](bone-barrier-world-cast-checkpoint.md). Talent/hotkey icon requirements remain separately recorded in [icons](skill-icons.md).

**Prepared barrier-collapse placeholder — 2026-11-16 JST.** The pending Wall/Prison world rule uses native action4 for a600ms collapse marker. Final BW death/collapse/removal animation and audio are still needed; collision release is mechanical and does not wait for final artwork. See [candidate](bone-barrier-retirement-checkpoint.md). Talent/hotkey icon requirements remain separately recorded in [icons](skill-icons.md).

Abyss uses custom art and Sonniss cues for its opening, pull pulses, and terminal collapse. The active field remains visible between pull pulses.

Flame Wave needs final revealing-wall708,rolling-front690,short-tail695,static-fire716/240,termination29 and cast/audio assets. Current UO flames and sound are identified placeholders;mechanical lifetimes are independent of final art.

**Chain learned — 2026-11-15 JST.** Mechanical admission retains identified cast0x1E5,endpoint0x37C4 and purple cloud/contact native placeholders. Final travelling head/tether/stamp/contact/expiry effects and matched audio remain required;the endpoint marker is not finished tether art. No imagegen performed. [Checkpoint](miasma-chain-cast-checkpoint.md).

**Prepared complete Chain world — 2026-11-15 JST.** Native cast0x1E5,linked-end0x37C4,cloud0x374A/contact0x375A with purple0x48D are identified placeholders. The endpoint marker is not final tether artwork. Final cast/head/tether/stamp/impact/expiry effects and audio remain required;no imagegen or damaging visual duplicates. [Checkpoint](miasma-chain-world-checkpoint.md).

**Miasma learned — 2026-11-15 JST.** Mechanical admission retains identified cast0x1E5,bolt0x36E4,cloud0x374A/contact0x375A purple-hue native placeholders. Final cast/flight/maker/cloud/contact/expiry effects and audio remain required;no imagegen performed. [Checkpoint](miasma-cast-checkpoint.md).

**Prepared Miasma Bolt world — 2026-11-15 JST.** Cast sound0x1E5,travelling bolt0x36E4,cloud0x374A and contact0x375A with hue0x48D are explicitly identified native placeholders. Final cast/flight/maker/cloud/contact/expiry effects and sound remain needed;magic clouds must not be presented as poison damage. No imagegen performed. [Checkpoint](miasma-bolt-world-checkpoint.md).

**Mirrored learned — 2026-11-15 JST.** Mechanical admission does not complete the mirror rig/weapon-copy,trails,projectile/contact effects or audio. Existing native weapon poses,0xF42projectiles and0x238audio remain explicit placeholders. [Checkpoint](mirrored-cast-checkpoint.md).

**Prepared Mirrored world — 2026-11-15 JST.** Primary UO weapon pose,shared0xF42projectile presentation and0x238sound are placeholders. Final floating weapon/mirror rig,copy attack/trail/impact and matched audio remain needed for melee,bow,crossbow and throw. No damaging visual-only duplicates or imagegen assets are added. [Checkpoint](mirrored-world-checkpoint.md).

**Echoing Strike learned — 2026-11-15 JST.** The approved cast/fan/outward-return/hit services are admitted;0x36E4 moving blades,0x375A contact and0x23B throw audio remain identified placeholders. Final706/720effects/audio are still needed;learned admission does not close this asset entry. [Checkpoint](echoing-strike-cast-checkpoint.md).

**Prepared Echoing world — 2026-11-15 JST.** Native prepared movement/contact uses identified0x36E4blade,0x375Acontact and0x23Bthrow-sound placeholders. Final706outward/return weapon images,visual-only720detonation,cast/hit/recovery/expiry feedback and original audio remain needed.720never creates a second damage actor. No imagegen or final art. [Checkpoint](echoing-strike-world-checkpoint.md).

**Echoing input/missile ownership — 2026-11-15 JST.** Immutable collection emits no VFX or sound. Final server706 Echoing blade,visual-only720 `echoingstrike_detonate`,`warlock_psychic_cast_1`,outbound/return/contact feedback and `Warlock_Skill_Soaring_Strike_Cast_03`/`Warlock_Skill_Echoing_Strike_01`/`Warlock_Skill_Soaring_Strike_Impact_06` audio remain needed. Visual720 must not become a second damage actor. No new placeholder or final artwork is created. [Checkpoint](echoing-strike-inputs-checkpoint.md).

**Approved Eldritch learned input — 2026-11-15 JST.** Learned cast and automatic periodic mechanics now work under the approved rule;existing cast/aura/contact/cosmetic-nova/sound markers remain placeholders. Final VFX/audio listed for state221/missile721 and cast/recovery/expiry remain needed. No new artwork or imagegen. [Checkpoint](eldritch-blast-cast-checkpoint.md).

**Prepared Eldritch Blast world — 2026-11-15 JST.** The pending [rule](eldritch-blast-world-rule-proposal.md) identifies0x375A cast/aura/contact,0x37CC cosmetic nova and0x1E5 audio as placeholders. Final state221,missile721,cast/Hex-explosion/expiry/recovery effects and associated audio remain missing; no final asset or imagegen is introduced. The cosmetic marker cannot cause another hit. [Checkpoint](eldritch-blast-world-checkpoint.md).

**Learned Bind effect boundary — 2026-11-15 JST.** Ordinary held Bind reuses identified human cast pose 17, shared animation packets and existing native/aura presentation. Final tether, progress, success/bound feedback and audio remain missing and nonblocking; no final artwork or imagegen. Native packet tests do not certify graphical rendering. Icons stay separate. [Checkpoint](bind-demon-learned-checkpoint.md).

**Prepared Bind client effect boundary — 2026-11-15 JST.** The held-skill whitelist adds no new artwork, animation or audio. Human cast pose 17 remains an identified server placeholder; final channel/tether/progress/success/bound feedback is missing and nonblocking. No imagegen or graphical acceptance is claimed. Icons stay separate. [Checkpoint](bind-demon-client-checkpoint.md).

**Prepared Bind input effect boundary — 2026-11-15 JST.** Input and attempts now use identified existing human cast pose 17 plus the shared animation packet. This is a placeholder, not D2R SQ6 timing or final art. Channel tether, progress, success/bound effects and audio remain missing and nonblocking. Original native body and shared aura placeholders remain. No artwork/imagegen; icons stay separate. [Checkpoint](bind-demon-input-checkpoint.md).

**Prepared Bind channel effect boundary — 2026-11-15 JST.** Chance and supplied-intent preparation add no final channel, tether, progress, success or bound-state artwork/audio. Successful binding retains the native body and identified shared aura placeholders. Learned action remains mechanically unfinished. Final effects are separately missing and nonblocking; no imagegen. Icons stay in their own register. [Checkpoint](bind-demon-channel-checkpoint.md).

**Bound life-buff effect boundary — 2026-11-15 JST.** Capturing an existing BO/Oak base adds no cast or effect;existing buff presentation remains. Final Bind channel/tether/success/bound feedback and audio remain missing and nonblocking. Underling ownership is a mechanical gap,not an asset gap. No artwork/imagegen;icons remain separate. [Checkpoint](bind-demon-life-checkpoint.md).

**Bound aura effect boundary — 2026-11-15 JST.** Five native providers reuse identified shared0x375A aura/pulse/contact placeholders and existing cold/slow presentation. Final native-source rings,affected-ally feedback,Thorns/Concentration/Fanaticism/Vigor/HolyFreeze audio and Bind channel/success/bound effects remain missing and nonblocking. No artwork/imagegen;mechanical delivery is separate from presentation. Icons remain separate. [Checkpoint](bind-demon-aura-checkpoint.md).

**Zero-direct Spectral effect boundary — 2026-11-15 JST.** Shared native chill/poison placeholders now accompany the mechanically retained zero-direct affix statuses. No new presentation/artwork/imagegen;final contact/status/audio and Bind channel/success/bound effects remain missing and nonblocking. Icons remain separate. [Checkpoint](bind-demon-spectral-zero-checkpoint.md).

**Bound modifier effect boundary — 2026-11-15 JST.** Strong/Fast add no new artwork. Spectral reuses existing native typed contact/chill/poison placeholders and original body presentation;final elemental contact/audio and Bind channel/success/bound feedback remain missing and nonblocking. Aura30delivery is mechanically unfinished,independent of final aura artwork. No imagegen/new assets;icons separate. [Checkpoint](bind-demon-affix-checkpoint.md).

**Bound Death Mark effect boundary — 2026-11-15 JST.** Native relocation reuses the identified shared landing0x3728 plus existing cast/target placeholders. Original creature presentation remains. Final warp/command/mark/expiry/audio and Bind channel/success/bound feedback are missing and nonblocking;no new artwork/imagegen. Icons remain separate. [Checkpoint](bind-demon-death-mark-checkpoint.md).

**Consume aura effect boundary — 2026-11-15 JST.** Four aura recipes are now mechanically delivered through existing identified Paladin pulse/ring placeholders and Consume owner/cast placeholders. Final HolyFire/Meditation/Salvation presentation/audio and Consume/Bind visuals remain missing and nonblocking;no imagegen/new artwork. Provider lifecycle/rank/weapon/mana verification does not certify final effects. Icons remain separate. [Checkpoint](consume-aura-checkpoint.md).

**Bound Consume numeric effect boundary — 2026-11-15 JST.** Actual native retirement/buff reuses identified shared owner0x375A and sound0x1E5 placeholders. Final Consume overlay/cast/sacrifice/audio and Bind channel/success/bound feedback remain missing and nonblocking. Four item-aura recipes still lack mechanical delivery,independent of final aura artwork;do not enable them merely by adding visuals. Icons remain separate;no imagegen. [Checkpoint](bind-demon-consume-checkpoint.md).

**Bound Blood Oath effect boundary — 2026-11-15 JST.** Redirect/native health integration invents no beam,animation or sound. Final passive/transfer/audio presentation and unverified source sound timing remain missing/nonblocking. Original native damage presentation remains;no imagegen or new artwork. Passive tree icon and Bind tree/hotkey icons stay separate. [Checkpoint](bind-demon-oath-checkpoint.md).

**Bound Engorge effect boundary — 2026-11-15 JST.** Actual bound corpse healing/buff uses existing identified shared0x376A heal/buff and0x1E5cast sound placeholders. Final warlockengorge overlay/cast/audio and Bind channel/tether/success/bound feedback remain missing,nonblocking and separate from icons. Original native bodies/abilities retain existing visuals;no imagegen/artwork. [Checkpoint](bind-demon-engorge-checkpoint.md).

**Bound recipient/Blood Boil boundary — 2026-11-15 JST.** Shared eligibility adds no effect. Actual bound Blood Boil reuses identified shared explosion0x36BD and sound0x1E5 placeholders. Final Blood Boil explosion/cast/audio and Bind channel/tether/success/bound feedback remain missing and nonblocking. Original native bodies/abilities retain their visuals;no imagegen/new artwork. Icons stay separate. [Checkpoint](bind-demon-recipients-checkpoint.md).

**Bound native typed damage boundary — 2026-11-15 JST.** Mechanical damage/recovery integration adds no new VFX/audio. Original Imp/native abilities retain existing visuals. Final Bind channel/tether,success/bound feedback and audio remain missing and nonblocking;no imagegen. Full skill admission remains a separate mechanical task. Icons stay in their own register. [Checkpoint](bind-demon-typed-checkpoint.md).

**Prepared bound-host boundary — 2026-11-15 JST.** Original living NPCs retain native body/AI/ability visuals. This host adds no cast/owner-link/bound-state VFX or audio;final Bind Demon channel,success and bound feedback remain missing. No mechanic is gated on new artwork and no imagegen/final effect was created. Source capture/channel and full bind delivery are independently unfinished. Icons remain separately tracked. [Checkpoint](bind-demon-host-checkpoint.md).

**Bind Demon capture boundary — 2026-11-15 JST.** Imported monster data/stat snapshots emit no effect. Final binddemonchannelmaker/channel tether,warlock_shadow_cast_2 channel/success feedback,Warlock_Skill_Bind_Demon_03 and chain audio remain missing,plus bound-state feedback as its world rule is qualified. No VFX placeholder or final artwork is added by this component. Effects/audio stay separate from icons and do not gate numeric implementation. [Checkpoint](bind-demon-suppliers-checkpoint.md).

**Prepared Consume effects — 2026-11-15 JST.** Runtime retirement/buff uses identified shared owner effect0x375A and sound0x1E5. Final warlockconsume overlay,warlock_shadow_cast_2 sacrifice/cast feedback and Warlock_Skill_Consume_03 audio remain missing. Slot/state/stat/save mechanics and town menu are implemented in the prepared candidate;learned gating concerns unverified protected mechanics,not artwork. VFX/audio remain separate from tree/hotkey icons;no imagegen. [Checkpoint](consume-world-checkpoint.md).

**Consume source/capture boundary — 2026-11-15 JST.** Capture-only code emits no effect. Final warlockconsume owner overlay,warlock_shadow_cast_2 cast/sacrifice feedback and Warlock_Skill_Consume_03 audio remain missing. Runtime consumption/buff/pool integration is not yet present;assets stay nonblocking and separate from skill icons. No artwork/imagegen. [Checkpoint](consume-suppliers-checkpoint.md).

**Prepared Blood Boil effects — 2026-11-15 JST.** Uses identified shared explosion0x36BD and cast sound0x1E5 placeholders. Final bloodboilexplode/warlock_fire_cast_1 visuals plus Warlock_Skill_Blood_Boil_Cast_01 and necromancer_corpseexp_1 audio remain missing. All erupting centers emit an explosion, including a center captured before lethal pet cost. No imagegen/final artwork;learned gate is mechanical adaptation,not assets. [Checkpoint](blood-boil-world-checkpoint.md).

**Prepared Engorge effects — 2026-11-15 JST.** Runtime now emits explicitly identified generic cast sound 0x1E5 and recipient heal/buff effect 0x376A. Final engorgecast/engorge/engorgecorpseeffect corpse transfer, recipient buff feedback and Warlock_Skill_Engorge_03 audio remain missing. No final art generated; learned activation waits on the mechanical adaptation decision, not assets. [Checkpoint](engorge-world-checkpoint.md).

**Engorge asset boundary — 2026-11-15 JST.** Numerical capture only; it emits no effect. Still needed: corpse-feeding/transfer, recipient heal/buff feedback and cast audio corresponding to owned engorgecast / engorge / engorgecorpseeffect and Warlock_Skill_Engorge_03 references. Final effects are not generated and no mechanic is gated on them; later runtime placeholders must be identified explicitly. Corpse/recipient delivery itself remains mechanically unfinished. [Checkpoint](engorge-suppliers-checkpoint.md).

**Prepared Blood Oath reaction (2026-11-15 JST).** No transfer beam,cast animation or standalone aura is invented. Native pet life loss/death presentation is reused. Any final passive/transfer/audio cue,including the pinned sound's unverified trigger timing,remains pending separately;mechanics use no final art.

**Demonic Mastery (2026-11-15 JST).** Passive numerical bonuses have no independent cast/event visual. Reuse each summon's separately recorded movement/attack presentation;do not invent a new aura or cast overlay. No asset blocks its existing mechanical consumers.

**Learned Death Mark (2026-11-15 JST).** Actual SC/warp/command/recipient mechanics now work through learned inputs. Cast sound0x1E5,landing0x3728 and mark0x375A remain identified placeholders. Final Warlock cast overlay,pet warp/landing,target mark/expiry and audio are still needed independently of mechanical completion. Native flying fit adds no effect.

**Prepared Death Mark player action (2026-11-15 JST).** Shared SC,cast sound0x1E5,pet landing0x3728 and target mark0x375A are identified placeholders. Actual original demon relocation/command/interruption works;final Warlock cast overlay,pet warp/landing,mark/expiry and audio remain pending. Flying-placement correction adds no new visual. See [checkpoint](death-mark-action-checkpoint.md).

**Learned Summon Defiler — 2026-11-15 JST.** [Checkpoint](defiler-cast-checkpoint.md). Automatic NPC405S1/linking and real player casts use the native Gazer body22/sound377,identified link flash0x375A/hue0x489 and shared summon sound0x1E5. Final player summon overlay,NPC S1 gesture/windup/release,link/group/shared-damage/expiry presentation and custom audio remain pending. These placeholders do not block mechanics;icons remain separate.

**Defiler own S1 casting — 2026-11-15 JST.** [Checkpoint](defiler-link-casting-checkpoint.md). Own S1 timing and link mechanics reuse the native Gazer and identified recipient flash0x375A/hue0x489. Final cast/windup/release gesture,link/group/shared-damage/expiry feedback and audio remain needed. Owned D2 animation45S1HTH supplies mechanical frames;it is not final Gazer artwork. Icons remain separate.

**Defiler Health Link component — 2026-11-15 JST.** [Checkpoint](defiler-health-link-checkpoint.md). Native Gazer body22/sound377 is reused. Recipient flash0x375A/hue0x489 is an identified placeholder. Final summon, link/group membership, shared-damage feedback, expiry and audio remain needed; no imagegen work is included. Automatic linking/flying host/learned casting remain unfinished. Icons stay in their separate register.

**Goatman body reuse clarification — 2026-11-14 JST.** The approved Gargoyle body and its native animations are already reused for prepared summons. A new Khazra body is not required. Remaining assets are the summon cast overlay/audio, Stun impact, Berserk/Frenzy state presentation and Cleave sweep/contact audio. Skill icons remain in the separate icon register. Historical placeholder-body entries below are superseded by this clarification.


**Learned Sigil: Death (2026-11-14 JST).** [Checkpoint](death-cast-checkpoint.md). SC/shared Sigil marker art,recipient0x375A/hue0x48E,explosion0x36BD and shared cast sound remain identified placeholders. Final small/medium/large cast/marker,recipient,death-trigger explosion/chain/expiry and audio remain needed. Nine real-input casts verify mechanics without final art;icons remain separate.

**Sigil: Death candidate (2026-11-14 JST).** [Proposal](sigil-death-rule-proposal.md). Existing shared Sigil marker art,recipient flash0x375A/hue0x48E and explosion0x36BD are identified placeholders. Final small/medium/large marker casts,curse_hit/Warlock_Skill_Seal_Death_Hit recipient state,death-trigger explosion/chain presentation and cast/contact/expiry audio remain needed. No final art was generated. Mechanical pulse/queue/damage work does not wait for these assets;talent/hotkey icons remain separate.

**Learned Ring presentation (2026-11-09 JST).** [Checkpoint](ring-cast-checkpoint.md). Selfcast uses existing humanSC and shared0x1E5sound;travel0x36D4 and firstexplosion0x36BD are identified placeholders. Final cast/expanding ring,first damaging contact,later knockback-only tail,terminal/expiry and audio remain needed. Approved mechanics and hotkey use do not wait for final art;icons remain separate.

**Miasma Chain launch preparation (2026-11-09 JST).** [Checkpoint](miasma-chain-launch-checkpoint.md). Pure maker request emits no world art. Final cast/maker,travelling or returning chain,contact/cloud-maker,layered cloud/expiry and audio remain required;the new43/45stages must not disappear behind a generic bolt visual. Identified placeholders may accompany later world mechanics. Icons stay separate.

**Ring candidate world producer (2026-11-09 JST).** [Checkpoint](ring-world-checkpoint.md). Candidate sparse travel uses generic fire0x36D4;first explosion uses0x36BD. Both are identified placeholders. Final cast/ring expansion,first damaging contact,later knockback-only tail,terminal/expiry and audio remain needed. Visual730must not introduce another server damage child. Talent/hotkey icons stay separate.

**Prepared Cleave sweep (2026-11-09 JST).** [Checkpoint](cleave-sweep-checkpoint.md). A pure arc/area request creates no world visual or damage producer. Existing SQ/contact presentations remain identified placeholders;final Cleave cast/sweep/contact/audio stay pending. Icons remain separate.

**Ring approved-age prepared recipient (2026-11-09 JST).** [Checkpoint](ring-age-checkpoint.md). The shared generic fire-contact presentation is an identified placeholder for explicit recipient checks;no automatic projectile/explosion producer or final art is added. Existing cast/ring/contact/terminal/expiry/audio work remains pending;icons remain separate.

**Ring prepared motion profile (2026-11-09 JST).** [Checkpoint](ring-profile-checkpoint.md). No world actor or visual is emitted by the prepared free-flight/packet plan. Existing Ring cast,expanding ring,contact,terminal explosion,expiry and audio entries remain pending;identified placeholders will be used when world emission is connected. Icons remain separate.

**Learned Apocalypse placeholders (2026-11-09 JST).** Mechanics now have a learned cast;art remains pending. Ground`0x1B7A`,recipient`0x36D4`,shared cast sound`0x1E5` and human casting pose are identified placeholders. Final cast/countdown/detonation/recipient effects and audio still need creation. [Checkpoint](apocalypse-cast-checkpoint.md). Talent/hotkey icons remain separate;no final artwork generated.

**Apocalypse ground controller placeholder (2026-11-09 JST).** Original static`0x1B7A` represents the ground countdown;recipient`0x36D4` remains a placeholder. Final cast,countdown,detonation/area,recipient effects and audio still need creation. [Checkpoint](apocalypse-ground-checkpoint.md). Talent/hotkey icons remain in the separate icon register;no final artwork generated.

**Apocalypse prepared impact (2026-11-09 JST).** Recipient effect0x36D4 is an identified placeholder. Final cast,ground countdown,detonation/area,recipient overlay and audio remain needed;the temporary resistance reduction does not create a new lasting curse visual. [Checkpoint](apocalypse-impact-checkpoint.md). No final image generation performed;icons remain separate.

**Death Mark intrinsic interruption (2026-11-09 JST).** Cancellation adds no final art. Issued Tainted Fire Balls retain their current travel/impact placeholders and independent damage after caster warp;pending casts stop emitting. Existing landing0x3728,target0x375A and final cast/warp/mark/expiry/audio remain pending. [Checkpoint](death-mark-interrupt-checkpoint.md). Icons remain separate.

**Approved native Death Mark bridge (2026-11-09 JST).** Live targeting and follower selection reuse the existing landing0x3728 and target0x375A placeholders;no new artwork is generated. Final cast,warp/landing,target/expiry and audio remain pending. [Checkpoint](death-mark-native-world-proposal.md). Icons remain separate.

**Combined Death Mark release (2026-11-09 JST).** Actual pet relocation and approved recipient binding reuse identified landing0x3728 and target0x375A/hue0x48A placeholders. Final cast,warp/landing,target mark/expiry and audio remain needed;no final artwork was generated. [Checkpoint](death-mark-world-release-checkpoint.md). Icons remain a separate asset register.

**Approved prepared Death Mark recipient (2026-11-09 JST).** Target effect0x375A/hue0x48A is an identified placeholder. Final cast,warp/landing,target mark/expiry and audio still need creation;no final artwork was generated. [Rule/checkpoint](death-mark-debuff-proposal.md). Mechanical work continues independently;icons remain separate.

**Live Death Mark demon bridge (2026-11-09 JST).** Actual relocation now emits identified landing placeholder0x3728. Final cast,warp/landing overlay,target mark/expiry and audio are pending;no final images were generated. [Checkpoint](death-mark-world-checkpoint.md). Icons remain separately tracked.

**Prepared Death Mark pet composition (2026-11-09 JST).** Relocation/command/control helpers are implemented as detached mechanics;no new world art is emitted. Final cast, pet warp/landing overlay, target mark/expiry and audio remain pending for world integration. [Checkpoint](death-mark-pets-checkpoint.md). No image generation performed;icons stay separate.

**Prepared Death Mark cast composition (2026-11-08 JST).** This component produces no world effect. Final cast, per-pet warp/landing overlay, target mark, aura/expiry and audio remain pending when the world route is connected. [Checkpoint](death-mark-cast-checkpoint.md). No final art or image generation was performed; icons remain separate.

**Shared Sigil markers (2026-11-08 JST).** Lethargy, Rancor and Death now use one prepared world host with item0x1B7A labelled by its own skill name plus “placeholder.” Rancor/Death have no admitted recipient effects yet. Final cast, animated ground, pulse, recipient, expiry and audio remain pending for each skill. [Checkpoint](sigil-family-checkpoint.md). No image generation was performed; icon work stays separate.

**Learned Lethargy (2026-11-08 JST).** Mechanical cast,stationary marker,pulses,recipient penalties and expiry now work. Item0x1B7Ais labelled`Sigil: Lethargy (placeholder)`;recipient effect0x375Aand cast sound0x1E5are identified placeholders. Final cast/animated ground/pulse/recipient/expiry art and audio remain needed;no final imagegen work was performed. [Checkpoint](sigil-cast-checkpoint.md). Icons remain separate.

**Prepared Lethargy world marker (2026-11-08 JST).** Real stationary item0x1B7Ais named`Sigil: Lethargy (placeholder)`. It follows the reviewed clock/retirement and emits mechanical pulse callbacks;final cast,ground animation,pulse,recipient,expiry and audio are still needed. [Checkpoint](sigil-actor-checkpoint.md). No final art or automatic Rancor/Death world effect is claimed;icons remain separate.

**Sigil roster tracking (2026-11-08 JST).** Prepared eviction changes the old missile's lifetime rather than emitting an instant deletion effect. No world art/audio is produced yet;all three Sigils retain their pending cast,marker,pulse,recipient and expiry families with identified placeholders. [Checkpoint](sigil-tracking-checkpoint.md). Icons remain separate.

**Shared Sigil launch (2026-11-08 JST).** Prepared factory requests create no world art. All three Sigils retain pending cast,ground marker,pulse,recipient,expiry and audio identities;use labelled placeholders when world consumers are connected. [Checkpoint](sigil-launch-checkpoint.md). No final effects were generated;icons remain separate.

**Prepared missile packet scaling (2026-11-08 JST).** Numeric-only component adds no effect or audio identity. Ring cast,expansion,contact,explosion and expiry remain in the existing pending family below;no final artwork is claimed. [Checkpoint](missile-damage-scale-checkpoint.md). Icons remain separately tracked.

**Prepared Ring launch (2026-11-08 JST).** Pure source launch producer emits only explicit factory requests;no world effect or final art is created. Cast,expanding ring,contact,explosion,expiry and audio remain required in the existing Ring of Fire family,using identified placeholders when world emission is connected. [Checkpoint](ring-of-fire-launch-checkpoint.md). Icons stay separate.

**Warlock ordinary cast clock (2026-11-08 JST).** Clock/planning adds no emitter or new asset identity. Prepared Warlock/Witch poses remain identified human-animation placeholders;final casting poses and each skill’s effect/audio stay in their existing families. [Checkpoint](warlock-cast-clock-checkpoint.md). Icons remain separate.

**Learned Shadow casts (2026-11-08 JST).** Mechanics reuse identified humanoid `0x190`/hue`0x455` appearance and shared `0x376A` emergence/`0x1E5` summon sound placeholders. Final original bodies/equipment,player cast,emergence,source attack/spell and death/expiry effects/audio remain required in the Shadow/shared skill families. [Checkpoint](shadow-learned-cast-checkpoint.md). No imagegen work;icons stay separate.

**Intrinsic Shadow Fury (2026-11-08 JST).** Actual action now emits through the existing identified `0xF42` blade/contact/return placeholders. Final original sequence poses,release,travel/impact/expiry and audio remain required in the shared Fury/Shadow family. [Checkpoint](shadow-fury-cast-checkpoint.md). No imagegen work performed;icons remain separate.

**Master Sentinel range (2026-11-08 JST).** AI eligibility adds no emitter or new visual identity. Actual casts retain identified shared S2/blade travel/contact/expiry and audio placeholders;final assets remain required in the Sentinel/Shadow family. [Checkpoint](shadow-master-range-checkpoint.md). Icons stay separate.

**NPC-owned Hydra world (2026-11-08 JST).** Connected NPC heads/bolts reuse identified Hydra body and fire-bolt placeholders;no imagegen work performed. Final appearance,emergence/attack/death,bolt travel/impact/expiry and audio remain required in the shared Hydra family. [Checkpoint](npc-hydra-world-checkpoint.md). Independent lifetime changes no asset identity;icons stay separate.

**Prepared original NPC Hydra factory (2026-11-08 JST).** No head/bolt asset is emitted by this prepared factory seam. Actual NPC-owned heads/bolts will reuse existing identified Hydra placeholders;final emergence/attack/death,fire-bolt travel/impact/expiry and sound remain required in the shared Hydra family. [Checkpoint](npc-hydra-birth-checkpoint.md). Icons remain separate.

**Prepared original Shadow death nova (2026-11-08 JST).** Mechanically connected to the retained dead NPC and existing shared Nova projectile/contact presentation. Treat the current presentation as a placeholder:final Shadow death collapse,lightning ring,travel/impact/expiry cues and sound remain required in the Shadow/Nova effects family. No imagegen work was performed. [Checkpoint](shadow-death-checkpoint.md). Icons remain separate.

**Own Sentinel/Shock Web S2 actions (2026-11-08 JST).** Connected mechanics reuse identified S2/cast cues,`0x23B` summon audio,blade travel/contact and web lob/field placeholders. Final NPC poses,release/impact/expiry cues and audio remain pending in their existing families. [Checkpoint](shadow-sentinel-action-checkpoint.md). Modifier42 nova artwork/audio is recorded for the upcoming mechanic;icons remain separate.

**Prepared Shadow Sentinel (2026-11-08 JST).** Reuses the existing blade actor/following-missile and identified cast/travel/contact placeholders. Final S2 pose,blade travel/turn/contact/expiry cues and audio remain pending in the shared Sentinel family;birth snapshot adds no visual identity. [Checkpoint](shadow-sentinel-checkpoint.md). Icons remain separate.

**Original Shadow Fury clock (2026-11-08 JST).** No effects/audio emitted by this numeric clock. Original mode14 uses SC poses inside its own17point sequence;final poses/channel/travel/impact/expiry cues and sounds remain pending in the existing Fury/Shadow family. [Checkpoint](shadow-fury-clock-checkpoint.md). Prior `0xF42` blade placeholder remains;icons stay separate.

**Own Shadow Fury travelling receiver (2026-11-08 JST).** The NPC route reuses identified `0xF42` travelling-blade and shared contact/return placeholders. Final NPC cast/channel poses,blade travel/impact/expiry cues and audio remain pending;reuse the player Fury family. [Checkpoint](shadow-fury-missiles-checkpoint.md). These assets do not gate mechanics. Icons stay separate.

**Own Shadow Fury packet/resources (2026-11-08 JST).** No new effects emitted by this snapshot/state component. Final NPC cast/channel poses,blade travel/impact/expiry cues and audio remain identified placeholders shared with player Fury. [Checkpoint](shadow-fury-packets-checkpoint.md). Icons stay separate.

**Own Shadow Flight action (2026-11-08 JST).** Mechanics use identified shared `0x3728` teleport, `0x1FE` teleport sound and `0x238` kick sound placeholders. Final NPC sequence poses, departure/arrival, kick/contact/charge-release cues and audio remain pending; reuse player Flight and charge families. [Checkpoint](shadow-flight-checkpoint.md). Icons stay separate.

**Own Shadow Flight packet (2026-11-08 JST).** No new emitter/art created. Final teleport departure/arrival, NPC sequence/kick/charge-contact cues and audio remain identified placeholders shared with player Flight; full NPC action is pending. [Checkpoint](shadow-flight-packets-checkpoint.md). Icons stay separate.

**Own Shadow Dragon Tail (2026-11-08 JST).** Kick and area mechanics reuse existing services. Final NPC A1/KK poses, charge releases, kick/contact/knockback, fire burst and audio remain pending. Shared `0x36BD`, hue `0x489` is an identified burst placeholder. [Checkpoint](shadow-tail-checkpoint.md). Icons remain separate.

**Own Shadow Dragon Talon (2026-11-08 JST).** Cached/repeated kick contacts and existing six-family releases now work mechanically. Final NPC A1/KK poses,kick/knockback/charge-release cues,elemental impacts/expiry and audio remain pending placeholders;reuse player Talon and existing charge families. [Checkpoint](shadow-talon-checkpoint.md). Icons stay separate.

**Approved shared sentry placement (2026-11-08 JST).** Search adds no new visual effect. Existing cast,placement,emergence,firing/impact/expiry cues and audio remain identified placeholders for all five sentries and both Shadows;share existing families. [Checkpoint](sentry-placement-checkpoint.md). Icons stay separate.

**Own Shadow Talon packet (2026-11-08 JST).** Prepared typed/charge damage uses existing contact/status services;allocation emits no new effect. Final NPC kick poses,impact/charge-release/chill/freeze cues and audio remain pending,sharing existing player and elemental families. [Checkpoint](shadow-kick-packets-checkpoint.md). Icons stay separate.

**Own Shadow kick equipment (2026-11-08 JST).** Prepared numeric capture emits no new visual effect. Existing player/NPC kick poses,contact and charge-release cues/audio remain pending;reuse those families when the NPC action is connected. [Checkpoint](shadow-kick-checkpoint.md). Icons stay separate.

**Own Shadow Dragon Claw (2026-11-08 JST).** Actual contacts and six-family releases now work through existing mechanical services. Final paired-claw A2/S4/basic poses,selected-hand contact,charge release/impact/expiry cues and audio remain pending placeholders;reuse the player Dragon Claw and existing elemental families. [Checkpoint](shadow-dragon-claw-checkpoint.md). Icons stay separate.

**Own Shadow Ice (2026-11-08 JST).** Inline cold/freeze,copied chill area and stationary cold/freezing cubes now work mechanically. Existing charge0x375A and cold-area/cube0x374A/hue0x480 markers are identified placeholders. Final claw/basic/A2/S4 poses,charge overlays,chill/freeze/shatter cues,area/cube/contact/expiry art and audio remain pending;share the player Ice family. [Checkpoint](shadow-ice-checkpoint.md). Icons stay separate.

**Own Shadow Thunder (2026-11-08 JST).** Inline lightning,64nova rays and16charged bolts now work mechanically in the shared player pool. They reuse identified charge0x375A and sampled lightning trail0x3818/hue0x482 placeholders. Final claw/basic/A2/S4 poses, charge overlays, nova/charged-bolt travel, impacts, expiry cues and audio remain pending;share the player Thunder family. [Checkpoint](shadow-thunder-checkpoint.md). Icons stay separate.

**Own Shadow Fists of Fire (2026-11-08 JST).** Inline fire/conversion, burst and scattered damaging fields now work mechanically. They reuse identified charge0x375A, burst0x36BD and flame0x398C placeholders. Final selected-claw/A2/S4/basic poses, charge overlays, burst/field art, hit/expiry cues and audio remain pending; share the player Fists asset family. [Checkpoint](shadow-fists-checkpoint.md). Icons stay separate.

**Shadow elemental charge sequences (2026-11-08 JST).** Fists/Thunder/Ice builders use existing identified charge marker0x375A. Final A2/S4 poses, selected-claw contacts, distinct gain/refresh/expiry overlays and audio remain pending placeholders; share the existing player elemental charge families. Their finisher emissions remain a separate mechanical dependency. [Checkpoint](shadow-claw-sequence-checkpoint.md). Icons stay separate.

**Own Shadow Phoenix (2026-11-08 JST).** Mechanical parent release,meteor/18fields,chain and chaos travel/contact/freeze now work in shared pools. Identified placeholders remain warning0x3709,meteor0x36BD,fire0x398C,chain0x3818 and ice0x374A. Final Shadow finisher pose,warning/fall/impact/ground fire,distinct chain/chaos travel and contact,freeze/thaw and audio are still needed;share existing player Phoenix/cold/fire families. See [checkpoint](shadow-phoenix-checkpoint.md). Icons stay separate.

**Owned NPC spell freeze (2026-11-08 JST).** Typed freeze delivery reuses identified cold/freeze marker0x374A. Final chaos-ice/Ice release travel/contact,freeze/thaw and audio remain pending within existing martial/cold families. No Phoenix projectile or elemental finisher emission is enabled by this receiver prerequisite. See [checkpoint](npc-spell-freeze-checkpoint.md). Icons stay separate.

**Shadow Tiger/Cobra finishers (2026-11-08 JST).** Mechanical ordinary-contact release now works for both families. Final basic-finisher/contact poses, Tiger foot-impact and charge overlays, Cobra drain overlays/audio remain pending; the flat-drain receiver reuses identified overlay0x376A. Share existing player charge families. NPC Hydra62 in one original item loadout is a separate mechanical adapter gap. See [checkpoint](shadow-finisher-checkpoint.md). Icons stay separate.

**Shadow A2 charge actions (2026-11-08 JST).** Mechanical release/end timing is connected using current owned animation data. Final visible A2 poses, weapon-contact cues, distinct charge gain/refresh/expiry overlays and audio still need assets; reuse existing player charge families and identified marker0x375A. See [checkpoint](shadow-charge-action-checkpoint.md). Icons remain separate.

**Shadow charge backend (2026-11-08 JST).** Tiger/Cobra/Phoenix gain reuses identified generic marker0x375A. Final NPC A2 and weapon-contact poses, family-specific charge overlays/refresh/expiry and audio remain pending; share the existing player charge families. A2 conductor and progressive finishers remain mechanically gated. See [checkpoint](shadow-charge-checkpoint.md). Icons remain separate.

**Master world (2026-11-08 JST).** Distinct Master now follows/fights in qualified internal fixtures using existing Shadow native movement/attack/cast and skill cues. Final walk/run/turn, retreat, attack/cast/contact/recovery, state overlays and audio remain pending placeholders;reuse the existing Shadow/skill families. See [checkpoint](shadow-master-world-checkpoint.md). No new final art is introduced;icons stay separate.

**Intrinsic Shadow stationary sentries (2026-11-08 JST).** All five do45 casts reuse identified native Shadow poses, summon sound0x23B and the existing sentry body0x9C/hue0x482 (Inferno0x489), projectile and impact placeholders. Final placement/S2, distinct trap models/emergence, firing/contact, expiry/retirement and audio remain needed; share their existing player-sentry asset families. See [checkpoint](shadow-sentry-checkpoint.md). Icons remain separate.

**Shadow Shock Web fields (2026-11-08 JST).** NPC fields reuse identified air0x36E4, electric-field0x374A and throw sound0x23B placeholders. Final lob, landing, field/contact, expiry and audio remain needed and can share the player Shock Web asset family. Action admission is still gated; see [checkpoint](shadow-shock-web-checkpoint.md). Icons remain separate.

**Dragon Flight (2026-11-08 JST).** Learned transfer/kick uses identified shared Teleport particles0x3728/sound0x1FE, native Wrestle/contact sound0x238 and charge cues. Final departure/arrival,dragonflight target overlay,directional kick/contact,progressive releases and audio remain pending. See [checkpoint](dragon-flight-action-checkpoint.md);icons remain separate.

**Dragon Claw (2026-11-08 JST).** The learned dual-claw action uses identified native weapon pose/contact sound0x238 and shared charge/element cues. Final directional primary/secondary sequence,contacts,progressive releases,miss/block/interruption/recovery and audio remain pending independently of mechanics. See [checkpoint](dragon-claw-action-checkpoint.md);icons stay separate.

**Learned Dragon Talon (2026-11-08 JST).** The approved series reuses native Wrestle pose/contact sound0x238 and existing identified charge/element/knockback cues. Final directional kicks,series/contact,charge releases,miss/block/interruption/recovery and audio remain needed independently of mechanics. See [checkpoint](dragon-talon-action-checkpoint.md);icons stay separate.

**Prepared Dragon Talon clock (2026-11-08 JST).** Numeric planning creates no effect producer or final animation. Kick/series/contact, progressive-charge effects and audio remain needed;existing generic melee presentation is a placeholder. See [checkpoint](dragon-talon-clock-checkpoint.md). Talent/hotkey icons remain separate.

**Approved learned Blade Fury (2026-11-08 JST).** The held-input route reuses native weapon-flight presentation as an identified placeholder. Final startup/loop/release poses, bladefragment1/2 travel/contact/expiry and audio remain pending. Mechanics introduce no generated asset. See [checkpoint](blade-fury-held-input-checkpoint.md); talent/hotkey icons remain separate.

**Blade Fury input proposal (2026-11-08 JST).** Test-only channel policy creates no new final asset. Startup/loop/release poses, bladefragment1/2 travel/contact and audio remain missing; native weapon-flight presentation is an identified placeholder. See [proposal](blade-fury-held-input-proposal.md). Talent/hotkey icons remain separate.

**Learned Arctic input (2026-11-08 JST).** Channel mechanics reuse the existing identified `0x374A`/hue `0x480` cold-stream marker. Final arctic_blast_cast/stream/caster-animation and druid_windcast/audio work remains pending. Mechanical acceptance does not certify final rendered timing. No new generated bitmap is introduced;tree/hotkey icons remain separate. See [checkpoint](arctic-held-input-checkpoint.md).

**Arctic potion delivery (2026-11-07 JST).** Reusing the spell receiver adds no new effect/audio asset. Existing cold-stream `0x374A`/hue `0x480` and Arctic cast/stream/audio work remain identified placeholders. Approved held-input integration is next; tree/hotkey icons remain separate. See [checkpoint](arctic-potion-checkpoint.md).

**Shared startup dispatcher (2026-11-07 JST).** No new visual or audio asset is required by the dispatcher itself. Existing Shadow cast/contact/buff/death effects and appearance placeholders remain in their separate entries; no new live Shadow effect is enabled. See [checkpoint](skill-startup-checkpoint.md).

**NPC item skill grants (2026-11-07 JST).** Skill ownership and source ordering add no final effects/audio. Existing Shadow appearance and pending cast/contact/death effects remain separate. See [checkpoint](npc-item-skills-checkpoint.md).

**Shadow passive lists (2026-11-07 JST).** Actual passive stats and graphics-change notifications are implemented without final client effects. Shadow body `0x190`, hue `0x455`, remains an explicitly named appearance placeholder. Block/contact reactions, casting, modifier-death effects and audio remain pending separately from stat-list ownership. See [checkpoint](shadow-passives-checkpoint.md).

**Shadow intrinsic skills (2026-11-07 JST).** Skill ownership adds no final effects or audio. Parked Shadow Warrior/Master bodies retain their explicitly named body `0x190`, hue `0x455` appearance placeholder. Casting, skill-specific effects, contact and modifier-death visuals remain pending in this effects list; ownership does not mark them complete. See [checkpoint](shadow-skills-checkpoint.md).

**Dedicated Shadow bodies (2026-11-07 JST).** Source resource actors use the named appearance placeholder body `0x190`, hue `0x455` and remain parked. No final art or effects are installed. Shadow emergence, disappearance, movement, attacks, skill releases, modifier death effects and audio remain pending. See [checkpoint](shadow-body-checkpoint.md).

**Prepared Shadow initialization (2026-11-07 JST).** No new artwork is installed. Original equipment and numeric storage fixtures use clearly labeled test holders. Actual Shadow appearance,spawn/despawn,combat/skill/death effects and audio remain pending;no playable Shadow route is enabled. See [checkpoint](shadow-initializer-checkpoint.md).

**Learned Valkyrie cast (2026-11-07 JST).** The playable route reuses clearly identified summon glow/audio and source movement/attack/proc/status placeholders. Final appearance, original body/equipment/spear, summon/return/death, elemental/projectile/contact/recovery and audio assets remain missing and listed separately. Mechanics do not wait for imagegen artwork.

**Valkyrie external buffs (2026-11-07 JST).** Original Enchant/aura damage support reuses existing identified weapon-glow, aura, elemental-hit, status and audio placeholders. No final art is generated. Valkyrie summon/body/equipment/movement/spear/death effects and talent/hotkey icons remain separate asset work.

**Valkyrie town and approved knockback direction (2026-11-07 JST).** Parking/return and same-tile facing change mechanical admission and movement, not final art. Existing appear/disappear, spear/knockback, recovery, status, curse/projectile and audio markers remain identified placeholders. Final Valkyrie body/equipment and summon/death assets remain separate; no imagegen artwork is created.

**Valkyrie startup and expanded procs (2026-11-07 JST).** Derived rates add no artwork. Original NPC Ice Bolt/Lightning reuse the existing identified projectile/impact/cold/line placeholders; Amplify Damage uses the shared curse effect/audio placeholder. Final effects/audio remain separate from Valkyrie body/equipment,summon/movement/spear/death assets. No imagegen asset is created.

**Valkyrie automatic A1 (2026-11-07 JST).** Actual AI/frame contact now uses the existing native `Animate(4,5,1,...)` as an identified attack placeholder. The source event plan owns mechanical timing. Final Valkyrie spear animation/impact, movement, defensive reactions, summon/death/equipment and audio assets remain pending separately; no imagegen artwork is created.

**Valkyrie source initialization (2026-11-07 JST).** Actual initialized bodies and original private equipment keep the existing appearance/equipment art placeholders. Summon/transformation/movement/weapon/contact/audio assets remain separately pending. Zero current component choices are source data,not a generated-art requirement. No artwork is created.

**Persistent NPC world allocation (2026-11-07 JST).** Saved stream ownership and factory binding are invisible. Existing generated-equipment art and fire/cold/lightning/cast/travel/link/impact/audio markers remain identified placeholders;this stage creates no artwork.

**Original NPC contact producers (2026-11-07 JST).** Actual ordinary/incoming item callbacks now reach shared fire/lightning/cold spell placeholders and item audio0x1E5 under explicit host services. No artwork is generated;final proc/cast/travel/link/impact/audio and client presentation remain pending. Existing knockback/get-hit/return markers are reused.

**Full original NPC proc callbacks (2026-11-07 JST).** Actual original-item callbacks now create Fire Bolt,Charged Bolt,Frost Nova,Nova and Chain Lightning through existing placeholder effects. Shared item-cast audio0x1E5 and fire/cold/lightning markers remain identified placeholders;final proc/cast/travel/link/impact/audio/client presentation still need assets. This checkpoint generates no artwork.

**Original NPC item event history (2026-11-07 JST).** Registration and persistence add no visible effect. Existing fire/lightning/cold/proc/impact/audio placeholders remain identified;final art and client presentation remain pending. Effect bodies still require the original combat dispatcher binding.

**Protected original item wrappers (2026-11-07 JST).** Successful target/position wrappers reuse identified audio placeholder0x1E5 after restoring their temporary frame. Fire/lightning/cold markers remain placeholders; final proc/cast/travel/link/impact/audio and client presentation remain pending. Mechanical restoration and save tests add no artwork.

**Actual NPC item Chain roots (2026-11-07 JST).** Item roots and saved child segments now use the shared native lightning 0x3818/hue0x482 placeholder. Final source-to-root/link/proc/impact/audio and client presentation remain pending; budget/HP/save verification creates no new artwork.

**NPC Charged Bolt/post-path RNG (2026-11-07 JST).** Live original-NPC bolts now share charged motion and resume saved partial paths with the initialized damage stream. Existing lightning 0x3818 remains an identified placeholder. Final charged travel/proc/impact/audio assets and client presentation remain pending; the RNG correction adds no artwork.

**Original NPC live item emission (2026-11-07 JST).** Outgoing Fire Bolt and target-centered Nova/Frost Nova now run natural contacts and saved partial rings. Shared fire 0x36D4, lightning 0x3818, cold 0x36E4 and cold-state 0x374A remain identified native placeholders. Final item-proc/travel/ring/impact/audio assets and client presentation are still pending; no new artwork is created by these checks.

**Original NPC item origins (2026-11-07 JST).** Factory-origin verification adds no artwork or live effect. Future outgoing Nova/Frost Nova presentation must center on the target, and outgoing Fire Bolt/Chain must use their verified launch origins. Existing fire/lightning/cold and cast-audio placeholders remain identified; final projectile/ring/proc/impact/audio work remains pending separately.

**Original NPC Chain Lightning (2026-11-07 JST).** Prepared natural NPC segments reuse native 0x3818/hue0x482 as an identified link placeholder. Final chain links, proc/impact cues and audio remain pending; mechanics and save tests do not certify final client presentation.

**Original NPC natural travel checks (2026-11-07 JST).** Natural charged/ring hits and resumed flights are mechanically exercised. Existing lightning 0x3818, cold 0x36E4 and cold-state 0x374A markers remain identified placeholders; final travel/ring/impact/audio assets and client presentation are still pending. This verification creates no new artwork.

**Original NPC spell flights (2026-11-07 JST).** Fire Bolt, Charged Bolt, Nova and Frost Nova now reuse shared travel markers from the original NPC origin. Fire 0x36D4, lightning 0x3818, cold 0x36E4 and cold status 0x374A are identified placeholders. Final projectile/ring, impact/proc and audio assets remain pending; native hit verification does not certify final client presentation.

**Original NPC item spell capture (2026-11-07 JST).** Numeric capture adds no rendered effect. Fire Bolt, Charged Bolt, Nova, Frost Nova and Chain Lightning will reuse their spell presentation when original-NPC delivery is connected. Existing shared native bolt/ring/lightning markers remain identified placeholders; final impact, cold, proc and audio assets remain pending separately.

**Valkyrie original ordered contact (2026-11-07 JST).** Prepared original packets now reach real HP,cold/poison and supported event/return presentation. Existing critical/defense/contact/status/retaliation markers remain identified native placeholders. Final spear impact,critical,cold/poison,knockback,retaliation,proc spell and audio work stays separate from body/summon/death artwork;no imagegen asset or learned cast is enabled.

**Valkyrie original ground movement (2026-11-07 JST).** Source movement and moving Evade now have native world consumers. The female native body/hue and defense marker 0x375A/hue0x481/sound 0x1A4 remain identified placeholders. Final walk/run,spear contact,Evade/Dodge/Avoid,recovery,summon/death,equipment and audio still need assets;mechanical movement does not certify client animation or create imagegen artwork.

**Valkyrie transformed timing (2026-11-07 JST).** Prepared mechanical clocks now identify the Amazon 2HT attack/walk/run rows. They add no rendered animation or final artwork. The existing female native body/hue remains an identified appearance placeholder. Summon,walking/running,spear attack/contact,defensive reactions,death/equipment and audio remain separate asset work; no imagegen task blocks this timing preparation.

**Valkyrie inherited defense (2026-11-07 JST).** Successful stationary Dodge/Avoid currently uses the identified native0x375A/hue0x481 marker and0x1A4sound. These are placeholders for final Valkyrie defensive animation/effect/audio, alongside summon/body/equipment/weapon/contact assets. No new recovery/cast-interruption lock or imagegen asset is introduced. Moving Evade presentation remains part of its future controller integration.

**Valkyrie damage allocation (2026-11-07 JST).** Physical/element/poison/drain and intrinsic-critical hit-class/overlay fields now exist as packet metadata; no new HP/status/proc delivery or final overlay presentation is enabled. Body/equipment,summon,weapon/contact,critical overlay and skill/proc effects/audio remain separately pending and identified placeholders.

**Valkyrie hand/RNG integration (2026-11-07 JST).** Saved weapon identity and attack-stream binding add no final visual or audio asset. The prepared female body and hidden item graphics remain explicitly identified placeholders. Final body/equipment, summon, weapon/contact and granted skill/proc effects/audio remain pending separately.

**Valkyrie resource integration (2026-11-07 JST).** Pool/regeneration and save/region corrections add no final visual or audio asset. The prepared female silhouette and invisible original item art remain labelled placeholders; final body, summon, weapon/contact and skill/proc effects/audio remain separately pending. No imagegen asset is added.

**Valkyrie numeric equipment (2026-11-07 JST).** Attack/defense/resistance delivery creates no final visual or audio asset. The prepared female silhouette and invisible private items remain identified placeholders. Final Valkyrie body/equipment,summon,weapon/contact,skill/proc effects and audio remain pending independently of this numeric checkpoint. No imagegen asset is added.

**Valkyrie loadouts (2026-11-07 JST).** The prepared female silhouette still needs final armor/weapon presentation. Actual generated items are hidden private mechanical carriers with explicitly identified graphics placeholders. Final summon/attack/impact/despawn effects and audio remain pending; source item creation supplies no final rendered equipment or skill effect.

**Valkyrie body checkpoint (2026-11-07 JST).** Body0x191/hue0x481 is an identified native female appearance placeholder for the prepared backend. Final summoned model/equipment, appear/despawn and attack/contact effects plus audio remain pending. Body persistence and stat checks create no final effect asset; see [checkpoint](valkyrie-body-checkpoint.md).

**Learned Leap Attack (2026-11-06 JST).** Mechanics now emit the approved circular landing hit and separate later weapon contact. Final jump/arc, landing-area presentation, weapon impact and audio are still missing; existing native cast pose/sound are identified placeholders. Neither an invisible final wave asset nor an icon is implied by mechanical completion. See [checkpoint](leap-attack-checkpoint.md).

**Leap Attack timing checkpoint (2026-11-06 JST).** Current SQ14 distinguishes jump poses and the later weapon contact. Its approved circular landing area still needs final jump/arc, landing-wave, weapon impact and audio presentation. Timing preparation supplies no final effect asset; keep these distinct from ordinary Leap knockback. See [checkpoint](leap-attack-sequence-checkpoint.md).


**Grim Ward placeholder checkpoint (2026-11-06 JST).** Casting/lifecycle uses stock skull items named startup, active totem and expiry, the native SC pose and an existing cry sound. Final three-size start/body/end, fear/slow/expiry and audio remain pending. These placeholders do not gate mechanics; see [checkpoint](grim-ward-actor-checkpoint.md).


**Approved Frenzy (2026-11-06 JST).** Its shared mechanical contacts use identified native weapon animation/sound placeholders. Final paired-strike, miss/block/contact and speed-charge/expiry presentation and audio remain pending. See [checkpoint](frenzy-checkpoint.md).

**Entropy cloud channels (2026-11-06 JST).** The shared captured duration/radius/server outputs add no rendered cloud. Final Bolt/Chain cloud layers, contact, expiry and audio stay pending; no do41 geometry or artwork is inferred. See [checkpoint](entropy-clouds-checkpoint.md).

**Approved Double Swing (2026-11-06 JST).** The two mechanical hand events use the existing native weapon animation and per-contact sound placeholders. Final alternating-hand swing/impact presentation and skill-specific audio remain pending; no image generation occurred. See [checkpoint](double-swing-checkpoint.md).

**Necromancer item skill grants (2026-11-06 JST).** Four newly available uniques use the existing curse, armor, barrier, poison, corpse and missile effect routes. Their skill-specific effects/audio remain the identified placeholders in this ledger; these grants add no new visual producer or asset identity. See [checkpoint](necromancer-item-skills-checkpoint.md).

**Original individual-skill properties (2026-11-06 JST).** Raw107 storage adds no cast/contact/particle/audio producer. Existing Iron Golem/Revive effects stay identified placeholders; NPC skill activation and graphical acceptance remain separate. See [checkpoint](single-skill-property-checkpoint.md).

**Saved forced ethereality (2026-11-06 JST).** Property23 reuses numeric finishing and adds no cast, contact, particle or audio producer. Existing Iron Golem/Revive effects remain identified placeholders; ethereal item appearance and final shared contact presentation are unverified. See [checkpoint](saved-item-ethereality-checkpoint.md).

**Saved skill-tree properties (2026-11-06 JST).** Original tree-layer projection adds no effect or audio producer. Existing Iron Golem/Revive summon and contact placeholders remain pending. The Sunder review adds no effect implementation. See [checkpoint](saved-tree-property-checkpoint.md).

**NPC Rest in Peace (2026-11-06 JST).** Item108/function29 changes unit/corpse state without a new visual or audio producer. Existing summon/contact placeholders and final assets remain separate; no new effect artwork is completed. See [checkpoint](npc-rest-in-peace-checkpoint.md).

**Approved pet slow (2026-11-06 JST).** Item150 and outgoing Clay slow reuse the existing status/rate presentation; no new visual or audio producer is added. Final Clay summon/contact and slow feedback remain pending with their identified placeholders. See [checkpoint](npc-item-slow-checkpoint.md).

**NPC on-kill healing overlay (2026-11-06 JST).** Stat86/139 callbacks reuse identified UO0x376A / hue0x21 as the placeholder for source overlay151 on the healed pet. Final shared healing presentation remains pending. This adds no final artwork or separate talent/hotkey icon; see [checkpoint](saved-npc-item-recovery-checkpoint.md).

**Approved Inferno integration (2026-11-06 JST).** Inferno uses the existing identified 0x36D4 / hue 0x489 stream placeholder. Final cast/channel, flame/contact/expiry presentation and audio still need assets; held input adds no final artwork. See [checkpoint](inferno-held-input-checkpoint.md).

**Howltusk / player-only mana (2026-11-06):** NPC callback13 emits no player mana-recovery overlay 152. Existing fear/knockback/summon placeholders remain; no new effect/audio identity is added. Ring of Fire creates no effect producer during its [preflight](warlock-ring-of-fire-preflight.md); final cast/ring/contact/explosion/expiry/audio remain pending.

**NPC item knockback (2026-11-06):** stat81/function07 reuses the existing native movement/get-hit presentation; no new particles/audio or final asset are supplied. Final impact/recovery presentation remains with the existing knockback effects. See [checkpoint](npc-item-knockback-checkpoint.md).

**Inferno review-only input prototype (2026-11-06):** no new effect or audio producer is introduced. The reused stream keeps identified placeholder 0x36D4/hue 0x489; final channel/contact/expiry/audio remain pending. See [proposal](inferno-held-input-proposal.md).

**NPC item fear reuse (2026-11-06):** stat112/function08 uses the existing identified fear placeholder `0x375A`, hue `0x21`, through the shared Terror controller. Its final visual/audio replacement remains with the existing fear assets; no separate effect identity is added. See [the checkpoint](npc-item-fear-checkpoint.md).

**NPC lightning return placeholder:** stat128 now uses the existing brief native contact marker`0x374A`, hue`0x481`. Final lightning-return contact effect and audio remain missing. This is not a new projectile/travel/radius effect. Mechanical/source/validation scope is in the [checkpoint](npc-lightning-return-checkpoint.md); no imagegen work has been performed.

**Queued equipment effect: lightning retaliation128.** Reference contract prepared; no runtime visual/audio implemented and Coif remains refused. Plan a brief lightning-return contact effect/audio after mechanical admission. No source radius, extra hit, projectile flight or existing asset is asserted. See the [checkpoint](npc-lightning-return-reference-checkpoint.md).

**NPC equipment blind:** direct blind-on-hit uses the existing target curse marker `0x375A` as a **clearly identified placeholder**. It does not introduce a caster spell animation/audio or certify final blind/curse visuals. Final presentation and audio remain separate asset work. See the [checkpoint](npc-item-blind-checkpoint.md).

**NPC equipment freeze:** original Iron/frozen Revive callbacks and filled NPC freeze delivery reuse native cold/freeze marker `0x374A` as a **clearly identified placeholder**. Final freeze application/thaw/shatter presentation and audio remain separate asset work; this mechanical checkpoint completes no visual asset. See the [checkpoint](npc-freeze-hit-checkpoint.md).

**NPC flat-drain delivery:** recovery reuses the existing native leech marker as an **identified placeholder for source overlay151**. It is not completed final effect/audio art. Existing contact, cold/poison/stun and summon placeholders remain separate. See the [checkpoint](npc-flat-drains-checkpoint.md).

**NPC full damage allocation:** no new effect/audio identity is introduced. Hit class and critical overlay are allocation metadata here; rendered overlay delivery, actual actor animation and existing summon/generated-item placeholders remain separate. The paired Tekagi visual preview is still pending. See the [checkpoint](npc-damage-fill-checkpoint.md).

**Necromancer army-size policy:** quantity progression, FIFO and count text introduce no new effect/audio identity. Existing Mage/Revive/summon placeholders remain separate; lower quantities do not complete their effects. See the [checkpoint](necromancer-army-checkpoint.md).

**Physical stat binding correction:** numerical percentage-field correction introduces no new effect/audio identity. Existing generated equipment/summon placeholders and paired Tekagi preview remain pending. See the [checkpoint](generated-npc-physical-stats-checkpoint.md).

**Generated NPC selected-hand physical:** exact numeric damage rolls introduce no new effect/audio identity. Actual hit/skill presentation and the paired Tekagi appearance/animation preview remain open; existing summon and generated-item art placeholders remain identified. See the [checkpoint](generated-npc-physical-checkpoint.md).

**Generated NPC selected-weapon stats:** no new effect/audio identity. This numeric hand policy does not complete selected-weapon animation, hit presentation or the paired Tekagi visual preview. Original generated-item art remains identified placeholders. See the [checkpoint](generated-npc-weapons-checkpoint.md).

**Mage town/safe-area validation:** no new effect/audio asset identity. The guard and fixture correction adds no cast animation or effect to rejected town casts. Existing Mage and parking presentation placeholders remain separately tracked. See the [checkpoint](mage-safe-area-checkpoint.md).

**Generated NPC live resources:** native fractional resource binding adds no new skill effect/audio identity. Prepared recipient/equipment art remains identified placeholders. Actual actor animation/rate and skill presentation remain open; paired Tekagi appearance/attack preview is still pending. See the [checkpoint](generated-npc-live-resources-checkpoint.md).

**Generated NPC resource callbacks:** numeric pool and regeneration changes introduce no new effect/audio identity. Existing summon/equipment placeholders remain identified; live effects and paired Tekagi appearance/animation preview remain pending. See the [checkpoint](generated-npc-resources-checkpoint.md).

**Generated NPC stat graph and claws:** numeric recipient graphs add no skill effect/audio identity. Existing Tekagi art (`0x27AB`/`0x27F6`) is the agreed claw presentation path; both-hand appearance and attack animation preview remain pending. Kama is an optional visual candidate. Generated summon items still use their identified stock placeholders, and live skill effects remain separately unfinished. See the [checkpoint](generated-npc-stats-checkpoint.md).

**NPC equipment placement:** canonical body placement uses hidden stock item art `0x14F0` with an explicit generated-equipment placeholder name and `Layer.Invalid`. NPC physical storage does not establish summon effects, spell effects or audio. Existing Valkyrie/Shadow presentation placeholders remain pending. See the [checkpoint](npc-equip-placement-checkpoint.md).

**Native generated items:** item artwork uses stock `0x14F0` with `[generated equipment; art placeholder]` in the name. This is a separate equipment presentation placeholder; no new skill effect/audio identity is complete. Existing Valkyrie/Shadow summon effects remain pending. See the [checkpoint](generated-npc-item-checkpoint.md).

**Prepared quality/automagic:** no new skill effect/audio identity. Generated equipment and Valkyrie/Shadow presentation retain their existing identified placeholders. Preparing a raw property graph does not certify item appearance or NPC spell delivery. See the [checkpoint](item-quality-current-checkpoint.md).

**Composed item affixes:** no new skill effect/audio identity. Raw proc/charge/skill fields do not establish NPC spell delivery or presentation. Generated equipment and Valkyrie/Shadow effects retain their existing identified placeholders. See the [checkpoint](current-item-affixes-checkpoint.md).

**Item staffmods:** no new skill effect/audio identity. Generated equipment and Valkyrie/Shadow presentation remain unfinished with their existing identified placeholders. Raw skill bonuses do not certify NPC spell delivery or presentation. See the [checkpoint](item-staffmods-checkpoint.md).

**Item finishing:** no new skill effect or audio identity. Actual generated-item appearance, ethereal presentation and Valkyrie/Shadow summon effects remain pending with their existing identified placeholders. Numeric graph acceptance does not certify presentation. See the [checkpoint](item-finishing-checkpoint.md).

**Item quality orchestration:** no new skill effect/audio identity or artwork. Controlled ethereal/finalizer markers are mechanical fixture services; complete native item presentation and existing summon placeholders remain pending. See the [checkpoint](item-quality-checkpoint.md).

**Item base initializer:** no new skill effect/audio identity or artwork. Stored inventory-variation choices still need eventual native item materialization; this checkpoint creates only hidden unattached verification items. Existing summon/item placeholders remain. See the [checkpoint](item-initialization-checkpoint.md).

**NPC retained-target bridge:** no new effect/audio identity or artwork. Existing shared curse/item-cast sound and curse markers remain identified placeholders; reflected-death targeting is mechanical. See the [checkpoint](npc-retained-target-checkpoint.md).

**NPC lethal incoming acceptance:** no new effects/audio identity or artwork. Existing curse and death placeholders remain in this ledger; retirement/persistence acceptance is mechanical. See the [checkpoint](npc-incoming-lethal-checkpoint.md).

**NPC on-struck curse checkpoint:** existing shared curse sound and curse markers remain identified placeholders. This event producer creates no new skill-effect identity or artwork. Final curse/item-trigger presentation remains pending; see the [checkpoint](npc-incoming-procs-checkpoint.md).

**Raven snapshot checkpoint:** no new visual or audio identities. Existing bird, summon, flight, strike and blind placeholders remain; packet/budget capture is mechanically verified. See the [checkpoint](raven-snapshot-checkpoint.md). Final effects and graphical acceptance remain pending.

**Druid snapshot checkpoint:** no new visual or audio asset identities. Spirit/Dire Wolf and Grizzly retain the identified summon/movement/attack and upgrade placeholders; the capture change is mechanical and verified separately. See the [checkpoint](druid-snapshot-checkpoint.md). Final effects and graphical acceptance remain pending.

**Mage/golem snapshots and payment (2026-11-05 JST).** Stat capture adds no effects/audio. Ordinary summon particles now occur only after payment succeeds. Existing Mage basic/extra-spell/armor/Enchant and Clay/Blood/Iron/Fire placeholders and final effect/audio gaps remain listed independently. No imagegen asset is created. See [checkpoint](necro-snapshot-checkpoint.md).

**Warrior snapshot rollout (2026-11-05 JST).** Hard-point persistence adds no effects or audio. Existing Warrior body/shield/raise/physical-return/Extra Fast placeholders and final asset gaps remain separately listed. No imagegen asset is created. See [checkpoint](warrior-snapshot-checkpoint.md).

**Learned Poison Creeper (2026-11-05 JST).** Public casting uses the identified body0x8 and summon sound0x1E5 placeholders. Heads/trails/mats currently run mechanically without field rendering. Final Creeper body/summon, vine head/trail/mat, poison/contact/wither and audio assets remain pending. The approved vulnerability rule needs no underground protection animation. Poison-state query and stat snapshots add no new artwork. See [checkpoint](creeper-learned-checkpoint.md).

**Learned Carrion/Solar (2026-11-05 JST).** Both use the existing visible silent `0x8` body placeholder, shared summon sound `0x1E5` and recovery marker `0x376A`. Final body/summon/corpse-eating/recovery/wither/audio assets remain pending. The approved always-vulnerable rule needs no mechanical underground/surface transition assets. Poison Creeper retains its separate head/trail/mat effects list and transport gate. No imagegen asset is created. See [checkpoint](recycler-learned-checkpoint.md).

**NPC on-kill curses (2026-11-05 JST).** Kill-triggered curses reuse the same identified curse marker `0x375A`, hue `0x455` and sound `0x1E5`. Final spell-specific effects/audio remain pending. Event activation adds no artwork or imagegen binding. See [checkpoint](npc-kill-procs-checkpoint.md).

**NPC item event graphs (2026-11-05 JST).** Actual supported item curses reuse the eight existing curse effects and sound placeholders (marker `0x375A`, hue `0x455`, sound `0x1E5`). Event registration and persistence add no artwork. Final curse-specific cast/state/audio assets remain pending; unsupported event producers do not count as delivered effects. See [checkpoint](unit-events-checkpoint.md).

**Eight NPC curse profiles (2026-11-05 JST).** Amplify Damage,Dim Vision,Weaken,Iron Maiden,Terror,Life Tap,Decrepify and Lower Resist retain the identified shared curse marker0x375A/hue0x455 and sound0x1E5. Their final skill-specific effect/audio assets remain pending. The full execution catalogue also retains raw presentation references for later asset work;it does not create artwork or count an unsupported spell as a delivered effect. No imagegen asset/binding is added. See [checkpoint](item-execution-metadata-checkpoint.md).

**Common skill executor (2026-11-05 JST).** The shared do/missile dispatcher adds no visual/audio asset. Actual Life Tap retains its identified curse marker and sound placeholders. Future admitted direct missile factories must reuse their spell-specific cast/travel/impact/expiry/audio requirements;the generic executor is not a visual placeholder that counts as delivered damage. No imagegen asset or binding is added. See [checkpoint](skill-execution-checkpoint.md).

**Item-effect casting (2026-11-05 JST).** Actual Blood Life Tap retains the clearly identified generic curse marker0x375A/hue0x455 and sound0x1E5;the sound now follows successful execution through notification. Final Life Tap curse/cast/audio and later NPC proc-specific effects remain pending. The generic driver creates no artwork or imagegen binding. See [checkpoint](item-effect-casts-checkpoint.md).

**Shared item proc callbacks (2026-11-05 JST).** Contract callbacks create no visual or audio assets. The actual Blood Life Tap retains its identified curse/audio placeholders; later generic proc delivery needs the corresponding spell's cast, projectile, impact, state and audio assets. Shadow/Valkyrie presentation remains pending. No imagegen asset is added. See [checkpoint](item-proc-callbacks-checkpoint.md).

**Raw proc/charge encoding (2026-11-05 JST).** Storing encoded stats produces no trigger,cast,projectile,contact,attachment or audio. Later actual proc/charge spell delivery needs its existing skill-specific effects and audio;Shadow/Valk presentation remains pending with identified placeholders. No imagegen asset is created. See [checkpoint](item-skill-property-checkpoint.md).

**Fresh item-owned property graphs (2026-11-05 JST).** Raw stat/seed/ethereal snapshots generate no cast,proc,weapon attachment or audio. Native test items are labelled unattached context fixtures with existing art. Final Shadow/Valkyrie summon/body/cast/weapon/skill presentation and later item-proc effects remain separate pending assets with identified placeholders. No imagegen asset is created. See [checkpoint](item-property-creation-checkpoint.md).

**Complete NPC affix pools/rare names (2026-11-05 JST).** Prepared selection/name services generate no item body,summon,cast,weapon effect or audio. Shadow/Valkyrie body/summon/cast/weapon/skill presentation and audio remain separately pending with identified placeholders. Unsupported source properties are retained for later real consumers;no imagegen asset is created. See [checkpoint](npc-item-affixes-checkpoint.md).

**Shared item-affix orchestration (2026-11-05 JST).** This prepared service component produces no summon,item body,cast,weapon effect or audio. Shadow summon/body/cast/weapon/skill effects and audio remain separately pending with identified placeholders;no imagegen asset is generated. See [checkpoint](item-affixes-checkpoint.md).

**Shadow equipment requests (2026-11-05 JST).** Request selection emits no visual/audio producer. Shadow summon/cast/body/weapon/skill effects and audio remain pending;identified placeholders will be tracked when actors/casting are connected. No final or imagegen asset is supplied. See [checkpoint](shadow-equipment-checkpoint.md).

**Prepared Dragon Flight landing (2026-11-05 JST).** Actual prepared transfer emits the existing shared **generic Teleport placeholders**:0x3728departure/arrival particles and0x1FEaudio,followed by existing native arrival-field effects. These are identified temporary effects,not final Flight assets. Dedicated teleport/kick/contact/charge presentation and audio remain pending;mechanical landing tests proceed independently. See [checkpoint](dragon-flight-landing-checkpoint.md).

**Dragon Flight fixed clock (2026-11-05 JST).** Teleport departure/arrival,kick/contact,charge release and audio remain pending separately. The new fixed two-event plan emits no particles or audio and supplies no final asset. Existing generic placeholders remain identified candidates for later learned-action integration. See [checkpoint](dragon-flight-mode-checkpoint.md).

**Dragon Flight sequence preparation (2026-11-05 JST).** Departure/arrival, kick/contact, progressive charge release and audio remain needed separately. The prepared two-event scheduler adds no effect producer or final artwork. Existing generic teleport/kick/charge effects are candidate identified placeholders for the future learned action; none is presented as final art or emitted by this planner. See [checkpoint](dragon-flight-preparation-checkpoint.md).

**Learned Dragon Tail (2026-11-05 JST).** Final KK windup/kick, contact/block, martial-charge release, radial fire burst, knockback/get-hit presentation and audio remain needed. Native Wrestle pose, sound0x238, existing charge markers and burst0x36BD/hue0x489 are identified placeholders. Actual damage, source knockback trajectories and expiry work independently of final art. No imagegen asset is generated. See [checkpoint](dragon-tail-learned-checkpoint.md).

**Dragon Tail preparation (2026-11-05 JST).** Clock and hit suppliers add no effect producer or final art. Kick/contact/charge/fire-burst/displacement/audio remain needed. Existing native presentation and fire burst0x36BD/hue0x489 are identified placeholders. Mechanical work proceeds independently; no learned route is enabled by this increment. See [checkpoint](dragon-tail-preparation-checkpoint.md).

**Learned Wake of Fire (2026-11-05 JST).** Final E9 emergence, firing, neutral and collapse art; moving maker trail; spreading fire waves/contact; cast/firing/impact audio remain needed. Body 0x9C/hue 0x489, fire marker 0x36D4/hue 0x489, native throw pose and cast sound are identified placeholders. The learned route adds no new effect producer or imagegen asset. Existing logical projectile damage/expiry and retirement timing are independent of unfinished art. See [checkpoint](wake-learned-checkpoint.md).

**Poison Creeper balance (2026-11-05 JST).** Higher damage adds no new effect producer. Final body/surface/burrow, vine heads/mats, poison contact/status, wither and audio assets remain needed; existing visible bodies and field/contact presentation are identified placeholders. The damage decision does not determine surface exposure timing or certify the graphical client. No imagegen asset is created. See [checkpoint](creeper-balance-checkpoint.md).

**Learned Lightning Strike (2026-11-05 JST).** Final spear/javelin lightning contact, travelling chain segments, impacts and audio are still needed. Native weapon pose/sound 0x238, contact 0x374A and chain 0x3818 are identified mechanical placeholders. Visible chains follow the approved opening from the struck monster; none is emitted without another eligible enemy. No imagegen/final asset is created. See [checkpoint](lightning-strike-checkpoint.md).

Ordinary curse catalogue expansion needs the existing blind/fear recipient feedback and audio across more source identities. Existing placeholders remain identified; no new effect producer, final artwork or imagegen asset is introduced. See [checkpoint](curse-ai-catalogue-checkpoint.md).

Shared curse AI family expansion adds no new effect producer. Existing Dim Vision blind feedback and Terror/fear presentation/audio remain identified placeholders for Fallen/Brute/Goatman recipients; final effects are still needed separately. Mechanical checks continue independently. See [checkpoint](curse-ai-families-checkpoint.md).

**Charged Strike speed capture (2026-11-05 JST).** Slowed bolts use the existing identified lightning trail/contact/audio placeholders with their captured movement speed. No additional effect or imagegen asset is introduced; final bolt/impact/audio work remains. See [checkpoint](charged-strike-velocity-checkpoint.md).

**Actual old-writer army upgrade (2026-11-05 JST).** Former poison Mages use the existing Ice Bolt and intrinsic Frozen Armor effects/placeholders after loading. Existing warrior, Mage cast/volley/contact/cold/status/audio assets remain pending as recorded; the upgrade adds no new effect or imagegen asset. See [checkpoint](necro-legacy-army-checkpoint.md).

**Earlier-build Necromancer upgrade (2026-11-05 JST).** Save/reader verification adds no effect or imagegen asset. Existing Bone Armor and Clay placeholders and final effects/audio requirements remain; see [checkpoint](necro-legacy-upgrade-checkpoint.md).

**Player melee return ordering (2026-11-05 JST).** No new standalone effect or imagegen asset. Existing Shiver/cold retaliation, Thorns and player-death cast/contact/status/audio placeholders retain their requirements. The admitted path keeps the late reaction before death cleanup; raw/unsupported contexts stay explicit. See [checkpoint](player-melee-return-checkpoint.md).

**Raise corpse-location towns (2026-11-04 JST).** Protected-corpse refusals create no summon,birth animation or effect. Permitted warrior/Mage field births retain their identified placeholders and final birth/combat/status/audio requirements. Shared region validation adds no standalone asset or imagegen work. See [checkpoint](raise-corpse-town-checkpoint.md).

**Explosion corpse-location towns (2026-11-04 JST).** Rejected protected-corpse casts emit no animation,cloud or explosion. Existing permitted-field CE/poison placeholders and final cast/travel/contact/status/audio requirements remain. The shared guard adds no standalone asset;no imagegen run. See [checkpoint](corpse-town-checkpoint.md).

**Poison Explosion ground targeting (2026-11-04 JST).** Shared acquisition/client point routing adds no standalone effect. Existing eight-cloud cast/travel/contact/poison/status/audio placeholders and final requirements remain; failed acquisition/release creates none. No imagegen assets were created. See [checkpoint](poison-corpse-aim-checkpoint.md).

**Necromancer town permissions (2026-11-04 JST).** Rejected town starts have no cast animation/effect/payment. Permitted Bone Armor and golem casts retain their identified existing placeholders and pending final cast/birth/status/audio work. No new standalone effect or artwork is needed for the location predicate. See [checkpoint](necro-town-checkpoint.md).

**Corpse Explosion ground targeting (2026-11-04 JST).** The targeting aid reuses the existing identified `0x36BD` explosion and cast/audio placeholders. Final corpse detonation and casting feedback/audio remain pending; no new effect or imagegen artwork was created. See [checkpoint](corpse-aim-checkpoint.md).

**Bone Prison ground acquisition (2026-11-04 JST).** The approved targeting aid reuses the existing identified cast and barrier placeholders. It adds no separate effect or audio cue. Final Bone Prison cast/barrier/status feedback and audio remain pending independently of mechanics. See [checkpoint](bone-prison-aim-checkpoint.md).

**Original summon Iron Maiden (2026-11-04 JST).** Cursed summon contact reuses the existing identified curse-state and retaliation placeholders, including `0x374A`/hue `0x481`. Final Iron Maiden state/return feedback and audio remain pending; this correction adds no separate effect or artwork. See [checkpoint](npc-iron-maiden-checkpoint.md).

**NPC allocation-time armor reduction (2026-11-04 JST).** The mechanical setup correction reuses existing Revive/Iron contact and status placeholders. It creates no separate skill effect or audio cue. Final summon/contact/status feedback and audio remain pending in this register; no imagegen artwork was created. See [checkpoint](npc-armor-setup-checkpoint.md).

**NPC-held defense conditions (2026-11-04 JST).** Retained player-only item flags create no NPC debuff, effect or audio. Revive/Iron summon/contact/status placeholders and final presentation requirements remain; Razoredge keeps the existing Open Wounds effect. This correction creates no standalone asset. See [checkpoint](npc-defense-conditions-checkpoint.md).

**NPC Prevent Monster Heal admission (2026-11-04 JST).** A retained inert NPC item property adds no PMH state, effect or audio. Existing Revive/Iron summon/contact/status placeholders and pending final artwork remain. Player PMH presentation is unchanged. See [checkpoint](npc-prevent-heal-checkpoint.md).

**Qualified equipped Revive (2026-11-04 JST).** Learned equipped casting reuses the existing identified Revive summon/windup/contact/status/return placeholders. This admission change adds no standalone effect. Final original-body feedback and audio remain pending; no imagegen asset was created. See [checkpoint](revive-equipped-checkpoint.md).

**Original Revive native A1 (2026-11-04 JST).** The native A1 conductor uses the identified native attack animation4/frame5 placeholder and existing shared contact/block/wound/return cues. Final original-body windup/contact/miss/block/status/return feedback and audio remain pending. Timing and packet persistence add no independent effect. See [checkpoint](revive-native-swing-checkpoint.md).

**Original Revive A1 clocks (2026-11-04 JST).** The owned A1 clock service adds no effect or audio. Revive’s existing identified windup/contact/block/wound/return placeholders and pending final feedback remain; verified timing metadata does not supply animation artwork. See [checkpoint](revive-swing-clock-checkpoint.md).

**Original Revive live melee (2026-11-04 JST).** Live original geometry/result/contact reuses the existing identified Revive contact, block, wound and return placeholders. Final windup/contact/miss/block/status/return feedback and audio remain pending; this service adds no standalone effect. See [checkpoint](revive-melee-geometry-checkpoint.md).

**Original Revive ownership graph (2026-11-04 JST).** The frozen item graph and native resource-recovery gates add no visual effect or audio. Revive keeps its existing identified summon/contact/return placeholders and pending final artwork. See [checkpoint](revive-graph-checkpoint.md).

**Original Revive Kind1 incoming (2026-11-04 JST).** The [incoming HP adapter](revive-incoming-checkpoint.md) shares the existing hit/retaliation feedback placeholders. It adds no effect ID or standalone effect. Revive’s pending summon/contact/return feedback and audio remain in the existing shared asset work; the mechanical HP/credit changes do not require artwork to function.

**Original Revive event/contact reuse (2026-11-04 JST).** The [prepared original-body contact component](revive-event-checkpoint.md) reuses existing Open Wounds/bleed cues, Crushing Blow overlay147 placeholder (`0x376A`, hue `0x21`), packet/contact overlay `0x37B9`/hue `0x481` and shared retaliation flash `0x374A`/hue `0x481`. Final application/contact/status/expiry feedback and audio remain pending in the shared wound/Crushing/return asset work; no new effect is created here. Native swing/action integration and current subscription chronology remain separate mechanical gates. Talent/hotkey icons remain in the icon ledger.

**Defiler vital body (2026-11-04 JST).** The approved Gazer body22/sound377 is available as appearance only. No new effect is generated by stat/owner persistence. Generic feedback remains identified placeholder presentation; final summon/S3, Health Link and hurt/death effects/audio remain pending. The user confirms link/follow support behavior with no ordinary attack, so no ordinary-attack effect is required. Health Link and follow mechanics remain unfinished. See [checkpoint](defiler-body-checkpoint.md).

**Demon incoming packets (2026-11-04 JST).** Source class/mode metadata and friendship protection introduce no new effect. Existing native hurt/death and generic aura markers remain identified placeholders; final demon summon,attack/spell,aura,hurt/death and audio remain pending. See [checkpoint](demon-incoming-checkpoint.md).

**Tainted resistance aura (2026-11-04 JST).** The prepared automatic NPC420 aura uses generic0x375A/hue0x489 as an identified placeholder. Final aura visuals/audio, summon/cast, Fire Ball/contact, Blood Boil reaction and hurt/death assets remain pending. Effects do not gate the mechanical pulse; icons remain in their own register. See [checkpoint](tainted-aura-checkpoint.md).

**Tainted rank refresh (2026-11-04 JST).** Sharing the NPC getter and refreshing the A2 release rank adds no new effect/audio asset. Existing invisible logical windup and generic0x36D4travel/0x36BDexplosion remain identified placeholders. Final cast/contact/aura/Blood Boil feedback remains pending independently. See [checkpoint](tainted-ranks-checkpoint.md).

**Tainted A2 release (2026-11-04 JST).** The automatic cast service currently changes facing and has an invisible logical A2 windup, an identified presentation placeholder. Final NPC A2 windup/release and audio are still required; generic0x36D4travel/0x36BDexplosion remain interim markers. This uses the approved existing Succubus artwork and adds no new base model request. Mechanical event scheduling proceeds independently. See [checkpoint](tainted-casting-checkpoint.md).

**Tainted Fire Ball flight (2026-11-04 JST).** Saved NPC422/missile734 travel now uses generic0x36D4 and its explosion uses generic0x36BD, both identified interim markers. Final summon/cast, flight/explosion/contact and audio, Resist Fire aura, Blood Boil and hurt/death presentation remain pending. Approved existing Succubus artwork is reused. Mechanical release/transport can proceed independently; see [checkpoint](tainted-fireball-checkpoint.md).

**Tainted body/hit backend (2026-11-04 JST).** A prepared saved body now reuses the approved Succubus artwork; no new base model is requested. Native sound0x4B0 and stationary/native pose are interim presentation. The qualified fire receiver adds no spell-specific visual/audio. Summon/cast, actual Fire Ball flight/explosion/contact, Resist Fire aura, Blood Boil and hurt/death feedback remain pending independently of mechanics. See [checkpoint](tainted-body-checkpoint.md).

**Tainted (2026-11-04 JST).** The approved UO Succubus mapping reuses existing body artwork; no new base model is requested. Prepared numerical suppliers emit no body or effect. Summon spawn/cast, Fire Ball flight/explosion/contact, Resist Fire aura, Blood Boil reaction, hurt/death and audio still need their final presentation. Any generic spell markers used during later integration must remain identified placeholders. Talent/right/quick artwork is tracked in the separate icon register; internal NPC420/422 add no player hotkey icons.

**Druid human SC routing (2026-11-04 JST).** No final art or sound is added. Human/form cast, elemental/armor and bird/wolf/bear/spirit emergence placeholders remain separate asset work. See [checkpoint](druid-human-cast-clock-checkpoint.md).

**Assassin SC timing (2026-11-04 JST).** No final graphics or sound are added. Cast, psychic hit, cloak/blind, speed/fade/venom and blade-state/pulse placeholders remain separate asset work, including claw-dependent casting presentation. See [checkpoint](assassin-cast-clock-checkpoint.md).

**Sorceress SC timing (2026-11-04 JST).** No final art or sound is added. Existing spell cast, field, projectile, armor and Teleport/arrival placeholders remain independent asset work, including weapon-dependent presentation. See [checkpoint](sorceress-cast-clock-checkpoint.md).

**Barbarian SC timing (2026-11-04 JST).** No final art or sound is added. Cry/wave, corpse-search, drop and affected-state placeholders remain separate work, including weapon-dependent casting presentation. See [checkpoint](barbarian-cast-clock-checkpoint.md).

**Paladin SC timing (2026-11-04 JST).** No final art or sound is added. Existing Holy Bolt, Hammer, Holy Shield and FoH cast/projectile/buff/impact placeholders remain separate asset work. See [checkpoint](paladin-cast-clock-checkpoint.md).

**Prepared Slow Missiles windup (2026-11-04 JST).** Cast overlay,affected-state visuals and amazon_handofathena audio remain missing final assets. The prepared flow keeps the identified ordinary cast marker and adds no final art/sound. See [checkpoint](slow-missiles-windup-checkpoint.md).

**Necromancer SC timing (2026-11-04 JST).** No final graphics or sound are added by the shared clock. Existing cast,curse,projectile,corpse/summon and contact placeholders remain;final effects and audio are still required independently of timing acceptance. See [checkpoint](necromancer-cast-clock-checkpoint.md).

**Amazon SC timing (2026-11-04 JST).** Source action/end clocks add no finished artwork. Existing Inner Sight/Slow Missiles cast overlays, Decoy silhouette and cast audio remain identified placeholders; final weapon-dependent cast/effect/Decoy visuals and audio still need assets. See [checkpoint](amazon-cast-clock-checkpoint.md).

**Ward periodic world pulse (2026-11-04 JST).** Real area curse effects still use the identified ordinary curse marker; fixture bodies use an explicitly named placeholder. Final Ward start/body/end,fear/slow/expiry and audio remain missing. No final visual or audio asset is created by the pulse adapter. See [checkpoint](grim-ward-world-pulse-checkpoint.md).

**Prepared Ward monster effects (2026-11-03 JST).** Actual recipient effects reuse the identified curse-marker placeholder. Final Ward start/body/end, fear/slow/expiry and audio remain missing; the saved payload introduces no finished artwork. See [checkpoint](grim-ward-transport-checkpoint.md).

**Prepared Grim Ward curse operation (2026-11-03 JST).** The stat operation adds no final artwork. Ward start/body/end,fear/slow/expiry and audio remain required;mechanical result checks proceed independently. See [checkpoint](grim-ward-curse-checkpoint.md).

**Saved shared pet placement registry (2026-11-03 JST).** The internal registry needs no visual effect. Native pet body/movement/teleport/contact/death/audio remain identified placeholders;final effects/audio stay separately pending. See [checkpoint](pet-placement-world-checkpoint.md).

**Primary pet target-reset acceptance (2026-11-03 JST).** Target metadata adds no artwork or audio. Native pet body/movement/teleport/contact/death/audio remain identified placeholders;final effects/audio stay separately pending. See [checkpoint](pet-path-targets-checkpoint.md).

**Current room-link/hook acceptance (2026-11-03 JST).** Reference comparisons add no new artwork or sound. Native pet body/movement/teleport/contact/death/audio remain identified placeholders;final effects/audio stay independently pending. See [checkpoint](pet-room-links-checkpoint.md).

**Native prepared-pet placement (2026-11-03 JST).** Actual world movement still uses identified native body/movement/teleport/contact/death/audio placeholders. This backend adds no separate final artwork;effects/audio remain pending independently. See [checkpoint](pet-native-placement-checkpoint.md).

**Current collision writes (2026-11-03 JST).** Occupancy updates add no independent artwork. Existing native pet body/movement/teleport/contact/death/audio remain identified placeholders;final effects/audio stay separately pending. See [checkpoint](pet-collision-writes-checkpoint.md).

**Current collision reads (2026-11-03 JST).** Room/grid/cardinal/box reads add no independent artwork. Existing native pet body/movement/teleport/contact/death/audio remain identified placeholders;final effects/audio stay pending separately. See [checkpoint](pet-collision-reads-checkpoint.md).

**Current nearby placement (2026-11-03 JST).** Candidate search adds no independent artwork. Existing native pet body/movement/teleport/contact/death/audio remain identified placeholders;final effects/audio stay pending separately. See [checkpoint](generic-pet-nearby-checkpoint.md).

**Current footprint dispatch (2026-11-03 JST).** Collision masks and occupancy lanes require no independent artwork. Existing native pet movement/teleport/body/combat/hurt/death/audio cues remain identified placeholders;final effects/audio stay separately pending. See [checkpoint](generic-pet-footprints-checkpoint.md).

**Current path commitment backend (2026-11-03 JST).** Raw coordinate/room/reset preparation adds no independent artwork. Existing native pet movement/teleport/body/combat/hurt/death/audio cues remain identified placeholders;final effects/audio remain pending. Mechanical helper acceptance does not certify world relocation or final presentation. See [checkpoint](generic-pet-path-commit-checkpoint.md).

**Original pet reconnect acceptance (2026-11-03 JST).** Returning the existing saved body adds no new art or mandatory summon flash. Existing native body/step/attack/contact/death/audio remain identified placeholders;final effects stay independently pending. See [checkpoint](pet-reconnect-checkpoint.md).

**Native obstacle acceptance (2026-11-03 JST).** Actual blocking-wall/follow tests add no final artwork. Existing native skeleton/body/step and other summon cues remain identified placeholders;final movement/contact/death/audio assets remain pending separately. See [checkpoint](pet-obstacle-checkpoint.md).

**Shared pet obstacle repair (2026-11-03 JST).** SourceAStarPath adds no separate artwork or sound. Existing native summon/body/step/attack/contact/death cues remain identified placeholders;final pet movement feedback remains pending. Mechanical path repair is verified separately in [checkpoint](astar-path-checkpoint.md).

**Frenzy release/charge preparation (2026-11-03 JST).** Internal state and graphic-flag callbacks add no live effect. Final paired strikes,charge/speed presentation,contact/miss/block and audio remain pending;ordinary weapon visuals remain identified placeholders when bound. No charge orb or aura artwork is implied by the control-flow checks. See [checkpoint](frenzy-release-checkpoint.md).

**Double Swing release/target policy (2026-11-03 JST).** Internal target selection and release ordering add no artwork or live player strike. Final Double Swing weapon-swing/contact/miss/block and audio remain pending under existing class requirements;normal weapon effects remain identified placeholders. No new autonomous effect is introduced. See [checkpoint](double-swing-phase-checkpoint.md).

**Shared player channel controller (2026-11-03 JST).** Timed fire/cold emissions reuse existing identified stream placeholders: Inferno0x36D4/hue0x489 and Arctic0x374A/hue0x480. No final cast/channel/contact/expiry/audio is created by the controller. Native effects and mechanical clocks are separate; final asset work remains pending. See [checkpoint](player-inferno-modes-checkpoint.md).

**Approved Warlock native models and Inferno stream (2026-11-03 JST).** Goatman now uses Gargoyle4/basic sound372; its old0x11visual placeholder is superseded. Defiler uses Gazer22/sound377 and Tainted Succubus149/sound0x4B0 when their bodies are implemented. These native body/basic-sound choices are approved assets; special summon/teleport/combat/skill VFX and custom audio still need their own work and animation review. Inferno's shared fire effect0x36D4/hue0x489 remains an identified placeholder; final channel/stream/contact/expiry/audio are missing. See [models](warlock-summon-models-checkpoint.md) and [stream](inferno-stream-checkpoint.md).

**Landing validation and placement policy (2026-11-03 JST).** These internal mechanics add no final artwork. Final Goatman summon/run/teleport/combat/skill/hurt/death effects and audio remain missing; native body0x11 remains an identified placeholder. Actual world relocation/teleport presentation is still pending. See [checkpoint](generic-pet-warp-candidate-checkpoint.md).

**Warp room-search/update primitives (2026-11-03 JST).** These internal helpers add no final artwork. Final Goatman summon/run/teleport/combat/skill/hurt/death effects and audio remain pending;native body0x11 is an identified placeholder. Physical world relocation and a live teleport visual are not enabled by this batch. See [checkpoint](generic-pet-warp-primitives-checkpoint.md).

**GenericPet warp recovery policy (2026-11-03 JST).** This internal recovery policy adds no final artwork or live teleport effect. Final Goatman summon/run/teleport/combat/skill/hurt/death effects and audio remain pending; native body0x11 is an identified placeholder. See [checkpoint](generic-pet-warp-checkpoint.md).

**GenericPet motion services (2026-11-03 JST).** Moving-state/relative-step policies add no final artwork. Final Goatman summon/run/teleport/combat/skill/hurt/death visuals and audio remain pending; native body0x11 is an identified placeholder. The newly captured warp routine has no implemented or approved final effect yet. See [checkpoint](generic-pet-motion-services-checkpoint.md).

**Composed GenericPet movement (2026-11-03 JST).** Verified movement services add no final artwork. Actual Goatman movement/teleport is still awaiting world integration; final summon/run/teleport/combat/skill/hurt/death effects and audio remain pending. Native body0x11 is an identified placeholder. See [checkpoint](generic-pet-follow-movement-checkpoint.md).

**GenericPet follow executor (2026-11-03 JST).** This internal policy creates no final art. Goatman follow/run/teleport, summon/attack/skill/hurt/death visuals and audio remain pending; its native body0x11 remains an identified placeholder. Actual world movement is still unfinished, so no teleport effect is claimed. See [checkpoint](generic-pet-follow-executor-checkpoint.md).

**Prepared Goatman body (2026-11-03 JST).** Native body0x11,placeholder name and absent final sounds are explicit placeholders. Final goatman summon/body/idle/walk/run/melee/Stun/Berserk/Frenzy/Cleave/hurt/block/death/teleport and audio remain pending. Preparing life/persistence does not generate art or certify an animation clock. See [checkpoint](goatman-body-checkpoint.md).

**Prepared vine windup (2026-11-03 JST).** Ordinary SC pose/messages and sound0x1E5 are identified placeholders. Final summon/burrow/surface/movement/poison-field/contact/corpse-consume/recovery/hurt/death/audio assets remain pending for all3vines. Visual creation will need the verified exposure timeline; this cast layer does not invent it. See [checkpoint](vine-cast-windup-checkpoint.md).

**Grizzly approved upgrades (2026-11-03 JST).** Final B7body/movement/summon/swipe/swat/hurt/death/teleport, stun/CB overlays and audio remain pending. Native body0xD4, stun0x375A/hue0x481 and CB0x376A/hue0x21 are identified placeholders. Shared NPC original-item CB adds no final artwork. See [checkpoint](grizzly-upgrades-checkpoint.md).

**Dire Wolf approved affixes (2026-11-03 JST).** Passive movement/strength changes reuse the current logical encounter modifiers. Final WFbody,movement,summon,feeding/rage,bite,hurt/death,teleport and audio remain pending; existing native presentation is an identified placeholder. No final artwork generated; see [checkpoint](dire-wolf-upgrades-checkpoint.md).

**Spirit Wolf approved upgrades (2026-11-03 JST).** Wounds reuse the shared logical bleed consumer; final bleed/contact visual/audio and wolf summon/bite/hurt/death/teleport effects remain pending. Existing body0xE1 and native effects are identified placeholders. The cold-resistance floor adds no final artwork. See [checkpoint](spirit-wolf-upgrades-checkpoint.md).

**Player attack-rate slow correction (2026-11-03 JST).** Shared arithmetic adds no final effects or audio. Existing per-skill cast, attack, status and contact placeholders remain independently pending. Actual-client animation/event acceptance is still required; see [checkpoint](player-attack-slow-checkpoint.md).

**Blade Fury reentry (2026-11-03 JST).** Server-mode entry/lease correctness creates no final player startup/loop/release pose. Bladefragment1/2 travel/contact and audio remain missing; existing native weapon-flight rendering/component plans are placeholders. No new artwork is generated. See [checkpoint](blade-fury-reentry-checkpoint.md).

**Automatic Wake/death (2026-11-03 JST).** Logical E9 death/retained issued fire supplies no final collapse artwork. Body0x9C/hue0x489 and marker0x36D4/hue0x489 remain identified placeholders. Final emergence/firing/neutral/collapse, maker trail, spreading wave/contact and audio are pending. See [checkpoint](wake-fire-automatic-checkpoint.md).

**Prepared Wake travelling fire (2026-11-03 JST).** Real maker/wave mechanics use native0x36D4/hue0x489 as an identified placeholder. Final E9 actor emergence/firing/neutral/collapse, source517trail, spreading518wave/contact and audio remain missing. No final artwork follows from travelling damage. See [checkpoint](wake-fire-missiles-checkpoint.md).

**Learned Death Sentry (2026-11-03 JST).** Native S2 throw/sound 0x23B, body 0x9C/hue 0x482, lightning marker 0x3818/hue 0x482 and corpse explosion 0x36BD are identified placeholders. Final LG emergence/firing/neutral/death, missile641 travel/contact, physical/fire explosion and audio remain pending. Logical casting/retirement does not complete visuals. See [checkpoint](assassin-death-learned-checkpoint.md).

**Learned Wake of Inferno (2026-11-03 JST).** Native S2 throw/sound 0x23B, body 0x9C/hue 0x489 and fire marker 0x36D4/hue 0x489 remain identified placeholders. Final E9 emergence/sequence/firing/neutral/death actor, beam/trail/contact and audio remain pending. Logical 25-frame death timing supplies no collapse artwork. See [checkpoint](assassin-inferno-learned-checkpoint.md).

**Learned Lightning Sentry (2026-11-03 JST).** Native S2 throw/sound 0x23B, body 0x9C/hue 0x482 and straight bolt marker 0x3818/hue 0x482 remain identified placeholders. Final S1/S2/neutral/death actor, source526 bolt travel/contact and audio remain pending. Logical casting/retirement does not complete visuals. See [checkpoint](assassin-lightning-learned-checkpoint.md).

**Learned Charged Bolt Sentry (2026-11-03 JST).** Native S2 throw animation and sound 0x23B, stationary body 0x9C/hue 0x482 and bolt marker 0x3818/hue 0x482 are identified placeholders. Final emergence/firing/neutral/death actor animations, bolt travel/impact and audio remain pending. Logical timing and casting availability do not complete artwork. See [checkpoint](assassin-sentry-learned-checkpoint.md).

**Charged Bolt Sentry retirement (2026-11-03 JST).** Mechanical death timing now exists, but the visible collapse/death animation and its sound are still missing. The original actor remains visible during its logical transition and becomes hidden at completion; its current body/hue, throw sound and bolt marker remain identified placeholders. Final art/audio is separate from the [mechanical checkpoint](sentry-retirement-checkpoint.md).

**Prepared Sentry retirement (2026-11-03 JST).** Logic can retain an invisible original dead actor for released missiles. UO bolt effect0x3818/hue0x482 remains an identified placeholder; final emergence, firing, impact and collapse effects/audio remain pending. No corpse-retention art or imagegen completion is implied. See [checkpoint](sentry-corpse-pool-checkpoint.md).

**Learned Blade Sentinel (2026-11-03 JST).** S2 native throw animation, sound0x23B and actor body0x9C hue0x481 are identified placeholders. Final summon/emergence/blade motion/contact/expiry effects and audio remain pending; no final imagegen work. The generated entry is updated independently of icons. See [checkpoint](assassin-blade-cast-checkpoint.md).

**Slow Missiles prepared release (2026-11-03 JST).** Identified caster placeholder: UO0x375A hue0x481. Final cast_slowmissiles, innersight target overlay and amazon_handofathena audio remain pending alongside the existing logical projectile markers. The generated effect entry is updated; icons remain separate. See [checkpoint](amazon-slow-missiles-cast-checkpoint.md).

**Barbarian binding follow-up (2026-11-03 JST).** Mouse/save policy adds no visual or audio effects. Existing 240-entry effect register and identified placeholders remain unchanged. See [checkpoint](barbarian-binding-checkpoint.md).

**Remaining Paladin aura save verification (2026-11-03 JST).** [Save-safety acceptance](paladin-aura-persistence-checkpoint.md) creates no final assets. Resistance/travel/elemental/return/undead/debuff aura overlays,pulse and impact art,and audio remain separate pending work. Existing effects remain identified placeholders.

**Offensive aura verification (2026-11-03 JST).** [Concentration/Fanaticism checkpoint](paladin-offensive-aura-checkpoint.md) creates no final assets. Separate aura rings/overlays and selection/refresh/expiry audio remain missing;shared `0x375A` stays an identified placeholder.

**Resource aura verification (2026-11-03 JST).** [Prayer/Cleansing/Meditation checkpoint](paladin-resource-aura-checkpoint.md) creates no final assets. Individual aura rings/overlays and selection/heal/cleanse/refresh/expiry audio remain missing;shared `0x375A` remains an identified placeholder.

**Blessed Aim verification (2026-11-03 JST).** [Mechanics checkpoint](paladin-blessed-aim-checkpoint.md) adds no final assets. Front/back aura overlays and selection/refresh/expiry audio remain missing; shared `0x375A` stays an identified placeholder.

**Defiance verification (2026-11-03 JST).** Shared mechanics reuse is accepted within the [checkpoint](paladin-defiance-aura-checkpoint.md); final front/back aura rings and selection/refresh/expiry audio remain missing. Shared `0x375A` is an identified placeholder. No final effect/audio assets were created.

Might/shared aura persistence review (2026-11-03): overlay `0x375A` remains an identified placeholder. Final front/back rings, selection/refresh/expiry presentation and audio remain pending. Atomic save/restore and instant input acknowledgements introduce no new asset. See [checkpoint](paladin-might-aura-checkpoint.md).

Holy Shield start/persistence review (2026-11-03): enchanted-shield overlay `0x373A` and cast sound `0x1E5` remain identified placeholders. Final cast, persistent shield/glow, expiry and audio remain pending. Start-payment tickets, rank retention and save safety introduce no new effect asset. See [checkpoint](paladin-holy-shield-cast-checkpoint.md).

FoH secondary migration (2026-11-03): shared source flight and typed impact add no new asset. Travel/trail `0x36E4` and existing cast/strike effects are identified placeholders. Final secondary trail, impact, wall/expiry and audio remain pending; potion-added cold/poison do not create chill/poison status effects. See [checkpoint](paladin-heaven-bolt-checkpoint.md).

FoH primary receiver review (2026-11-03): sky-warning/strike `0x3709`, secondary travel `0x36E4` and cast sound `0x1E5` remain **identified placeholders**. Final warning, lightning strike, burst, secondary trail/contact/expiry and audio remain pending separately. Impact potion additions add damage without poison/chill status effects. See [checkpoint](paladin-heaven-fist-damage-checkpoint.md).

Hammer curved source-flight review (2026-11-03): persisted curve/RNG/collider/lifecycle state adds no new asset. Hammer/trail `0x36E4`, hue `0x481` and cast sound `0x1E5` remain **identified placeholders**. Final source-timed travel/contact/wall/expiry/audio remains pending independently. FoH startup preparation introduces no effect; its warning/sky-strike/burst/secondary presentation remains separately pending.

Blessed Hammer damage/capture review (2026-11-03): hammer/trail `0x36E4`, hue `0x481` and cast sound `0x1E5` remain **identified placeholders**. Final hammer travel/trail, contact, wall/expiry and audio are pending. Typed damage and impact potion additions introduce no poison/chill status effect. See [checkpoint](paladin-blessed-hammer-damage-checkpoint.md).

Holy Bolt source-flight review (2026-11-02): saved path/RNG and reference cleanup add no new visual asset. Holy travel/trail `0x36E4`,healing `0x376A` and existing cast/impact sounds remain **identified placeholders**. Final source-timed travel,healing/hit,wall/expiry and audio remain pending independently.

Holy Bolt typed receiver/capture review (2026-11-02): projectile/trail `0x36E4`,healing `0x376A` and cast sound `0x1E5` remain **identified placeholders**. Shared typed return adds no new effect;final holy impact/healing/expiry/audio assets remain pending. Potion additions cause damage without poison/chill status effects.

Charge contact review (2026-11-02): source hit class `0x70` combines with the ordinary weapon class; overlay147 currently uses native `0x374A` as an **identified placeholder**. Existing weapon sound `0x238`, travel/impact, knockback/get-hit, cold/poison/leech and audio need final assets. Failed weapon rolls still request the mapped knockback reaction. Adjacent basic-attack fallback retains ordinary presentation.

Conversion contact review (2026-11-02): ordinary failed-attempt sound 0x238 and shared impact/block/retaliation/cold/poison/leech effects remain identified placeholders. Final allegiance onset, friendly-state marker and expiry presentation require assets. Successful conversion bypasses weapon damage and its associated contact effects.

Zeal per-event contact review (2026-11-02): existing weapon sound0x238 and shared block/hit/return,cold/poison/leech effects are identified placeholders. Final rapid melee animation/rollback feedback,weapon-specific impacts,miss/block/retaliation and audio still require assets; the approved low-mana ordinary attack uses its ordinary presentation.

[Vengeance allocation/contact](paladin-vengeance-contact-checkpoint.md) retains weapon sound `0x238` and contact marker `0x374A` as identified placeholders. Final combined elemental contact and cycling fire/cold/lightning feedback, cold/poison/leech overlays and audio remain pending. These feedback layers do not alternate the actual damage elements; each successful hit still carries all three.

[Smite ordered shield contact](paladin-smite-contact-checkpoint.md) keeps the existing shield-bash/S1 pose, sound `0x238`, stun/block marker `0x375A`, contact, knockback/get-hit and leech placeholders. Final timing/pose/overlay/audio artwork remains pending; the shared ordered phases add no independent effect.

[Sacrifice ordered contact](paladin-sacrifice-contact-checkpoint.md) shares existing weapon/blood/contact, shield, leech, cold/poison and native reaction placeholders. The shared adapter adds no new independent effect; final blood emission, block/get-hit/return feedback and audio remain pending.

[Sacrifice start allocation](paladin-sacrifice-allocation-checkpoint.md) reuses the existing weapon contact, sound `0x238` and blood effect `0x37B9` as identified placeholders. Windup capture and cancellation introduce no new asset; final blood emission/contact/audio remain pending.

The [Paladin baseline/binding checkpoint](paladin-baseline-checkpoint.md) retains the existing aura, weapon, projectile, shield and contact placeholders. Slot validation and corrected approved-pet/species fixtures add no effect or audio requirement; final per-skill effects remain pending independently.

The [Sorceress coverage/binding review](sorceress-coverage-review.md) changes mouse-slot admission and saved normalization only. It introduces no new particle, body animation or audio asset. Existing per-skill placeholders and missing-effects entries remain authoritative.

[Teleport](sorceress-player-teleport-checkpoint.md) uses the existing cast pose. On successful transfer, a cyan crease closes at departure and a second one opens at the landing point. Learned casts and scrolls share this effect and its transfer cue. [ImageGen sheet](../art/spell-effects/teleport/sheet.png)

The [Hydra](hydra-world-checkpoint.md) summons three fire serpents that rise from embers, turn toward their targets, open their jaws at release, and sink at retirement. ClassicUO positions each sprite from its projected head location. The server keeps its existing nonblocking emitters. The fire bolts use their own directional travel and ember burst. [Hydra sheet](../art/spell-effects/hydra/sheet.png) · [Bolt sheet](../art/spell-effects/hydra-bolt/sheet.png)

Frozen Orb uses a rotating core and directional ice bolts. The shared cold contact cue accompanies successful bolt hits, and a fuller shatter marks the terminal burst. [Frozen Orb](sorceress-player-frozen-orb-checkpoint.md)

Blizzard cues its release, plays wind while shards are falling, and plays an ice tick on damaging shard contact. Wind and contact cues have separate repeat intervals.

Meteor and the Fire Mage's Meteor share a ground warning, falling stone, impact, and flames on active ground tiles. Both use the same cast, descent, impact, and fire cues.

[Fire Wall](sorceress-fire-wall-native-checkpoint.md) ignites each cell as its emitters reach it. The flames loop and die down according to each cell's lifetime. Visuals on overlapping cells share one animation. [Fire Wall sprite source](../art/spell-effects/fire-wall/sheet.png)

[Blaze](sorceress-blaze-checkpoint.md) starts with a swirl at the caster's feet. Accepted movement leaves low burning patches that settle to embers as their damage window expires. [Blaze sprite source](../art/spell-effects/blaze/sheet.png)

[Enchant release](sorceress-buff-payment-checkpoint.md) triggers its cast effects on successful application, after the existing rank and payment checks.

[Energy Shield](sorceress-energy-shield-checkpoint.md) wraps the caster in amber arcs and sparks on the side of an absorbed hit. The shield breaks when absorption drains the caster's mana and fades when the buff expires. The existing cold armor can remain active through either event. [Energy Shield sprite source](../art/spell-effects/energy-shield/sheet.png)

[Shiver Armor](sorceress-shiver-armor-checkpoint.md) forms a violet ward. A violet flash marks the melee attacker when the cold retaliation lands, and the shared chill overlay follows that attacker. [Shiver Armor sprite source](../art/spell-effects/shiver-armor/sheet.png)

[Chilling Armor](sorceress-chilling-bolt-checkpoint.md) forms a teal ward and pulses when it releases a return bolt. The bolt shares Ice Bolt's flight, contact, and chill effects. [Chilling Armor sprite source](../art/spell-effects/chilling-armor/sheet.png)

[Frozen Armor](sorceress-cold-armor-checkpoint.md) forms a pale blue ward that flashes when a melee hit freezes the attacker. The attacker uses the shared freeze overlay. Casting another cold armor replaces the active cold ward. [Frozen Armor sprite source](../art/spell-effects/frozen-armor/sheet.png)

[Enchant](sorceress-enchant-cast-checkpoint.md) marks application with rising copper runes, keeps paired embers around the recipient during the buff, flashes on landed weapon contact, and fades at expiry. The Mage's automatic application shares the recipient art and cast cue. [Enchant sheet](../art/spell-effects/enchant/sheet.png)

[Thunder Storm pulses](sorceress-storm-pulse-checkpoint.md) show a cloud on the caster and a vertical strike on the selected target. The charge and strike have Sonniss cues. [Storm sprite source](../art/spell-effects/thunder-storm/sheet.png)

[Player Chain Lightning](sorceress-player-chain-checkpoint.md) uses violet arcs for the opening flight and jumps, with flashes on damage. Mage Chain Lightning and Amazon Lightning Strike share the arc and contact sprites. [Chain sprite source](../art/spell-effects/chain-lightning/sheet.png)

[Charged Bolt](sorceress-charged-flight-checkpoint.md) uses directional ground sparks and contact crackle along its independent paths. The Skeletal Mage and original-item Charged Bolt reuse these flight and contact sprites. [Bolt sprite source](../art/spell-effects/charged-bolt/sheet.png)

[Lightning](sorceress-lightning-flight-checkpoint.md) uses narrow segments to show a continuous streak and flashes at damage contacts along its route. [Lightning sprite source](../art/spell-effects/lightning/sheet.png)

The [four-passive increment](sorceress-passives-checkpoint.md) creates no active cast or projectile. Existing fire/lightning/cold/recovery cues remain identified placeholders for status feedback. Final passive/affected-target feedback and audio, where useful, stay pending and should share elemental/recovery assets. Missing art does not block passive suppliers. Talent icons remain in their separate register.

[Static Field](sorceress-static-field-checkpoint.md) uses an expanding ground ring and small shocks at the feet of damaged targets. Three visual ring sizes communicate the pulse as skill radius grows. The shared Mage cast uses the same sprites. [Static Field sprite source](../art/spell-effects/static-field/sheet.png)

Frost Nova spreads a frost ring and shows the shared chill overlay on damaging contact. Nova uses an electric ring, caster sparks, and a separate electric contact flash. Original item emissions share the corresponding ring and contact art. [Nova mechanics](sorceress-nova-checkpoint.md) · [Frost Nova sprite source](../art/spell-effects/frost-nova/sheet.png) · [Nova sprite source](../art/spell-effects/nova/sheet.png)

The [saved player Fire Ball increment](sorceress-fire-ball-checkpoint.md) uses travel `0x36D4`/hue `0x489`, explosion `0x36BD`/hue `0x489` and sound `0x1E5` as identified placeholders. Final cast/travel/contact/explosion/expiry presentation and audio remain pending. Share matching Mage and fire-area assets; visual wall placement must follow the eventual shared geometry correction. Tree and hotkey artwork stays in the separate icon register.

Glacial Spike shows a directional crystal and an area burst at the resolved impact. The burst plays for a target, a wall, or the end of an open flight. The Mage flight and area spell share the same graphics. [Glacial Spike mechanics](sorceress-glacial-spike-checkpoint.md) · [Sprite source](../art/spell-effects/glacial-spike/sheet.png)

Ice Blast uses a compact crystal comet and a burst at the struck target. Successful freeze shows an ice collar through the actual status duration, followed by a brief thaw. [Ice Blast mechanics](sorceress-ice-blast-checkpoint.md) · [Sprite source](../art/spell-effects/ice-blast/sheet.png)

Ice Bolt uses a thin directional shard and a small chip flash on contact. Its chill overlay follows the affected target. The Mage and original item bolt paths share the art. [Ice Bolt mechanics](sorceress-ice-bolt-checkpoint.md) · [Sprite source](../art/spell-effects/ice-bolt/sheet.png)

[Fire Bolt](sorceress-fire-bolt-checkpoint.md) uses a directional dart and compact contact flash. The Mage flight and original-item spell share the same art. [Fire Bolt sprite source](../art/spell-effects/fire-bolt/sheet.png)

[Inferno](player-inferno-modes-checkpoint.md) draws its stream from the emitted flames and flashes on damaging contact. Releasing the held input ends new emissions, and existing flames retire with their flight. [Inferno sprite source](../art/spell-effects/inferno/sheet.png)

The [combined Necromancer acceptance](necromancer-acceptance-checkpoint.md) verifies mechanics and adds no final artwork. Existing summon, cast, curse, projectile, cloud, impact, barrier, parking and audio requirements remain pending with identified UO placeholders. Reuse shared Sorceress/encounter assets where the presentation matches; missing art does not block mechanics. Talent and hotkey icons stay in the separate icon register.

The [Mage basic-spell/triple increment](mage-basics-checkpoint.md) uses fire `0x36D4`, ice `0x36E4` and lightning `0x3818` travel markers as identified placeholders. Final Fire Bolt/Ice Bolt/Charged Bolt travel, collision/impact/chill, triple-fan/charged-volley presentation and audio remain pending and should share Sorceress assets. Existing Mage body/windup/recovery/death and secondary assets remain listed separately; art does not gate the mechanics.

The [approved Fire damage/cold upgrades](fire-upgrades-checkpoint.md) retain body `14`/hue `0x489`, native contact/attack cues, aura `0x375A` and explosion `0x36BD` as identified placeholders. Final doubled-physical/fire impact, pulse/weapon aura, Cannot Be Frozen/cold-immunity status, immunity-break/expiry feedback and audio remain pending. Reuse the Paladin Holy Fire and shared cold-state assets where presentation matches. Art does not gate mechanics; icons stay separate.

The [Iron hard-20 Spectral Hit unlock](iron-golem-spectral-checkpoint.md) reuses existing contact/chill cues and poison particles `0x374A`/hue `0x3F` as identified placeholders, alongside body `14`/hue `0x835` and summon `0x376A`. Final magic/fire/lightning/cold/poison impact, active-status/expiry and audio feedback remain pending and should share encounter-affix assets. Art does not gate the mechanical effect; talent/hotkey artwork is tracked separately.

The [Iron hard-10 Extra Strong unlock](iron-golem-strong-checkpoint.md) retains body `14`/hue `0x835`, summon `0x376A`, contact `0x37B9`/hue `0x481` and sound `0x1E5` as identified placeholders. Final stronger-hit/status feedback and audio remain pending and should share encounter-affix assets. Spectral Hit’s final five-channel effects remain separate pending work.

The [Iron damage foundation](iron-golem-damage-checkpoint.md) retains body `14`/hue `0x835`, summon particles `0x376A`, contact overlay `0x37B9`/hue `0x481` and sound `0x1E5` as identified placeholders. Final summon/contact/audio and the pending Extra Strong/five-channel Spectral Hit feedback remain separate asset work. No affix is enabled by this damage-only increment.

The [Blood hard-20 Life Tap proc](blood-life-tap-checkpoint.md) uses curse marker `0x375A`/hue `0x455`, recovery marker `0x376A`/hue `0x26`, and cast sound `0x1E5` as identified placeholders. Final automatic proc/area application, affected-enemy status, recovery, expiry and audio feedback remain pending and should share player Life Tap assets. Blood body `14`/hue `0x21` and summon `0x376A` remain placeholders. Talent/hotkey icons stay in their separate register.

The [NPC Open Wounds/Blood hard-10 increment](npc-open-wounds-checkpoint.md) enables automatic wounds using existing contact/bleed cues as identified placeholders. Final wound application, pulse, expiry and source/owner feedback/audio remain pending and should share the item-wound assets. Blood body `14`/hue `0x21` and summon particles `0x376A` remain placeholders. That checkpoint preceded the now-enabled Life Tap proc; its final curse feedback remains pending. Icons are tracked separately.

The [shared Open Wounds fixes](open-wounds-state-checkpoint.md) retain the existing item/bleed damage feedback as placeholders. Final bleeding/status/expiry and source feedback/audio remain pending and should be shared by future NPC wound abilities. At that checkpoint, Blood’s NPC Open Wounds and Life Tap were still inactive; the subsequent NPC wound checkpoint above supersedes the wound status.

The [Blood damage foundation](blood-golem-damage-checkpoint.md) reuses Blood body`14`/hue`0x21`, summon particles`0x376A` and existing contact/healing cues as identified placeholders. Final body/contact/healing feedback and audio remain pending. At that checkpoint Open Wounds/Life Tap were not active; wound mechanics are now enabled as described above. Their final feedback should share the existing bleed/curse/recovery asset work.

The [Clay Golem upgrades](clay-upgrades-checkpoint.md) reuse Clay body`14`/hue`0x96D`, summon particles`0x376A`, and existing contact/reactive-slow cues as identified placeholders. Final Stone Skin/Magic Resistant status cues and feedback/audio remain pending; share eventual monster-affix assets where appropriate. Live unlock/respec needs no new summon or healing animation.

The [Skeleton Warrior unlocks](skeleton-upgrades-checkpoint.md) reuse retaliation flash `0x374A`/hue`0x481` and the existing skeleton body/movement cues as identified placeholders. Final return-hit/contact, faster movement, death and audio feedback remain pending; reuse shared item-retaliation assets where appropriate. Unlocking requires no new summon/reset animation.

The [historical Mage rollout](mage-historical-checkpoint.md) shares the existing body/cast/projectile/field placeholders and future Mage/Sorceress assets. Adoption needs no new summon/reset visual. Final movement, cast/contact, death, cold/shatter and audio work remains pending.

The [three-element Mage roster](mage-elements-checkpoint.md) reuses existing body and cold/fire/lightning cue placeholders. Final element-distinct summon/body/cast/contact/death and cold/freeze/shatter feedback/audio remain pending. In-place poison-to-ice conversion adds no required migration animation; old poison missile/DOT visuals must follow their actual remaining states.

The [Mage Meteor secondary](mage-meteor-checkpoint.md) shares warning `0x3709`, impact `0x36BD` and ground `0x398C`/hue`0x489` as identified placeholders. Final warning/fall/explosion/ground lifetime/contact feedback and audio remain pending; reuse eventual Sorceress Meteor assets.

The [Mage Glacial secondary](mage-glacial-checkpoint.md) reuses cold travel `0x36E4` and explosion `0x36E4`/hue`0x480` as identified placeholders. Final cast/travel/explosion/freeze/thaw/shatter feedback and audio remain pending; share eventual Sorceress Glacial Spike assets.

The [Mage Chain secondary](mage-chain-checkpoint.md) shares segment `0x3818`/hue`0x482` as an identified placeholder. Final Mage cast/travel/hop/contact/critical feedback and audio remain pending; reuse the eventual Sorceress Chain Lightning assets.

The [Mage Static secondary](mage-static-checkpoint.md) uses caster effect `0x3818`/hue`0x482` as an identified placeholder. Final Static area/cast/recipient feedback and audio remain pending; share the eventual Sorceress Static Field assets rather than creating a separate duplicate.

The fire Mage's [automatic Enchant](mage-pet-buffs-checkpoint.md) shares the player's application and active embers. The ice Mage's intrinsic armor and retaliation presentation still need their asset pass.

The [learned Mage world controller](mage-world-checkpoint.md) retains `Skeletal Mage [placeholder]`, the existing native skeleton body, casting animation4 and summon/elemental missile/contact particles/audio. Final emergence, movement/casting/recovery/death and elemental/upgraded spell feedback/audio remain pending. Reuse the existing Mage effects entry; no final art is generated.

The [learned Fire world controller](fire-golem-world-checkpoint.md) retains `Fire Golem [placeholder]`, body14/hue`0x489`, native A1animation4, summon particles/audio, Holy Fire pulse`0x375A` and retirement explosion`0x36BD` as identified placeholders. Final G3 emergence/body/walk/attack/critical/contact/recovery/death, self/weapon/pulse/retirement feedback and audio remain pending. Reuse the existing Fire effects entry; no final art is generated.

The [learned Blood world controller](blood-golem-world-checkpoint.md) keeps `Blood Golem [placeholder]`, body14/hue`0x21`, native A1animation4, existing summon particles/audio and healing overlay151's native `0x376A`/hue`0x21` as identified placeholders. Final G2 emergence/body/walk/attack/contact/critical/recovery/return/death and healing/audio remain pending. Reuse the existing Blood effects entry; no art is generated.

The [learned Clay world controller](clay-golem-world-checkpoint.md) uses `Clay Golem [placeholder]`, body14/hue`0x96D`, native A1animation4 and existing summon particles/sound as identified placeholders. Final G1 emergence/body/walk/attack/contact/slow/recovery/death and audio remain pending. Reuse the existing Clay asset entry; no final art is created.

The [learned skeleton world controller](skeleton-world-checkpoint.md) keeps body50, `Skeleton Warrior [placeholder]`, native A1 animation4 and existing emergence particles/sound as identified placeholders. Final SK emergence, walking/following, attack/critical/block/recovery, return, death and audio remain pending. Mechanical source clocks do not certify graphical-client animation. Reuse the existing Raise Skeleton asset entry; no new spell effect or final art is created.

The [Dim Vision casting increment](dim-vision-cast-checkpoint.md) retains generic curse marker `0x375A`/hue `0x455` and cast sound `0x1E5` as identified placeholders. Final Dim Vision cast/affected-state feedback and audio remain pending independently of the accepted cast mechanics. No art or complete blind-AI acceptance is claimed.

The [Life Tap casting increment](life-tap-cast-checkpoint.md) retains curse marker `0x375A`/hue `0x455`, sound `0x1E5` and healing flash `0x376A`/hue `0x26` as identified placeholders. Final cast/hit/`cursereversevampire` state and healing effects/audio remain pending. Casting and fractional healing do not wait for final art.

The [approved pet design](pet-adjustment-review.md) now specifies **always-on Frozen Armor** for ice Mages and **automatic combat-start Enchant application**, so these require status/application feedback rather than new buff-casting sequences. Druid wolf Open Wounds/affixes and Grizzly Stun/Crushing Blow should reuse existing feedback where suitable; any pet-specific variants remain pending. Poison Creeper’s damage scaling adds no separate spell or required new effect beyond its already tracked vine/poison presentation. No new art or upgrade implementation is claimed.

The [Iron Maiden increment](iron-maiden-checkpoint.md) retains curse marker `0x375A`/hue `0x455`, cast sound `0x1E5` and shared retaliation flash `0x374A`/hue `0x481` as identified placeholders. Final cast/hit/`curseironmaiden` state and retaliation effects/audio remain pending. Fractional event and damage timing do not wait for art.

The approved [Necromancer pet adjustments](pet-adjustment-review.md) need a later asset pass: basic fire/charged/ice Mage bolts and multishot, pet Meteor/Enchant/Chain Lightning/Static Field/Glacial Spike/Frozen Armor casting and impacts, cold corpse shattering, Skeleton ATD/Extra Fast feedback and golem affix/Open Wounds/Life Tap/cold-protection feedback. Reuse existing spell assets where suitable. These are **planned missing or reusable assets**, with no new art or enhancement implemented yet; identified placeholders must keep mechanics moving. Exact additional effects depend on the remaining design choices.

The [Lower Resist increment](lower-resist-checkpoint.md) retains marker `0x375A`/hue `0x455` and sound `0x1E5` as identified placeholders. Final cast/hit and `curselowerresist` affected-state effects and audio remain needed; resistance mechanics do not wait for final art.

The [Decrepify increment](decrepify-checkpoint.md) retains the identified generic `0x375A`/hue `0x455` marker and `0x1E5` cast sound. Final `cursedecrepify`/`cursecast` cast, persistent affected-state art and audio remain pending independently of the mechanics.

The [Weaken increment](weaken-checkpoint.md) retains generic marker `0x375A`/hue `0x455` and cast sound `0x1E5` as identified placeholders. Final `curseweaken`/`cursecast`, persistent affected-state art and audio remain pending; mechanical work proceeds independently.

The [Amplify Damage increment](amplify-damage-checkpoint.md) retains the identified generic curse marker `0x375A`/hue `0x455` and cast sound `0x1E5`. Final `curseamplifydamage`/`cursecast` cast, affected-state and audio assets remain pending; the immediate mechanical curse does not wait for art.

The [Fire Golem delayed-death increment](fire-golem-death-checkpoint.md) uses location effect `0x36BD` as an identified explosion placeholder. Final fire death/body/explosion/critical feedback and audio remain pending; no imagegen assets are created here.

The [Fire Golem death source review](fire-golem-death-source-review.md) adds no art or runtime effect. Missile117/corpse explosion, intrinsic-critical feedback and final fire death/body/audio assets remain pending; existing immediate feedback remains an identified legacy placeholder.

The [Fire Golem Holy Fire increment](fire-golem-aura-checkpoint.md) keeps native pulse/body/hue feedback as identified placeholders. Final fire ring, pulse, weapon-contact/critical and death/explosion effects and audio remain needed; the shared aura mechanics are independent of final art.

The [Fire Golem combined-combat increment](fire-golem-combat-checkpoint.md) uses the existing body/hue and native attack/hit/critical feedback as identified placeholders. Final fire body, melee/critical/impact, Holy Fire pulse and death/explosion effects and audio remain needed. Capturing and releasing one combined packet does not wait for those assets.

The [Fire Golem absorption increment](fire-golem-absorption-checkpoint.md) adds no final art. Existing native hit/health feedback remains an identified placeholder; final fire immunity, absorption/healing, aura/pulse/hit/death effects and audio remain needed. The effective40%cap and fractional health order are mechanical behavior, independent of these assets.

The [Fire Golem birth/resource increment](fire-golem-birth-checkpoint.md) retains native body14/hue0x489, summon flash and inherited fire-hit/pulse/explosion presentation as identified placeholders. Final emergence/body/attack/critical, affinity/absorption feedback, Holy Fire aura/pulse/contact, hit/death/explosion effects and audio remain needed. Resource/stat mechanics do not wait for art; full aura/damage/death semantics still need acceptance.

The [Blood Golem combat/healing increment](blood-golem-combat-checkpoint.md) retains native body/attack, ordered-hit/return and healing flashes as identified placeholders; source healing overlay151is represented by the existing native effect. Final emergence/body/attack/critical/Enchant, owner/pet healing/overflow, hit/return/death and audio remain needed. Correct mechanical action/healing timing does not wait for final effects.

The [Blood Golem exact birth/resource increment](blood-golem-birth-checkpoint.md) retains native body14/hue0x21 and the existing summon flash as identified placeholders. Final emergence/body, attack/critical, healing/owner-share feedback, hit/death effects and audio remain needed. Resource mechanics do not wait for this asset pass; full healing-event semantics remain separate work.

The [Clay prepared-contact increment](clay-golem-contact-checkpoint.md) retains the native body/attack and ordered-hit/retaliation presentation as identified placeholders. Final G1 emergence, attacks, critical/Enchant, block/hit/slow/death and audio still need assets; no new player action is introduced. Mechanical preparation and release do not wait for final effects.

The [Clay unarmed-damage/G1 clock increment](clay-golem-combat-checkpoint.md) retains native body14/hue0x96D, attack animation and hit presentation as identified placeholders. Final emergence, attack, intrinsic-critical/Enchant feedback, hit/slow/death effects and audio remain needed. The400ms mechanical release is independent of final art.

The [Clay incoming-melee slow correction](clay-golem-slow-checkpoint.md) needs final reaction/status feedback and audio, alongside the existing Clay body/emergence/cast/hit/death work. The shared mechanical slow uses identified native presentation; final visual assets do not block its event, timing or saved values.

The [Clay Golem birth/resource correction](clay-golem-birth-checkpoint.md) retains body14/hue0x96D and the native summon flash as identified placeholders. Final G1 emergence/body/casting, attacks, incoming/outgoing slow, hit/retaliation/death and audio remain needed. Exact life and regeneration do not wait for art.

The approved [later Mage upgrade](skeletal-mage-upgrade-plan.md) will need effects/audio for its extra spells once the spell list is selected. No specific spell or asset is invented yet; baseline Mage placeholders remain usable.

The [Skeletal Mage skill-clock correction](mage-clock-checkpoint.md) keeps native body56/attack animation and the four colored projectile/hit presentations as identified placeholders. Final emergence/casting, elemental bolts, impact/chill/poison/critical feedback, death and audio remain needed. Their mechanical launch now uses the source400ms event; no final art is generated.

The [regular skeleton ordered-contact increment](skeleton-contact-checkpoint.md) reuses native body50, attack/block/get-hit and shared return presentation as identified placeholders. Final warrior emergence, weapon/shield variants, hit/critical/block/retaliation/death effects and audio remain needed. Captured packets and ordered health/return do not wait for these assets.

The [regular skeleton damage/A1 increment](skeleton-combat-checkpoint.md) keeps native body50, attack animation and shared hit presentation as identified placeholders. Final SK warrior emergence/attack, intrinsic-critical feedback, hit/death, weapon/shield variants and audio remain pending. Damage and its 480 ms release do not depend on that art.

The [Necromancer mouse-selection correction](necromancer-binding-checkpoint.md) creates no final effects or audio. Skeleton warrior/Mage body, attack and shield presentation remain identified native placeholders. The separate [SK preflight](../tools/reference/skeleton-world-preflight.json) records current animation speeds and release events for later mechanical integration; those records do not supply final art.

The [regular skeleton birth/resource correction](skeleton-birth-checkpoint.md) retains the existing native skeleton body, shared corpse animation and cast sound as identified presentation placeholders. Final summon emergence, body/weapon/shield variants, attack/hit/death effects and audio remain pending; this increment creates no imagegen assets and does not depend on them.

The [Poison Explosion moving-cloud path](poison-explosion-checkpoint.md) uses cast sound0x1E5 and travel0x374A as identified placeholders. Final corpse rupture/green light, PoisonSparks drift, poisonpuff, poison state and cast/travel/contact/expiry audio remain pending independently of the moving clouds and damage.

The [Poison Nova saved-ring path](poison-nova-checkpoint.md) uses cast sound0x1E5 and travel marker0x374A as identified placeholders. Final poisonNova directional travel/contact/state effects and cast/travel/impact/expiry audio remain pending independently of the64rays and poison ticks.

Bone Spirit renders a custom directional skull along its [saved-guidance path](bone-spirit-checkpoint.md) and bursts on a damaging hit. The native cast sound `0x1E5` remains a placeholder.

Teeth renders custom directional shards along its [saved-flight path](teeth-checkpoint.md) and chips on a damaging hit. The native cast sound `0x1E5` remains a placeholder.

Bone Spear uses custom directional flight art, a flash on piercing contact and a full shatter at the end of its [flight path](bone-spear-checkpoint.md). Its Sonniss cues await a listening pass.

The [Bone Armor correction](bone-armor-checkpoint.md) retains the native0x375A shield marker and shared cast sound as identified placeholders. Final orbiting-bone, absorption/break/state effects and audio remain pending; exact shield arithmetic does not depend on those assets.

The [skeleton/Mage roster correction](skeleton-roster-checkpoint.md) adds no new visual or audio producer. Native summon particles/body/attack/death cues remain identified placeholders; source destruction/event presentation is not certified by FIFO tests. Final summon and elemental effects/audio remain pending.

The [Skeletal Mage birth/resource correction](mage-birth-checkpoint.md) adds no visual/audio producer. Native summon particles, body/attack poses and typed projectile markers remain identified placeholders. Final summon formation, directional/attack/recovery/death frames, elemental travel/impact/state effects and audio remain pending independently of mechanics.

The [Skeletal Mage flight path](mage-missiles-checkpoint.md) uses native body/attack pose and typed position markers as identified placeholders. Raise Skeletal Mage needs final poison/cold/fire/lightning travel and impact effects (`necromage1..4`, fire's `shamanexp` presentation), cold/poison state effects, directional/attack frames and audio. Mechanical collision is now independent of the placeholder visuals. Its talent/hotkey icon remains in the separate icon ledger.

The [weapon-start correction](weapon-start-checkpoint.md) adds no new visual or audio producer. Prevent Monster Heal and flat armor loss use existing state services; final Poison Dagger effects/audio remain pending alongside the identified native placeholders. The separate talent/hotkey icon ledger is unchanged.

The [ordered Poison Dagger contact](poison-dagger-contact-checkpoint.md) retains native weapon pose/sound, shield/contact/retaliation flashes and shared cold/poison markers as identified placeholders. Final poison impact/state, weapon/block/return/overlay effects and audio remain pending. Its 127 native markers verify mechanical behavior; no final art was created. Talent/hotkey icons remain separate.

The [player-source damage bridge](prepared-player-native-damage-checkpoint.md) adds no assets. Its 124 native markers verify shared notification/credit support; actual Poison Dagger contact integration is next. Existing final effects/audio and talent/hotkey icons remain independently pending.

The [prepared Poison Dagger path](poison-dagger-attack-checkpoint.md) uses the ordinary native weapon pose and sound plus the shared shield cue as identified placeholders. Final poison contact/state and block effects/audio remain pending. Its 121 native markers verify mechanics; the icon remains separately tracked.

The [Poison Dagger composition fix](poison-dagger-checkpoint.md) uses one combined poison packet. Final poison contact/state effects and audio still need creation and mapping; native weapon presentation and generic cast sound remain identified placeholders. Its 118 native markers are mechanical evidence, not final asset verification.

The [NPC resource correction](npc-high-level-resources-checkpoint.md) adds no assets. Its 97 focused and 1,180 full regression markers pass. Final effects/audio and talent/hotkey icons remain separate pending assets. Coverage remains 186 handlers, including 30 Necromancer handlers; no class is complete and the candidate is uninstalled.

Learned Iron Golem now creates the prepared actor through right/quick item targeting. Native summon0x376A, sound0x1E5and the labelled body/contact/return markers remain explicitly identified placeholders. Final original-item consumption/formation, body/directional frames, attack/get-hit/block/death/overlay and audio assets remain pending. See [casting checkpoint](iron-golem-casting-checkpoint.md).

Ordered Iron Golem contact now retains source hit-class/overlay descriptors through actual HP/retaliation/return. Native prepared body, contact, block (0x375A), get-hit and critical overlay (0x37B9) remain explicitly identified presentation placeholders; final animation/effect/audio is pending. This mechanical increment introduces no new independent icon. See [checkpoint](iron-golem-ordered-contact-checkpoint.md).

Ordinary NPC min-only item steal/stamina-consumption compatibility adds no healing visual/audio. Do not imply player percentage leech under the recorded older NPC rule;actual Life Tap remains a separate recovery effect. See [mechanical scope](npc-item-drain-checkpoint.md).

Prepared NPC impact allocation adds no art/audio. Existing poison/cold/contact/retaliation placeholders remain. Final poison presentation must account for the captured initial impact and separate active periodic duration after mechanical/client acceptance;see [scope](npc-impact-allocation-checkpoint.md).

NPC offensive snapshot and native credit changes add no visuals/audio. Existing attack,poison,Life Tap and retaliation placeholders still need final assets and client timing validation. The [initial-poison-total discrepancy](npc-weapon-preparation-checkpoint.md) is mechanical verification work,not an art blocker.

The ordered monster phase consumer reuses Life Tap0x376A/hue0x26 and retaliation0x374A/hue0x481 placeholders. No final art/audio was added. Their final timing must follow the accepted recovery/HP/retaliation phases; actual-client validation remains pending. See [mechanical checkpoint](monster-weapon-phases-checkpoint.md).

Prepared Iron Golem recipient blocks reuse generic UO0x375A/hue0x481 and sound0x1A4 placeholders. Final shield/block presentation and audio must follow the recipient model and accepted block mode; no final art was created. See [mechanical scope](iron-golem-melee-result-checkpoint.md).

Shared golem roster changes add no new visual assets. Prepared Iron body14/hue0x835 and generic summon/retaliation flashes remain identified placeholders; final golem bodies, summon/replacement/death effects and audio still require creation and client verification. See [mechanical checkpoint](golem-roster-checkpoint.md).

This register is independent of mechanical completion. No final assets have been generated or installed in this pass. Keep the original prompt/source image, transparency, scale/directional frames, timing, client mapping and actual-client verification with each eventual asset.

Sanctuary presentation preference: visible damaging effects should match their active damaging lifetime. This is user taste, not D2R reference evidence; final assets must communicate the supported mechanics. See [reference precedence and recorded preference](skill-mechanical-reference.md#sanctuary-decisions-retained).

Prepared Arctic Blast emissions use generic UO0x374A hue0x480 cold markers. The prepared mode has no final S1 startup/channel/turn/release animation. Final ArcticBlastIce directional stream, arctic_blast_cast overlay, contact/expiry, druid_arcticblast travel and druid_windcast audio remain pending. The public channel is disabled; its talent-tree and left/right/quick icons are listed separately.

Carrion Vine and Solar Creeper prepared bodies use silent UO corpser0x8 with explicit placeholder names. Prepared summon sound0x1E5and generic0x376Adelayed steal-life/mana overlay are identified placeholders. Final K9 bodies, underground/surface/corpse-feed, recycler vines/explosion, steallife/stealmana overlays and summon/feed/expiry audio are still needed. NPC307/325are internal actions; the separate icon register retains each player talent and right/quick-hotkey icon.

Existing UO bone/magic-arrow, cloud, curse-marker and summon bodies are temporary placeholders. Names, damage type and mechanics are authoritative independently of their placeholder appearance. Effect concepts for unimplemented skills are a source-derived backlog, not an implementation claim.

Grizzly's prepared saved body is explicitly **Grizzly [placeholder]**,using silent UO bear0xD4. Final B7 emergence/body/walk,A1attack,A2BearSmite sequence,contact/knockback,hurt/death and bear/summon audio are still needed. The separate icon register retains its talent/right/quick icon work. Detailed placeholder descriptions are kept in [the runtime effects register](../data/Sanctuary/Balance/skill-effects.json).

The learned Grizzly route now creates the saved world body and delivers ordinary/BearSmite damage and mapped knockback mechanically. Its generic0x1E5druid-summon cast sound is an identified audio placeholder. Its silent UO0xD4body remains an identified placeholder; final B7motion/attack/contact/audio and actual-client presentation still need asset work.

The user observes Grizzly knockback without stun. BearSmite's prepared packet also has zero stun; do not invent a stun overlay from the hidden formula label. Its animation/contact/knockback work remains pending even though packet preparation is verified. NPC326 is an internal Grizzly action and needs no separate player talent or hotkey icon.

Player-form mechanics keep the existing human avatar as a clearly identified placeholder. The learned casting candidate uses SCpose16,flash0x373A,sound0x1E5 and ordinary melee Wrestle pose until final form art and motion are available. It adds no invulnerability, invisibility or body override. Final transformation/model40/TG motion/contact/audio remain pending even though projected stat pools and saved expiry are verified. Rate/restriction/learned-casting completion is separate from this asset register.

Player Werewolf and Werebear assets are separate from summoned wolves/Grizzly: current transform states139/140 map to models430/code40 and431/codeTG. Their22animation records include distinct S1 transformation, attack, cast, block, hurt and movement timings. Final wolf_into/wolf_undo, bear_into/bear_undo, body/motion/contact and druid_morph audio remain pending. The native casting candidate uses the identified generic flash/audio above; final player-form body and effects remain pending independently of mechanical acceptance. Lycanthropy has no independent active effect.

The staged form recovery/block adapter retains generic human animation20as an identified placeholder. Final player wolf40GH/BLand bearTGGH/BLmotion remain missing; those clips are distinct from summoned-beast reactions. Mechanical clocks do not depend on creating these assets. The existing Werewolf/Werebear talent/right/quick icon rows remain in the separate icon register; this adapter adds no new icon requirement.

Maul uses identified human Wrestle/contact sound0x238 and charge flash0x375A. Final maul-orb front/back overlays, charge-size display, bear A1/contact/audio remain pending independently of combat; its talent/right/quick icons are tracked separately.

The prepared original-equipment NPC strike now delivers inherited fire, lightning, cold and magic mechanically. Final elemental weapon glows, impact cues and contact audio for Iron Golem remain needed; fire/lightning/magic currently have no dedicated final cue. Cold uses the existing generic UO 0x374A/hue 0x480 marker as an identified placeholder. Keep any future damaging presentation synchronized with the actual hit or timed effect. This adds no new active talent and does not certify learned Iron Golem.

Original-equipment NPC poison now uses the mechanical rate/frame clock. Final poison weapon/contact cues and a victim poison-state cue remain needed for Iron Golem; no final dedicated poison effect is installed by this adapter. Any persistent cue must follow the victim's actual remaining poison state, including cure and expiry, even after the source summon is removed. The durable kill-credit adapter is not a visual effect.

Original-equipment NPC criticals need final impact/audio presentation for Iron Golem. The source hit-class bit and overlay 54 are mechanically described but not mapped to final client artwork; ordinary UO contact presentation remains the explicitly identified placeholder. Damage and poison clocks do not wait for this asset work.

Inherited item resistances need no new independent active effect. Iron Golem still uses existing UO summon/contact art as its identified placeholder; its final body, summon, thorns and other recorded item cues remain pending. Resistance arithmetic and immunity are not conditional on those assets.

Inherited NPC item absorption now changes life mechanically. Final Iron Golem absorption/contact/audio presentation remains pending; existing UO contact art is an identified placeholder. No source-specific absorption overlay is certified by this adapter. Keep any eventual cue synchronized with the actual absorbed hit.

<!-- BEGIN GENERATED: skill-effects -->
| Class | Skill | Effect work | Current presentation or source cues |
| --- | --- | --- | --- |
| Amazon | Jab | native | Native weapon thrusts and contact audio. |
| Amazon | Power Strike | native | Native weapon thrusts and lightning effects. |
| Amazon | Poison Javelin | native | Native spear and Poison Field graphics. |
| Amazon | Impale | native | Native weapon thrusts and contact audio. |
| Amazon | Lightning Bolt | needed | lightningjavelin, lightningjavelin |
| Amazon | Charged Strike | native | Native weapon thrusts and lightning effects. |
| Amazon | Plague Javelin | native | Native spear and Poison Field graphics. |
| Amazon | Fend | native | Native weapon thrusts and contact audio. |
| Amazon | Lightning Strike | native | Native weapon thrusts and lightning effects. |
| Amazon | Lightning Fury | needed | lightningfury, lightningfury |
| Amazon | Inner Sight | native | Native spell flashes and target feedback. |
| Amazon | Critical Strike | not-applicable | criticalstrike |
| Amazon | Dodge | needed | dodge, S1, amazon_dodge_1, 2 |
| Amazon | Slow Missiles | native | Native spell flashes and target feedback. |
| Amazon | Avoid | needed | avoid, S1, amazon_dodge_1, 2 |
| Amazon | Penetrate | not-applicable | penetrate |
| Amazon | Decoy | native | Native humanoid bodies and summon effects. |
| Amazon | Evade | native | Native Evade sound. |
| Amazon | Valkyrie | native | Native humanoid bodies and summon effects. |
| Amazon | Pierce | not-applicable | pierce |
| Amazon | Magic Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Fire Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Cold Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Multiple Shot | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Exploding Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Ice Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Guided Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Immolation Arrow | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Strafe | native | Native arrows, bolts, spell flashes and fire fields as applicable. |
| Amazon | Freezing Arrow | needed | freezingarrow, freezingarrow |
| Sorceress | Ice Bolt | native | Native spell projectiles and frost-tinted effects. |
| Sorceress | Frozen Armor | native | Native ward flashes and retaliation effects. |
| Sorceress | Frost Nova | native | Native spell projectiles and frost-tinted effects. |
| Sorceress | Ice Blast | native | Native spell projectiles and frost-tinted effects. |
| Sorceress | Shiver Armor | native | Native ward flashes and retaliation effects. |
| Sorceress | Glacial Spike | native | Native spell projectiles and frost-tinted effects. |
| Sorceress | Blizzard | needed | blizzardcenter, blizzardcenter, blizzardcenter, ice_cast_3, blizzardcenter, ice_cast_3 |
| Sorceress | Chilling Armor | native | Native ward flashes and retaliation effects. |
| Sorceress | Frozen Orb | needed | frozenorb, ice_cast_3, frozenorb |
| Sorceress | Cold Mastery | not-applicable | coldmastery |
| Sorceress | Charged Bolt | native | Native lightning effects and sounds. |
| Sorceress | Telekinesis | native | Native spell flashes and sounds. |
| Sorceress | Static Field | native | Native lightning effects and sounds. |
| Sorceress | Lightning | native | Native lightning effects and sounds. |
| Sorceress | Nova | native | Native lightning effects and sounds. |
| Sorceress | Chain Lightning | native | Native lightning effects and sounds. |
| Sorceress | Teleport | native | Native spell flashes and sounds. |
| Sorceress | Thunder Storm | native | Native lightning effects and sounds. |
| Sorceress | Energy Shield | native | Native ward flashes and retaliation effects. |
| Sorceress | Lightning Mastery | not-applicable | lightningmastery |
| Sorceress | Fire Bolt | native | Native fire projectiles, fields and flashes as applicable. |
| Sorceress | Warmth | not-applicable | warmth |
| Sorceress | Inferno | needed | infernoflame1, infernoflame1, infernoflame1, infernoflame1, infernoflame2 |
| Sorceress | Blaze | native | Native fire projectiles, fields and flashes as applicable. |
| Sorceress | Fireball | needed | fireball, fire_cast_2, fireball |
| Sorceress | Fire Wall | native | Native fire projectiles, fields and flashes as applicable. |
| Sorceress | Enchant | native | Native fire projectiles, fields and flashes as applicable. |
| Sorceress | Meteor | needed | meteorcenter, meteorcenter, meteorcenter, fire_cast_2, meteorcenter, fire_cast_2 |
| Sorceress | Fire Mastery | not-applicable | firemastery |
| Sorceress | Hydra | needed | hydra1, fire_cast_2, fire_cast_2 |
| Necromancer | Raise Skeleton | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Skeleton Mastery | not-applicable | skel_mastery |
| Necromancer | Clay Golem | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Golem Mastery | not-applicable | golem_mastery |
| Necromancer | Raise Skeletal Mage | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Blood Golem | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Summon Resist | not-applicable | summonresist |
| Necromancer | Iron Golem | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Fire Golem | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Revive | native | Native creature bodies and summon effects. Revive keeps the original creature. |
| Necromancer | Teeth | needed | teeth, teeth, teeth, bonecast |
| Necromancer | Bone Armor | needed | bonearmor |
| Necromancer | Poison Dagger | native | Native weapon and poison effects as applicable. |
| Necromancer | Corpse Explosion | native | Native Explosion graphics and sound. |
| Necromancer | Bone Wall | needed | bonewallmaker, bonewall |
| Necromancer | Poison Explosion | native | Native weapon and poison effects as applicable. |
| Necromancer | Bone Spear | needed | bonespear, bonespear, bonecast |
| Necromancer | Bone Prison | needed | bonewall |
| Necromancer | Poison Nova | native | Native weapon and poison effects as applicable. |
| Necromancer | Bone Spirit | needed | bonespirit, bonespirit, bonespirit |
| Necromancer | Amplify Damage | native | Native curse graphics and sounds. |
| Necromancer | Dim Vision | native | Native curse graphics and sounds. |
| Necromancer | Weaken | native | Native curse graphics and sounds. |
| Necromancer | Iron Maiden | native | Native curse graphics and sounds. |
| Necromancer | Terror | native | Native curse graphics and sounds. |
| Necromancer | Confuse | native | Native curse graphics and sounds. |
| Necromancer | Life Tap | native | Native curse graphics and sounds. |
| Necromancer | Attract | native | Native curse graphics and sounds. |
| Necromancer | Decrepify | native | Native curse graphics and sounds. |
| Necromancer | Lower Resist | native | Native curse graphics and sounds. |
| Paladin | Prayer | custom | Copper loops on active recipients and amber ribbons on actual healing. |
| Paladin | Resist Fire | custom | Copper shields follow the active recipient lease. |
| Paladin | Resist Cold | custom | Ice shields follow the active recipient lease. |
| Paladin | Resist Lightning | custom | Gold and violet arcs follow the active recipient lease. |
| Paladin | Defiance | custom | Steel-blue crenellations follow active defense recipients. |
| Paladin | Cleansing | custom | Violet purification on status shortening and shared Prayer healing. |
| Paladin | Vigor | custom | Moving mint leaves follow active stamina and movement recipients. |
| Paladin | Meditation | custom | Blue player aura, native mana gain sparks, and shared Prayer healing. |
| Paladin | Redemption | custom | Caster ring, corpse flash, jade transfer thread, and distinct life and mana receipts. |
| Paladin | Salvation | custom | Three gold and jade shields follow each protected recipient. |
| Paladin | Might | custom | Copper pennants follow the damage-bonus recipients. |
| Paladin | Holy Fire | custom | Gold aura ring, radius-scaled pulse, and fire contacts. |
| Paladin | Thorns | custom | Plum hooks follow the recipient, with a separate burst at the attacker on damaging return. |
| Paladin | Blessed Aim | custom | Teal arrowheads follow the active accuracy recipients. |
| Paladin | Concentration | custom | Amethyst standards and a flash when the attack withstands interruption. |
| Paladin | Holy Freeze | custom | Frost aura ring, radius-scaled pulse, cold contacts, timed slow seal, and shared shatter. |
| Paladin | Holy Shock | custom | Violet aura ring, radius-scaled pulse, and lightning contacts. |
| Paladin | Sanctuary | custom | Caster lanterns, a range-scaled wave, an undead damage or knockback flash and a landed weapon spark. |
| Paladin | Fanaticism | custom | Fast crimson pennants follow the caster and allies with the bonuses. |
| Paladin | Conviction | custom | Garnet caster ring, a debuff application flash and a timed mark that follows affected enemies. |
| Paladin | Sacrifice | custom | Garnet swing, separate actual life payment and confirmed contact effects. |
| Paladin | Smite | custom | Steel-and-ivory shield sweep and impact, with the shared timed stun marker. |
| Paladin | Holy Bolt | custom | Ivory directional flight, green healing, piercing contacts, and terminal fade. |
| Paladin | Zeal | custom | Fast bronze arcs per swing and landed sparks that follow the chosen targets. |
| Paladin | Charge | custom | Teal launch, afterimages on completed steps, and a hit or knockback impact. |
| Paladin | Vengeance | custom | Three-element swing and contact, plus the shared marker on actual chill. |
| Paladin | Blessed Hammer | custom | Spinning hammer along the live curve, gold chip contacts, and terminal fade. |
| Paladin | Conversion | custom | Jade release, success knot, moving allegiance mark and timed breakaway. |
| Paladin | Holy Shield | custom | Gold cast, equipped braces, successful block flash, and timed breakaway. |
| Paladin | Fist of the Heavens | custom | Tracked warning and strike, saved-position burst, and silver-blue secondary bolts. |
| Barbarian | Howl | custom | Steel-blue cast and wave, attached fear mark, and shared expiry fade. |
| Barbarian | Find Potion | custom | Green cast and corpse sweep, then a vial on actual yield or dust on empty search. |
| Barbarian | Shout | custom | Steel-blue release, travelling wave, defense braces, and expiry fade. |
| Barbarian | Taunt | custom | Copper cast and an attached rose mark for the untimed attack and damage penalties. |
| Barbarian | Battle Cry | custom | Amber cast and wave, attached broken shield, and shared expiry fade. |
| Barbarian | Find Item | custom | Amber cast and corpse sweep, then a gold flash on added loot or dust on empty search. |
| Barbarian | Battle Orders | custom | Three-color release and wave, attached resource lights, and expiry fade. |
| Barbarian | Grim Ward | custom | Three totem sizes, radius-scaled pulse, violet curse mark, and collapse. |
| Barbarian | War Cry | custom | Ivory-blue cast and wave, qualified contact flash, and shared stun feedback. |
| Barbarian | Battle Command | custom | Gold and lilac release and wave, attached skill-rank ring, and expiry fade. |
| Barbarian | Blade Mastery | not-applicable | blademastery |
| Barbarian | Axe Mastery | not-applicable | axemastery |
| Barbarian | Mace Mastery | not-applicable | macemastery |
| Barbarian | Polearm Mastery | not-applicable | polearmmastery |
| Barbarian | Throwing Mastery | not-applicable | throwingmastery |
| Barbarian | Spear Mastery | not-applicable | spearmastery |
| Barbarian | Increased Stamina | not-applicable | increasedstamina |
| Barbarian | Iron Skin | not-applicable | ironskin |
| Barbarian | Increased Speed | not-applicable | increasedspeed |
| Barbarian | Natural Resistance | not-applicable | naturalresistance |
| Barbarian | Bash | custom | Amber swing and impact, with dust on completed knockback steps. |
| Barbarian | Double Swing | custom | Opposing hand sweeps and separate confirmed contact flashes. |
| Barbarian | Leap | custom | Dust takeoff and airborne trail, radius-scaled knockback ring, and interrupted landing settle. |
| Barbarian | Double Throw | custom | Per-hand flight, spinning blades, directional polearms, and contact sparks. |
| Barbarian | Stun | custom | Blue swing and impact, an applied-stun spiral, and the shared stun marker. |
| Barbarian | Leap Attack | custom | Shared airborne trail, area impact and separate weapon sweep and contact. |
| Barbarian | Concentrate | custom | Jade swing and impact, with braces throughout the action's defense window. |
| Barbarian | Frenzy | custom | Paired copper slashes, contact sparks, and an active speed ring with an expiry fade. |
| Barbarian | Whirlwind | custom | Caster spin, completed-step wake, landed sparks and fade. |
| Barbarian | Berserk | custom | Violet swing and magic impact, vulnerability ring, and expiry fade. |
| Druid | Firestorm | custom | Directional traveling flames, ground fire and damaging contact. |
| Druid | Molten Boulder | custom | Formation, rolling, contact chips, terminal shatter and independent fire trail. |
| Druid | Arctic Blast | custom | Directional windup, held frost stream, ice contact, chill and vapor fade. |
| Druid | Fissure | custom | Opening eruption, active scattered cracks and damaging contact. |
| Druid | Cyclone Armor | custom | Rising and sustained wind shell, absorption flare and depletion. |
| Druid | Twister | custom | Three low traveling eddies, contact, shared stun and dissipation. |
| Druid | Volcano | custom | Vent opening, flying debris, lava beds, contact and cooling. |
| Druid | Tornado | custom | Traveling funnel, timed area pulse, contact and dissipation. |
| Druid | Hurricane | custom | Frost opening, attached storm, orbiting wisps, cold contact, chill and expiry. |
| Druid | Armageddon | custom | Moving weather aura, warning, rock descent, impact and independent ground fire. |
| Druid | Werewolf | native | Native Timber Wolf sprite at 120% scale in the client. |
| Druid | Lycanthropy | not-applicable | Passive; no active effect |
| Druid | Werebear | native | Native Grizzly Bear sprite at 120% scale in the client. |
| Druid | Maul | needed | maul |
| Druid | Feral Rage | needed | feralrage |
| Druid | Fire Claws | needed | fire_hit |
| Druid | Rabies | needed | rabiesplague |
| Druid | Shock Wave | needed | shockwave, shockwave, shockwave, shockwave |
| Druid | Hunger | needed | No source effect cue; final presentation pending |
| Druid | Fury | needed | No source effect cue; final presentation pending |
| Druid | Raven | custom | Feather arrival, talon contact, attached blindness and departure. |
| Druid | Poison Creeper | custom | Native vine, directional thorns, poison mat and wither, and committed poison flash. |
| Druid | Oak Sage | custom | Green arrival, life aura on each recipient, and shared spirit departure. |
| Druid | Summon Spirit Wolf | custom | Frost arrival, cold bite, shared chill, departure and return. |
| Druid | Carrion Vine | custom | Native vine, copper arrival, corpse feeding, and delayed life flash. |
| Druid | Heart of Wolverine | custom | Copper arrival, attached attack aura, and shared spirit departure. |
| Druid | Summon Dire Wolf | custom | Copper arrival, physical bite, feeding rage and departure. |
| Druid | Solar Creeper | custom | Native vine, blue arrival, corpse feeding, and delayed mana flash. |
| Druid | Spirit of Barbs | custom | Thorn arrival, attached reflection aura, retaliation, and shared spirit departure. |
| Druid | Summon Grizzly | custom | Earth arrival, ordinary swipe, heavy Bear Smite and departure. |
| Assassin | Tiger Strike | custom | Amber sweep, gold contact, and up to three tiger-eye motes. |
| Assassin | Dragon Talon | custom | Directional kicks, landed contacts, deflection and completed knockback dust. |
| Assassin | Dragon Claw | custom | Lilac and blue directional hand sweeps, independent contact effects and shared charge payloads. |
| Assassin | Fists of Fire | custom | Copper sweeps, ember motes, ground pulse, and active scatter fire. |
| Assassin | Cobra Strike | custom | Green claws, serpent-scale sparks, charge motes, and recovery ribbons. |
| Assassin | Dragon Tail | custom | Copper kick, fire ring over the confirmed area and embers on completed displacement. |
| Assassin | Claws of Thunder | custom | Charged claw arcs and motes, nova ring, moving rays and charged bolts. |
| Assassin | Blades of Ice | custom | Cyan claws and motes, shard ring, ice crystals and shared cold statuses. |
| Assassin | Dragon Flight | custom | Committed departure and landing, directional kick and landed contact. |
| Assassin | Phoenix Strike | custom | Feather sweeps and motes, meteor and ground fire, chain lightning, and chaos ice. |
| Assassin | Claw Mastery | not-applicable | clawmastery |
| Assassin | Psychic Hammer | custom | Violet cast knot and downward strike on the accepted impact tile. |
| Assassin | Burst of Speed | custom | Copper release, moving speed wisps and expiry. |
| Assassin | Cloak of Shadows | custom | Radius-scaled veil, caster mantle, blindness and defense-only marks. |
| Assassin | Weapon Block | needed | weaponblock, SC |
| Assassin | Fade | custom | Violet cast, active veil, expiration and existing body translucency. |
| Assassin | Shadow Warrior | custom | Indigo cast, arrival, moving veil and dismissal on the native humanoid. |
| Assassin | Mind Blast | custom | Magenta wave and contacts, with shared stun and allegiance effects. |
| Assassin | Venom | custom | Jade cast and coating, shared poison application and expiry. |
| Assassin | Shadow Master | custom | Amethyst threshold, gold spark veil and departure on the native humanoid. |
| Assassin | Fire Blast | custom | Tumbling ember, landed fuse, radius-scaled blast and shared Fire Bolt contact. |
| Assassin | Shock Web | custom | Tumbling shards, independent half-tile fields, landed arcs and retirement. |
| Assassin | Blade Sentinel | custom | Teal steel construct, fractional movement, linked cut effects and retirement. |
| Assassin | Charged Bolt Sentry | custom | Crystal coil, jagged sparks, contact and collapse. |
| Assassin | Wake Of Fire | custom | Low brazier, spreading fire crests, contact and collapse. |
| Assassin | Blade Fury | custom | Hand glint, directional tumbling blades, contact, deflection and spent fragments. |
| Assassin | Lightning Sentry | custom | Violet fork, straight lightning, contact and collapse. |
| Assassin | Wake Of Inferno | custom | Tall brazier, directional fire stream, contact and collapse. |
| Assassin | Death Sentry | custom | Bone and iron altar, lightning, radius-scaled corpse blast and collapse. |
| Assassin | Blade Shield | custom | Raising blades, attached orbit, periodic cut, deflection and expiry. |
| Warlock | Demonic Mastery | not-applicable | demonic_mastery |
| Warlock | Summon Goatman | custom | Native Gargoyle with rubble summon and melee effects, plus the shared Berserk loop. |
| Warlock | Blood Oath | not-applicable | bloodoath |
| Warlock | Death Mark | custom | Violet warp at both ends and a timed target glyph. |
| Warlock | Summon Tainted | custom | Approved Succubus and red flare with an ember summon and fire resistance ward. |
| Warlock | Blood Boil | custom | Donor blooms, radius-scaled rings, and enemy damage flashes. |
| Warlock | Summon Defiler | custom | Native Gazer with burgundy summon effects and recipient Health Link marks. |
| Warlock | Engorge | custom | Corpse-to-demon tethers, heal flashes, and timed recipient wards. |
| Warlock | Consume | custom | Demon dissolve, owner tether, and a ward that fades when its slot clears. |
| Warlock | Bind Demon | custom | Channel rune, paid attempt chain, and a seal on the successfully bound demon. |
| Warlock | Levitation Mastery | not-applicable | levitate_mastery |
| Warlock | Hex: Bane | custom | Violet weapon thorns, a thistle target mark, and a landed-contact flash. |
| Warlock | Cleave | needed | warlock_psychic_cast_1 |
| Warlock | Echoing Strike | needed | echoingstrike, warlock_psychic_cast_1, echoingstrike |
| Warlock | Hex: Purge | custom | Copper weapon crescents, a charged triangle, and a radius-matched blast. |
| Warlock | Blade Warp | needed | bladewarp, warlock_psychic_cast_1, bladewarp |
| Warlock | Psychic Ward | needed | psychicward, psychicward_hit0, warlock_psychic_cast_1_no_hex |
| Warlock | Eldritch Blast | needed | eldritchblastnova, surge_hex_explode, eldritchblastperiodic, warlock_psychic_cast_1_no_hex, eldritchblastnova |
| Warlock | Hex: Siphon | custom | Teal weapon glow and target mark with separate life and mana return ribbons. |
| Warlock | Mirrored Blades | needed | No source effect cue; final presentation pending |
| Warlock | Miasma Bolt | custom | Directional flight, collision burst, life-loss flash, and lingering magic wisps. |
| Warlock | Ring of Fire | custom | Ember cast, moving ray samples, bursts, and damage flashes. |
| Warlock | Sigil: Lethargy | custom | Teal radius boundary and target-bound slow hourglass. |
| Warlock | Sigil: Rancor | custom | Bronze radius boundary, recipient triangle, and effective confusion flash. |
| Warlock | Miasma Chain | custom | Moving clasp, pulsing links, frozen path wisps, and separate link and cloud fades. |
| Warlock | Flame Wave | custom | Lateral reveal, moving front, terrain stops, and lingering fire. |
| Warlock | Sigil: Death | custom | Magenta boundary, bone seal, and corpse-centered four-tile eruption. |
| Warlock | Enhanced Entropy | not-applicable | shadow_mastery |
| Warlock | Apocalypse | custom | Radius warning, delayed eruption, and damage cinders. |
| Warlock | Abyss | needed | abysscenter, warlock_shadow_cast_1, abysscenter, fire_cast_2 |
<!-- END GENERATED: skill-effects -->

Smite and Zeal pair their custom impacts with native character animations. Their talent-tree and hotkey icons are tracked in the icon register. Leap and Whirlwind use native character poses with custom movement and contact effects. Their talent and quick-slot icons are tracked in the icon register. Double Throw keeps its native character poses and adds custom per-weapon flight and contact effects. Its talent and quick-slot icons are tracked in the icon register.

Blade Shield uses original cast, orbit, contact and fade effects at the existing combat events. The orbit follows its authoritative active state.

Shared weapon leech: life/mana overlays151/152 still need final recovery effects. Native0x376A with life hue0x26 or mana hue0x489 is an identified placeholder. No separate talent/hotkey icon is required for this shared item effect; each actual skill retains its own icon row.

Molten Boulder uses a native cast pose with custom formation, rolling rock, chips, shatter and ground flames. The source-path mechanics are tracked in `molten-boulder-native-evidence.json`. Talent and quick-slot icons are tracked in the icon register.

Shared Crushing Blow: overlay147 still needs its final impact effect. Native0x376A with hue0x21 is an identified placeholder. This item effect has no separate talent/hotkey icon.

Life Tap recovery: native0x376A hue0x26 identifies the temporary steallife effect. Final steallife, curse cast/target effects and the talent/hotkey icon remain in their existing Life Tap rows; this shared callback adds no second icon.

Shared incoming Graveyard firebolt uses the existing firebolt family: native0x36D4 logical flight,0x3709 impact and0x15E/0x208 sounds are identified placeholders. Final projectile/cast/impact assets remain pending with Sorceress Fire Bolt; this NPC consumer needs no extra talent/hotkey icon.

Raven uses native bird body0x6, identified placeholder name and attack animation, shared SC pose and sound0x1E5. Final druidhawk model/emergence/flight/dive/retirement, blind presentation and druid_summon audio remain pending. Talent-tree and right/quick icons stay in their independent icon row.

Heart of Wolverine: silent UO wisp0x3A/red hue0x21 and Heart of Wolverine [placeholder] name identify its temporary body. Shared SC pose and summon sound0x1E5 are placeholders. Final X3 emergence/walk/hurt/death, heart_of_wolverine recipient/control overlays and spirit/summon audio remain pending. Talent/right/quick icons stay in the independent icon row; physical/AR aura mechanics do not depend on final art.

Oak Sage: silent UO wisp body0x3A is named Oak Sage [placeholder]. Learned casting uses shared SC pose/sound0x1E5 as identified placeholders. Final XW body/emergence/walk/hurt/death, oak_sage recipient and control overlays, spirit/summon audio remain pending. Talent/right/quick icons remain separate text-label placeholders. The life aura functions independently of final presentation.

Spirit Wolf: prepared silent UO body0xE1 is named Spirit Wolf [placeholder]; cold state uses temporary0x374A/hue0x480. Final wolf model/emergence, walk/run, bite/cold contact, intrinsic critical hit-class0x10/overlay54, teleport, hurt/death and wolf/summon audio remain pending. Talent-tree/right/quick icons stay in the separate icon row. Learned casting uses the shared SC/summon-sound placeholders; exact mechanical parity remains unfinished.

Fire Claws uses human Wrestle,weapon sound0x238 and fire contact0x374A/hue0x489 as identified placeholders. Final fire_hit,both player-form A1/contact/cast audio and independent talent/left/right/quick icons remain pending.

Feral Rage uses identified human Wrestle/contact sound0x238 and generic charge flash0x375A. Final feralrage orb front/back overlays,charge-size display,wolf A1/contact and cast/expiry audio remain pending independently of leech/speed mechanics. Its talent/left/right/quick icons remain in the separate register.

Hunger uses identified human Wrestle pose for its S3bite and contact sound0x238. Shared life/mana recovery uses0x376A with life hue0x26 or mana hue0x489; current Hunger has no separate source impact overlay. Final wolf/bear S3body,bite/contact,leech and audio remain pending independently of mechanics.

Inherited NPC item resource maxima add no independent active effect. Iron Golem retains the identified UO summon/contact placeholders; its final summon, body and item-effect presentation remain in this register. Resource arithmetic and fractional balances do not wait for those assets.

NPC poison-length/half-freeze/Cannot Be Frozen defenses add no independent active effect. Ordinary cold/freeze retain the identified UO 0x374A/hue 0x480 marker; poison retains its generic green marker. Final Iron Golem body/contact/state/audio remains pending, separately from these duration mechanics.

NPC Replenish Life and signed poison recovery add no independent active effect. Existing generic UO poison markers remain identified placeholders; final Iron Golem state/contact/audio is still needed. Any eventual poison cue should reflect the active poison state even when replenishment offsets its damage.

Exact NPC maximum-life regeneration callbacks add no independent visual effect or audio. Final Iron Golem summon/body/contact/state cues remain pending; existing generic UO presentation is an identified placeholder.

NPC current-life/mana/stamina percentage callbacks add no independent active effect or audio. Final summon/body/contact/state presentation remains pending; numerical pool changes do not wait for assets.

Prepared Iron Golem body/controller: silent UO body14/hue0x835 and the explicit “Iron Golem [prepared placeholder]” name identify temporary presentation. Moving and ordinary-contact mechanics now have a prepared controller; learned casting remains disabled. Final G4 emergence/walk/attack/death, summon, thorns, contact/state effects and audio remain pending. G4 has no separate GH/BL/RN animation row in the pinned file; do not invent one by copying another summon. Talent and hotkey icons remain separate; the internal controller adds no icon.

Prepared Iron Golem/NPC flat retaliation uses the shared UO0x374A/hue0x481flash as an identified placeholder. Final thorns/contact/audio and any state-appearance change remain pending. Same-state replacement/expiry does not certify a final fade or aura graphic; the current body remains the named UO placeholder. This internal consumer adds no separate talent/hotkey icon.

## Shadow Warrior ground/AI presentation — 2026-11-08

Mechanical motion/AI adds no final artwork. Body `0x190`/hue `0x455`, native A1 animation and existing shared contact/Fire Blast effects remain identified placeholders. Final Shadow movement, equipment, attacks, casts, contact, death and audio remain pending. See [mechanical checkpoint](shadow-ground-checkpoint.md); talent/hotkey icons stay in `skill-icons.md`.

**Shadow Quickness/Fade (2026-11-08):** own SC events and buff lists are mechanical additions. Final Shadow SC animation, quickness/fade overlays, application/expiry and audio remain pending with the named body/equipment presentation placeholder. Reuse player buff effects where appropriate during the asset pass. See [checkpoint](shadow-buffs-checkpoint.md).

**Shadow Venom (2026-11-08):** own state and original melee poison now work. Final SC/venomclaws presentation, poison contact/ticks, cure/expiry and audio remain pending; the named body/equipment and shared presentation remain identified placeholders. Reuse the existing player Venom/poison effect family during the later asset pass. See [checkpoint](shadow-venom-checkpoint.md).

**Shadow Psychic Hammer (2026-11-08):** own cast/damage/knockback mechanics reuse the player Hammer presentation (`0x374A` impact, `0x1E5`/`0x1F1` audio) as identified placeholders. Final Shadow SC/cast/impact/recovery and audio remain pending with the named body/equipment; no travelling projectile is introduced. See [checkpoint](shadow-psychic-checkpoint.md).

**Shadow Mind Blast (2026-11-08):** own SC/area/damage/conversion/stun now work. Final SC/cast/center/impact, conversion, stun/recovery and audio remain pending; `0x373A` center, `0x374A` impact, `0x1E5` audio, shared status effects and named Shadow body/equipment are identified placeholders. Reuse the player Mind Blast/conversion/stun asset family. See [checkpoint](shadow-mindblast-checkpoint.md).

**Shadow Cloak (2026-11-08):** own SC/armor/target curse/blind now work. Final SC/cast/darkness, caster armor/expiry, target cloak/blind/recovery and audio remain pending; `0x373A` cast, `0x375A` target overlay, `0x1E5` audio, shared status effects and named Shadow body/equipment are identified placeholders. Global darkness remains a presentation gap. Reuse the player Cloak/defense/blind asset family. See [checkpoint](shadow-cloak-checkpoint.md).

**Shadow Blade Shield (2026-11-08):** own startup/pulses/full weapon contacts now work. Final SC/cast,attachment/orbiting shield,pulse/weapon contact,expiry and audio remain pending. Named Shadow body/equipment and existing native combat/status feedback are identified placeholders;no final NPC shield-ring asset is present. Reuse the player Blade Shield/contact/poison asset family. See [checkpoint](shadow-blade-shield-checkpoint.md).
