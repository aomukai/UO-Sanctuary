# The invasion campaign

Britannia’s invasion plays out across Normal, Nightmare, and Hell. The player can investigate at their own pace or head straight to the three story bosses in Shame 5. Each boss is the gate to the next stage. A level 1 character who could survive the encounters could advance through them in order and unlock Hell. Levels prompt guidance. Boss victories earn access.

Diablo is the intended final boss in Hell. The current candidates for the earlier fights are King Leoric, the Skeleton King, in Normal and Archbishop Lazarus in Nightmare. The Butcher is another candidate for Normal. [Campaign boss encounters](boss-encounters.md)

## Normal Britannia

New Haven is the arrival point. Iolo introduces the Avatar to Britannia and mentions Lord British early in the story. Around level 20 or 30, the next eligible guard the Avatar passes opens a private conversation and asks them to visit Lord British at Britain Castle. The Avatar can close the conversation and carry on.

British has heard that dark places all over Britannia are getting worse at the same time. He suspects a connection and asks the Avatar to investigate. His last useful lead is a scout who went to Covetous and never came back. The scout gives a player looking for direction somewhere to start. The investigation itself is a scavenger hunt spread across the eight anti-virtue dungeons. A dead adventurer, a map, an item in a chest, or a prisoner can tell part of the story. Pick up the trail anywhere and in any order.

Defeating the Normal story boss in Shame 5 opens Nightmare town destinations at the moongates. The Avatar can travel between Nightmare and their home in Normal.

## Nightmare Britannia

Lord British is sick in bed. His friend Blackthorn governs and is sincerely trying to hold the country together. The same invasion is further along here. The player can see it on familiar roads and in familiar towns. Guards sleep at their posts, drink on duty, or demand protection money. Vendors pay less and charge more. Bandits are more common, animals are more aggressive, and there are fewer prey animals and more predators.

Around level 50, the next eligible Nightmare guard opens a private summons to see Blackthorn. He explains how far the corruption has spread and asks the Avatar to investigate. Nightmare has its own clues across the dungeons. The Avatar can follow them or head straight to the story encounter in Shame 5.

Defeating the Nightmare story boss earns entry to Hell. Give the player an explicit warning before they move on that accepting the transition ends access to Nightmare for this character. Until then, they can keep exploring it. After the transition, they can travel between Normal and Hell.

## Hell Britannia

The mainland towns have been overrun. The surviving towns are on islands. New Haven, Nujel’m, Moonglow, Serpent’s Hold, and Buccaneer’s Den are the working roster. Moongates connect the islands. Reaching the mainland takes a boat, and the pirates in Buccaneer’s Den can provide passage.

The dungeons hold more pieces of the invasion story. The Avatar can search for them or go directly to Diablo in Shame 5. After victory, an explicit departure closes Hell for that character and opens the remaining supported regions and facets for endgame exploration. Lost Lands, Ilshenar, Malas, and Ter Mur were discussed; confirm Tokuno and the exact roster and routes. One proposed distribution gives the other regions Nightmare overworlds and Hell dungeons, with Normal outdoors and Nightmare in deeper public dungeons in the home realm. Returning the Avatar to their original arrival point in Normal is a proposed way to end the campaign.

## What the Avatar brings home

Normal Britannia is the permanent home. Housing placement and purchase are unavailable in Nightmare and Hell. Give the player a short in-world message, such as *You don’t feel at home here*. Use the existing housing entitlement and ownership rules for permanent housing. [Housing](t42-housing.md)

A character has the same bank across all three realities. Money and items banked in Nightmare are there when they visit a banker in Normal. Realm closure applies to the character’s access and discoveries in the realm. Handling any recoverable belongings left in a temporary realm needs a decision. [Travel, shrines, and recovery](travel-and-recovery.md)

Found journal cards stay with the Avatar. The chance to discover Nightmare- or Hell-specific lore ends when that character leaves the reality for good. A player can rush to unlock the endgame, then make another character and take the time to read the world. Playing a human, elf, or gargoyle offers another reason to start a new adventure even with the wardrobe available. Each character has their own *Adventures of the Avatar*. Normal’s local leads can still be discovered after the invasion and should read as history at that point. [The Avatar’s journal and virtues](journal-and-virtues.md)

