# T10 — shared reward life and manual corpse gold

Confirmed 20 October 2026 (JST): implement all four proposed decisions. **Implemented, tested and deployed.** The live server has passed save, clean shutdown, restart and another save, and is left running.

Every creature death claims its existing saved `Corpse.SanctuaryNoRewards` flag before native-loot cleanup or reward consumers run. A repeated callback does nothing, including leaving previously generated gold intact. Revival carries the spent-life state through the existing raised/no-awards contract. The common entitlement contains the credited class player, resolved monster level and equipped Gold Find snapshot. XP, gold and future gear/mould/map/recipe consumers use that entitlement independently; an XP increase is never the eligibility gate.

Gold uses the entire existing 730-type reward roster:324 mapped common creature types are eligible and 406 explicit exclusions stay excluded. Vendors, controlled/summoned/animated victims, no-awards victims and raised graveyard creatures remain excluded. Player credit includes controlled/summoned killers' owners. A dead or distant credited player still earns corpse gold; XP retains its alive, same-map and 18-tile checks. Uncredited and non-class kills create no player rewards.

## Source rules and extraction

`tools/build_creature_gold.py` verifies pinned `MonStats` and `TreasureClassEx` SHA-256 hashes from `data/Sanctuary/Balance/sources.json`, then writes `creature-gold.json`. The immutable LoD 1.13 source commit is `45112569deb9384738ccafe5c24ebbb71f41c7c9`. Each eligible type reuses its reviewed D2 analogue's Normal `TreasureClass1`; a missing treasure class explicitly means no gold (nine eligible types have no class). No boss/elite multipliers or Nightmare/Hell activation are introduced.

All 104 reachable nonempty treasure-class rows have one positive pick and unmodified `gld` leaves. The extractor fails if that assumption changes, if a modifier appears, or if classes cycle. It retains NoDrop and every non-gold weight, including nested classes. Non-gold branches produce no supported reward and are never rerolled as gold. Since this complete Normal projection has at most one terminal outcome, it remains below D2's six-item limit without approximating a multiple-pick class.

For example, Skeleton's `Act 1 H2H A` has NoDrop 100, gold 21, equipment 16, junk 21 and good 2. Its gold chance is 21/160 (13.125%). A successful pile at resolved monster level L is uniformly L through 6L−1. Gold Find changes that pile to `floor(base × (100 + bonus) /100)`; it never changes drop chance. There is no one-tenth multiplier or connected-player scaling. These are the agreed players-1 starting prices/quantities, subject to later measured economy tuning.

The algorithm reference is D2MOO's reconstructed [Items.cpp](https://github.com/ThePhrozenKeep/D2MOO/blob/80169009eea696f83d949cc3a6f1bc9302f2fd53/source/D2Game/src/ITEMS/Items.cpp), specifically `D2GAME_InitItemStats_6FC4E520` and `D2GAME_DropTC_6FC51360`: uniform level-based base quantity, weighted NoDrop selection and additive owner Gold Find with integer truncation. The project implements the bounded projection independently; D2MOO is research evidence, not a native-fork code import.

Reproduce with `python3 tools/fetch_balance_sources.py --destination <folder>`, then `python3 tools/build_creature_gold.py --source-dir <folder> --check`. Committed generated data and source pins are the runtime/cold-start evidence; ignored research caches are optional.

## Equipment and corpse behavior

Working equipped numeric `gold%` affixes and saved gold-material weapon `gold-find` rolls add together. Carried, broken, cosmetic or suppressed-slot items contribute nothing. Pet kills use the owner's equipment at death. If death has moved the owner's equipment onto their own corpse, those items are no longer equipped and give no bonus. Existing items retain their saved rolls; nothing is rolled retroactively. Gold Find generation and its material tooltip become active; Magic Find remains gated for its later gear consumer.

Gold stays on the monster corpse. The existing loot grid, ownership/criminal checks, distance/LOS rules, weight and backpack-capacity checks govern collection. A failed transfer leaves the stack there. The manual looter receives it; no automatic collection or party split runs. Native corpse-deleting species retain their body when supported reward contents are present. Piles over 60000 are split into native-sized stacks without reducing the reward total.

## Acceptance

- Extracted-policy coverage, source hashes and exact gold/no-drop boundaries; amount bounds, truncation and unchanged chance with Gold Find.
- Player/pet/summoned-killer credit; excluded victims, missing/non-class credit; XP0 and level99 payouts; distant/dead owners and actual delayed poison deaths.
- Generated material plus numeric-affix equipment bonuses, break/repair, equip state and save/reload preservation.
- Native loot suppression, repeated callbacks, manual looting, full backpack, ordinary and native corpse-deleting deaths.
- Existing real revival/revival-chain tests plus saved paid corpses across repeated restarts.
- Reproducible native patch series, cumulative gear/crafting regressions and safe local deployment with saved-world preservation.

## Verified outcome and deployment

Native/content/integration builds pass with zero warnings/errors. All 14 Python checks and both source-catalogue reproduction checks pass. The current affix export measures 33 accepted numeric codes (29 present in complete spawnable affixes), 24 exact pool queries and 1,536 seeded rolls. All 20 ModernUO patches reproduce 52 paths exactly; all 22 ClassicUO patches reproduce 36 paths exactly, including reverse round trips.

Disposable four-boot suites pass the reward-life checks, actual revival/revival-chain gold exclusions, combat/XP and legacy migration checks, equipped gear/material effects, professions, tools, processing and equipment crafting. The actual installed client displays generated gold in the combined corpse grid and transfers it only on a click; harvesting and retained-corpse reload checks also pass. A real pet final blow while its owner is dead and a poison final blow after its owner leaves both retain gold. Full-backpack and duplicate-callback checks preserve the original stack.

The live server was saved and stopped before installation. A complete pre-install server/client backup is retained in `.local/backups/pre-t10-20261020-002205/`. All 56 recorded save/configuration/preference files were byte-identical after staging. Installed Server/UOContent/Sanctuary.Content hashes match the builds; the client binary is unchanged. The live assembly list contains only UOContent and Sanctuary.Content, with no integration fixture assembly. The profession flag remains enabled.

Pre-install, installed and post-live-restart copied-world inspections match both characters' profiles, allocations, native skills/caps/locks, worn identities, saved gear and material stacks. All 13 client preference files and server configuration files remain unchanged. Live boot/save logs and the rollback manifest are retained under `.local/logs/deploy-t10/`; test evidence includes `t10-final-reward-check.log`, `t10-xp-loot-check.log`, `t10-cumulative-check.log` and `t10-python-tests.log`. Rollback after new gameplay requires the matching runtime and save backup.

T10 completes this reward contract and the agreed Normal gold projection. Gear, mould, map and recipe drops remain assigned to their later roadmap cards and will consume the shared entitlement. Magic Find remains unavailable until its gear-reward consumer is implemented. T11 is the next card.
