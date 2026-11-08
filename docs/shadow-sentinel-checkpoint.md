# Prepared Shadow Blade Sentinel checkpoint

2026-11-08 JST. **208/240 handlers; Assassin28/30; no class complete.** The prepared nested Sentinel factory works. Its own S2 action/current localdelay/AI and learned Shadow admission remain unfinished.

## Implemented and reused

The full older `sub_6FCF8610` ascends a casting monster's owner chain before summon registration,base and passive inheritance. `AITHINK_Fn102_BladeCreeper` later uses that resolved owner for the first weapon missile,then reassigns the missile to the blade actor. For a Shadow Sentinel,this means **root player ownership/base/passives/weapon/roster**,with **Shadow intrinsic rank,current level and origin** supplied separately. Copying Shadow Fury's own-claw collector would have been wrong here.

`ShadowBladeSentinels.CreatePrepared` reuses the existing actual blade backend,without invoking a paid player cast or borrowing the root player's learned Sentinel rank. A root with rank0 can receive the blade produced by its original Shadow's rank12. Existing blade controller,movement,following missile,lifetime,roster and offline rules remain the shared services. No player/Shadow mana or Shadow critical RNG is spent by this prepared adapter. The first missile still uses the existing root-player critical policy;the separate proposed NPC missile policy is not enabled.

The shared factory now applies the approved **snapshot-on-summon rule**: root weapon damage,critical flag and inherited AR are captured when the blade is created,instead of reading live gear at the first subsequent AI frame. A saved optional first-missile snapshot is consumed once when that frame arms the following missile. Gear changes between birth and arming cannot rewrite it. Old/synthetic saved bodies without this optional field retain their existing fallback. New optional context fields retain the root's base-stat level separately from the caster-level override and record caster provenance. Pre-arm difficulty rescale updates the deferred base stats without recalculating its weapon packet.

Reference remains **D2R Reign of the Warlock3.3.93847**,with the documented older D2MOO bridge. Snapshot timing deliberately follows Sanctuary's approved rule;current executable parity is not claimed.

## Verification

Accepted **Content1 / Integration2 / snapshot3 / native-run2**,stage `.local/class-skills/shadow-sentinel`. Content inputs match their original prebuild snapshot;only the test fixture changed afterward. Both builds have zero warnings/errors.

**27 native success markers,three boots,two actual reloads,three saves.** Both original Shadow species produce correctly rooted prepared blades with their own ranks/origins. Tests cover zero learned root Sentinel rank,no mana/Shadow RNG debit,shared roster,birth weapon capture,gear removal before first AI,once-only arming,pre-arm difficulty rescale and exact pending packets saved/reloaded while the root is unarmed. Retained live player blade spawn,following damage and lifecycle tests also pass. Installed artifacts/configuration,saves and historical client files pass the existing preservation check;tests use disposable `/dev/shm` servers. No deployment occurred.

Existing full summon/FIFO5208 and blade7781 source proofs are authenticated and reused;no old compiler matrices were rerun. The initial native fixture accidentally inherited all rank1talents from the common test player. It now resets allocated points before training Shadow prerequisites,so rank0root/own-rank separation is actually tested. Failed logs/inputs remain in the stage.

- [Native evidence and preservation](../tools/reference/shadow-sentinel-native-evidence.json)
- Runner: `SANCTUARY_TEST_SERVER=/media/aomukai/FILES/Sanctuary/.local/server python3 tests/check_shadow_sentinel.py`
- Receipt: `python3 tools/record_shadow_sentinel.py --check`

## Fury gap and next work

Twelve new composed older-source controls combine the actual mode14 callback,sequence,do48,start26,state removal and event rewind. Under the declared bounded event host,the same successful first shot at frame10 ends at frame26 when started at game epoch1,but at frame12 when started at epoch1000. The source sequence's pose frame is being consumed by a helper that interprets it as an epoch. These controls are **not modern D2R observations**,and the event host's services/limits are explicit. [Evidence](../tools/reference/shadow-fury-callback-source-evidence.json). Missing service declarations in the first harness were corrected;failed source/compiler logs are retained.

Automatic Fury rewind behavior is therefore parked as an unsupported reference gap. Work moved to Sentinel rather than implementing uptime-dependent combat. The earlier [unsigned-critical proposal](shadow-missile-critical-proposal.md) remains unanswered and unconnected to gameplay. A current routine/observation or an explicit adaptation is needed before automatic Fury admission.

Next finish Sentinel's own S2 cast and AI;its current localdelay25 versus the older NPC executor's policy must remain explicit. Then modifier42death and learned Shadow admission. The earlier Shock Web localdelay15 question remains nonblocking. Different root/caster levels through the full native factory,arbitrary callback/stat mutations,current executable/PvP and full Shadow certification still need verification.

Effects/audio remain the identified existing blade travel/cast/contact placeholders;the prepared factory introduces no new artwork identity. Final effects/audio and talent/hotkey icons stay in their separate inventories and do not block mechanics.
