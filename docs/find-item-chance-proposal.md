# Find Item chance policy proposal

**Adopted — 2026-11-18 JST.** The user approved the existing curve. [Exact decision](../tools/reference/find-item-chance-approval.json). The text below is the reviewed proposal;chance is no longer an open Sanctuary policy question. Exact modern executable provenance and the separate target/treasure boundaries remain explicit.

Reference: D2R Reign of the Warlock3.3.93847. This is a proposed Sanctuary decision,not a newly verified modern executable result.

Retain the current runtime's two-stage integer curve:

```text
base = 5 + floor(55 * min(100, floor(110 * effective_rank / (effective_rank + 6))) / 100)
chance = base + hard_Find_Potion_points
```

The base chances at ranks1–20 are **13,19,24,29,32,35,37,39,41,42,44,45,46,47,47,49,49,50,50,51percent**. Each hard Find Potion point adds one percentage point. Item-granted Find Item ranks affect the base curve;item-granted Find Potion ranks do not add synergy. For example,rank20Find Item with20hard Find Potion points gives71percent. The existing100-sided roll and one-attempt corpse state remain.

The [saved engine comparison](../tools/reference/find-item-engine-evidence.json) binds all20historical Blizzard base chances,624engine contexts and the original source hashes. The [native receipt](../tools/reference/find-item-native-evidence.json) covers real cost,success/failure,one-attempt exclusion,captured ordinary drop context and original loot/resources across two reloads. The current Barbarian473-result cohort retains its ordinary learned right/quick routes. `SkillValues.Diminishing` and `BarbarianFindItem.Release` already implement this rule;no gameplay change or new rounding helper is proposed.

The unresolved reference conflict is the older engine helper's rounding:rank2can yield20rather than19. Other current-build tooltip readings support the retained rounding for their own skills,but do not prove Find Item's modern callback. Adopting this proposal makes the current curve an explicit Sanctuary rule rather than a claim of exact modern provenance.

This decision covers chance only. It does not approve new auto-approach,ground acquisition,range,quest/boss/elite/changed-level treasure rules,or expand item-generation support. Their individual boundaries remain recorded. Mana7on successful start,attempt consumption,old loot preservation and existing supported treasure generation retain their separate evidence. The installed build already uses the proposed curve;while the question is open,it remains labeled uncertain.
