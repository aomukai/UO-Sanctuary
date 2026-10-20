# T30 — The travelling cook's recipe book

Use a skillet at a free public Cooking station, then choose **Cook prepared meals**. The book has three tier tabs. Every recipe produces one meal and spends one skillet use, with crafted-tool saving. Each ingredient below costs **one unit**. A flour portion is one of the 20 portions in a native flour sack. Wooden Chips are fuel, at 5/10/30 per tier. Recipes retain the ordinary required-skill→required+50 success/gain curve, caps and locks; quality does not change potency. Failure consumes half of each ingredient and the fuel, rounded up, plus one skillet use with saving.

Meals combine familiar fish, carved meat and inexpensive produce with a few stranger ingredients. Bat wing bits feature in Twilight Batwing Bites and the stealth meals; daemon blood enriches Red Harvest Ragout and Ghostwalk Supper. Fish must be caught and cut into native raw fish steaks; meat comes from eligible carved animals/monsters. Every other ingredient has the existing town supply route. Alchemists additionally sell bat wings for 10 gold and daemon blood for 15 gold. Carving a wild mongbat/greater mongbat/strong mongbat/vampire bat yields one bat wing; a wild daemon/balron yields one daemon blood. These unusual yields occur once and do not receive the food's meat/skin/feather bonus.

## Tier 1 — 5 Wooden Chips

| Meal | Cooking | Effect | Ingredients, one each |
| --- | ---: | --- | --- |
| Lamplighter's Broth | 0 | Light radius +1 | carrot, onion |
| Porter's Flatbread | 8 | Carry stones +25 | flour portion, apple |
| Forager's Skewers | 16 | 10% extra primary resources +1 | raw ribs, onion |
| Quickhand Fish | 24 | Gathering seconds −1 | raw fish steak, onion |
| Hunter's Ribs | 32 | Meat/skins/feathers +1 | raw ribs, cooking spices |
| Twilight Batwing Bites | 40 | First aggression seconds +1 | bat wing bits, apple, cooking spices |

## Tier 2 — 10 Wooden Chips

| Meal | Cooking | Effect | Ingredients, one each |
| --- | ---: | --- | --- |
| Watchman's Fish Stew | 50 | Light radius +2 | raw fish steak, carrot, onion, cooking spices |
| Trailwarden's Pie | 58 | Carry stones +50 | raw ribs, flour portion, onion, cooking spices |
| Prospector's Fishcakes | 66 | 10% extra primary resources +2 | raw fish steak, flour portion, onion, cooking spices |
| Rushlight Skewers | 74 | Gathering seconds −2 | raw bird, apple, cooking spices |
| Trapper's Pie | 82 | Meat/skins/feathers +2 | raw bird, flour portion, carrot, cooking spices |
| Stalker's Batwing Stew | 90 | First aggression seconds +2 | bat wing bits, raw ribs, onion, cooking spices |

## Tier 3 — 30 Wooden Chips

| Meal | Cooking | Effect | Ingredients, one each |
| --- | ---: | --- | --- |
| Dawnwatch Chowder | 100 | Light radius +3 | raw fish steak, carrot, flour portion, cooking spices, bat wing bits |
| Giant's Hearthpot | 103 | Carry stones +74 | raw ribs, cabbage, carrot, flour portion, cooking spices |
| Red Harvest Ragout | 106 | 10% extra primary resources +3 | raw ribs, carrot, onion, cooking spices, daemon blood |
| Fleetfinger Feast | 109 | Gathering seconds −3 | raw fish steak, raw bird, carrot, cooking spices, ginseng |
| Master Hunter's Roast | 112 | Meat/skins/feathers +3 | raw lamb leg, raw bird, onion, cooking spices, garlic |
| Ghostwalk Supper | 115 | First aggression seconds +3 | bat wing bits, raw fish steak, raw lamb leg, cooking spices, daemon blood |

## Eating and effects

