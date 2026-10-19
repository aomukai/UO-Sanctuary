# Mouse combat prototype

Class characters use left click to select a hostile creature, path toward melee range, and repeat their left-bound attack. Clicking ground cancels combat before starting a ground path. Holding left mouse keeps updating that destination as the cursor steers. A short click completes its point path; holding for at least 200 ms makes release stop the route after any committed step. Shift pauses held movement, which resumes when Shift is released while left mouse remains down. A ground gesture never selects an enemy or interacts with an NPC merely because the cursor crosses one. Right click uses the right-bound action on the pointed unit or ground position; holding repeats it at the server-controlled casting cadence. Unit casts retain their chosen target; ground casts follow the cursor direction. Releasing the button, leaving the world view, losing focus, losing the ability, or target death ends held casting. A short server lease also ends casting if input updates stop. Death, deletion, loss of visibility, changing maps, leaving sight range, and disconnect stop the selected attack. No replacement target is acquired. Blocked pursuits time out. Shift stops movement and prevents paths or keyboard walking while held; pressing Shift alone does not fire an ability.

Diablo ground movement and pursuit opt into responsive route searches: four milliseconds or 256 expanded nodes per search, plus initialization/reconstruction overhead. Searches inspect only the populated portion of the node lists. When a route cannot finish within that budget, the client uses a safe partial route that approaches the destination, then continues planning after queued steps complete. A blocked destination cannot make held movement repeatedly freeze the main thread. Native pathfinding callers retain their existing default search behavior. Optional `SANCTUARY_TRACE_MOVEMENT=1` logs synchronous route durations for diagnostics; normal play does not log every search.

An actual-client regression holds left mouse towards ground enclosed by impassable objects. The previous implementation repeatedly took about 180–196 ms per search; the first fixed run's worst search was 8.58 ms, while the character still approached the blocked area. The test runs against a disposable world and leaves live character progress untouched. Baseline and fixed logs are `.local/logs/movement-baseline-client.log` and `.local/logs/movement-fixed-client.log`.

The final blocked-destination run peaked at 8.39 ms. Full combat/loot, Shift swings and movement, Run/Walk persistence, talent text clicking, summoning, pet stats, relogging and save/reload checks passed. The updated client was installed with the server left running. Backup: `.local/backups/pre-movement-talent-fix-20261018-003216/`, including the real client `settings.json` and `Data/Profiles`, the rest of client Data, and a fresh server save/configuration snapshot. All 41 client preference/data files retained their hashes; the client was left closed.


**Shift + left mouse** starts an attack in place using the left binding. Click once for a swing, or hold to repeat at the same server-controlled melee interval, including when aimed at empty space or a distant creature. Each heartbeat follows the current cursor selection; only the selected eligible hostile within melee range, line of sight, and height tolerance takes damage. Empty swings consume the same shared action cooldown and cannot acquire nearby creatures. Pressing Shift alone does not attack. Releasing Shift ends this attack gesture; the mouse-up is still consumed so it cannot start a chase or a ground path. A ground movement gesture begun before Shift continues to use pause/resume. Friendly NPCs remain protected, and ordinary unmodified clicks retain interaction.

Vendors, neutral animals, guards, pets, summons, and other players are interaction targets. The server classifies hostile wild creatures and sends a bounded nearby list to the client. Notoriety alone cannot authorize an attack, and all targets are checked again on the server when an action executes. The existing single/double-click, context-menu, container, drag/equip, and bladed-tool targeting paths remain available. Right click over UI retains ordinary UI handling.

The current defaults are **left: Basic Attack**, **right: Teeth for Necromancers** (None for other classes). A small readout shows these defaults and effective Teeth rank. Class → Mouse skills now assigns Attack, Teeth, Raise Skeleton or None to either button and saves the choices. Class → Pets inspects guardians. [Progression and pets](death-and-pets.md) describes hovered/nearest-corpse summoning, hotkeys, life-cycle rules and friendly-fire protection.

