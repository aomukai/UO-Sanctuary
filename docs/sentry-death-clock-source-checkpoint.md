# Sentry death-clock source checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first.

The existing complete death-entry, cleanup, state and owner bodies now compose with the unchanged NPC frame-bonus routine, ordinary animation scheduler, dead-unit classifier, event dispatcher, ENDANIM callback and death-end body. The event enumeration comes directly from its pinned header. The older reconstructed engine and current owned animation metadata retain their distinct authority.

All **90 contexts** pass with ASan/UBSan and empty runtime stderr. They cover frame counts 1/23/25, rates 0/128/256/341/512, frozen/unfrozen actors and three inherited-stat values. At the current `LGDTHTH` inputs of 23 frames and speed 256, animation completion occurs 23 game frames after entry. A frozen dead actor still receives completion. The same registered NPC, owner and inherited root stats remain after mode changes from DEATH to DEAD.

The harness retains explicit services for outer mode change, rate/animation initialization, callback lookup and event storage/execution. Chart rows and scheduler selection are checked source inputs; the full outer mode-change function and current executable are not executed. The result therefore does not settle every modern modifier or callback order.

No production C#, balance data, native kernel, installed build or save changes are part of this source increment. The preceding [prepared corpse acceptance](sentry-corpse-pool-checkpoint.md) remains the native evidence for an explicitly supplied completed-death boundary. This source proof does not turn that boundary into a naturally invoked retirement path.

Next: trace the invocation and modern corpse-pool insertion phase, then connect and verify natural shot exhaustion/replacement, pending death animation, save/reload and issued-bolt ownership. Keep public Charged Bolt Sentry gated until its required casting and lifecycle behavior is supported. Effects/audio and talent/hotkey icons remain separate pending assets. No class is certified complete; coverage remains 187/240, Assassin 18/30.

See [source evidence](../tools/reference/sentry-death-clock-source-evidence.json). Raw licensed animation data and generated probe binaries remain local.
