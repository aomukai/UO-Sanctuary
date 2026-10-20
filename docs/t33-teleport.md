# T33 — Teleport and single-use scroll controls

Implemented 21 October 2026. The learned Sorcerer/Sorceress talent and single-use scroll share one server action and landing validator. The user confirmed an **18-tile range** and **no mana cost for the scroll**.

## Effect and costs

[Blizzard's Teleport description](https://classic.battle.net/diablo2exp/skills/sorceress-lightning.shtml#teleport) supplies the level18/Telekinesis prerequisite, rank-one24mana cost, one less mana per additional rank, obstacle/stair shortcuts and Faster Cast Rate interaction. It does not give a UO tile distance. The18-tile limit is the agreed project adaptation, measured with native tile range, independently of client zoom or resolution. Higher learned ranks reduce mana to a minimum of1; scrolls consume one physical scroll instead of mana and require no learned talent.

The existing class casting profile and FCR calculation supply the cast cycle and action frame: Sorcerer/Sorceress takes13frames/520ms at zero FCR and8frames/320ms at105FCR. Each frame is40ms. Other classes use their existing class cast profile when reading a scroll. The project action-frame placement is retained; this is not an added cooldown or a claim that UO and Diablo animation frames are identical.

Teleport crosses intervening walls, closed doors and corners. The destination must be on the current facet, within18tiles, different from the caster's position and supported by real walkable land/static/fixed-item geometry. The exact chosen elevation is retained; the server never guesses a nearby floor or silently moves a target onto a different level. A16-high body must fit, including native impassable geometry, BlocksFit items, headroom and other mobiles. Landing inside a wall, closed door, floating space, water or occupied tile fails. There is no path or intervening line-of-sight requirement.

Native TeleportFrom/TeleportTo restrictions, multis and housing exclusions still apply. A dead, frozen, paralyzed, overloaded, item-holding or recovering character cannot start/finish the effect; native spell casting and faction-sigil restrictions also remain. The caster, facet, location, connection, equipped weapon and scroll ownership/parent/amount/origin are checked again at the action frame. Landing geometry and travel restrictions are checked again then too. Native field effects at the arrival tile still execute.

## Controls and conservation

Learn the existing level18Teleport talent after Telekinesis, then choose Teleport in `[Bindings`. Right mouse casting uses the world cursor; left binding and Shift casting use the same action. `[Cast Teleport` and the native Teleport hotkey are also cursor-aim controls for class characters.

Right-click a Teleport scroll in your backpack to open the normal UO target cursor. Double-click is supported too. Cancel or invalid targets use nothing. The scroll remains present through the windup and is consumed exactly once only when a valid teleport resolves.

Load a Teleport scroll through an empty belt cell's normal backpack target. Column hotkeys1–4 use the current world cursor directly, without a second target cursor. A cursor over UI or an undrawn world pixel cannot use it. Clicking its belt icon while the cursor is on the panel therefore does not teleport; its tooltip explains the hotkey. Right-click returns it to the backpack. Existing potion fill, removal, compaction, overflow, death transfer and instant drinking remain intact; automatic filling skips scroll-led columns. Scrolls keep individual item identities and cannot stack during transfer.

The shared action deadline prevents overlapping/replayed casts. Accepted movement, death, hit recovery, disconnect and explicit stop cancel the pending effect without consuming a scroll. A failed action retains its started cycle. Held learned casting continues only under the existing input lease and available mana. Pending casts/target cursors are transient and never resume after restart; physical scrolls and mouse bindings are saved normally. No new active timed-effect save schema is needed.

T34 owns natural scroll acquisition: the1% bonus from full-tome crafting at100, along with the remaining Town Portal/Identify tome recipes and supplies. T33 does not grant scrolls on login or add a vendor/free production source.

## Verification

- Content and integration builds pass with zero warnings/errors; the matching client builds with its9existing warnings.35Python checks pass. Focused/cumulative four-boot save/reload checks and actual client controls are exercised against source and installed builds.
- Disposable worlds cover learned prerequisites/mana/ranks/FCR, shared action timing, wall/closed-door crossing, stair elevation and headroom, occupied/restricted/house/out-of-range/floating targets, actual native fire-field damage on arrival, cancellation/movement/death/recovery/ownership/facet changes and repeated requests.
- Scroll serials, origin, backpack/belt position and Teleport bindings survive saved restarts; an unfinished cast does not resume.
- Real ClassicUO exercises inventory right-click/target/cancel, belt loading, invalid UI cursor rejection, direct world cursor use and learned right-click casting. Existing belt controls and potion use receive a regression run.

Client combat protocol6 and belt protocol2 must be installed with the matching content assembly. The new client patch is0027. All34ModernUO patches/75paths and27ClassicUO patches/37paths reproduce exactly and reverse cleanly; native repository pins remain unchanged.


Deployed with the matching client and content on21October2026. The full pre-install runtime backup is retained, with all62protected stage payloads and34non-save payloads (including client preferences) preserved. Pre-install, installed and post-live save copies match both characters, native skill values/caps/locks, gear, materials and485gold stacks. Live save, clean stop, restart and second save passed; the second server boot remains running. Existing476merchants and22civic offices were retained, with no new population objects or production test fixtures. Evidence is under `.local/logs/t33/` and `.local/logs/deploy-t33/`.