A Necromancer's equipped, explicitly marked starter dagger adds one effective rank of Teeth, stacking with learned ranks. The grant disappears on unequip; learned ranks remain usable. Ordinary daggers never grant Teeth. Starter identity is persisted independently of the display name, with a one-time migration for the existing named blessed starter. Other classes may wield a transferred dagger but receive no Teeth grant. See [converted gear](balance-foundations.md#equipment).

Teeth directly uses UO's Magic Arrow missile art (`0x36E4`) as a placeholder. It does not invoke the UO spell system: no reagents, Magery, spellbook, UO spell skill check, or separate target cursor. It currently fires one missile at a unit, or along a ground-directed ray toward one eligible hostile. Aim distance does not limit firing: near and far ground points project the same direction to the sixteen-tile range limit, clipped at blocked line of sight or the map edge. A creature beyond reach can be aimed at, but cannot be damaged beyond reach; a hostile along the bounded ray can be hit. The final Teeth fan, projectile travel and collision, resistances, and balance are future work. Damage resolves at the cast release frame; the missile currently provides visual feedback rather than simulated travel.

Provisional tuning uses native equipped weapon reach (unarmed/melee usually 1), Teeth range 16, class/weapon frame timing, weapon/stat-derived physical damage, and `4 + 2 × effective rank` Teeth damage costing 1 mana. Initial Necromancer timings are Dagger 480 ms, Broadsword 680 ms, Halberd 800 ms and Teeth 600 ms before IAS/FCR. Actions resolve their hit/release partway through the cycle; the [timing profiles, weapon modifiers and tooltip rules](combat-timing.md) supersede the historical fixed-800-ms checks recorded below. The selected ability determines its interval through one server timing function, including current equipment IAS/FCR rules; the next-action deadline is shared between buttons. Neither button can bypass it by rapidly clicking, switching, or canceling/restarting. Held input refreshes every 200 ms with a 750 ms lease; the server runs combat checks every 40 ms independently of input frequency. Class characters regenerate 1 mana every 2 seconds and 1 stamina every 5 seconds, independently of Meditation and Focus. UO skill gains are restricted to the 13 trade skills for class characters; hidden combat checks cannot train other UO skills or award their associated stat gains. Weapon damage formulas, ammunition, durability loss, XP awards, recovery/breakpoints, and final class balance are not supplied by this prototype. Separate rates are planned for players, monsters, pets, attacks (IAS), casts (FCR), and projectiles, with run speed affecting movement. Hit recovery will be an explicit action state triggered by sufficiently strong hits and shortened by FHR. Thresholds, duration, interrupted/blocked actions, queued input, and breakpoints remain tuning decisions. Recovery must share the server action validation path so client input cannot bypass it.

War mode has no player control in class combat: the former paperdoll toggle selects Run/Walk, Tab switches that movement choice, and war-mode macros cannot activate war mode, and server requests normalize class players to peace. This is not permanent war mode. A small UOContent hook prevents the stock player Combatant property from scheduling an additional swing or automatic retaliation; the custom session owns the selected attack. NPC combatants, aggression tracking, attack animations, and legacy staff characters continue using their existing systems. No server-engine files are changed.

## Practice and checks

On a living class character, `[CombatTest` places a small practice encounter at Britain cemetery, equips a Necromancer's existing backpack dagger when possible, and creates two stationary skeleton targets. If no dagger exists, it supplies one. `[CombatTest return` returns to the location recorded before practice. Repeating the command resets the encounter's named targets in its bounded area; this is a shared local development encounter. Targets are stock NPCs, not new serialization types. Ordinary nearby vendors can be used to check friendly interaction.

Build server, content, client, and the integration module before running:

```bash
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_COMBAT=1 SANCTUARY_TEST_UI=1 \
  SANCTUARY_TEST_COMBAT_UI=1 \
  SANCTUARY_TEST_UI_STATE="$PWD/.local/logs/combat-ui-state.json" \
  python3 tests/check_britain.py
```

