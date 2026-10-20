# Completed-replies review — 19 October 2026

Read all 735 lines of [the completed replies](economy-open-questions-replies.md). Their contents are preserved unchanged, including the explicitly provisional interpretations/corrections at the top. The original reply-file SHA-256 is `f2ddcefa818c73adb5c135c92b18865e28c4185f9e209513b58c19e60f0c3a94`.

The [current specification](crafting.md) consolidates the answers. The [decision tracker](economy-open-questions.md) reconciles 117 rows while retaining A03 and later Q answers, including always-active gathering, Cartography as a craft, public wardrobe access and sex-specific class names. Older handoffs are explicitly historical. Current implementation descriptions remain separate; no target is claimed already installed.

## Important changes now recorded

- All professions remain in scope; the later user request replaces all-at-once delivery with the [dependency-ordered implementation roadmap](implementation-roadmap.md). Native-style ordinary crafting/gathering, paid training to30, no-output practice to70, ordinary successful quality89%/10%/1% retain the reply's rules.
- Physical material decides maker/repairer; bone is heavy Tailoring armour. Crafted item levels35/70/99 and base equip levels0/60/80. Quality sockets and durability/crafter-name perks start at 100.
- Repair costs and max-durability loss/saving tables now have starting values. Exact material subtype is required; zero-durability equipment loses its effects but is not deleted. Chitin becomes Powder of Fortifying through Alchemy.
- Tier/skill-based Imbuing costs, jewellery progression and the improved Rare chance at 120 replace the skill-free fixed prototype target.
- Five sockets on eligible slots; irreversible tinker-added sockets prohibit later Imbuing. Socket attempts cost tool charges only. Additive tool bonuses and a skill 120 intuition retry are specified.
- Moulds determine rune property before crafting. Rune skill30–100, revised category-minimum-plus-tier formula, quality gates and grade-dependent mould chances replace earlier random-stat/universal 1–8% assumptions.
- Bark unlocks45–80, gem qualities40–80;5% find chance per successful harvest using trained Base skill. New prices and decorative-only legacy gems are recorded.
- New tome charge ladder and quality-added charges replace old20-charge/charge-saving tomes. Full-tome crafting can produce a Teleport scroll.
- Alchemy's initial catalogue,30s buffs extendable to90s, food catalogue/one-hour effects,30-minute map buffs, keys and treasure recipe rewards are specified.
- Manual corpse gold, D2-derived drop/gamble direction, new persistent tier-based resale/no buyback, flat20% carried-gold death loss,24-hour corpse/escrow, insurance prices, mercenary resurrection and both housing acquisition types are recorded.

## Clarifications resolved after this review

The user explicitly answered these on 19 October 2026, with the T20 socket clarification added on 20 October. They supersede the ambiguous wording in the otherwise unchanged replies file:

| IDs | Confirmed rule |
| --- | --- |
| G04/E04 | Minimum values are only for calculating rune baselines. Actual dropped/imbued gear retains normal random affix-value rolls. |
| K05 | The player manually drops excess weight. Food expiry causes normal encumbrance, not automatic inventory spilling. |
| B11/E01/F04 | Eligible white weapons, shields, rings and amulets are included in Imbuing. Swords are weapons and are also socketable, subject to the normal origin/cap/provenance rules. |
| A01 | Implement in small, dependency-ordered playable slices; retain all professions in the eventual scope. |
| A03/Q08 | Wardrobe opens with current class/craft checked; exactly one of each enables Swap. New characters default to Alchemy, with first crafting-tool use confirming the actual first-profession lock until class80 OR craft100 and wardrobe access. Gathering tools are exempt. |
| F02/F05 (20 October) | Resolved20 October in [T20](t20-sockets.md): crafted Average/High Quality/Superior tools give+1/+2/+3 percentage points; shop/legacy+0. Intuition stops before acting with no charge/item change, then offers safe abort or one paid retry with normal charge saving and no recursive intuition. |
| G03/G08 (20 October) | T21 uses one independent0.1% roll for one mould alongside ordinary loot, without MF scaling. User chose Diablo affix frequencies: sum distinct applicable equipment affix rows once per semantic percentage family, with aliases collapsed. Catalogue and baselines: [T21](t21-moulds.md). |

## Other remaining design decisions

These are the residual fields in existing IDs, not a new request to re-answer the completed questionnaire. They can be answered in batches or deferred where the feature does not yet need them.

