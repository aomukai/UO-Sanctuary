# Ordinary chilled corpse rule — proposed Sanctuary adaptation

**Pending user decision.** Reference: D2R Reign of the Warlock **3.3.93847**. The saved reference establishes ordinary cold duration/slow and existing freeze behavior, but does not establish a reliable current ordinary-chill corpse destruction chance. The older raw SHATTER/deadCol branch is not treated as proof. No newer executable or CASC is inspected.

Proposed rule:

- When a monster actually creates a corpse while its ordinary `ColdStates` chill is active, roll once with a **20%** chance to mark that corpse shattered. The killing blow can use any damage type. Existing immunity, Cannot Be Frozen, duration and expiry rules decide whether the chill exists; this adds no new chill.
- Existing frozen and Holy Freeze shatter rules retain their result and take no extra roll. Expired/removed chill and unchilled deaths take no roll. This rule covers ordinary chill represented by the shared state service, not unidentified legacy slow effects.
- Store the result once in the existing `CorpseSkillRecord.Shattered` flag. Repeated capture, access and reload never reroll it. Existing corpse consumers, including Necromancer summons/explosions, use their shared availability guards. Their existing exclusions and reservation rules remain intact.
- Use one draw from the existing server RNG for an eligible death. No new random stream, persistence profile or lifetime controller is introduced.
- Preserve corpse contents and loot rights. Shattering makes the body unavailable for skills; it does not destroy loot. Historical corpses retain their recorded state. Existing art/effect placeholders remain pending a separate asset pass.
- Use the existing world RNG for one 0–99 roll at first eligible corpse capture; values 0–19 shatter. No extra random stream, timer, persistence profile or entity serializer is introduced.

The 20% chance, death-time roll and killing-blow independence are **explicit design choices**, not claimed recovered D2R behavior. This closes the playable ordinary-chill rule without guessing silently. Frozen/Holy Freeze producers remain separate.

The candidate is implemented through an explicit per-creature enrollment used only by integration fixtures. **Ordinary play remains unenrolled and unchanged until approval.** The candidate does not expose a new talent, item, menu or button. After approval, normal-route activation and an actual Mage cold-kill check will be qualified separately; current proposal tests must not be presented as an enabled gameplay rule.

Verification is recorded in the [candidate checkpoint](ordinary-cold-corpse-checkpoint.md). Source limits remain in the [original reference-gap record](../tools/reference/cold-corpse-reference-gap.json), which is historical evidence and is not rewritten to imply a recovered current chance.