The fixture uses disposable accounts and saves. Server checks cover hostile/friendly ownership, range, repeated attacks, cooldowns, cancellation, death/no retarget, mana, frozen state, gear grants and allocated ranks, no UO skill gains, tooltip/name, a transferred dagger on a Barbarian, held-cast cadence/release/lease/revocation, empty/selected stationary melee with cadence, release, binding and range checks, near/far aim and bounded ray hits, and unchanged legacy combat. The real client test covers path approach, repeated melee, ground cancellation and continuous held movement, Shift pause/resume and explicit stationary swings, right-click and held Teeth, cast cadence/release, far screen aiming, unequip disabling Teeth, death/no retarget, and a friendly NPC click. Combat feel, animation readability, and responsiveness still need the user's play feedback.

The complete real-client combat flow and all server checks passed on 17 October 2026, including the blacksmith's normal context menu, saved class/talent migration, and unchanged town identities across three test boots. Saves, configuration, and actual client profiles were backed up before installing the live update under `.local/backups/pre-combat-20261017-180922/`. No test accounts or characters are installed in the live world.

The updated live server loaded the existing world, retained the same town object identities, and wrote a successful world save. Its class/talent progression file was byte-for-byte unchanged from the pre-installation backup. The client was left closed to avoid stealing typing focus.


## Held controls and equipment update — 17 October 2026

Server checks and the actual ClassicUO client passed continuous left-held ground movement, Shift pause/resume, repeated right-held Teeth at 800 ms, release cancellation, and far-screen aiming. The server also verified heartbeat expiry, cooldown enforcement under extra input requests, revocation on unequip, death without retargeting, bounded damage for near/far aim, the exact dagger tooltip, and a Barbarian safely wielding the same dagger without Teeth. All creation/progression migration and town save/restart checks passed across three isolated boots. Six developer-helper tests and fresh-source patch application checks passed.

The live world, configuration, and client settings/profiles were backed up at `.local/backups/pre-held-controls-20261017-183555/` before installing the update. The casting interval remains provisional; Diablo FCR breakpoints, IAS, projectile simulation, and hit recovery are still future work.

The installed server loaded the live world, retained town object identities, and completed a world save. Character class/talent progression and client settings/profiles were byte-for-byte preserved against the backup. The client remains closed.


## Run/Walk choice and projectile range — 17 October 2026

The former paperdoll Peace/War button now displays **RUN** or **WALK**, indicating the selected mode. Click it or press **Tab** to toggle. Holding Tab toggles only once; releasing it does not toggle again. The choice is saved per character in the client profile (`diablo_run`, default Run), sent to the server, and shown in the combat HUD. Keyboard walking, ground click/hold movement, and hostile approach all pass through the same speed choice. Legacy Always Run and cursor distance cannot override Walk. Shift still stops movement. Existing stamina, hiding, and server movement restrictions can prevent running. Base on-foot timing remains UO's provisional 200 ms running / 400 ms walking per step.

Accepted running steps suppress weapon evasion and parrying on class players. Stationary Run selection, direction-only turns, and failed moves do not suppress defense. The penalty lasts for the accepted step's movement duration; choosing Walk does not erase an already accepted running step. Walking, or completing the last running step and standing still, restores normal defense. This is a small bridge to the current UO combat system; Diablo defense, block formulas, and item balance remain future work. Armor damage mitigation is separate from avoiding/blocking an attack. NPCs and legacy characters retain their existing behavior.

Teeth's provisional reach increased from **8 to 16 UO tiles**, independently of cursor distance or viewport dimensions. Ground-ray obstruction checks use projectile height rather than aiming at the caster's foot height along every tile. Walls and map boundaries still clip shots; the larger reach does not authorize damage through walls or beyond range. This is a play-test setting, not a claim of exact Diablo-to-UO unit conversion. Ability reach remains separate from the 18-tile nearby hostile advertisement radius. Long-range future abilities can query server spatial data beyond the visible client area without automatically selecting a replacement target.

