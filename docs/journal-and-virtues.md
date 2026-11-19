# The Avatar’s journal and virtues

The journal is a collection of short memories the Avatar picks up by exploring Britannia. Each entry is a card in that collection. Its reward is a piece of lore, whether it comes from a turning point in the invasion or a sign that most people walk past. The player can spend an evening following a lead, stumble upon a story much later, or get on with fishing, crafting, or fighting. Journal and virtue progress are independent of campaign access, gear, and combat power.

## Collecting memories

A new discovery adds its card automatically and gives a short system message, such as *New journal entry — The Bell at the Docks*. Read a book or sign, inspect a map, visit a place, meet someone, learn a companion’s story, make a choice, or see what came of a past deed. Each can yield an entry when it reveals something worth remembering. Revisiting a place or person can yield another card when there’s something new to learn.

The journal holds the invasion, mercenaries’ private stories, and the details of ordinary life together. A map found in Wrong and a conversation with a prisoner in Hythloth can each stand on their own. A mercenary might mention a smith who knew their brother. The entry keeps the smith’s name so the player can follow up if they happen to be in town weeks later. Dialogue and entries can give specific leads that the player is free to pursue in the world.

Show a connection once the player has discovered both entries. For example, a plaque at the docks records that the bell rings for ships that never come home. Later, a dockworker tells the player whose ship the flowers beneath it were for. Both discoveries get their own cards, and either can come first. The connection gives the older entry new meaning while preserving what the Avatar knew at the time.

The book shows the cards and connections the player has found. Empty slots, discovery totals, objective checklists, and completion percentages would give away the shape of the collection. Players can compare memories and trade leads with friends on the shard. In Normal Britannia, someone who heads straight to the Shame encounters can still follow local leads after the invasion, with the journal and conversations reflecting that it’s over.

Nightmare and Hell are temporary realities. Once the player chooses to close one, that character’s opportunity to find its undiscovered lore ends. The cards they found remain part of their own *Adventures of the Avatar*. A new character can take a different route through the campaign and find memories the first character missed.

## The journal’s voice

The journal is written in first person by the Avatar looking back years later. It fills as the player lives through the adventure. That framing makes room for private, easily missed details that the Avatar happened to remember, alongside events the whole of Britannia would know.

The opening entry starts after a hard day at work. Weeks of overtime have worn the Avatar down. They turn on their PC, Iolo summons them into Britannia, and they arrive in New Haven. The number of years between the adventure and the narrator’s telling, the final opening prose, and how the journal first appears to the player are open. A magical artifact received early is one possibility.

Write the body of the journal from what the Avatar saw, learned, and chose. The opening establishes a personal voice, and later entries leave room for the player’s own motives. Authored variants can reflect discovery order, past choices, which companion was present, and the state of the invasion. A memory can record a rumour as a rumour, a companion’s account as something they told you, and a firsthand discovery as something you saw.

## The principles and virtues index

Use the [principles and virtues diagram](assets/principles_and_virtues.png) as an illustrated index to the same journal. At rest, the page shows the circles and symbols without printed names. Hovering or focusing a symbol or circle reveals its name. Each virtue symbol opens a filter for entries associated with that virtue, and each principle circle opens a broader filter.

| Principle | Entries shown |
| --- | --- |
| Truth | Honesty, Justice, Honour, Spirituality |
| Love | Compassion, Justice, Sacrifice, Spirituality |
| Courage | Valor, Honour, Sacrifice, Spirituality |

Clicking Sacrifice shows the Sacrifice entries. Humility has a separate symbol outside the three circles. An entry can fit several virtues or none. A chronological view of all discovered entries is the proposed starting view, with the Infinity symbol as a possible way to return there. Keeping category names off individual cards would let the index reveal a new reading of a familiar story.

The symbols begin as pale gray line art, legible and usable from the start. As the Avatar gains virtue points, the corresponding emblems gain ink and line strength. The principle circles can deepen as the Avatar makes choices guided by those principles. For example, a choice guided by Love can strengthen its circle and any virtue emblems whose scores rose. Justice can also grow through choices guided by Truth. A pale symbol can already lead to a page of memories because discovering a story and earning recognition for a deed are separate events.

