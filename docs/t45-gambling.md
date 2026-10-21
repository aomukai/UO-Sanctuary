# T45 — Tavern gambling

Staged T49 update (22 October2026): actual eligible named items now share this path. The [complete return list](named-item-readiness.md) records every held definition and its prerequisites; unavailable outcomes retain their original weight. These changes are isolated staging; production remains T45.

Implemented, tested and deployed21 October2026. The production server is running this build; the client is closed. T46 care preparation and T47 progression coverage are now complete in staging. **Classes and full skills are next**; see the [22 October checkpoint](checkpoint-2026-10-22.md).

## Decisions and source baseline

N01–N03 already specify a gambler in every tavern, vendor-style access with hidden stock, all intended equipment families, initially D2-derived prices and level/quality/base selection, and normal loot improvement eligibility. The user confirmed21 October that unavailable Set/Unique gambling outcomes **refund the stake without an item or replacement roll**. The shared T49 Set/Unique integrations are now staged across acquisition and item-handling paths. Held skill-dependent items return with their class prerequisites, and final T49 follows complete classes and mercenaries.

The [official Blizzard gambling reference](https://classic.battle.net/diablo2exp/basics/gambling.shtml) and the pinned D2MOO gamble-inventory routine supply the expansion baseline:14 offers with guaranteed ring/amulet, character level−5 through+4 item levels clamped5–99, normal appearances before purchase, immediate identification on purchase, refresh by reopening, and fixed quality chances (Magic89.85%, Rare10%, Set0.10%, Unique0.05%). **Magic Find does not affect gambling**; the T19 monster-drop quality/MF calculation remains separate. Exceptional/Elite checks use the documented level-relative probabilities; source catalogue quality levels and equip restrictions remain authoritative.

Prices use pinned Weapons/Armor/Misc tables from commit45112569deb9384738ccafe5c24ebbb71f41c7c9 and the expansion transaction-cost calculation in the locally pinned D2MOO0c0bbd1e6bef9181a3da1c046523af622a925416, `D2Common/src/Items/Items.cpp`, `ITEMS_CalculateTransactionCost`. Jewellery stays50,000/63,000gold. Other prices vary by character level, normal/Exceptional/Elite base costs and source stack size, with integer truncation and a minimum price level5. The decompilation itself flags possible rounding differences: this is a **D2-derived initial baseline**, not a claim of bit-for-bit game parity. No discounts or economy tuning are introduced. T48 must evaluate these sinks against actual earnings.

## Implementation scope

- `GamblingCatalog` validates source costs, ordinary usable equipment and the five project potion-belt appearances:82 normal base families and220 appearances. Native named/special artifacts and unrelated consumables are excluded. All intended mapped equipment families are represented; higher-tier equipment retains the project's equip floors60/80.
- Two supplemental full-plate base definitions complete Exceptional/Elite identity for the existing normal Dragon/Stone armour appearances. This adds no recipes and leaves the ten deferred exotic crafting recipes deferred. Existing monster-drop exclusions are unchanged.
- The authored tavern roster uses all native TavernKeeper spawn anchors across facets/expansion directories, merging nearby keepers in the same tavern. The36 resulting services have persistent native NPC identities, idempotent installation and ordinary Buy speech/context-menu access.
- Each menu owns14 private descriptions/art/price quotes. No reward item, item serial, affix roll or upgrade result exists before a purchase attempt. The character's level and connection/session are checked again on response. Bought/refunded offers are consumed; reopening/refreshing creates fresh descriptions. A stale, replayed, disconnected, distant, dead or level-changed menu cannot purchase.
- Payment follows existing vendor purse rules: use backpack gold when sufficient; otherwise purchases of at least2,000gold may use the entire price from the bank. Funds are checked before rolling. A supported real item and the complete pack delivery are prepared before charging; no partial purchase or floor drop is permitted. Deferred Set/Unique outcomes leave balances unchanged and consume the selected offer without another roll.
- Purchased items have `Drop` origin, are immediately identified and retain ordinary loot resale/improvement/salvage eligibility. No Vendor origin or vendor-refund purchase record is attached. Real gear and its rolls survive native item/project ledger persistence; menus are transient.

## Verification

Project content and integration builds passed with zero warnings/errors. All41 Python tests passed. `tools/build_gambling_catalogue.py --source-dir .local/research/foundation-pinned --check`, first-loop export/check, root diff checks and both complete native patch forward/reverse reproductions passed. Native/client source pins and binaries did not change.

Source and installed cumulative/legacy cohorts passed across four boots each. Focused gambling coverage exercised220 appearances at all three tier outcomes, unlock/item-level and quality boundaries, bank and purse payment, whole-pack capacity, refunded deferred outcomes without extra rolls, actual equip/native resale/salvage and saved gear/roll/origin/gambler identities. Actual ClassicUO speech/mouse checks covered both pages, hidden stock, closing, insufficient funds/full pack, purchase/reveal, refund, refresh, stale/replayed/distant callbacks and restart. These are automated client observations; no new human playtest feedback is claimed.

Source, installed and post-live copies of the actual world each passed two-boot comparison. Both characters' complete profiles, histories, stat allocations, native skills/caps/locks, bindings/titles, worn identities, existing gear/materials and all507 existing gold stacks were preserved. The36 new NPC-owned gold stacks are isolated from the existing-gold comparison. All71 protected payloads and all client files were preserved during staging;34 non-save payloads, including12 client preference files, remain identical after deployment.

Local evidence: `.local/logs/t45/{focused,cumulative,legacy,gambling-ui,audit}.log`, `.local/logs/t45/pre-copy`, `.local/logs/deploy-t45/{cumulative,legacy,gambling-ui}.log`, installed/post-live copied-world reports, screenshots `.local/logs/t45-gambling-*.png`, and the deployment manifest. Production contains only the native and project content assemblies; no fixtures or test accounts were installed.

## Deployment, migration and rollback

The new `SanctuaryGambling` version0 ledger (priority136) indexes the36 authored service NPC identities. `TavernGambler` uses generated version0 serialization. Existing item/gear/character schemas are unchanged. Missing legacy service state is populated once at startup; subsequent boots retain the same NPCs. Stock descriptions and menu callbacks are transient.

T44 saved22:13:20 and stopped22:13:24. Full rollback backup: `.local/backups/pre-t45-20261021-221335`. Only project DLL/PDB and project data were installed. Native content, server/application/logger binaries, configuration, accounts, saves and client settings/profiles were preserved during staging. T45 boot1 saved22:20:11 and shut down22:20:16; boot2 loaded4698items/572mobiles and saved5304entities22:21:20. Final protected-state and semantic comparisons passed. For rollback, stop cleanly and restore the matching pre-T45 runtime **and save**, rather than attempting to load new NPC types with an older content assembly.

Known bounds: prices are the initial source-derived baseline pending T48 outing budgets; actual Set/Unique items and bonuses remain deferred to the required T49 pass, with refunds in gambling until then. The ten deferred exotic crafting recipes remain deferred. T46 is not started.
