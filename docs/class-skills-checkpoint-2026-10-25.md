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

Follow-on review: the supplementary server mode gate only tests `seqinput` as nonzero; its exact client/heartbeat policy is not established by that check. Avoid calling the active start wrapper on every initial heartbeat, which can emit before the planned action. Review `KeepCursorStateOnKill` and `ContinueCastUnselected` against generic held-input cancellation. Source anchors and these open details are recorded in `tools/reference/blade-fury-preflight.json`.
