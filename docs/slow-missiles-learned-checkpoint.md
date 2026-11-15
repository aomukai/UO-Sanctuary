# Slow Missiles: approved learned input checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations first. The [exact user decision](../tools/reference/slow-missiles-rule-approval.json) approves the complete [rule](slow-missiles-rule-proposal.md), including native target classification, packet ordering and the deferred legacy-monster boundary. No new live reference reads or newer-build substitutions were made.

## Implemented

Learned Amazon right-click and quick-cast now enter the existing caster-centered SC/FCR action. The shared client metadata identifies Slow Missiles as a self-cast; left binding remains refused. Existing rules handle unpaid windup, cancellation, current effective rank at release, one five-mana payment, recipient priority and independent captured missile speed/damage. Actual encounter missiles and Graveyard Lich firebolts retain their established factories and typed impact.

No timer, persistence profile, serialization version, protocol extension or client patch was added. The isolated client build reuses all 44 accepted patches and client sources. Its only shared change is SelfTarget membership. Source-warning and package-audit message sets match the baseline; `--no-restore` emits 20 warning occurrences versus 44 baseline occurrences, with zero errors. This is a build/input check, not a graphical playtest or deployment.

## Verified

Content build 1, integration build 5, source snapshot 5 and native run 5 pass **201 markers, 90 authenticated connections, three saved boots and two actual reloads**. The suite includes **57 actual socket casts**: nine new Slow Missiles casts and 48 retained Abyss, Flame Wave, Miasma Chain, Miasma Bolt and Mirrored Blades casts. [Native receipt](../tools/reference/slow-missiles-learned-native-evidence.json); [client receipt](../tools/reference/slow-missiles-learned-client-evidence.json).

- Dagger right-click, unarmed quick-cast and original Magefist FCR quick-cast each pass on all three boots. They verify the respective SC action clocks, unpaid windup, exactly one payment and automatic incoming slowed-firebolt life loss, without manually pumping the world clock.
- Public dispatch verifies self-targeting, bindings, cancellation, insufficient mana at release, and existing respec/Stop/class-change/death behavior. Released debuffs remain on recipients according to the approved rule.
- Original Amazon identity, dagger, fractional life/mana, bindings and recipient creator/rank/payload/UTC expiry remain unchanged across two reloads. Fresh learned casts work after loading without replaying transient windup.
- Prepared native/encounter controls retain rank priority, independent speed/damage, actual firebolt travel/impact, captured packet immutability, six-channel integer scaling, unchanged durations, CanSlow-zero damage reduction and atomic UTC restore.

Four failed native runs are preserved with their exact sources and assemblies. Early fixture expectations confused dagger and unarmed action clocks. The single-firebolt delivery fixture also inherited Avoid from an all-skills helper, making required life loss random; it now trains only this skill and prerequisites and asserts zero Avoid chance. Rich reload diagnostics isolated the second-reload failure to life loss while identity, gear, mana, bindings and debuff remained correct. Saved targets were resuming ordinary AI because test freeze/stop controls are transient; existing WorldLoad fixture hooks now reapply those controls before persistence inspection. Gameplay content remained build 1 throughout these fixture corrections.

## Remaining scope

**236/240 handlers are present; Amazon has 30/30. No class is certified.** Handler admission and these scoped checks do not establish every class interaction or graphical acceptance. Blood Oath, Engorge, Blood Boil and Consume remain separate frozen pending proposals.

Legacy UO instantaneous ranged hits and native spell/breath timers remain on the explicitly approved [monster conversion track](monster-combat-conversion-notes.md), and are not claimed slowed. Unknown native classifications, unqualified special/child producers, PvP and broader item-property contexts remain explicit gaps. Current protected D2R ordering and native geometry/classification remain approved adaptations.

Installed protection checks retain **106 artifacts, 38 save files and 20 configuration files**. No deployment or save migration occurred. Final cast/recipient/projectile/audio effects remain in [effects](skill-effects.md); talent and hotkey icons remain in [icons](skill-icons.md). Existing markers/sounds are placeholders and no images were generated.
