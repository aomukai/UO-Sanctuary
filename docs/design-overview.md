# Sanctuary design overview

Sanctuary brings Diablo II-style classes, combat, and equipment into a persistent Britannia. The aim is for a dungeon outing and an evening spent crafting, fishing, furnishing a house, or talking to neighbours to feel at home in the same game. Play locally or with a small PvE community using ModernUO and ClassicUO. The server owns character progression and game outcomes; the client handles presentation, controls, and optional dialogue inference. [Running locally](../README.md#running-locally) · [Building Britannia](worldbuilding.md)

## Existing systems and implementation evidence

- **Classes and presentation.** Use the [class skills completion record](class-skills-complete.md), [supported gameplay contexts](class-mechanics-supported-contexts.md), and [skill effects and sound](skill-effects.md) for their respective implementation status. The [class skill roadmap](class-skill-roadmap.md) records the class work and prerequisites.
- **Combat, progression, and gear.** [Balance foundations](balance-foundations.md), [combat timing](combat-timing.md), [gear tiers](gear-tiers.md), and [difficulty variants](t19-difficulties.md) describe the relevant rules. Class progression supplies combat competence; professions progress through their own activities.
- **Crafting and economy.** [Crafting decisions](crafting.md), [equipment economy](economy.md), and the numbered [foundation roadmap](implementation-roadmap.md) lead to the implementation cards. [Profession coverage](t47-profession-coverage.md), [runes and gems](runes-and-gems.md), and [named gear readiness](named-item-readiness.md) cover exact remaining boundaries.

## World and player experience designs

- [The invasion campaign](invasion-campaign.md) and [campaign boss encounters](boss-encounters.md) cover the three realities, optional investigation, and one-clear story fights.
- [The Avatar’s journal and virtues](journal-and-virtues.md), [companions](companions.md), [NPC dialogue](npc-dialogue.md), and [social encounters](social-encounters.md) cover personal discoveries and relationships.
- [Building Britannia](worldbuilding.md) and [travel, shrines, and recovery](travel-and-recovery.md) collect world authoring and travel proposals.
- [Game UI and localisation](game-ui.md) and the [launcher](launcher.md) cover how players reach and use those systems.

## Delivery and playtesting

Use the numbered roadmap for foundation and economy cards. Use the design docs above to shape later playable slices. For example, one authored note can create a journal entry and change what a named NPC says when told about it. Check scripted dialogue, discovery order, and saves on that small loop before expanding content. Another slice can follow one companion through being recruited, witnessing an event, returning to the inn, and hearing what happened while away.

Final T48 economy and combat outings depend on the intended encounter and reward progression, full companion play, and supported class and equipment effects. Sample income, resale, and expenses over representative outings, including on new characters. Measure Gold Find alongside the kill speed it trades against. Verify real client responsiveness, saved state and rewards, and small-shard performance before making playability or capacity claims. [Foundation roadmap](implementation-roadmap.md)
