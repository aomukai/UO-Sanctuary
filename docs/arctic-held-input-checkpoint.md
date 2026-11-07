# Learned Arctic Blast checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. The user separately [approved Arctic Blast's input rule](arctic-approved-input-rule.md). Its learned left/right/quick route now shares the existing Inferno transport and sequence controller, while selecting Arctic's own cold emitter and packet.

A fresh rank-one press requires six mana and spends 0.375. The first stream is emitted at frame 12 (0.48 seconds), then held renewals occur every two 25-Hz frames. Heartbeats only renew aim/lease; they cannot restart a released or expired press. Release stops new emissions, and a fresh press starts a new windup. Live ranks/costs affect later invocations; existing streams retain their captured state. Normal one-shot routes explicitly refuse these channels.

The cold packet, Cyclone Armor synergy, range/lifetime, chill and DamageRate41 remain separate from Inferno's fire values. The [verified potion receiver](arctic-potion-checkpoint.md) is unchanged. Patch42 extends existing client gestures and sends the captured ability, ending a gesture on rebinding rather than changing its skill midway.

**Wider verification is running for Content 1 / Integration 2 / snapshot 2.** Both server projects build with zero warnings/errors. The expanded targeted cohort passes **54 checks across three boots, two reloads and three saves**. It covers both Arctic and retained Inferno raw wire sources 0/1/9, threshold/once-only fractional payment, windup/renewal/exhaustion, short release/reentry, live soft ranks, independent fired damage and actual saved streams. Arctic additionally verifies changed aim, Stop, movement, rebinding and potion compatibility. Two distinct sockets keep the actual saved casters connected at the same world-save boundary; held intent does not replay after restart.

The initially passing C1/I1 cohort is preserved separately. I2 expands test coverage; production Content1 is unchanged. The review helper's initial attempt to decode a client binary as text was corrected before receipt verification. There have been no native or client build failures in this stage.

The client compiles with **44 existing project warnings and zero errors**; no dependency audit is claimed. All **42 patches over 42 paths** reverse to the exact pinned base and reapply to the exact built inputs. Actual rendered mouse/cursor/animation behavior remains unverified. The new patch retains all 41 earlier patches unchanged and uses the existing protocol-7 envelopes.

Coverage is **204/240 handlers**, with **30/30 Druid talents present**. This does not certify every Druid interaction or complete a class. Exact modern D2R input chronology is not asserted. Modern Slow Missiles damage ordering refuses before entry; foreign/item-only suppliers, source-to-UO geometry, global cross-owner chronology, owner/PvP and final rendered acceptance retain their recorded limits. Existing cast/stream/audio and tree/hotkey assets remain separate placeholders. No installed build or saves are deployed or migrated.

Evidence: [server scope review](../tools/reference/arctic-held-input-review-evidence.json), [client build/patch review](../tools/reference/arctic-held-input-client-evidence.json). Current execution handles and next steps are saved under `.local/class-skills/arctic-held-input/execution-state.json`.
