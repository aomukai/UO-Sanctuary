# Shadow NPC cooldown proposal

Status: **approved by the user,2026-11-08 JST.** Exact answer: “Approve no additional NPC cooldown”. Reference: D2R Reign of the Warlock3.3.93847,current tables plus the pinned older common executor.

The current rows list `localdelay=25` for Blade Sentinel257 and `localdelay=15` for Shock Web256. The older common skill executor applies its delay only to player units. That older code does not establish how the current NPC executor reads the newer field.

Approved explicit Sanctuary rule for **intrinsic original Shadows using these two skills**: add no separate skill cooldown. Their own attack-rate S2 windup/recovery and normal AI cadence still apply. Each completed action can release only once. No player or item-proc cooldown policy changes.

Blade Sentinel's concrete prepared path now implements this older NPC execution policy: `ShadowActions.BeginBladeSentinel` uses the own S2 plan and generic NPC startup/do44 wrapper,then reuses the shared root-owned blade factory. `LegacyRecord(false,1)` supplies only the older execution fields;it does not claim that current localdelay is verified. `ShadowNpcCooldowns` now supplies the explicitly approved records for both skill IDs. Sentinel’s automatic route is enabled for world/AI verification. Shock Web’s separate action consumer remains unfinished;its cooldown decision is resolved.

The prepared path passes both original species,ground/live/deleted target and live-rank cases,one-time allocation,zero NPC/player mana,ten cancellations and range failure. Shared S2 sentries and blade factory/spawn checks pass through two actual reloads. This makes the proposal concrete without exposing an unapproved automatic route.

This approval authorizes the no-extra-cooldown rule for both skills. Automatic selection/world timing tests and Shadow class completion remain separate requirements;the decision alone does not certify them.
