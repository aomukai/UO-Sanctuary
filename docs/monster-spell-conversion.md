# Monster spell conversion

Recorded user scope clarification, 2026-10-30:

> monster spells need to be changed for everything in UO. all monsters need their UO spells taken and be given D2 spells. that's a thing of its own.

This is a separate game-wide task, currently recorded for later implementation. It covers all UO monsters, including their ordinary wild versions. Replace their UO spell repertoires with explicitly assigned D2 spells and D2 combat rules. Converting only revived creatures would leave the underlying task unfinished.

**Latest inheritance decision (reconfirmed 2026-10-31 after returning to COSMIC):** Revive assumes that the monster can use whatever it could while alive. Retain its existing repertoire now, including native spells while world conversion is pending. Later, change the shared monster implementation to D2 spells; both living and revived instances use that implementation. Global conversion and individual replacement-spell design are not prerequisites for implementing Revive. Do not add a Revive-only spell whitelist or duplicate the repertoire in the corpse snapshot.

Keep compatible creature identity, movement, AI structure and presentation where useful. Retaining those pieces does not authorize retaining a vanilla UO spell's combat mechanics. Audit generic MageAI selection, custom caster overrides and other spell dispatch paths so an unconverted fallback cannot bypass a converted repertoire. Native special abilities such as breath require their own compatibility review under the approved reuse/adaptation policy; do not assume every special ability is a UO spell.

Assign repertoires by monster role and relative strength, using the pinned D2R 3.3.93847 data and approved Sanctuary deviations. Record the selected spells, effective ranks or monster-specific values, damage channels, targeting, timing, resource rules and difficulty scaling. No blanket player-skill rank or numerical replacement is established by this scope decision.

Generic UO Heal, Greater Heal and Cure belong to this later world conversion; do not remove them only from revived versions in the meantime. The user separately approves explicitly authored special monsters that heal allies or resurrect corpses, such as the lich concept. Track those as special abilities when designing the converted repertoire. Existing D2-specific healing remains under its own rules; automatic potion-consuming AI has not been specified.

Revive reuses the monster's current repertoire and shared ability implementations, with Revive-specific ownership, hostility, lifecycle and stat rules layered on top. The latest instruction includes existing special abilities; it supersedes the earlier proposal to leave inheritance of healing/resurrection undecided. Historical D2's prohibition on revived resurrection is therefore not an extra exclusion to introduce here. An actual technical failure still needs a specific explanation, but an unconverted spell repertoire by itself is not such a failure. Ordinary corpse/boss eligibility remains separate from which abilities an eligible monster retains.

Implementation should inventory actual dispatchers, assign and convert one reviewed monster role at a time, then verify wild and revived versions, interruption, friendly targeting, resistance/immunity, delayed callbacks, difficulty and persistence. The [Revive behavior notes](revive-behavior-replacements.md) contain candidate conflicts and reusable services, not acceptance of this global task. No runtime changes or new test results are claimed at this checkpoint.