### Legacy Diablo II reference

The root kickoff's D2MOO links were used to inspect missile creation and the Teeth/Strafe server skill handlers. [D2MOO](https://github.com/ThePhrozenKeep/D2MOO) reconstructs **1.10f** behavior; the numeric comparison below uses extracted **1.13** tables. Neither source is a current D2R balance specification.

[Missile creation](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/MISSILES/Missiles.cpp) initializes a lifetime from Range/LevRange and velocity separately, including a shared 75% velocity adjustment. [Teeth and Strafe](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/SKILLS/SkillAma.cpp) create missiles from their data records; Strafe additionally evaluates its own search-radius formula. Thus acquisition radius, projectile travel, and what a screen displays are separate limits. Ground aiming provides a direction rather than a universal maximum firing distance.

The [extracted 1.13 missile table](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/Missiles.txt) gives these baseline values. Products below are relative travel proxies before collision and skill-specific behavior, not UO tiles or rendered-screen pixel distances:

| Projectile | Velocity field | Lifetime field (frames) | Speed × lifetime relative to Teeth |
|---|---:|---:|---:|
| Teeth | 16 | 50 | 1.00 |
| Strafe arrow/bolt | 24 | 50 | 1.50 |
| Fire Bolt / Fire Ball | 20 | 50 | 1.25 |
| Bone Spear | 24 | 40 | 1.20 |
| Poison Nova | 12 | 30 | 0.45 |

The [1.13 skill table](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/Skills.txt) links Strafe to `strafearrow`/`strafebolt` and uses `par5` (35 native units) for its search radius. This does not translate directly to 35 UO tiles. Per-skill velocity, lifetime, acquisition radius, collision, and eventual projectile simulation should be tuned separately. Offscreen reach must not depend on monitor resolution or zoom.


Run/Walk server checks passed accepted-step defense suppression, recovery after the step interval, walking, turns, failed moves, and unchanged legacy movement. The actual client passed both toggles, Tab repeat/release handling, Walk overriding legacy Always Run, switching speed during a held path, and saving the per-character preference. Combined combat, range/wall, creation, and migration checks passed with three isolated save/restart boots. Six helper tests and fresh/repeated patch-application checks passed. Before installation, live saves, configuration, settings, and profiles were backed up at `.local/backups/pre-runwalk-range-20261017-190524/`.

The installed live server loaded the existing world, retained town object identities, and completed a save. Class/talent progression and client settings/profiles were byte-for-byte preserved against the installation backup. The client remains closed.


## Selection, collision, and passive skill gains — 17 October 2026

Mouse selection and projectile collision use separate shapes. For class combat, eligible hostile creatures have a filled version of their rendered sprite bounds with three pixels of padding in world-render coordinates. Pixel picking still runs first using the existing renderer and depth order. A visible creature pixel (including a friendly NPC) or usable item always wins over a hostile's filled fallback, even when that fallback belongs to a nearer creature. When both creature sprites cover the pixel, normal depth order chooses the front creature. If only their hollow/transparent areas cover it, the nearer eligible fallback wins. Foreground walls retain their ordinary occlusion. Class-mode inverse mirror picking uses the last source pixel at width minus one, matching the rendered horizontal flip. The shared pixel mask also includes span boundaries: previously the first opaque pixel in every run was rejected, making single-pixel bones impossible to pick precisely. A standalone mask test covers opaque/transparent runs, isolated pixels, row edges, and bounds. UI, item dragging, tool target cursors, corpse interaction, friendly picking, and a ground gesture already in progress retain their existing handling. The Run/Walk button is hidden with the rest of a minimized paperdoll so it cannot intercept world clicks.

Teeth tests a world-space segment against simple body footprints: body radius 0.5 tiles, missile radius 0.25 tiles, with a 16-Z-unit vertical tolerance. The first intersecting eligible hostile body is hit. Unit aim and ground aim use the same collision test, so selecting a rear monster does not skip a monster standing in the projectile's path. Equal-distance ties are deterministic. These are provisional common body dimensions; creature-specific footprints and real projectile travel remain future work. Enlarging the screen selection area never increases damage range or physical body size.

