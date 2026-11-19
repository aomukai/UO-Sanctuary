# Companions and the Britain inn

The Avatar can travel with one active companion at a time. Over the course of an adventure, they can recruit several people and get to know them through their own stories. The Britain inn is where the Avatar can reconnect with companions they’ve already recruited. Finding people to travel with is part of exploring the world.

## Finding a companion again

The innkeeper opens the Avatar’s personal companion roster. When someone is available, the innkeeper can call them down. Sending a companion back to the inn makes them inactive. When the Avatar calls them back, it’s the same companion with the same gear, potion reserve, and progress. They can catch each other up before setting out together again.

A companion’s personal story can take them elsewhere. The innkeeper gives the information the companion left with them, if any.

- If the companion shared where they were going, the Avatar can go meet them there.
- If the companion said when they would be back, the Avatar can return later.
- If the companion left without word, the Avatar can follow up on their own and discover where they went.

These departures are authored story beats. At the end of most companions’ stories, the Avatar can access them through the innkeeper. Iolo’s retirement takes him to the bard’s college in Britain.

## In the field

An active companion fights and uses their potion reserve on their own. Their background also affects what they notice and do outside combat. A scout might spot a dungeon patrol before the Avatar does, warn them, or hamstring the runner before it can alert a nearby pack. A mage might miss that same patrol. This gives a dungeon author a reason to place a patrol where spotting it early changes the encounter.

A companion matches the Avatar's active class level when they join, return, or the Avatar levels up. Their own internal class determines their stat growth, attack speed and hit recovery. Each companion has a small authored skill progression and equipment restrictions. Gear and healing supplies carry over between trips. Hit points are their sole combat resource. Special attacks use timing and cooldowns without a mana cost.

The worldbuilding direction is for dark dungeons where the Avatar can see the area around their light. Companions can provide light in ways that fit them. A mage might summon a glowing wisp, while someone else carries a lantern or torch. The exact lighting, patrol routes, detection ranges, and reactions belong to encounter design. The game handles scouting, slowing, and light in either dialogue mode.

Other proposed specialties include a defender taunting enemies, a mage identifying items, a rogue spotting traps or hidden doors, and a bard introducing a specialist vendor. A town guard could point the Avatar to a local bounty. These are candidates alongside tank, melee, ranged, and caster archetypes. An equipment interface could let the player compare and swap loadouts, including a proposed drag-and-drop shortcut that returns the old item. [Building Britannia](worldbuilding.md)

Place and object triggers can prompt a brief line, an emote, a coded action, or silence. A companion might draw a torch in darkness or crouch to inspect tracks, if the situation supports it. First and return visits can yield different reactions. Use restrained timing, with a cooldown or recent-line check for repeat crossings. Keep emotes in the existing UO convention and voice spoken lines.

## Goals of their own

Every companion has a personal goal. A barbarian might want to recover the lost totem of a fallen friend and take it home to honour them. A mage might be looking for a particular book. Companions talk about what they’re after, and the Avatar can ask questions or choose to help.

An inactive companion can pursue their goal on their own. That can move their story forward and change where the Avatar can find them. A setback still leaves a way to find and bring them back. For example, a companion could wind up locked in a dungeon cell, with their equipment held in a chest at the other end of the level. Finding the companion and recovering the gear would be part of that story. The Avatar can follow up when they choose.

Recruitment examples include finding an injured warrior who needs bandages, escorting a mage out of a crypt, and meeting people openly looking for work in Serpent’s Hold. The authored roster and any separate generated hirelings are open. Relationships that respond to repeated treatment were discussed; deciding what the story can infer from a companion’s death needs care.

## Iolo and the original companions

After the summons, the Avatar meets Iolo at the Bountiful Harvest Inn. He suggests getting some practice at the old graveyard northeast of town while he finishes his business. When the Avatar has reached level 6 or higher and speaks with him at the inn, he unlocks the island’s moongate and offers to join. Once recruited, his dialogue offers Equipment, and the owner can move gear onto and off his paperdoll.

Choosing to travel alone leaves Iolo at the inn until the Avatar departs through New Haven’s moongate. He tells the Avatar where to find him if they leave without him. After crossing, the Avatar can still revisit New Haven and other players can still meet Iolo at the inn.

In the planned Britain chapter, an Avatar who left alone can talk to Iolo at the bard’s college, where he has retired from adventuring. If they travel together, they meet Lord British in Britain. Iolo gives the Avatar his old runebook and retires when that shared adventure ends. [Travel, shrines, and recovery](travel-and-recovery.md)

The old companions can have jobs, homes, and their own selected histories in this Britannia. Geoffrey, Dupre, and Shamino are examples of possible recruits; others could simply be residents. Katrina tending sheep in Magincia is one way ordinary gathering can become an introduction. The traditional associations used for character writing are Iolo with Compassion, Mariah with Honesty, Geoffrey with Valor, Jaana with Justice, Julia with Sacrifice, Dupre with Honour, Shamino with Spirituality, and Katrina with Humility.

Iolo is a bard. His internal combat profile uses Amazon scaling. He uses Magic Arrow from level 6, Cold Arrow from level 10, and Guided Arrow from level 18. He wears light armour, clothing and accessories, and wields crossbows. He refuses incompatible gear in a private message to his owner.

## Reunion and dialogue

At reunion, the Avatar tells the companion what happened while they were apart. Record that knowledge as something the Avatar shared. Events they experienced together can come up later as shared memories. The companion can also tell the Avatar what they did while away. A destination they mention, an introduction to someone they know, or the account of a setback can become a journal entry that gives the Avatar something to follow up on later. [The Avatar’s journal and virtues](journal-and-virtues.md)

Use the same private dialogue gump for authored scripts and optional LLM roleplay. The innkeeper and companions can speak from the same saved facts in either mode. The server determines who is available, what the innkeeper knows, which story beats have happened, and when a companion can join. Both modes offer the same actions and story outcomes. Keep exchanges between companions and NPCs short enough to return the conversation to the player. [NPC dialogue](npc-dialogue.md) · [Social encounters](social-encounters.md)

## Witnesses and what NPCs know

For each meaningful story checkpoint, save what the Avatar discovered or did and the identity of any companion who witnessed it when it happened. A companion’s knowledge comes from their background, what they saw with the Avatar, and what they learned later. If one companion witnessed the rescue in Hythloth and another heard about it during a catch-up, their dialogue should reflect those different experiences.

Use the same distinction for talkative NPCs when they have a reason to know about a story. Their dialogue can draw on relevant facts and how they learned them in both scripted and LLM modes. Utility and decorative NPCs can simply serve their role in the world. [The Avatar’s journal and virtues](journal-and-virtues.md)

## Saved roster and existing care rules

The server saves the roster with the character. Keep an active companion slot and a persistent record for each recruited companion, including their relationship, personal story progress, and availability. Returning to the inn preserves the companion’s gear, healing reserves, and progression. If a story temporarily puts gear in a chest, preserve the identity and ownership of those same items through capture and recovery. Roster changes must preserve the one-active-companion limit through saves, reconnects, and retries.

The existing care preparation already enforces one active mercenary contract per character. Its retirement operation consumes unused healing doses and retains gear for owner recovery. Lodging a companion at the inn needs its own reversible transition. Reconcile death, resurrection, and when independent story beats advance when designing the full companion loop. [Mercenary care preparation](t46-mercenary-preparation.md)

On a community shard, decide how duplicate personal recruits appear to other players. A possible presentation gives the owner the companion’s name and other players a generic description. Verify that story attendance, equipment, and roster transitions stay tied to the right character.
