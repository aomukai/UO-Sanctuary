# Sanctuary handbook

| I want to… | Start here |
| --- | --- |
| Play, travel, recruit a companion, or recover after death | [Player guide](player-guide.md) |
| Find a talent, class mechanic, or summon rule | [Classes and talents](classes.md) |
| Find a spawner, place a camp, or author a dungeon | [Worldbuilding and GM catalog](worldbuilding.md) |
| Follow or build the Britannia campaign | [Campaign](campaign.md) |
| Find a recipe system, loot rule, or profession | [Items and crafting](items-and-crafting.md) |
| Build, test, or update the game | [Local development](local-development.md) |
| Check what remains and what has shipped | [Roadmap](roadmap.md) |

The handbook describes current source behavior. The roadmap tracks unfinished work and decisions still open. Links to code and data identify where exact values, placement lists, and tables are maintained. The installed game and a saved world may reflect an earlier build or contain manual GM placements.

## Historical records

The pre-consolidation tree retains the old design discussions, checkpoint notes, milestone cards, and recordings index at commit `528dd686e310f2885504ad2e6ca4ab47ba254ef5`. For example, from the repository root:

```sh
git ls-tree --name-only 528dd686e310f2885504ad2e6ca4ab47ba254ef5:docs
git show 528dd686e310f2885504ad2e6ca4ab47ba254ef5:docs/companions.md
```

Some frequently referenced records from that tree:

| Topic | Former paths under `docs/` |
| --- | --- |
| Character formulas and combat | `character-stats.md`, `combat-timing.md`, `melee-reach.md`, `hit-recovery.md`, `balance-foundations.md` |
| Class mechanics and tested boundaries | `skill-mechanical-reference.md`, `class-mechanics-supported-contexts.md`, and the eight `*-coverage-matrix.md` files |
| Crafting decisions and task-by-task delivery | `economy-open-questions-replies.md`, `crafting.md`, `implementation-roadmap.md`, and the numbered `t*.md` cards |
| Story and authored dialogue | `invasion-campaign.md`, `companions.md`, `journal-and-virtues.md`, `npc-dialogue.md` |
| Early boss proposals | `boss-encounters.md` |
| Maps and creature design | `new-haven.md`, `worldbuilding.md`, `early-normal-monsters.md`, `monster-spell-conversion.md` |

Current machine-generated reports and test inputs live under [tools/reference](../tools/reference/README.md). Historical recording recipes can refer to paths as they existed when recorded, including retired run artifacts. The archive is useful for looking up a decision or an evidence record. Use the handbook and active source for current rules.