Double-click a prepared meal in your own backpack. It works at full hunger and applies no native hunger or stamina restoration. Ordinary native food also bypasses hunger/restoration for enabled class characters; pets and other native users keep their rules. The one food effect lasts one hour; eating another meal replaces it and restarts one hour, including the same effect. One potion buff and one map effect remain independent. Effects belong to the character, survive craft/class changes, advance offline/during downtime and clear on death. The item retains its recipe identity through stacking, trade and save/reload.

Light is a real personal radial light in the matching client:1/2/3 tiles, rendered at 44 screen pixels per tile before camera zoom. It leaves other lights and existing brightness preferences intact, expires with the food and does not change server visibility/range. Carry capacity adds 25/50/74 to the native maximum, including status and encumbrance checks. Replacement/expiry restores ordinary capacity; belongings stay in the backpack and the player manually drops items if necessary.

Extra gathering rolls once at 10% for an additional 1/2/3 of the actual primary log, ore or fish identity. It never applies to gems, Runebark, treasure maps, fishing special outcomes or other byproducts, and it never repeats extraction/byproduct checks. The native bank loses its ordinary extraction amount; food supplies the extra units. Normal delivery/full-pack handling remains.

Gathering speed subtracts 1/2/3 seconds from the entire action, with the user-approved one-second floor for enabled character gathering. All native animation/sound intervals scale together, retaining the same number of effects and exactly one completion. The delay is chosen when the action starts. Thus an action with three effects at 1.6 s and a 0.9 s final sound takes 4.1 s normally and 1.1 s with the best meal; an 8 s fishing action takes 5 s. An already short action cannot become instantaneous. Unaffected native users keep original timings.

Carving food adds 1/2/3 to each eligible positive meat, skin and feather yield, after ordinary racial/map multipliers. It creates no absent material family, does not increase wool/scales/exotic ingredients, preserves hide/leather types and uses the ordinary saved once-only Carved boundary. Summoned/bonded/animated and otherwise ineligible corpses retain their exclusions.

Quiet meals delay a monster's first eligible acquisition of the character by 1/2/3 seconds. This is additional to its ordinary notice timing; its next scan can occur at the food deadline rather than waiting a longer ambient poll. Re-eating, walking nearby and brief line-of-sight breaks do not restart the delay. Only 10 continuous seconds outside perception or line of sight reset the encounter. Attacking cancels the delay and allows immediate native retaliation. Existing combat, aggression, forced targets and pet orders are unaffected. Encounter awareness is transient AI state; the saved meal deadline is absolute and never refreshed by reconnect/restart.

## Transactions and compatibility

Confirmation binds the exact recipe, stack quantities, flour portions, parents, origins, tool/uses/provenance, trained skill, connection, active profession and request session. Cancellation, replay, changed portions, craft changes, moving out of station range and insufficient post-consumption capacity consume nothing. Poisoned food cannot serve as a clean ingredient. Native crafting performs the delay; unbound native requests cannot create meals. Output capacity handles empty sacks disappearing and partial sacks remaining.

PreparedMeal uses generated version 0 item serialization. Existing timed-effect version 0 records save the food id, frozen magnitude and absolute expiry. Native content patch 0032 supplies optional consumers without engine/application changes. Client patch 0026 renders the radial light; the shared stat protocol becomes version 6 with one appended 0–3 light field, retaining all prior field positions. Native dependency pins are unchanged.

Completed and deployed on 20 October 2026 (JST). Native/content/integration builds have zero warnings or errors; the client builds with nine existing upstream warnings. Source and installed cumulative/legacy checks, actual cooking-book/cancel/confirm/native brewing/inventory eating, carry expiry without spilling, visible radial light and saved-food identities/deadlines pass. Source belt controls and Second Wind's 200→154→200 ms running regression pass. All 33 Python checks, refreshed first-loop evidence/catalogue and exact reproduction/reversal of 32 server and 26 client patches pass. The full rollback backup and protected source/installed/final-world comparisons retain prior character, gear, material, gold and preference data. Production uses content assemblies only, with no fixture world or population changes.
