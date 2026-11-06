# Inferno held-input proposal

Status: **review-only prototype; not approved or enabled**. Mechanical reference remains **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations taking precedence. This proposal changes no production C#, runtime data, learned handler, client input or installed build. It concerns Sorceress Inferno only; Arctic Blast and Blade Fury require their own decisions and integration.

The existing fire stream, source sequence, resource helper and supplied-intent controller are reused. Exact modern accepted-start/reentry/payment chronology remains unverified. The prototype supplies an explicit input policy to those components; its observed timings are consequences of that policy, not a claim about D2R's actual held-input scheduler.

| Input or boundary | Proposed Sanctuary behavior |
| --- | --- |
| Fresh press | Require at least six mana, or the current invocation cost if higher. Debit the current table's invocation cost, not six mana. |
| Holding | Immediately before each due sequence action, supply one accepted start callback; advance that sequence frame. Repeated delivery at the same frame must not charge or rewind again. No arbitrary client heartbeat renewal. |
| Release | Cancel the channel and temporary lease immediately. Emit no further flames. Already-fired flames retain the existing independent stream host and lifetime. |
| Press after release | Start a fresh windup and entry threshold. Do not enter the unresolved early double-rewind path. |
| Insufficient mana | End the channel. A failed fresh press changes no resources or flights. |
| Equipment changes | Read rank and table cost for the next invocation; retain previously emitted packets unchanged. Existing eligibility/cancellation rules still apply. |
| Disconnect or restart | Do not retain or replay held input. Persist existing emitted packets under their existing stream contract. |

At effective rank 1, the prototype measures a **0.375-mana entry debit**, first flame at **0.48 seconds**, then renewal/emission every **two 25-Hz frames**. Steady debit after windup is **4.6875 mana per second**. A two-second hold makes 22 debits and 20 flames; six mana funds 16 debits and 14 flames before exhaustion. The pinned per-invocation cost is `(24 + effective rank - 1) / 64` mana. These numbers do not establish the modern D2R mana-per-second contract.

The fixture is confined to `InfernoHeldCandidateChecks` and its disposable-world driver. It tests one-, three- and seven-frame catch-up chunks, duplicate same-frame delivery, low resources, early release, fresh reentry, real Tarnhelm equipment changes, independent damage from already-fired flames, and two reloads of original wounded owner/resources/flights. A separate connected save command starts a real prepared mode at the first save boundary. The preceding simulated client is closed, but its asynchronous disconnect may still be pending. The save setup explicitly cancels any preceding input before authenticating its new connection and starting the fresh press. Subsequent saves must not restore it. Simulated future epochs isolate the explicitly supplied schedule from real-time timer execution; this is not a raw held-wire or graphical-client test.

The first focused run exposed missing fixture Strength for real gear; that failed binary/source/log are preserved. The first broad run exposed the disconnected-client/save-boundary assumption. A subsequent focused run caught the opposite race: socket closure had not yet dispatched its cancellation. Both failed save assumptions and the earlier successful focused and retained cohorts are archived separately. Neither correction changes production mechanics. Final acceptance counts and exact hashes belong in the [candidate receipt](../tools/reference/inferno-held-candidate-native-evidence.json).

**Final prototype acceptance:** unchanged production Content 5 from the NPC item-fear checkpoint, Integration 4 / snapshot 4, passes **12 focused + 185 retained markers**, each across three boots, two reloads and three saves. The retained cohort preserves all 182 historical markers. Fresh audits retain 573 named decisions and 1088/21 input fingerprints; 71 Python tests, all catalogue checks and installed-artifact/save/client protection pass. This is review evidence, not a learned-route or complete-class certificate. One Inferno-only adaptation question is pending; no answer leaves the route gated.

## Reference availability

A fresh read-only maps observation on 2026-11-06 found a different running D2R process from the November 1 context. The candidate slot11/RVA `0x43bf70` still spans inaccessible mappings, so its complete 398-byte unwind extent was not sampled. **Zero process-memory bytes were read**; no game input, protection changes or writes occurred. The on-disk executable is pinned independently; in-memory identity and dispatcher linkage remain unverified. See [context metadata](../tools/reference/inferno-reference-context-2026-11-06.json), reproducible offline with `python3 tools/observe_inferno_current_context.py --check`. Do not retry this unchanged context as though its blocker had changed.

## Assets and remaining work

Inferno's existing `0x36D4`/hue `0x489` flame remains a clearly identified placeholder. Final channel/stream/contact/expiry effects and audio remain in [effects](skill-effects.md); talent and left/right/quick icons remain in [icons](skill-icons.md). This proposal adds no asset identity.

Approval would authorize an explicit Sanctuary timing adaptation. Actual learned input, press/hold/aim/release wiring, natural scheduling, interruption and integration verification would still be necessary before registering the skill. Slow Missiles' unverified modern damage-order context remains explicitly refused. Full modern executable, PvP, world geometry and graphical-client parity are not certified. Coverage remains **197/240 handlers and zero completed classes**. Pending this decision, continue unaffected class work; this skill-specific reference gap does not block the entire roadmap.

Recheck the recorded acceptance with `python3 tools/record_inferno_held_candidate.py --check`. The local native drivers require `SANCTUARY_TEST_SERVER=/media/aomukai/FILES/Sanctuary/.local/server` and the exact saved integration/content artifacts.
