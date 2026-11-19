# Game UI and localisation

Sanctuary uses ClassicUO for the game client. Its interface should make the character, combat controls, and stories easy to reach while supporting ordinary UO speech and interactions.

## Character and combat UI

- **Combat dock.** Mouse bindings, combat hotkeys, the potion belt, menu icons, and an experience strip share a movable dock. It starts at the bottom center and saves its position for the character. Menu icons have hover labels, and extra belt rows open above the dock.
- **Game world.** The world fills the game window and resizes with it. Gumps sit over the world and can be moved independently.
- **Avatar health.** Drag the avatar in the world to open a movable gump with their name, saved character portrait, and live life, mana, and stamina.
- **Character screen.** The warm bronze frame brings the paperdoll, equipment and live stats into one gump. Character, status shortcuts and the level-up Stats button use this screen. Equipment and attribute spending use their existing server actions.
- **Journal and virtues.** Open the book from the character sheet or combat dock. It has a page for collected memories and the illustrated principles and virtues index with hover names. Meeting Iolo in New Haven records the first memory once per character. The book also links to the chat log. [The Avatar’s journal and virtues](journal-and-virtues.md)
- **Window shortcuts.** B opens the backpack, C opens the character sheet, J opens the journal, M opens the world map, and Esc opens Options. Press the same key again to close the window. Closing the world map with its shortcut also saves its settings. These shortcuts are suspended in world chat and while a dialogue or another text field has focus.
- **Options and help.** Options has Graphics, Sound, Music, and Hotkeys tabs. Graphics offers Classic and Book dialogue fonts. Apply saves changes, and the Hotkeys tab edits the four letter shortcuts. Log Out opens the normal confirmation. Help is a separate gump with key bindings, interaction tips, and access to shard staff. Sanctuary uses the world map for navigation.
- **Loot.** Nearby loot uses the bronze frame and item grid. Hover an item for its tooltip and click it to take it.

## Conversations and world chat

Talkative NPCs use a private dialogue gump that shows the current speaker’s portrait and name, their line, and the player’s portrait and available replies or topics. When LLM mode is enabled, the same window can accept free text. The player can close the window and resume play. [NPC dialogue and personal world state](npc-dialogue.md) · [Ultima VII dialogue reference](assets/ultima-7-dialogue.png)

Click a friendly NPC to interact. Merchants open the buy window, bankers open the player's bank box, and talkative NPCs open their private dialogue with selectable keywords. Use regular UO chat for speech in the world, player conversation, and service commands such as `vendor buy`.

## Initial languages

English, Japanese, and German are the initial languages the creator can review in-game. The launcher selects the language for itself and the client. Localise Sanctuary’s gump labels, tooltips, system messages, journal entries, and scripted conversations, alongside the rest of the game text. Use stable text IDs and a shared glossary for recurring names, places, skills, and virtues. [Sanctuary launcher and first-run setup](launcher.md)

Review the three languages in the actual layouts. Check that longer German labels fit and that Japanese text renders, wraps, and can be entered with an IME in world chat and any free-text dialogue field. Check that Enter works as intended in both contexts. All scripted story content is available in each language.

## Typography and tone

Ultima’s English dialogue can have a lightly old-fashioned register. The Classic dialogue setting uses the client's regular bitmap face. Book uses IM Fell English for Latin text, including German umlauts and ß, and a Noto Serif Japanese Mincho face for kana and kanji. Japanese writing can give a court summons a formal voice and a tavern greeting an everyday one. [Dialogue font sources](../art/player-ui/dialogue-font/README.md)
