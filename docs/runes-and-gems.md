# Runes, Runebark and socket gems

Reconciled 20 October 2026 (JST) from [the completed replies](economy-open-questions-replies.md), G01–H09, and [the current specification](crafting.md). These are first-implementation rules; [T20](t20-sockets.md) installs empty-socket creation and [T21](t21-moulds.md) installs collectible known-stat moulds. [T22](t22-runebark.md) adds Runebark harvesting and purchase-only resale. [T23](t23-runes.md) installs declared-stat rune crafting. [T24](t24-gems.md) installs mining gems/upgrades and [T25](t25-rune-sockets.md) installs permanent rune bonuses and requirements. [T26](t26-gem-sockets.md) activates permanent gem contents and slot effects. Later replies supersede the old 50–120 bark / 50–90 gem thresholds, stat rolling at craft time and universal 1–8% rune assumption.

## Runes and moulds — G01–G09

**The mould now determines the stat.** Crafting no longer chooses a random property: a Crude Mould of the Apprentice or Flawless Mould of Chance produces its corresponding percentage property. The [T21 catalogue](t21-moulds.md) defines15 working percentage families with equipment-derived minima and source affix-frequency weights. Flat/binary properties are excluded; no runewords.

| Tier | Mould monster levels | Inscription | New minimum Lumberjacking | Bark consumed | Working tier name |
| --- | --- | ---: | ---: | ---: | --- |
| 1 | 20–29 | 30 | 45 | 1 | Crude |
| 2 | 30–39 | 40 | 50 | 2 | Rough |
| 3 | 40–49 | 50 | 55 | 3 | Shaped |
| 4 | 50–59 | 60 | 60 | 4 | Etched |
| 5 | 60–69 | 70 | 65 | 5 | Refined |
| 6 | 70–79 | 80 | 70 | 6 | Polished |
| 7 | 80–89 | 90 | 75 | 7 | Flawless |
| 8 | 90–99 | 100 | 80 | 8 | Perfect |

**Implemented T23:** [the rune crafting flow](t23-runes.md) uses the active Inscription craft, scribe’s pen and public desk. Confirmed failure costs are one mould, half the bark rounded up and one pen use; crafted-tool saving applies. Native success/gain checks use the recipe requirement through50 points above it. Cancel/stale/capacity rejection spends nothing. Consume one mould, the listed bark quantity and one pen charge. Higher bark substitutes downward but does not change the mould tier. High Quality becomes eligible at recipe skill +10; Superior at +20. Before +10: Average only. Between +10 and +20: Average 90%, High 10%. At +20: Average 89%, High 10%, Superior 1%. Thus Superior T8 needs Inscription 120.

**Provisional formula specified in the replies:** `percentage = selected stat-category minimum + (tier − 1) + quality bonus`, with Average +0 / High +1 / Superior +2 percentage points. A 10% FRW baseline gives T1 Average 10%, T8 Average 17%, T8 Superior 19%. This replaces the old universal 1–8% assumption. The category minima and canonical lowest-tier suffix names come from selected D2 affix data, e.g. Chance/Greed/Balance. **Explicit clarification, 19 October: minimum values apply only to rune baseline calculation. Dropped and imbued gear retain their normal random affix-value rolls within eligible ranges.**

| Monster grade | Mould drop chance |
| --- | ---: |
| Ordinary | 0.1% |
| Champion | 1% |
| Unique random boss with minions | 5% |
| Named dungeon super-unique | 10% |

Previously spent lives cannot reward again; owner credit for pet kills remains. Confirmed20 October for T21: one mould per successful independent0.1% roll, alongside ordinary loot and unaffected by MF; stat weights sum each applicable Diablo equipment affix row’s frequency once, with aliases grouped semantically. See the [catalogue](t21-moulds.md). The common level-85 roster ceiling must not cap future dungeon content: level 3+ dungeons progress from 60–70 near the entrance through 70–80 and 80–90 to 90–99 near the boss, providing T8 sources.

**Implemented [T25](t25-rune-sockets.md):** double-click a rune, target an identified owned backpack host and confirm permanent installation. Cost is the rune alone; no tool, station, skill or gold. All15 supported rune stats add once through existing consumers, including damage/defense from any permitted slot. Filled sockets raise equipment's minimum character level: 1/2/3/4/5 filled require 30/40/50/60/70. Empty sockets add no requirement. Rune/gem identity itself adds none under this design; existing base/affix/attribute requirements still apply.

