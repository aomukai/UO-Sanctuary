# NPC dialogue and personal world state

Britannia’s talkative NPCs use a private dialogue gump. Scripted mode uses a fixed authored portrayal and a finite set of conversations. Scripts can respond to discoveries, relationships, and story beats. Optional LLM mode uses the same game facts and adds open-ended conversation, including personal portrayals that can vary from one Avatar to another. Utility and decorative NPCs can simply serve their role in the world.

Elin, the New Haven arrival guard, is the first scripted speaker. She greets each new character privately and tells them that Iolo is waiting at the Bountiful Harvest Inn. Click her in the world to start the conversation again. The topics **Iolo**, **New Haven**, and **Bountiful Harvest Inn** stay available throughout the conversation. The server checks the speaker, range, line of sight, and active choice whenever the player replies. The larger dialogue window shows the speaker's name, portrait, and current line alongside the player's saved portrait.

Click a nearby merchant to open its buy window, or a banker to open your bank box. The service uses the server's normal access rules. Regular UO chat still accepts its usual service commands.

Iolo sits at the south table of the Bountiful Harvest Inn. His **Summons**, **Danger**, and **Graveyard** topics explain why he called the Avatar back and point toward the old graveyard northeast of New Haven. The first conversation adds *An Unexpected Summons* to the Avatar’s journal. Once the character has reached level 6 or higher, speaking to him permanently unlocks New Haven’s public moongate and offers **Travel Together** and **Travel Alone**. If the Avatar chooses to go alone, Iolo says he’ll wait at the inn until they leave and points them to the bard’s college in Britain after that. **Equipment** appears throughout the recruited companion’s dialogue and opens his paperdoll for his owner.

The shared audience at Britain Castle uses alternating portraits for Lord British and Iolo. Iolo reports on Krulg if the Avatar defeated him. After the farewell, the journal records both the king’s request and Iolo’s retirement. Geoffrey records a separate discovery on the first meeting at the guard tower. He offers Wolfgang’s company after Iolo retires.

## Companions and variable personalities

Each companion has an authored identity, background, and goals in both modes. What they know about the Avatar can change through experiences, disclosures, and their personal story. Their optional LLM dialogue portrays that same person. [Companions and the Britain inn](companions.md)

For selected talkative NPCs in LLM mode, roll a personal profile the first time the Avatar meets them. Reuse that profile on later visits for the same character and NPC. One Avatar might meet a fun-loving, boastful blacksmith who used to adventure. Another might know the same blacksmith as a bitter widower. They both know him as the smith who runs the same forge and offers the same services. Scripted mode uses the authored portrayal.

Meeting different portrayals is another reason to roll an alt, alongside playing another race and finding lore the first Avatar missed. Acquaintances develop separately for each character in either dialogue mode. The blacksmith might greet one Avatar as a friend and meet another as a stranger.

An authored range of traits and compatible backstories is one way to define what can vary for each NPC. Where an existing character or event matters to a story, supply those facts when choosing the profile and when generating dialogue. For example, the widower profile fits a character whose family history is open to variation. If the smith’s spouse appears in an authored scene, the personal profile needs to fit that scene. A version that changes who appears or what happened would need its own authored story state.

## Which facts the game saves

- **Shared world facts.** The server defines who runs the forge, where it is, what services it offers, and which authored events are possible.
- **Personal story facts.** The server saves what this Avatar discovered or did, whom they know, who witnessed an event, and what they reported later. These facts drive the journal, campaign progress, NPC reactions, and companion stories. [The Avatar’s journal and virtues](journal-and-virtues.md) · [The invasion campaign](invasion-campaign.md)
- **Personal conversation.** The rolled profile and optional chat history give the LLM continuity for this Avatar’s relationship with the NPC. The proposed client-side setup uses a local model, a player-run endpoint, or a player-selected remote provider. It runs inference while the player is talking. Profile persistence across devices and the amount of conversation history to retain are open. [Social encounters](social-encounters.md)

Use the NPC’s knowledge and its source when choosing scripted lines or supplying LLM context. A character can recall something they saw, refer to what the Avatar told them, or pass on a rumour as a rumour. The game records an authored journal discovery when its story condition is met. Improvised small talk belongs to the conversation history.

Both dialogue modes give access to the authored story beats, journal discoveries, services, and companion recruitment. An LLM can interpret a request to see the smith’s wares. The server checks whether the action is available and opens the trade interface. The extra depth in LLM mode comes from the repertoire of things a player can talk about and the personality they get to know over time.

## Sessions and game actions

Use stable character and NPC identities for conversation state. A proposed short session window lets the player reopen an accidentally closed conversation. Ignore replies that arrive after the conversation ends or changes target. For a new session, supply the canonical NPC profile, relevant facts and their sources, and the available actions. The client can add the personal profile and conversation history.

The model can suggest a vendor purchase, repair, dice game, or recruitment. The server rechecks the speaker, range, session, and current eligibility when the action is requested. It runs the actual transaction or story action and returns the result. A character saying they plan to visit Covetous belongs in chat history; a verified visit belongs in game state. Scripted choices can reach the same actions.

Model size and context length are benchmark decisions. A small local model and an 8K–16K context were starting points. Generated summaries can help with conversational continuity; the game’s event records remain authoritative. Mycelium is a separate research idea.

## Recorded dialogue

Voice the scripted lines in English, Japanese, and German. Produce the recordings ahead of release, review the performances and pronunciation in each language, and ship the approved audio with the game. ElevenLabs is the likely production tool. Match each recording to its authored line and language.

Scripted moments use the same authored lines and recordings in either dialogue mode. A guard’s summons or a companion’s authored story scene still plays the recording when reached through an LLM-enabled conversation. The freeform replies around it are text-only.
