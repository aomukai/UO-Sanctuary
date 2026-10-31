# Native notifications for ordered monster damage

Reference: D2R Reign of the Warlock **3.3.93847**, with approved Sanctuary deviations taking precedence. This increment is a native UO bookkeeping adapter, not new evidence about the modern D2R executable. Learned Iron Golem remains disabled; the live prepared controller still uses immediate legacy delivery.

`PreparedMonsterNativeDamage.Notify` dispatches the notification portion of native `Mobile.Damage` before the ordered source HP write: region callback, spell interruption, real responsible-owner damage credit, disruptive action, paralysis clearing, visible damage, `OnDamage`, and mount notification. It never invokes incoming `Damage`, changes source HP itself, or triggers native death. The shared damage service now exposes this commitment boundary while retaining actual credit registration as its default.

The display-HP projection matches `SkillHealth.Damage`, including fractional carry and lethal capping. Native `willKill` is false at that capped exact-zero boundary; source return remains responsible for death. Sub-display loss generates no native notification. Region vetoes/modifiers and callbacks that mutate HP or delete the recipient raise explicit unsupported-context errors. This is fail-fast handling, not rollback: earlier callback side effects can have occurred.

## Verification

The [acceptance receipt](../tools/reference/prepared-native-damage-evidence.json), reproduced with `python3 tools/record_prepared_native_damage.py --check`, binds build5/run3 to:

- 256 paired native/ordered comparisons per boot: four starting life balances, eight damage sizes, four visible-damage settings, and mounted/unmounted recipients. Pre-HP callback ordering, credit visibility, paralysis/meditation, exactly-once `OnDamage`, zero incoming `Damage` calls, final life and deferred death are checked.
- Three region refusals and four callback HP-mutation refusals per boot, without subsequently applying incoming source HP loss.
- 73 retained native markers over three boots, two actual reloads and three saves, including allocation, source contact/return/phases, original items, minimum-only drains, owner credit/rewards, retaliation and connected world controls.
- Four separate Paladin/catalogue audit markers, 71 Python tests, unchanged 573 named decisions and all 106 installed artifacts. Audits cover 810/21 fingerprints. No full-class regression, old-content migration or deployment is claimed.

Two fixture corrections are retained in the local failed-run logs. Native death cleanup can temporarily see an old fractional remainder before its final clear; the notification trace therefore ends before HP/death cleanup, with final HP/death checked separately. The notification fixture now uses an explicit isolated hit-class counter so it cannot contaminate the retained test's shared-history precondition. Neither correction changes gameplay.

## Next checkpoint

Wire the real Iron actor's contact, event registry and return services, using this bridge at commitment. Preflight unsupported active item procs, Iron Maiden and native damage modifiers rather than silently skipping them. Supply real room/mode/recovery metadata and preserve credit after attacker removal. Native `BaseCreature.Damage` modifiers such as Evil Omen, Blood Oath and tamed-pet scaling are outside this notification bridge and require explicit actor support or refusal. Then migrate the world action and finish learned casting/lifecycle/client acceptance. Initial-poison current applicability, remaining procs/Sunder and modern Thorns coexistence remain uncertain or unsupported as previously recorded.

Coverage stays **185 handlers / Necromancer29; no class complete**. Revive keeps the living monster's repertoire; global UO-to-D2 spell conversion is separate. This bridge adds no independent final artwork: [effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) remain separate pending registers with existing identified placeholders. Working build and saves remain unchanged.
