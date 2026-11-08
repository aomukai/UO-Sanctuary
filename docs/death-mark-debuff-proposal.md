# Approved Death Mark recipient rule

2026-11-09 JST. Reference: **D2R Reign of the Warlock 3.3.93847**. **Prepared, tested and explicitly approved by the user:** “Approve this Death Mark recipient rule.” [Approval record](../tools/reference/death-mark-recipient-approval.json). Death Mark's learned route remains disabled. The roster remains **211/240 handlers, Warlock 2/30; no class certified**.

The pinned skill row375 supplies these signed penalties at effective rank `r`:

| Recipient stat | Value |
| --- | --- |
| Armor class | `−50 − 35 × (r − 1)` |
| Flat physical damage reduction | `−5 − 2 × (r − 1)` |
| Flat magic/elemental damage reduction | `−5 − 2 × (r − 1)` |
| Duration | `125 + 13 × (r − 1)` frames, at 25 frames/second |

Proposed application: add the armor penalty to flat armor alongside Inner Sight, before the common armor percentage calculation and existing nonnegative final clamp. Combine each flat damage-reduction penalty with the recipient's corresponding item reduction. A negative total increases each already-present matching damage component **before its own resistance**. It does not create an absent component and does not affect poison. Positive total reduction retains the existing damage-rate scaling; negative total reduction stays unscaled. Keep Sanctuary's safe zero and integer-maximum clamps rather than reproducing legacy overflow.

State212 is not a curse and occupies its own slot, coexisting with ordinary curses and Inner Sight. The proposed replacement rule rejects a weaker active mark, refreshes an equal mark while retaining its original creator, and replaces it with a stronger mark. Duration uses the pinned effective rank. Death or deletion of the recipient removes the state; an absolute saved deadline continues to expire without a recovery/reset on reload. No pet/player payment or learned admission is added by this prepared recipient operation.

The prepared world scope uses the existing hostile ordinary-NPC predicate. Player/PvP targets, enemy-owned pets, unclassified species, and full current-client stat-graph parity are outside the verified scope.

## Evidence and remaining gap

The user's two Death Mark casts unlocked the full pet callback and enemy command helper; both are saved and previously compared. The new bounded read of direct recipient callees preserved 233 bytes of helper `3afe80` and 1,221 bytes of state factory `3a80c0`. Helper `3afe80` registers aura **events**, not numeric stats. The compiled row has six aura-stat IDs at offset `0x74`: `31,34,35,−1,−1,−1`; its four event IDs at `0xa4` are all `−1`, with four zero event functions at `0xac`. Thus no Death Mark aura event is registered through that helper.

Numeric binder `45f8d0` remains unreadable across all six unwind fragments. Its current stat binding and the complete current signed-reduction execution path have **not** been verified. The older retained reducer scales positive reductions only and subtracts negative reductions before resistance, supporting this proposal; it does not establish current-build parity. Replacement/creator semantics in this proposal are explicit Sanctuary choices, not a new current-factory equivalence claim. No protections, process input or game memory were changed to obtain the source.

## Verification checkpoint

The focused native receipt is [death-mark-debuff-native-evidence.json](../tools/reference/death-mark-debuff-native-evidence.json). It records the exact source and compiled artifacts, three boots, two actual reloads and three saves. Checks cover all 255 supported rank formulas, actual armor and native damage hooks, per-component resistance and immunity, poison exclusion, absent-component guards, positive reduction scaling, safe saturation, rank/creator/deadline replacement, coexistence with an ordinary curse and Inner Sight, expired-state pruning and atomic rejection of a malformed restore. The saved rank255 mark uses its natural 137.08-second duration; there is no artificial timer hold. Existing original-item/resource regression checks run in the same disposable host. These are Sanctuary proposal tests, not a comparison against the protected numeric binder.

Installed artifacts, existing saves and configuration remain untouched. Persistence profile247 stores only this prepared state. Target effect `0x375A` is an identified placeholder; final cast, warp/landing, mark/expiry and audio remain pending in the separate effects register. Talent and left/right/quick icons remain separate pending assets.

Next: connect the approved recipient to the already-compared cast composition. Actual GenericPet controller/command consumption, action interruption, startup/payment and left/right/quick input still precede learned Death Mark admission. Reuse the existing live demon placement and status helpers; do not repeat solved callback/source matrices.
