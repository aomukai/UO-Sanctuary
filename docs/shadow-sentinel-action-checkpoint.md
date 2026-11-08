# Shadow Sentinel and Shock Web S2 checkpoint

2026-11-08 JST. **208/240 handlers; Assassin28/30; no class complete.** Original Shadow Sentinel257 and Shock Web256 now have connected S2 actions and automatic consumers. Learned Shadow Warrior/Master admission remains gated by other unfinished dependencies.

## Approved rule and implementation

The user explicitly answered **“Approve no additional NPC cooldown”** for both skills. Intrinsic original Shadows add no separate cooldown beyond their own S2 action and normal AI cadence. Current localdelay25/15 fields are retained and checked. `ShadowNpcCooldowns` supplies the explicit older-executor record for these two NPC skills only;player and item-proc policies are unchanged. [Approval and concrete rule](shadow-npc-cooldown-proposal.md).

Both actions reuse the own original S2 attack-rate clock,NPC startup/common executor and one-time release/recovery conductor. Sentinel do44 calls the existing prepared root-player blade factory with the actual Shadow rank/level/origin. Ground aim,live target movement,deleted-target cached aim and live rank changes are handled at release. Moving zero-footprint blades do not acquire the stationary sentries' occupied-tile placement adaptation.

Shock Web do43 calls its already qualified original-NPC lob/field producer through the same S2 route. Own identity/rank,world/private missile streams,scatter,landing,field damage,shared per-cast leases and saved expiry remain the existing services. A zero-shard close-point exclusion remains a successful do43 attempt. Both routes spend neither root-player nor NPC mana and do not borrow the root weapon's attack RNG for spell scatter.

Automatic GroundSkill consumers and Master action eligibility now recognize the skills. Actual world release/recovery and **natural Warrior AI selection/release/recovery** are verified for both. Natural Master selection and the earlier unqualified missile-range expressions remain separate acceptance work.

## Verification

Accepted **Content3 / Integration5 / snapshot6 / native-run4**,stage `.local/class-skills/shadow-sentinel-action`. Final production inputs match Content3's prebuild snapshot;later changes extended integration coverage only. Builds have zero warnings/errors.

**24 native success markers,three boots,two actual reloads,three saves.** Both original species exercise Sentinel's ground/moving/deleted target and live-rank paths,Shock Web's actual S2 shard emission,one-time release,no early allocation/free resources,ten cancellation paths,failed release range,original item/resource/wound/RNG persistence and no completed-action replay. Both skills pass explicit world and natural Warrior AI controls. Existing root blade factory/birth snapshot,S2 stationary sentries,blade spawn and own Shock Web field/damage/lease/restore/expiry tests pass in the same focused cohort.

One cancellation fixture initially called a generic stun API that did not resolve metadata for the generic Shadow profile. It now supplies an explicitly resolved25-frame prepared stun duration;that tests interruption without asserting generic stun eligibility. Prepared-only native2 passed before approval. The subsequent integration build found a helper named `World` shadowing `Server.World`;it was renamed. Native3 passed Sentinel AI and both consumers;native4 extended the same world/natural AI controls to Shock Web. Logs/snapshots and the intermediate approved Sentinel pass are retained. No old compiler matrices were rerun.

Installed build/configuration,existing saves and historical client artifacts pass the existing preservation check. Tests used disposable `/dev/shm` servers;no deployment occurred.

- [Native/preservation receipt](../tools/reference/shadow-sentinel-action-native-evidence.json)
- Runner: `SANCTUARY_TEST_SERVER=/media/aomukai/FILES/Sanctuary/.local/server python3 tests/check_shadow_sentinel_action.py`
- Receipt: `python3 tools/record_shadow_sentinel_action.py --check`

## Remaining work and assets

Modifier42 is next:it marks the original unique and schedules a death nova. The exact older-source mapping is now [reviewed](shadow-death-modifier-review.md),with body hashes in a [read-only receipt](../tools/reference/shadow-death-modifier-source-review.json). That review is not implementation or compiled/native verification. Retained dead-NPC spell delivery,terminal reason separation,unique recipient semantics and original cleanup must be covered together.

Fury's current callback/rewind reference gap and the unanswered unsigned NPC missile-critical proposal remain nonblocking. Do not adopt the older uptime-dependent rewind as modern D2R behavior. Full Shadow learned admission/class certification,current executable/PvP,arbitrary callback mutation and remaining Master/range/item contexts remain unverified.

Presentation remains identified placeholders:shared S2/cast cues,`0x23B` summon sound,blade travel/contact and web lob/field cues. Final effects/audio remain in [effects inventory](skill-effects.md);talent/hotkey text placeholders remain separately in [icons](skill-icons.md). Missing art does not gate mechanics.
