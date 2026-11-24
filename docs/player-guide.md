# Player guide

## New Haven and leaving the island

Follow the harbour guard to Iolo at the Bountiful Harvest Inn. At level 6, speak to Iolo again to unlock New Haven’s moongate. You can take him as a companion or travel alone. Surface encounters include farm bandits, slow early undead, and level 1–3 boglings. The mines introduce elementals and the named ogre Krulg. Healers mend nearby wounded players automatically. The [worldbuilding catalog](worldbuilding.md#spawners) lists the encounter tables.

Find Elara the Scholar in the north mage tower for identification (50 gold), Identify scrolls, and Town Portal scrolls. Trade trainers charge 1 gold per 0.1 skill up to 30.0. [Items and crafting](items-and-crafting.md)

### Graveyard crypts

Jens Won sells one shovel for 100 gold at `(3555, 2456, 15)` and restocks every five minutes. Carry it in your backpack and double-click it within eight tiles of `(3599, 2452)` to spend it on a random one of four fixed crypts. The arrival room is quiet and holds **a rope back to the graveyard**. The other rooms and corridors contain Haven ghosts. Look for optional chests and left-click their gold once to take it. A cleared room remains cleared throughout the visit. A new visit to a vacant crypt gets fresh encounters and treasure rolls. A defeat inside sends you and your corpse back to the graveyard. [Crypt implementation](../src/Sanctuary.Content/HavenCrypts.cs)

## Progression and controls

Level 99 is the class cap. The current cumulative XP curve is one-third the original progression curve, rounded up. Each level grants five spendable stat points and one talent point. The old New Haven starting XP multiplier was removed. [Experience curve](../src/Sanctuary.Content/ExperienceLevels.cs)

| Control | Action |
| --- | --- |
| Left-click or hold ground / enemy | Move or approach and use the left action |
| Shift + left-click or hold | Use the left action in place |
| Right-click or hold | Use the right action |
| S / F1–F12 | Assign / use a quick skill |
| B / C / T / K | Backpack / character / talents / trade skills |
| J / M | Journal / map |
| 1–4 | Drink from one of the four belt columns |
| F | Gargoyle fly or land |
| Tab | Run or walk |
| Esc / Enter | Options / chat |

Change shortcuts under **Options → Hotkeys**. Running trades defence for speed. Bows, crossbows, and gargoyle throwing weapons use ranged attacks without ammunition management. The throwing weapons behave like bows and retain their race requirement. Restricted gear shows the required race in the tooltip. [Classes and talents](classes.md) · [Gargoyle weapons](../src/Sanctuary.Content/GargoyleBows.cs)

The starter belt holds one row. A dose removed by its hotkey frees that position. Multi-row belts hold more doses per column, and manual potion kegs provide storage between fights. The loot window saves its position between logins.

## Travel and recovery

Double-click a teleporter or moongate to use it. After the shared audience with Lord British, visit Iolo at Britain’s bard college for a runebook with the college rune. Use **Travel** on an unlocked rune to go there with eligible allies, without charges, mana, or reagents. Some destinations require dismounting. Elves can use the Yew route to Heartwood without requesting the mayor’s permission. Arena gates and island voyages follow [campaign progression](campaign.md#progression-and-arena-access).

Normal defeats use corpse recovery, equipment insurance, and carried-gold death rules. Bank-held gold is protected. Unclaimed corpses can reach banker escrow under the recovery rules. The graveyard crypts and solo story boss encounters have their own defeat destinations. [Equipment, insurance, and recovery](items-and-crafting.md#rewards-insurance-and-recovery)

## Companions

One active story companion fights alongside the Avatar and matches the active class level. Companions have their own equipment and healing supplies. The small companion vitals window shows their portrait, health, mana, and stamina and can dock to the player vitals. Retreat far enough and the companion leaves combat to catch up.

| Companion | Role and where to look |
| --- | --- |
| Iolo | New Haven’s inn, ranged crossbow support. Retires to Britain’s bard college after his introductory story. |
| Wolfgang | Geoffrey’s young recruit in Britain, sword-and-shield tank. Starts with medium armour. |
| Merydwen | A bow-using freebooter who hires out for 5,000 gold. [Recruitment](../src/Sanctuary.Content/Merydwen.cs) |
| Nightwind | A scout connected to Yew and Heartwood. |

Ayila at the East Britain inn manages the recruited roster. Lodging the active companion is free. Calling a companion back costs 500 gold, paid from carried gold and then the bank. Gear, supplies, and story state travel with the companion. Town care can heal active companions and raise fallen ones. Check the healer’s service for the fee or eligible free recovery. [Companion care](../src/Sanctuary.Content/MercenaryCare.cs) New Haven becomes a meeting point during the [ghost-town chapter](campaign.md#ghost-towns).

Jaana speaks more openly to a Druid. Relevant conversations can mention Lord British’s quest, Iolo’s reports, and Krulg’s defeat when the Avatar has learned about them. Jaana warns Druid Avatars about Covetous and can give them a two-row magic belt after they help her and unlock the boat rides tied to the orc chief. [Journal and dialogue protocol](../shared/PortraitProtocol.cs)
