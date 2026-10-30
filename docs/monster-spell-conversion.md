# Monster spell conversion

Recorded user scope clarification, 2026-10-30:

> monster spells need to be changed for everything in UO. all monsters need their UO spells taken and be given D2 spells. that's a thing of its own.

This is a separate game-wide task, currently recorded for later implementation. It covers all UO monsters, including their ordinary wild versions. Replace their UO spell repertoires with explicitly assigned D2 spells and D2 combat rules. Converting only revived creatures would leave the underlying task unfinished.

Keep compatible creature identity, movement, AI structure and presentation where useful. Retaining those pieces does not authorize retaining a vanilla UO spell's combat mechanics. Audit generic MageAI selection, custom caster overrides and other spell dispatch paths so an unconverted fallback cannot bypass a converted repertoire. Native special abilities such as breath require their own compatibility review under the approved reuse/adaptation policy; do not assume every special ability is a UO spell.

Assign repertoires by monster role and relative strength, using the pinned D2R 3.3.93847 data and approved Sanctuary deviations. Record the selected spells, effective ranks or monster-specific values, damage channels, targeting, timing, resource rules and difficulty scaling. No blanket player-skill rank or numerical replacement is established by this scope decision.

Generic UO Heal, Greater Heal and Cure belong to this world conversion. The user separately approves explicitly authored special monsters that heal allies or resurrect corpses, such as the lich concept. Track those as special abilities rather than leaving the generic UO spell bag active. Existing D2-specific healing remains under its own rules; automatic potion-consuming AI has not been specified.

Revive should reuse the monster's converted repertoire and shared ability implementations, with Revive-specific ownership, hostility, lifecycle, stat and ability-eligibility rules layered on top. Do not create a second caster conversion only for Revive, and do not automatically grant resurrection merely because the wild monster has it. An unfinished monster role is a specific unsupported context; it does not require delaying unrelated class skills until the entire world conversion is finished.

Implementation should inventory actual dispatchers, assign and convert one reviewed monster role at a time, then verify wild and revived versions, interruption, friendly targeting, resistance/immunity, delayed callbacks, difficulty and persistence. The [Revive behavior notes](revive-behavior-replacements.md) contain candidate conflicts and reusable services, not acceptance of this global task. No runtime changes or new test results are claimed at this checkpoint.
