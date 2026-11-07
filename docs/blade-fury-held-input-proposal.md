# Blade Fury held-input proposal

Status: **review-only; not approved or enabled**. Mechanical reference is **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. Inferno and Arctic Blast approvals do not cover Blade Fury. No production C#, runtime data, handler registration, client patch or installed build changes in this proposal.

The candidate reuses `BladeFuryModes`, its checked SQ23 sequence, start26/do48 resource composition and the existing saved weapon-flight service. The missing modern outer input acceptance rule remains unverified. The following policy is an explicit Sanctuary proposal, not a claim that the current D2R client uses this scheduler.

| Boundary | Proposed behavior |
| --- | --- |
| Fresh physical press | Require six mana, or the current per-blade cost if higher. Start a fresh windup. No upfront debit. |
| Hold | Before each due sequence action, supply one accepted start callback, except at exact `NextShot` equality; at equality advance the existing do event only. Preserve its strict-less-than emission test and rewind. Client heartbeats would renew only an input lease, never create extra resource callbacks. |
| Release | Cancel the mode and temporary lease immediately. Stop new blades. Already-issued blades retain their independent damage and lifetime. |
| New press after release | Discard the old transient channel and start a fresh windup/threshold. Do not enter the unresolved active/future-shot fresh rewind path. |
| Insufficient mana | End the gesture; require a fresh press to restart. No extra blade or refund. |
| Gear and aim | Read current effective rank and per-blade cost for subsequent emissions. Existing blades keep their captured packets and direction. |
| Movement, interruption, disconnect or restart | Reuse the existing cancellation/ownership rules. Held intent must not be saved or replayed. Actual transport integration and its checks are future work. |

The native candidate measures **first blade at frame12 (0.48 seconds), then every five 25-Hz frames (0.20 seconds)**. Effective rank1 costs **one mana per blade**, or **five mana per second after windup**; higher ranks cost `(8 + rank - 1) / 8` mana per blade. A two-second hold issues eight blades and pays eight mana. Six mana issues six blades before exhaustion. These numbers are consequences of this proposed policy and the pinned components, not verified modern D2R input timing.

The test-only fixture checks one-, three- and seven-frame catch-up chunks, duplicate same-frame delivery, failed low-mana entry, exhaustion, early release, fresh windup, actual Tarnhelm rank/cost changes, changing aim without rewriting earlier blades, and independent damage after release. Eight original released packets and the same wounded owner's exact fractional resources are retained through two real reloads. Their inspection deadline is deliberately moved into the future; this is not a claim of uninterrupted real-time flight through downtime. Held input is not serialized. No raw channel packets or graphical-client gestures are tested here.

The first native run failed a **test expectation** of six frames between blades: the unchanged source composition actually emitted at12,17,22,27,32,37,42,47. That diagnostic, original fixture, pre-build hashes, log and integration DLL are archived under `.local/class-skills/blade-fury-held-candidate/failed-cadence-i1`. The revised expectation records the measured five-frame cadence. Production DLL bytes remain identical to the accepted Arctic checkpoint.

**Verified candidate:** Integration2/snapshot2 passes **12 focused and231 retained markers**, each across three boots/two actual reloads/three saves. All228 prior Fury/sentry markers remain, including36 raw sentry inputs. All71Python tests pass; the integration build has zero warnings/errors. Production DLL/kernel/data and42client patches are unchanged. Installed106artifacts,38saves and20configs retain their protected hashes. No new runtime item audit is claimed for this test-only change. Recheck exact inputs with `python3 tools/record_blade_fury_held_candidate.py --check` and [the candidate receipt](../tools/reference/blade-fury-held-candidate-native-evidence.json).

This proposal adds no playable handler: coverage remains **204/240**, Assassin24/30, with no certified complete class or deployment. After approval, implement actual press/hold/aim/release, binding and lease validation, natural scheduling, interruptions and raw-input/save regression before enabling learned Blade Fury. Keep its existing modern AR/mastery/Slow Missiles ordering, geometry, item/proc, owner/PvP and rendered-client limits explicit.

Final startup/loop/release poses, bladefragment1/2 travel/contact and audio remain missing. Native weapon-flight presentation is an identified placeholder. Talent and left/right/quick icons remain separately pending in [effects](skill-effects.md) and [icons](skill-icons.md). No image generation is performed. This design question is nonblocking; continue unaffected class work while it is unanswered.
