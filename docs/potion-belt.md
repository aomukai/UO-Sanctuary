# Potion belt comparison

> **Design update, 19 October 2026:** [Current first-implementation crafting/economy rules](crafting.md) reconcile the completed replies. The implementation/verification descriptions below refer to the existing comparison build, not completion of the new recipes, socket rules, potion/food catalogue or progression changes.

18 October 2026. This is a playable belt/storage and restorative-potion prototype. Acquisition, profession recipes, temporary stat potions and meals are separate work.

## Controls and equipment

The client always uses **Enter to chat**. Existing profiles that saved `activate_chat_after_enter: false` are accepted but the effective value is true; additional punctuation shortcuts are disabled. Options shows a fixed reminder instead of either toggle, and Reset cannot restore always-active chat. Shift+Enter's existing send behavior remains configurable. Other text fields retain their usual input.

For class characters, unmodified **1, 2, 3, 4** are reserved for the four belt columns, ahead of ordinary macros. A held key consumes only on its initial press. Active chat and other focused text fields receive digits normally. Other keys, modified digit macros, skill bindings, mouse controls and attack/cast deadlines retain their existing behavior.

A permanent potion panel sits at the game viewport's lower right. Its bottom row is the ready row. **1–4** drink from that row and remaining active potions move down within their own column. Click an occupied cell to drink that specific potion; right-click to return it to the backpack. Click an empty active cell and target a restoration potion in your own backpack to load one unit. The arrow expands the rows. **+** fills from the backpack, using healing for empty left columns and mana for empty right columns; it preserves an existing ready potion's kind. No automatic pickup, purchases, or inventory scanning runs on movement.

Potions can also be drunk by **double-clicking them in the backpack**, using the existing UO item interaction. The project healing/mana potions use the same restoration and bottle-return rules in both locations.

There are four slots per row. Without a belt, one row is available. Five project tabards occupy the existing **MiddleTorso / client Tunic** paperdoll slot, all retaining Surcoat graphic `0x1FFD` and its UO paperdoll appearance:

| Tabard base | D2 base code | Rows | Potion capacity |
| --- | --- | --- | --- |
| Sash / Light belt | lbl / vbl | 2 | 8 |
| Belt / Heavy belt | mbl / tbl | 3 | 12 |
| Plated belt | hbl | 4 | 16 |

The existing D2 base requirements, defense and affix machinery apply. In particular Belt/Heavy/Plated require 25/45/60 Strength. Existing UO items, art and saved base identities are preserved. Native cosmetic tabards do not automatically gain a new mechanical base. While a project belt tabard is equipped it supplies the single mechanical Belt stat slot; a legacy Waist belt does not contribute a second set of belt stats. Existing Waist items work as before when no project belt tabard is worn.

This first comparison deliberately adds no drop-table, starter-loadout or vendor changes. A GM on a living class character can use **`[PotionBeltTest`** for five tabard bases and stacks of basic healing/mana potions. Drag a tabard onto the paperdoll to equip it, then press **+** on the panel. Strength requirements still apply. No kit is granted on login and no live character is changed automatically. Regular acquisition and alchemy recipes remain next steps.

## Inventory and authoritative state

The holder is a nontransferable, invisible, zero-own-weight Item attached to the character. It stores actual potion items in the normal world-save graph, one unit per cell; potion weight still counts. Splitting a backpack stack preserves origin, kind and tier. Slots never contain virtual stock or unlimited stacks. Kind/tier are explicitly checked when backpack stacks merge.

The content service validates a living class owner, active capacity, actual parentage, empty destination and stack amount before loading. Loading and returning are refused during an item drag. Existing targets are not replaced by a belt load cursor. Drinking accepts only the owner's backpack or active belt cells, including while in hit recovery. Server checks do not trust the HUD or client quantity.

On a capacity decrease, inactive cells return to the backpack when it can accept them. A full backpack leaves them stored as **inactive overflow**, shown in expanded rows with a warning; they cannot be drunk or promoted into active cells. Returning them manually or re-equipping a larger belt preserves every unit. No potion is discarded or dropped on the ground to satisfy capacity.