## Story Shame and regular Shame

Each reality has a story version of Shame 5 and a regular version. Route eligible characters to the story version for their first victory. Record that victory once per character. On subsequent visits, send that character to regular Shame 5 whenever they still have access to the reality. Another player’s first clear depends on their own campaign state.

Shame uses immunity-free monster profiles at every difficulty. Every build needs a route through its campaign encounter, including in Hell. Lazarus’s ward is a temporary arena mechanic tied to his position and a living guardian. Use boss patterns, positioning, and monster pressure for challenge. Normal Shame uses ordinary rewards. Nightmare and Hell Shame serve story progression. Keep valuable repeatable drops out of both the story and regular versions so players looking for tradable gear have a reason to explore the rest of each reality.

Nightmare Britannia gives the existing Nightmare equipment tiers a natural source. Author ordinary encounters using the existing monster variants and their difficulty-specific loot. That system assigns difficulty and rewards to the creature, so map placement alone does not define either. Confirm the final Normal Shame floor bands before authoring its public spawns. [Nightmare and Hell monster variants](t19-difficulties.md)

## Boss encounter direction

- **King Leoric** shares damage aimed at him with his living skeletons. The Avatar can clear the army to concentrate damage on the king or pressure him directly and wear down the army in the process.
- **Archbishop Lazarus** moves through a fixed circuit of five elemental pentagrams. Each stop gives the player a choice between pressuring Lazarus and dealing with the demons that sustain his wards.
- **Diablo** temporarily disrupts habits the player relies on. A ranged fighter may have to reposition, a melee fighter recover from displacement or fear, and a summoner get close enough to reclaim control of their army. The exact mechanics and their behavior with a mixed party need playtesting.

Ordinary encounters can teach parts of these fights and carry their influence into places like Umbra, Blood Dungeon, and Doom. The detailed fight rules, including Lazarus’s ritual circuit and the proposed dream awakening after defeat, belong with the encounter designs. [Campaign boss encounters](boss-encounters.md) · [Social encounters](social-encounters.md)

## Dialogue and saved campaign state

Run the same campaign on a local ModernUO server or on a community shard, using ClassicUO as the client. The server owns its characters and saves personal campaign state with them. A community shard can host players at different stages at the same time. Realm closure is per character. The technical mapping of three Britannias onto server maps or instances is open.

- **Campaign access.** Save story boss victories and confirmed realm departures. Use those facts to validate story encounter entry, moongates, boat arrivals, recall, and other travel on the server. A saved victory earns its next unlock once, including after a reconnect or retry.
- **Guard summons.** Derive eligibility from the character’s level and campaign stage. Store whether they saw the summons and whether they spoke to the ruler as separate facts. The first eligible guard encounter presents the invitation. The player can dismiss it and proceed to Shame, or follow the invitation through the ruler and investigation. The character’s place in the boss sequence determines boss access.
- **Discoveries.** Entering a place, inspecting an object, or choosing a dialogue topic can reveal a fact and add a journal card. Save what was discovered and use it to select later dialogue. This is the same pattern companion stories can use.
- **Private dialogue.** Use one conversation gump for authored NPC dialogue and optional LLM roleplay. A guard can open that gump directly for the player, with an authored summons available in either mode. The server owns story outcomes, rewards, and unlocks. Other players have their own conversations. [Game UI](game-ui.md) · [NPC dialogue](npc-dialogue.md)

## Open decisions

- Set the exact guidance levels, the eight dungeon identities, and each reality’s authored clues.
- Resolve party entry and credit for players at different campaign stages, including friends who offer to help after clearing or leaving a reality.
- Choose the warning and confirmation flow for leaving temporary realities, including how it handles corpses and recoverable possessions.
- Confirm the Hell island roster, endgame destinations and travel routes, and the exact difficulty and loot rules for regular Shame.
