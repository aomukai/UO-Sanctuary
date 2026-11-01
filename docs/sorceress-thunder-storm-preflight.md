# Thunder Storm acceptance preflight

Reference: D2R Reign of the Warlock **3.3.93847**, approved Sanctuary deviations first. This is preparation for the next Sorceress contract, with no new Storm runtime acceptance.

Reuse the current saved `ThunderStormState`, `SorceressEffects` profile/scheduler, serial-order target cycling, generic typed spell damage and the retained `ThunderStormChecks`. Existing controls already cover natural rank1/rank20 timing, cast-rank retention, once-only mana, radius/cycling and both reloads. Do not replace those with a duplicate controller.

Current state captures rank/radius/interval/deadlines; each strike evaluates `SkillMechanics.Values` at retained rank and applies current mastery/gear through `SkillCombat.Hit`. Unlike the former Chain player path, that generic spell path already provides separate potion components. Establish the source callback's capture/live supplier order before changing damage timing or snapshotting the entire buff.

The current timer checks profile/class/rank, map/town and deadline but does not explicitly require an active player connection before selecting/dealing damage. A disconnected owner may remain in the world before logout/internalization. Add an actual-disconnect control and preserve deadline advancement/no burst replay rather than assuming internalization always happens immediately. Review dead/facet/class/respec/recast/expiry behavior through existing profile services.

Next: verify pinned source duration/interval/radius/damage/target rules, real self right/quick input and payment, live gear/synergy/mastery/potion behavior, actual disconnect/no replay, native ordinary/immune targets and two real save/reload cycles. Full modern callback/event/filter geometry, PvP/Sunder, graphical and old-DLL/unheld-save limits stay explicit. Effects/status/audio and tree/hotkey icons remain separate; Inferno stays individually deferred.
