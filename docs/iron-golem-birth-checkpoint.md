# Iron Golem birth checkpoint

Reference: D2R Reign of the Warlock **3.3.93847**, with approved Sanctuary decisions taking precedence. Branch: `codex/iron-golem-birth`. This component does not enable the learned Iron Golem cast; coverage remains **185 handlers / Necromancer 29**, with no completed class.

`IronGolemBirth.Prepare` now composes post-equipment stats with the shared summon level calculation, difficulty/game-context armor and attack additions, effective Golem Mastery, hard-point Clay/Blood/Fire Golem synergies, all ten current passive fields, thorns/fade aura values, default equipment-level request and absorption-aware Summon Resist additions. It reuses `ValkyrieCreation.Base` and `SourcePercentage` rather than introducing competing level or percentage formulas.

The creation order is important: do57 equips the original item before the base, passive and resistance helpers. The passive helper reads accrued maximum life and then **writes a base maximum-life value** and current life. The prepared result names those writes explicitly and keeps monlvl armor/AR additions separate from passive additions; these are operations to apply, not final accrued armor/AR totals. It must not be installed as the final accrued maximum while the item's statlists remain attached; that graph application and its callbacks are the next integration step.

Verification:

- Four complete unchanged older routines run under ASan/UBSan: percentage, summon base stats, passives and resistance. **36,720** birth contexts cover all ranks 1–255, owner levels 1/24/99, all three difficulties, both game contexts, four mastery/hard-synergy combinations and two fractional post-equip life inputs. **81** separate resistance contexts cover negative/zero/positive absorption and bonus values.
- The same numeric results pass in the native C# server. All ten current passive fields are retained; separate nonzero values verify their identity/order, without claiming modern sunder delivery. Invalid contexts are refused, hard synergies ignore effective item ranks, and zero Summon Resist avoids querying absorption.
- **9 native checks / 3 boots / 2 actual reloads / 3 saves** also retain the existing original sword/armor and exact resource callback fixtures. This introduces no persistence schema. Both clean builds have zero warnings/errors. Source/data snapshots match the tested assemblies and copied data.
- The first disposable attempt passed its initial checks but failed native shutdown because PingServer was disabled. The corrected attempt initializes it on an isolated port and exits cleanly on every boot. Failed logs are retained locally; they are not acceptance evidence.

The [native receipt](../tools/reference/iron-golem-birth-native-evidence.json) binds the successful logs, exact tested inputs and preserved installed artifacts. The [source receipt](../tools/reference/iron-golem-birth-source-evidence.json) identifies the declared services: post-equip getters, simple stat writes, successful allocation, asserted formula values, event/state recording and equipment request. It does not execute the complete item dependency graph, original formula VM or current executable. The older passive helper loops five slots; modern ten-slot inheritance remains a separate boundary. Source thorns registers attacked-in-melee function 6; the new numeric preparation does not register or deliver that event on an actor.

Next work:

1. Apply the prepared base/current-life writes through the actual equipped stat graph; verify the final accrued maximum and resource/regeneration callbacks with retained original items.
2. Bind retaliation and the remaining inherited item events/procs, preserving explicit unsupported contexts and the deferred skill-dependent effect ledger. Resolve modern sunder delivery separately.
3. Complete the Iron Golem actor, AI/attack timing, paid targeting/placement/replacement and owner/facet/logout/death/respec/save integration. Do not use a renamed Fire Golem as a substitute.
4. Verify the complete learned skill before claiming Necromancer completion. Continue unaffected roadmap work when a particular source mechanic is unavailable.

Final body, summon, thorns, item-contact/state effects and audio remain in [the effects register](skill-effects.md). Talent-tree and hotkey artwork remain separately in [the icon register](skill-icons.md). No new assets are generated, and missing art does not block mechanics. The installed working build and saves remain unchanged.