## Gathering byproducts and gems — H01–H09

Use **trained Base skill**. A successful wood/ore harvest makes one 5% byproduct roll for bark/gems respectively, not 5% per tier. On a find, select a random unlocked tier and announce the find. **Confirmed T22:** a successful wood extraction awards one piece on its single5% roll, selected uniformly among unlocked bark tiers. A find goes into the backpack or, if full, at the character’s feet with an explicit message. Primary wood delivery failure does not suppress bark after successful extraction. **Authored T24 defaults:** one gem per find, uniform family and unlocked-quality selection, backpack or feet with an explicit message. Only successful ore extraction qualifies, excluding sand/granite. These proposed tuning defaults were adopted after the optional questions remained unanswered; see [T24](t24-gems.md).

New Mining quality unlocks are **40/50/60/70/80** for Chipped/Flawed/Standard/Flawless/Perfect. Bark unlocks are **45/50/55/60/65/70/75/80**, as above. These replace the old 50–90 gem and 50–120 bark thresholds.

Retain all nine UO families. Use original D2 gem effects, including equipment-category differences, for the mapped families; the two additions use flat-number effects regardless of slot. **Confirmed T24 mapping:** Amethyst/Diamond/Emerald/Ruby/Sapphire retain their counterparts, Citrine uses Topaz and Tourmaline uses Skull. Amber gives flat Attacker Takes Damage4/8/12/16/20; Star Sapphire gives the user-selected Magic Damage Reduced1/2/3/4/5. The [catalogue and source](t24-gems.md) record all five-quality slot-specific values. Authored upgrade defaults:3matching gems →1next quality, active Tinkering/public bench, trained40/50/60/70, guaranteed success, one tool use with saving and no gold. Bark upgrading remains disallowed. [T26](t26-gem-sockets.md) permanently installs these frozen effects and activates their real slot consumers. Gems and runes share capacity, level floors and guarded confirmation, consuming one socketable with no other fee.

| Gem quality | NPC purchase price |
| --- | ---: |
| Chipped | 25 |
| Flawed | 75 |
| Standard | 250 |
| Flawless | 500 |
| Perfect | 1,500 |

**Replacement provisional bark prices:** T1–T8 pay **10/20/30/40/50/60/70/80 gold** per unit. Do not restore the discarded exponential rule. Vendors buy but never resell gems/bark. Legacy native gems become decoration only. Gathering-only gem supply conflicts with the new treasure-chest random-gem reward in L08; identify whether that is a usable-gem exception or decorative legacy reward before implementing it.

## Host sockets and transformation

Helmet/chest/weapon/shield cap at5 sockets, independent of rarity. Swords are weapons and follow the same socket rules. Any permitted socket accepts a rune/gem without host-slot property restrictions; totals add to existing bonuses and retain their normal effect formulas/breakpoints. No removal/replacement. Salvage destroys contents without extra yield.

Native crafted quality sockets (0/1/2 at skill 100+) may survive Imbuing. Any tinkerer-added socket makes the item ineligible for later Imbuing, with a warning before socketing white gear. Socket provenance must persist. The complete skill/success/tool-cost table is in [crafting.md](crafting.md).

## Confirmed implementation baseline

T24 supplies a separate persistent45-identity socket-gem model; native gems remain decoration-only with their saved identities intact and cannot fund recipes or upgrades. Native bonus harvesting/ML gems do not become usable socket gems. T21 supplies known-stat moulds, T22 Runebark and T23 declared-stat runes. T25 installs and applies permanent rune bonuses with version4 gear persistence and empty-content migration for older gear. [T26](t26-gem-sockets.md) installs and applies gem contents through the same capacity and level rules; version5 retains prior rune data. Reference affixes are broader than currently implemented gear effects; never activate an inert advertised stat.

The monster reward lifecycle already suppresses repeated rewards from revived lives. New mould/treasure rewards must share that entitlement while preserving pet-owner credit, and must not depend on positive XP after level penalties. Future 90–99dungeon spawns require extending encounter/profile generation rather than silently altering the existing XP curve.

The original documentation reconciliation changed no gameplay. T20–T26 implementation and deployment are recorded in their linked cards.