| Area / IDs | Still missing |
| --- | --- |
| Practice/processing — A04, C01 | T05 settles crafting-only practice, the linear100%→1% curve, configurable1gold/0.1 training and unrestricted basic-processing access with active-craft gains. Consistent conversion weights and remaining station/portable details still need a rule. |
| Materials/jewellery — B04, E01/E02 | Material bonus roll/scaling and random-element pool; jewellery item levels/equip/cost mapping. |
| Repairs — C03, D03/D04/D08/D10/D13 | T12 confirms full repair/floor1, scraps1/3/5, reserved Unique3×, all-resource saving, charged repair tools/workshops and NPC handling; see [T12](t12-repairs.md). Chitin rates/recipe and maximum-restoration amount/limits remain T36. |
| Gems/gathering — H02–H08, L08 | Per-find quantity/full-pack behavior; nine-family mapping and second flat effect; gem upgrade recipe. Is treasure-chest “random gem” an intentional usable-gem exception to mining-only supply or a decorative legacy reward? |
| Potions/food — J01/J02, K01 | Magical Penetration's component formula; Protection mitigation ordering; coexistence of different potion buffs; minimum gathering duration and aggro-delay application/reset conditions. |
| Tomes/maps/keys — I02, L04/L05/L07 | Vendor tome charges/prices; whether5% vendor benefit affects purchases/sales/both; whole-dungeon versus floor boundaries; skeleton-key charges. |
| Shops/drop adaptation — M05/M06, N02 | What “level41/70” stock caps measure; Vendor-origin resale cases; unsupported Set/Unique outcomes when adapting D2 generation. |
| Death/companions — O02/O03/O05 | Escrow fee/timing/capacity and deletion decision point; insurance duration/payment/renewal/retention; mercenary drink thresholds and reserve-capacity meaning. |
| Wardrobe/titles — Q02–Q13 | Unfilled class pool/status/timer, summon, shared state, storage, ownership/first-class setup, Power Scroll, workshop-exception and title choices remain as previously listed. Copied blank Q rows are not answers. |

## Delegated authoring and reference work

These tasks do not require the user to supply every value manually:

- Build the full recipe books within the given bands, including useful training outputs, economical food names/ingredients, exact tool recipes and ordinary vendor supply lists.
- Extract selected D2 gold/drop/potion/gamble data and map to UO bases and monster counterparts; implement only effects whose behavior actually exists.
- Map modern five named treasure tiers to the chosen server behavior and custom dungeon/key system. This checkout's native treasure map thresholds use six levels and differ from modern reference pages.
- Verify native Fishing/bottle/sea encounter behavior against the requested reference baseline, distinguishing SOS rewards from custom land treasure entrances. Do not import unrelated removed skills.
- Verify vendor OPL delivery. The existing ClassicUO shop already has a serial-based tooltip hook, so a new shop UI is not presumed necessary.
- Choose concrete adapted dungeon sections/spawns and housing availability/prices later, as requested.

The supplied Fandom pages were unavailable to direct browsing; UOGuide direct opens were blocked, with partial search-index access. Primary alternatives were checked: [Blizzard gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml), [suffixes](https://classic.battle.net/diablo2exp/items/magic/suf.shtml), [gems](https://classic.battle.net/diablo2exp/items/gems.shtml), [potions](https://classic.battle.net/diablo2exp/items/potions.shtml), [official UO treasure maps](https://uo.com/wiki/ultima-online-wiki/gameplay/treasure-maps/) and [sunken treasure](https://uo.com/wiki/ultima-online-wiki/skills/fishing/sunken-treasure/). Native source and pinned local D2 tables supply further implementation evidence; “use D2/UO” is a baseline selection, not proof that every custom mapping is finished.

## Tuning observations, not changes to decisions

- The new NPC resale ranges are substantially larger than the installed rarity caps. Preserve the supplied starting ranges, but measure resale separately from kill gold in the 30-minute outing budget; do not silently reapply the old 10% multiplier.
- Vendor cloth can become crafted items sold to NPCs. Cloth/tool costs and the new sale ranges must be assessed together when checking currency creation. This does not revoke profitable crafting or the supplied resale values.
- A tier3 meal subtracts 3 seconds from gathering, while native Mining/Lumberjacking effect delays include 1.6-second values. Define the action-time floor before implementing that bonus.
- Fixed stack weights and1→10 conversions cannot deliver exactly half input weight for every raw/processed input form without an explicit normalization or stack-weight policy. Current ingots/cloth weigh 0.1, leather 1.0, and project scrap stacks 0.1 per unit.

No code, live state, balance catalogues, binaries, accounts, saves or client preferences changed in this review. No commit or push was made. Documentation snapshots are under `.local/research/economy-reconciliation-20261019/`.

T06 follow-up: the user confirmed the six shared crafting choices in [T06](t06-crafting.md): public/free/concurrent town stations and practice, explicit portable exceptions,10gold/20–30-use vendor tools,100/110/120-use crafted tools with10% saving on committed attempts, preserved existing uses and the ten-tool recipe cohort. Prices/capacities remain tunable. The tracker/specification record these decisions; the implementation remains disabled and undeployed. Original replies remain unchanged.