[D2MOO's missile collision handler](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Game/src/MISSILES/MissMode.cpp) checks saved world-path steps using collision masks and unit size. Its [unit collision routines](https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Common/src/Units/Units.cpp) use unit footprints independently of rendered sprite pixels. The [legacy monster table](https://github.com/fabd/diablo2/blob/master/code/d2_113_data/MonStats2.txt) separately exposes mouse-hit-test/rectangle fields and SizeX/SizeY. These establish the distinction, but are not a verified specification of every legacy client hover heuristic. The filled UO sprite proxy is a deliberate usability choice for this project's art.

The stock UO stamina-regeneration callback checked Focus, causing passive skill gains despite the custom mana-regeneration path already bypassing Meditation. Class stamina regeneration now bypasses Focus too, at the prior zero-Focus baseline of one point per five seconds. A small UOContent gain-policy hook allows class characters to train only gathering, crafting (including Imbuing), and Cartography; it guards ordinary checks, direct gains, and the early low-skill gain path. It leaves NPCs and legacy staff/player characters unchanged. Existing accidental skill values are preserved, but Focus no longer affects class stamina recovery.


Verification for this update includes standalone pixel-mask checks, forced skill gains across all retained/blocked skills, repeated mana/stamina regeneration, normal legacy Focus training, projectile grazing/miss/height/endpoint checks, and first-body collision for both aim modes. Actual-client tests isolate two overlapping skeleton sprites and identify exposed rear pixels, shared opaque pixels, and shared transparent pixels from screenshots before clicking them. They also click terrain visible between a skeleton's bones to select/approach it, lock held Teeth on a transparent body gap, move the cursor away while maintaining that lock, and check Focus/Meditation remain at zero after movement/combat.

All server checks, standalone mask checks, six developer-helper tests, patch application checks, actual-client overlap/combat tests, and three isolated save/restart boots passed. The installation backup is `.local/backups/pre-hitboxes-focus-20261017-193121`, containing live saves/configuration and client settings/profiles.

The installed live server reloaded the existing world, retained town identities, and completed a save. Class/talent progression and client settings/profiles were byte-for-byte preserved against the installation backup. The client was left closed.


## Stationary melee — 17 October 2026

Shift + left-click/hold now swings in place with or without a victim, following cursor selection at the existing input heartbeat. The server owns swing cadence and independently checks hostility, melee reach, line of sight, and elevation before applying damage. Empty swings share the action deadline with ordinary melee and Teeth. Button-specific release requests prevent an unrelated button release from cancelling the active hold. Protocol version 3 is staged with both client and content.

Server checks and the actual client passed empty swing cadence, cursor-selected hits, harmless distant swings, binding changes, frozen state, lease expiry, death without replacement, mouse release, and Shift release without pursuit. Existing overlap picking, ordinary approach/repeat, held movement, Teeth, friendly interactions, and three save/reload boots also passed. Six helper tests and fresh-source patch checks passed. Live saves, configuration, and client settings/profiles were backed up at `.local/backups/pre-stationary-melee-20261017-194720/` before installation.

The installed server reloaded the existing world, retained town identities, and completed a world save. Class data, configuration, and client settings/profiles matched the backup byte for byte. The desktop client remains closed.

## Movement comparison — 18 October 2026

The [movement smoothness report](movement-smoothness.md) traces the client/server pipeline and records the isolated responsive-path improvement, comparison measurements, verification limits and proposed finer-position prototype. Existing controls and combat action timing remain in use.

## Melee reach investigation — 18 October 2026

The [melee reach report](melee-reach.md) records the revised 200 ms pursuit using received step endpoints and the provisional one-tile dagger/two-tile halberd comparison. Impact range, LOS, elevation and windup timing remain strict. [Hit recovery](hit-recovery.md) records the separate player recovery prototype and monster reaction findings.
