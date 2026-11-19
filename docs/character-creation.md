# Character creation

Choose an appearance, answer “By what name wouldst thou be known?”, then select a first class. Back returns to the previous page and keeps the appearance and name. The official UO profession, skill allocation, and city selection pages are bypassed.

The appearance picker offers 56 finished portraits for each of the six human, elf, and gargoyle race and sex combinations. Browse the portraits in the gallery to choose a template. Each template determines the character's native skin hue, hairstyle or horns, hair color, and any facial hair or facial horns. Details such as scars and eyepatches can appear in the portrait. The avatar health gump and private dialogue show the chosen portrait.

New characters enter New Haven on Trammel with their selected class at level 1 and zero combat XP. Elin prompts them privately to meet Iolo at the Bountiful Harvest Inn. Closing the conversation leaves the player free to explore. Existing characters retain their progression, names, and starting locations.

The server grants the generic starter backpack, clothes, and items. There is no client-supplied starting skill allocation. Neutral class-creation stats go through the existing server class progression hooks. Account privileges and existing staff characters are preserved.

Each starting dagger grants one tier-one skill while equipped by its class. Amazon gets Inner Sight, Sorceress or Sorcerer gets Fire Bolt, Necromancer gets Teeth, Paladin gets Sacrifice, Barbarian gets Bash, Druid or Druidess gets Firestorm, Assassin gets Fire Blast, and Witch or Warlock gets Miasma Bolt. The dagger supplies one rank without spending a skill point.

## Code and persistence

- [Appearance templates](../shared/AppearanceTemplates.cs) maps stable portrait IDs to native appearances. The creation packet sends the template ID alongside the class, and the server checks that the requested appearance matches.
- [Character profiles](../src/Sanctuary.Content/CharacterProfiles.cs) stores the template ID and arrival greeting state with the server-owned character. Older characters continue to use their previous portrait data and native appearance.
- [Client patch](../patches/ClassicUO/0123-sanctuary-avatar-templates.patch) · [Portrait atlas catalogue](../art/player-ui/avatar-templates/README.md)
