# Learned Terror checkpoint

Mechanical reference: **D2R Reign of the Warlock 3.3.93847**; approved Sanctuary deviations take precedence. This is a learned-cast and supported native AI integration increment, not a complete skill/class or current-executable parity claim. Build 3/run 2 passes the relevant verification scope below.

Terror now uses the shared do30/no-stat curse receiver. It costs seven mana at release, uses fixed source radius 4 and lasts `200 + 25 × (rank − 1)` frames before the configured owner-facet divisor of 1/2/4 and curse resistance. The monster's spawn difficulty does not select game difficulty. Weaker same-skill ranks reject; equal ranks retain the original curse creator, refresh expiry and dispatch fresh AI initialization; higher ranks replace. Attract remains protected. Empty or fully resisted valid areas still pay once. Existing casting, target tracking, cancellation and recipient radius/LOS rules are reused.

The learned AI initializer captures the monster's existing native `Combatant` before the old control state is reset. That is an explicit adapter for the source dynamic-path target. It does not substitute the curse caster. A test positions the held target on the opposite side from the caster and observes an actual escape step away from that target. With no held target, the payload records no override; a separate test observes ordinary acquisition of a nearer threat. Howl retains its different caller contract: explicit caster override and stored Terror rank 1. The learned path does not add Howl's one-frame idle request; native initial event scheduling remains a named adapter.

Every accepted factory result initializes a genuine `TerrorAi` payload tagged with learned Terror and the effective rank. Save validation now accepts that tag alongside existing Howl and Taunt tags. Existing version-0 and bare legacy curse records remain readable; legacy bare Terror keeps its prior cast-origin fallback. Supported transitions currently cover the traced Skeleton/Zombie/Megademon AI families. Other native/source AI mappings reject with an explicit warning, rather than receiving an invented conversion.

Verification covers 3,600 source scalar/rank/creator/duration comparisons, independent facet-versus-spawn checks, refresh and replacement, unknown AI, Attract protection, geometry/LOS, cancellation, seven-mana payment and real Wormskull effective rank 21. Two reloads restore the learned phase/override/goal and resume actual native escape steps while retaining the legacy shape, mana and binding. Six real right/quick casts observe autonomous, non-damaging escape without directly invoking an AI decision, then actual control retirement after the normal eight-second rank-1 duration. Observations follow the running source clock and never advance inspected deadlines.

The run passes **54 native checks over three boots, two reloads and three saves**, plus four shared audit checks, all 71 Python tests and 573 unchanged named readiness decisions. Both clean rebuilds have zero warnings/errors; audit fingerprints remain 852/21. Runtime data, the staged kernel and all 106 installed artifacts are unchanged. The [receipt](../tools/reference/terror-cast-native-evidence.json) binds exact inputs and artifacts. All jobs are terminal.

The relevant regression also covers Dim Vision's learned receiver and saved chase, blind policy/world/natural timing, Howl and Taunt, shared class AI, and original-item/resource persistence. It does not claim the full extended 521-marker cohort. Effects/audio remain the identified generic curse marker/cast sound; `curseterror` and `cursecast` assets and talent/hotkey icons are separately marked needed in [effects](skill-effects.md) and [icons](skill-icons.md).

Remaining boundaries:

- The legacy AI initializer/dispatcher bodies are reviewed, not executed as a complete engine. Current-executable corroboration, dynamic-path versus native target identity and callback-bearing prior AI transitions remain open. Native `Combatant` cannot represent the source initializer's missile-target branch; that case is unsupported.
- Existing `SourceTerror` target scanning, LOS/ties, coordinate projection, path/blocked fallback, mode/cadence, initial event scheduling and RNG remain world adapters. This increment does not certify their exact source behavior or restore native step cooldowns across reloads.
- Enemy-owned summons, bard/PvP/special alignment, uninterruptible-state handling and unmapped native encounter AI remain unsupported or uncertain.
- No complete Necromancer/class/goal, deployment or final art is claimed. Pet upgrades and actual-safe-area parking remain queued.

Reproduce with the isolated `python3 tests/check_terror_cast.py`, selected-build runtime audits and `python3 tools/record_terror_cast.py --build 3 --run 2 --check`.

Nonblocking reference question: does the current 3.3.93847 executable retain the existing-target initializer when the monster was fighting someone other than the caster? The pinned legacy body and native adapter agree; this specific current-game observation remains uncorroborated.

Next: Confuse and Attract, whose do61/do59 application and allegiance/target contracts differ from this do30 receiver. Continue unaffected work while these explicit source/world gaps remain recorded.
