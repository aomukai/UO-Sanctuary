# T32 — Crafted consumable maps

Implemented 20 October; deployed 21 October 2026 (JST).

Five Cartography maps: City (0–30), World (30–60), Dungeon survey I (60–80), II (80–100), III (100–120). Each attempt uses one Blank Map and one pen use, with crafted-tool saving; failure consumes the map and use. Normal success/gain runs across the exact listed bands. Craft at a free public Cartography station using the chosen craft; the finished map needs neither Cartography nor the maker's skill to consume.

Double-click one owned backpack map to consume once and apply a 30-minute character map effect. One map effect, independent of food and potion; refreshing resets 30 minutes. Offline time and downtime advance it; death clears it. City maps discount ordinary town-shop purchases 5%, rounding each unit upward to whole gold and retaining a minimum of one gold; sale quotes and fees are unchanged. Shop menus and payments agree; if eligibility changes while the menu is open, reject and reopen rather than silently increasing payment. Purchased equipment records the actual discounted price for resale.

World maps add five tiles to native treasure-dig search tolerance, retaining the native target reach, tool, skill, facet, collision and digging checks. Dungeon surveys grant +10/+20/+30 Magic Find across the current supported named dungeon, independent of physical floor. They bind to its facet/name on activation; activating outside a supported dungeon consumes nothing. Leaving immediately removes the effect, with no revival on re-entry; logout preserves the real location and the original expiry. Treasure acquisition/deciphering and private dungeons are implemented in [T37](t37-treasure-hunts.md).

Acceptance: exact costs/output, cancel and stale transaction rejection, actual price UI/payment/resale, native search hit/miss, MF through the real death reward path, floor/subregion transitions versus exits/facet changes, independent buffs, relog/downtime/death, actual client interaction, saved restart and installed-world preservation.


## Recipe book and acquisition

Use a Cartography pen's **Draw consumable maps** action at a public Cartography desk. Review and confirm one map; cancellation costs nothing. Cartographers already sell Blank Maps for 5 gold. All five survey types are craft-only, with no additional drop rolls or vendor sales.

| Survey | Cartography/gain band | Cost | Effect for 30 minutes |
| --- | ---: | --- | --- |
| City trade map | 0–30 | 1 Blank Map, 1 pen use | 5% discount on ordinary town-shop purchases |
| World explorer map | 30–60 | 1 Blank Map, 1 pen use | Treasure-dig search tolerance +5 tiles |
| Dungeon survey I | 60–80 | 1 Blank Map, 1 pen use | +10 Magic Find in the activated dungeon |
| Dungeon survey II | 80–100 | 1 Blank Map, 1 pen use | +20 Magic Find in the activated dungeon |
| Dungeon survey III | 100–120 | 1 Blank Map, 1 pen use | +30 Magic Find in the activated dungeon |

Trained Base skill gates recipes; the existing native effective-skill success check and ordinary skill-gain/lock/cap rules apply. No quality or extra-output roll is added. The three dungeon recipes are survey grades, usable across the whole named dungeon; they do not require the reader to stand on a numbered physical floor. City and World maps work globally. A finished map can be traded and used without selecting Cartography or having the maker's skill.

The supported catalogue contains 33 native named dungeon/facet identities in Felucca and Trammel, including Deceit, Despise, Destard, Covetous, Hythloth, Shame, Wrong, Ice, Fire, Orc Cave, Terathan Keep and the named Britannian expansion dungeons. It excludes the generic Misc Dungeons region and outdoor regions typed as dungeons on other facets. Nested subregions and disconnected floor rectangles resolve to their outer named dungeon. A different facet is a different identity. Native world placement/populations are unchanged; instanced dungeon layouts remain later work.

## Consumers and persistence

City eligibility is captured when opening an ordinary supported town shop. Each displayed unit rounds the 5% discount upward, with a minimum of 1 gold: 400 becomes 380, 101 becomes 96, and prices below 20 can stay unchanged. Eligibility is frozen throughout each menu and payment batch, so clock expiry cannot change later rows mid-transaction. Changed map effects, refreshes, disconnects, vendor changes or missing network quotes require reopening the shop before any payment. Equipment records the actual price paid for its existing vendor-origin resale calculation. Sale offers and training/repair fees are unchanged. The existing bank-payment threshold applies to the final total.

World maps alter the native treasure-dig tolerance, from the Mining-based 1–4 tiles to 6–9. The clicked target must still be within the native six-tile reach; ownership/decoder/tool/facet/line-of-sight/chest-fit/digging checks still run. This supplies a working consumer before T37's treasure drop/deciphering progression; it does not introduce treasure-map drops or change chest contents.

Dungeon Magic Find adds once to existing equipped affix/material MF and reaches the death-time shared reward path, including existing pet-owner credit rules. Reading a stronger map replaces rather than stacks the previous map. Leaving clears only the map category; food and potion effects remain independent. Map-item identity is saved in its new version-zero item fields. Active effects use T27's existing version-zero UTC timed-effect persistence; logout uses the actual LogoutMap/LogoutLocation without resetting expiry. Death clears the effect, and re-entering after a real exit cannot revive it.

## Verification

- Native UOContent, content and integration builds pass with zero warnings/errors. No client or engine change is required.
- Focused and cumulative fixtures craft all five maps, verify exact successful/spoiled attempts and pen saving, reject altered/cancelled/remote/wrong-craft/forged requests, and verify that a full backpack can replace its consumed Blank Map without gaining an item slot.
- Consumers cover native treasure target hit/miss with and without the World bonus, real reward-path MF, nested/floor transitions, immediate exit/re-entry and facet changes, logout/relog, fixed UTC expiry across saved restarts, independent food/potion slots and death clearing.
- Actual ClassicUO verifies book/confirmation/cancellation, native timed drawing, inventory double-click consumption, displayed 400→380 purchase and exact payment, stale discount rejection with zero charge, and reopened 400 payment. Screenshots were inspected at the standard 640×480 size.
- 35 Python checks pass, including five agreed bands/effects and the actual native dungeon catalogue. First-loop evidence is refreshed/reproduced; affix values and pools are unchanged. Original questionnaire replies remain unchanged.
- Patch0034 contains the optional native location/search/vendor quote hooks. All34ModernUO patches/75paths and26ClassicUO patches/36paths reproduce exactly and reverse cleanly. Native pins remain unchanged.


Deployment uses matching native/content assemblies and the unchanged client. A full pre-install backup and all 62 protected save/configuration/preference files were retained. Source, pre-install, installed and post-live world copies preserve both characters, native skills/caps/locks, gear, materials and 485 gold stacks. Both live boots saved successfully, with a clean shutdown between; the second boot remains running with the original 476 merchants and 22 civic offices. Production contains only UOContent and Sanctuary.Content, with no test fixtures. Local evidence is under `.local/logs/t32/` and `.local/logs/deploy-t32/`.
