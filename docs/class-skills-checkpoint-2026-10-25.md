# Class mechanics checkpoint — 25 October 2026

Goal active. Reference **D2R3.3.93847 — Reign of the Warlock**; approved Sanctuary deviations take precedence. Installed150 handlers: Necromancer28, Sorceress29, Paladin30, Barbarian25, Amazon27, Assassin11; Druid/Warlock0. No class completion certificate; Blade Shield remains latest playable.

## Implemented

- Blade Fury ordinary-player channel/payment/SQ planning components and dedicated player weapon/skill capture. Shared BowLaunch arithmetic preserves96/48 weapon scaling, unscaled skill physical, typed/poison/drain and per-missile critical. Current ToHitCalc contributes10percent per effective rank.
- Real saved bladefragment1 flights reuse shared motion/health/mitigation/leech/poison/event consumers with source-size1 collision, no Pierce/ammunition/wear and strict profile validation. Source missile AR mastery exclusion is explicitly identified, with current behavior still unverified. Slow context is captured with the existing scoped payload adapter. Death/respec/wardrobe cleanup is wired; only death/respec have dedicated live-flight controls.
- Sentry backend remains installed and gated. Corpse/GUID retirement is individually deferred; its native living-actor gate remains unchanged.

## Verified

- Focused/full suites each pass three boots/two reloads. Per boot:960 complete creator/tier/typed source comparisons,6912+12 channel/expiry,7200 resource and490 animation comparisons. Three natural server-timer damaging flights per suite run without manual tick calls.
- Actual unarmed, selected claw/mastery critical, two-handed and bow/no-ammo capture; immutable item poison; travelling damage/miss/no-pierce/no-wear; actual slow-state capture; death/respec cleanup; Internal-map damage suppression/removal pass.
- Actual player/weapon/path/packet/profile/clock persist through two world reloads. Released damage survives weapon removal; continuation damages once without another payment. Persistent stat allocations corrected the fixture's initial saved-weapon re-equip failure. A separate initial failure identified missing lifecycle cleanup, now wired and verified; the failure logs are retained. A full-suite fixture clock interference was corrected by advancing only the actual flight lifetime; production mechanics were unchanged.
- 70 Python tests, fresh547/21 runtime/property fingerprints,573 unchanged named decisions, zero-warning/error content/integration builds and original-world smoke pass. Previous03eb611 content/data/saves/client are backed up in `.local/class-skills/pre-blade-fury-missile-install`; save/config/client bytes unchanged; server stopped.
- Source preparation remains available:24 ordinary SQ initialization/rate/router/scheduler controls,960 creator cases and23040 retained-dead-owner Sentry HP cases. These are bounded older-engine proofs, not complete current executable certificates. The earlier Sanctuary scene-AI reload fixture correction remains in force.

## Next and unresolved

Connect Blade Fury mode entry and held input to the installed channel/payment/SQ planner and saved damaging flights. Use the24-case fixed sequence-rate composition; retain source deadline/lease/event ordering, aim and interruption behavior. Keep the optional press/release question nonblocking and current AR-mastery/slow/property/client limits explicit; do not count a learned handler until actual input and lifecycle acceptance pass.

Current mastery AR, exact slow rounding, complete weapon-property/return-fire/kill-credit/motion and graphical acceptance remain open. The optional D2R press/release observation remains pending and nonblocking. Actual flight persistence is verified; production held-channel persistence/input is not implemented. Effects/audio and talent/hotkey icons retain separate240-entry inventories, with clearly labelled UO0xF42 travel placeholders; no image generation in this phase.

Follow-on review: republished Blizzard field documentation describes `seqinput` as an in-sequence repeat delay. `KeepCursorStateOnKill` retains the held click when a target dies; `ContinueCastUnselected` continues at the cursor without a selected target, rather than after a binding change. Exact client/heartbeat timing remains unverified. Source anchors are recorded in `tools/reference/blade-fury-preflight.json`.

## Release-state correction

Blade Fury now distinguishes the Inferno state bit from its timed stat list. The legacy stop intent clears the bit while retaining the lease, next-shot deadline and unrelated flags. An already scheduled do can emit and pay once without rewinding; retained-list reentry can renew below the fresh six-mana threshold without allocation or reactivation. Actual travelling damage and607FP owner mana pass, including two character reloads with an explicit released component fixture. Focused/full suites each pass three boots/two reloads and5400 complete source stop/state/list/resource comparisons per boot, alongside prior960 missile,7200 resource,490 animation and6912+12 channel controls. All70 Python tests, fresh547/21 audit fingerprints,573 unchanged named decisions and original-world installed smoke pass. Save/config/client bytes unchanged; server stopped. Total handlers remain150, Assassin11. Learned mode/held input and exact current stop/reentry timing remain pending; Blade Shield stays latest playable.

The new optional `Lease` component field preserves old coincident bit/list JSON. A valid older stop0x12 clears the bit; expiry separately removes the list and sets flag64. The source probe executes the complete bit helper, state toggler/checker, both list chains, packet callback and ordinary start/do/resource wrappers. Declared ABI/world/animation services remain scoped. The saved component is a fixture; this does not establish production held-channel persistence. The initial fixture mana-drift log is retained; applying its regeneration policy before birth resolved the two reload controls. No production regeneration change was made.

Next: Compose Blade Fury fresh SQ23 mode entry and action/end/state-removal event dispatch using the distinct state bit and timed lease. Apply documented seqinput/cursor-retention semantics to held input without early st26 heartbeat emission or an invented release tail. Keep the optional release observation nonblocking; exact current client timing, mastery AR, slow rounding and broader property/client acceptance remain explicit. Register a learned handler only after actual input and lifecycle checks pass.

Previous8997941 native/content/data/saves/client are backed up in `.local/class-skills/pre-blade-fury-release-install`; installed save/config/client bytes remain unchanged.