On player death, all belt potions move into the actual corpse under the project's existing corpse policy. They can be recovered into the backpack after resurrection. The empty holder stays with the player. Saves preserve each occupied cell, potion serial, type, tier, origin and equipped tabard; restoration effects themselves are transient and stop at death/logout/disconnect.

A separate extended command `0xF6`, version 1, carries 12-byte requests and 172-byte authoritative snapshots. The HUD polls every 250ms and rebuilds only after state changes. Server polling is bounded, sequences reject replay, and use is limited per column; different columns may be used together without one suppressing the other. Existing combat protocol 5 and movement packet handling remain unchanged. No Server-engine or new ModernUO patch is needed.

## Healing, mana and bottles

Ten project restoration potions cover five healing and five mana strengths. Ordinary UO potions remain native and are not accepted by this initial belt; silently inheriting UO poison restrictions, free-hand checks and healing cooldowns would defeat the faster combat comparison.

Class-dependent restoration amounts follow [Blizzard's potion table](https://classic.battle.net/diablo2exp/items/potions.shtml). Healing base amounts are 30/60/100/180/320 for Necromancer, Sorceress and Druid, multiplied by 1.5 for Amazon/Paladin/Assassin and 2 for Barbarian. Mana base amounts are 20/40/80/150/250 for Barbarian, multiplied by 1.5 for Amazon/Paladin/Assassin and 2 for Necromancer/Sorceress/Druid.

Restoration happens over time rather than instantly. The initial timing comes from `Misc.txt`'s `len` at 25 frames/second: healing 7.68/6.40/6.84/7.68/10.24 seconds and mana 5.12 seconds. The pinned [1.13 data source](https://github.com/fabd/diablo2/blob/45112569deb9384738ccafe5c24ebbb71f41c7c9/code/d2_113_data/Misc.txt) has SHA-256 `d01a37412cc170b744ae4cde66f5f5130e97f69274bad60b2d3ef0f470d7e207`. Belt capacities match [Blizzard's belt table](https://classic.battle.net/diablo2exp/items/normal/belts.shtml). These are source-derived starting values, not a claim of complete potion parity.

Health and mana have separate restoration lanes serviced by one shared 100ms timer. Repeated same-strength drinks extend remaining restoration and duration rather than multiplying the rate. Mixed strengths use a provisional weighted remaining rate. Reaching full cancels that resource's excess restoration. A full resource refuses its potion without consuming it. Pending restoration is bounded to ten minutes per resource as a request-abuse guard. D2's random double-restoration roll is not implemented.

Drinking has no bandage-slip chance, native heal-potion cooldown, free-hand requirement or drinking animation. It does not clear a pending class attack/cast, change its deadline, or cancel/shorten hit recovery. It reveals the drinker and plays the retained UO drinking sound. The native backpack item-use path still performs normal ownership/access/region checks; the class abilities use their existing independent combat timing.

**Each successful drink has a 20% chance to recover one empty bottle**, as a project rule requested by Andy. The bottle retains the consumed potion's origin. It goes to the backpack if it can fit or merge; otherwise it is lost, without cancelling restoration or dropping a bottle on the ground. Failed/full-resource drinks return nothing. This rule applies to these new restoratives, not to unrelated native UO potion classes.

## Profession direction, not yet implemented

Alchemy and cooking should offer preparation choices without making recurring buffs a requirement for ordinary combat. Consumables may help a poorly equipped character take on a harder encounter at a real preparation/resource cost; monster balance must not assume permanent consumable uptime.

- **Potions:** short, clutch effects. Thirty seconds is the baseline for future temporary IAS, FCR, resistance and similar power bonuses. Extra doses may extend duration within a tested cap, never multiply the bonus. Ninety seconds is a provisional project cap from the discussion, not a verified D2 limit. IAS/FCR must feed the existing class/weapon frame breakpoints, preserve action deadlines and be tested against gear. Healing/mana retain their short restoration timelines rather than becoming long buffs. Antidote/thawing-type cure plus short resistance effects are future candidates.
- **Meals:** about an hour of convenience, for example light radius or carry capacity. Avoid hour-long IAS/FCR or other combat power that becomes obligatory maintenance. A carry-capacity meal should increase the carrying limit directly so it does not also grant Strength, melee damage or equipment eligibility. Food and potion effects need separate stacking/display rules and safe expiry/relog/death handling before implementation.
- **Acquisition:** normal healing/mana drops and alchemy recipes need their own pass. Rejuvenation/full rejuvenation are intended to be crafted by alchemists and excluded from drops. Out-of-combat regeneration recipes remain a proposed alchemy role. No recipes or rewards were added by this belt prototype.

Blizzard confirms [antidote and thawing potions](https://classic.battle.net/diablo2exp/items/potions.shtml) give +50 relevant resistance and +10 maximum resistance for 30 seconds. That supports the short-effect model; it does not establish a 90-second D2 stacking limit. No new resistance-cap mechanics are implemented here.

## Verification

Builds passed for content and the client. Content/integration have zero warnings/errors; the client retains its existing upstream warnings. Client patch 0019 reproduces the change after the preceding 18 patches on the pinned source. Tests run only in disposable worlds with the installed artwork.

The server check covers ownership rejection, stack/origin conservation, all five tabard capacities and retained art/layer, single mechanical Belt stats, full-backpack overflow and later return, inactive-use rejection, full-resource refusal, use during hit recovery with unchanged pending-action/recovery deadlines, timed class-scaled restoration, same-strength stacking without doubled rate, full-resource cancellation, native backpack item use, exact bottle-return boundary and full-pack bottle loss, death-to-corpse, and occupied-cell identity/origin after three reloads. Four boot/save/shutdown cycles passed, alongside class/gear/origin persistence.

The actual SDL client check starts with a profile that explicitly disables Enter-chat. It fills all 16 cells, uses all four hotkeys, checks downward compaction, holds a key through keyboard repeat, types and sends `1234` as speech without consuming potions, saves the enforced chat values checks a rapid four-column burst and right-click return, and drinks during active hit recovery without changing either deadline. Compact and expanded HUDs were visually inspected using UO artwork. Screenshots: `.local/logs/belt-filled.png`, `belt-chat-digits.png`, `belt-expanded.png`. The load-target callback and existing-target preservation are exercised on the server; individual inventory targeting is not part of the GUI exercise.

Reproduce after building content, integration and the client (do not rebuild a DLL while a test server maps it):

```bash
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_BELT=1 \
python3 tests/check_britain.py

SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_UI=1 SANCTUARY_TEST_COMBAT_UI=1 \
SANCTUARY_TEST_BELT_UI=1 SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/belt-ui-state.json" \
python3 tests/check_britain.py
```

The final combined server/belt check and full real-client combat regression passed after the restoration cancellation fix. That regression covered movement/held steering/release, pursuit and pixel picking, Shift attacks, held Teeth/cast transitions, dagger/halberd cadence, equipment IAS/FCR, loot/harvesting, and saved Run/Walk preferences, followed by four world save/reload cycles. Logs are `.local/logs/belt-combat-regression.log` and `belt-combat-regression-server.log`; the focused belt/client run is `belt-final-tests.log` with `belt-final-server.log`. All 19 ClassicUO patches reproduce all 34 affected files; the unchanged 12 ModernUO patches reproduce all 26 affected files. Six Python helper tests and whitespace checks passed.

## Installation

The matched client/content build is installed. The prior live world was saved and shut down cleanly, then backed up under `.local/backups/pre-potion-belt-20261018-173118/`. All 91 protected save/configuration/client preference files were byte-identical during installation. Live reload/save passed with the same town identities; 75 progression/configuration/preference files retain their payload, with only the standard Accounts.idx save-start timestamp changing. Installed content, client, Server and UOContent match their build outputs. No integration assembly, test account or comparison kit was installed into the live world.

Deployment record: `.local/logs/potion-belt-deploy.json`. Live server log: `.local/logs/server-potion-belt.log`. The desktop client is left closed for Andy to launch. Manual comparison against live monsters remains Andy's playtest; the automated tests establish behavior and persistence, not final combat balance.
