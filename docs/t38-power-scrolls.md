# T38 — Boss Power Scrolls and profession caps

Power Scrolls permanently raise one named trade skill's cap on the character. They cover all thirteen retained skills: the ten crafting professions plus Mining, Lumberjacking and Fishing. A scroll can be traded before use, including to a character whose named craft is inactive or whose first craft is still unconfirmed.

The mandatory ladder is **100 → 105 → 110 → 115 → 120**. A scroll increases only the cap, never the trained skill, another skill's cap or a skill lock. Existing earned and staff-set caps are retained. Duplicate, lower and out-of-order scrolls remain unconsumed. The character still needs normal training and any learned rare recipe to perform higher-level work.

## Boss supply

| Source | Separate chance for one scroll |
|---|---:|
| Dungeon random unique | 1% |
| Named dungeon super-unique | 5% |
| Ordinary monsters, champions and minions | None |

The source is an authored T35 dungeon encounter, including T37's final unique boss. Native analogue monsters and unsupported Diablo act bosses do not become scroll sources. Difficulty does not alter these starting rates; Magic Find, Gold Find and player count do not affect them. This roll is additional to ordinary loot, moulds, chitin and maps, through the same saved once-only reward-life claim. Summons, controlled creatures, animated/raised creatures and other reward-disabled lives remain excluded. Pet kills use their existing credited owner.

| Actual boss level | Available scroll caps |
|---|---|
| Below 60 | None |
| 60–69 | 105 |
| 70–79 | 105, 110 |
| 80–89 | 105, 110, 115 |
| 90–99 | 105, 110, 115, 120 |

After a successful roll, each available cap has equal weight, then each of the thirteen skills has equal weight. Selection does not inspect the recipient's profession, trained skill, cap or prior scroll use, and does not reroll duplicates. Drop tickets, denominator and level thresholds live in `data/Sanctuary/Balance/power-scrolls.json`.

Drops are native Power Scroll items with their saved skill and cap, Drop origin and regular loot type at every tier. They stay on the corpse for ordinary manual looting. Existing permanent world populations remain unchanged; boss placement follows the T35 world-authoring plan.

## Using and preserving caps

Read a scroll in your own backpack to preview the named skill, current/new cap and unchanged trained value. **Raise cap** consumes one scroll only after rechecking ownership, connection, exact item skill/value/quantity, current cap, trained value, lock and readiness. Cancel spends nothing. Trading, banking or otherwise changing the quoted scroll invalidates the request. Concurrent confirmations cannot apply the same step twice. Movement-only, frozen, paralyzed or dead characters cannot apply it.

Inactive crafts can receive cap upgrades without changing the selected craft or confirming a first profession. Gatherers retain their normal always-active access. Native gain checks continue to enforce the cap and lock; the existing independent-skill rules prevent aggregate-cap losses. Changing craft/class, respeccing, death and logout do not erase cap progression.

The native `Skills` save already stores each character's individual caps. T38 uses that authority directly, with no duplicate cap ledger or save migration. Existing trained values, locks and elevated caps are not reset. Native Power Scroll double-click and direct-use paths route class characters through the guarded preview, including a native special-scroll confirmation that was opened separately. Non-class/staff native behavior is retained. No new era rule or client protocol is introduced.

## Verification

`PowerScrollChecks` tests exact success/miss tickets for every grade and difficulty, all level boundaries, every available cap/skill branch, all thirteen ordered cap ladders, duplicates/skipped steps, inactive/unconfirmed crafts, character ownership, trading, stale changes, unsupported native skills/values and preserved staff caps. A real native boss-death chain supplies all four Alchemy upgrades; ordinary native gains then reach each cap, and a learned Protection recipe crafts successfully at120. Saved checks preserve trained values, caps, locks, inactive selection, recipe knowledge, an unlooted scroll and its paid corpse across four boots.

`check_power_scroll_ui.py` uses the actual client to cancel and confirm a dropped scroll, retain duplicates/skipped steps, reject a native confirmation bypass and apply the next cap to an inactive craft. Full cumulative/legacy suites, native patch round trips, catalogue reproduction and copied-world comparisons form the deployment checks.

## Deployment checkpoint — 21 October 2026

Installed and live-save/restart verified. Final source focused/client/cumulative/legacy suites and installed cumulative/legacy/client suites pass. Catalogue reproduction, 41 Python tests and exact round trips for 38 ModernUO and 30 ClassicUO patches pass. Pre-install, installed and post-live copied saves preserve both characters, their trained values/caps/locks and all tracked gear/material/gold snapshots. Staging preserved all65 protected files;34 non-save payloads include12 client preference files.

The complete backup is `.local/backups/pre-t38-20261021-060605`; local evidence is under `.local/logs/t38` and `.local/logs/deploy-t38`. Both live boots saved successfully with a clean shutdown between; the second remains running. Engine, logger, application and client are unchanged; only native/content assemblies and balance data changed. Production contains only UOContent and Sanctuary.Content, with no integration fixture. Permanent populations are unchanged.