Image generation is an option for exploring the book’s visual style and emblem artwork. The labels, filters, and gradual ink changes are interactive parts of the UI. The exact ink progression for principles and how much numeric detail to expose are open.

## Deeds and virtue points

After the invasion, optional stories can present choices that express Truth, Love, or Courage. A city might introduce a conflict and a dungeon might hold evidence that changes how the player sees it. A companion or Iolo can talk through what’s at stake. The circumstances and the people involved should convey the values of the choices, so a newcomer can learn how virtues work by playing.

One worldbuilding proposal follows the eight virtues and anti-virtue dungeon themes through a larger set of linked dilemmas. Author it in small encounters the Avatar can discover on their own schedule. [Building Britannia](worldbuilding.md)

A card’s category describes what the memory is about. Learning about someone’s kindness can give the Avatar a card under Compassion. An authored choice to act compassionately can also give the Avatar points. Keep a record of the actual deed so it can inform later memories and reactions.

One proposed dilemma starts with an orc raid map. The Avatar can bring it to a shaman who hopes to stop the raid or give it to Yew’s druids, who want to protect nearby homesteads by moving against the orcs. The discovery yields lore on its own. Following up reveals what the Avatar decided and why each side thought it was worth doing. The awards for this encounter are open.

The working cap is **eight points per virtue**. The current point schedule is a proposal for narrative choices.

| Chosen principle | Two points | One point each |
| --- | --- | --- |
| Truth | Honesty | Justice, Honour, Spirituality |
| Love | Compassion | Justice, Sacrifice, Spirituality |
| Courage | Valor | Honour, Sacrifice, Spirituality |

Under this schedule, Spirituality caps after any eight eligible principle choices. Decide whether that reflects the intended meaning of Spirituality. The authored opportunities should let one character attain all eight titles through a variety of choices, with enough alternatives to allow missed or mutually exclusive outcomes. Earning any or all of the titles remains optional.

The working Humility proposal is to take up another discipline as a beginner after earning wardrobe access. A distinct additional class reaching level 5 or a distinct additional crafting profession reaching trained skill 50 earns one point, once per discipline. The initial discipline grants no automatic point. Gathering eligibility and the treatment of existing progress need a decision. Use the existing wardrobe rules as the starting point for that work. [Wardrobe progression](wardrobe-progression.md)

## Recognition and titles

When a deed earns points, tell the player in a system message. *You have gained recognition for your deeds of {virtue}.* If a deed advances several virtues, grouping their names in a single message is a presentation option. Reaching the cap gives a separate message. *You have unlocked the title Avatar of {Virtue}.* The title is cosmetic and the player can choose to wear it. Its place in the existing title selector is open. [Earned titles](t41-earned-titles.md)

NPCs in the virtue’s associated city respond to the achievement itself, regardless of the displayed title. Trinsic might make an occasion of the Avatar of Honour’s first arrival, with salutes and occasional greetings on later visits. In Cove, the same traveller gets an ordinary welcome. A first recognition scene can become a journal entry of its own. The form of recognition can reflect each city and virtue.

## Story state and authored text

Keep discovered facts, choices, witnesses, reported accounts, and campaign state in persistent story data. The journal presents those facts in the Avatar’s voice. Stable entry and deed identities let the game keep discoveries across reloads and award a one-time entry, point gain, or title once. Companion knowledge can use the same facts while distinguishing a scene the companion witnessed from one they heard about later.

Class can open a conversation, and an acquaintance can persist across wardrobe changes. An authored conversation can record what this Avatar learned from an NPC, while a later visit can reveal a new detail based on their relationship. The server records the discoveries and acquaintance used by both scripted and optional AI dialogue. [Social encounters](social-encounters.md)

Provide authored English, Japanese, and German entries, system messages, and index labels. The complete journal and virtue experience is available with AI disabled. Any optional generated wording draws from the established facts. Game logic determines discoveries, choices, awards, and NPC reactions.
