# Consume source and numerical suppliers checkpoint

2026-11-15 JST. Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. This completes the imported-data/numerical capture component, **not Consume's learned cast**. Coverage remains **226/240 handlers, Warlock 17/30; no class certified**. Blood Oath, Engorge and Blood Boil adaptation decisions remain independently pending; do not reask or wait globally.

The missing demon-specific data is `monpet`, not an absent formula guessed from the skill description. Its complete JSON is now pinned from `blizzhackers/d2data` commit `fa1286949266498b294c40c2942521a7c0d7207a`, exact Git blob `3ceda4619056670e5be2344ecab826e80eb9cbe7`. All 21 non-line-number fields in the three original demon rows match `pinkufairy/D2R-Excel` commit `1f16064e09b97e3e65abd6943662207cff00b07f`, exact `monpet.txt` blob `141c0734c780878edf4bb4edfe4aef4a3330f3ad`. The TSV is stored losslessly as deterministic gzip (116,304→3,442 bytes), preserving its exact original bytes. [Provenance](../tools/reference/d2r-consume/sources.json), [importer](../tools/build_consume_bonuses.py), [boundary review](../tools/reference/consume-source-review.json). The original 28-file source manifest stays unchanged.

The imported runtime rows retain their original formulas and source monster indices 742/743/744. `ConsumePrepared` reuses `SkillValues` and the existing formula evaluator in an explicit caller context: supplied caster effective Consume rank, hard Death Mark points, and difficulty codes 1/2/3 for Normal/Nightmare/Hell. Only these monpet expressions receive a scoped `sksrc`→`skill` parser alias; no global formula grammar or other callback interpretation changes. This is a declared numerical interface, **not proof of the protected do159 caller**.

| Supplier | Verified values |
|---|---|
| Mana | 25 at rank 1, +1 per additional effective rank |
| Duration | 40 seconds at rank 1, +20 seconds per additional rank |
| Maximum life | +5% at rank 1, +1 percentage point per additional rank |
| Effective movement stat | Diminishing 15/28/34/40/46 at ranks 1/5/10/20/60 |
| Goatman damage | Consume rank ×1/3/5 +20 in Normal/Nightmare/Hell |
| Goatman crushing blow | 5 + hard Death Mark points |
| Tainted | Fire mastery min(rank+9,20), fire piercing min(rank+4,15) |
| Defiler | Magic mastery min(rank+9,20), magic piercing min(rank+4,10) |

Positive piercing values represent the source's resistance-reduction magnitude. No stat writer is installed yet. Unknown monster kinds, unsupported stat names/contexts, invalid ranks/difficulties and malformed immutable captures refuse explicitly. Other bound-monster rows remain in the full source blob for future work; they are not silently mapped onto these three rows.

**Content 1 / Integration 1 / source snapshot 1 / native run 1** passes 15 focused markers over three authenticated sessions, three boots/saves and **two reloads**, with zero build warnings/errors. Twenty literal common/difficulty/species/cap contexts and six malformed captures pass. A real Witch's Tarnhelm changes Consume 19→20 while soft Death Mark 4→5 leaves crushing blow at 9%; training a fifth hard point makes new captures 10%. Previous captures retain their original values. The three original learned demon birth snapshots, fractional wounds, RNG and controllers survive both parked reloads unchanged. Fixture JSON serializes numerical captures only; **production Consume buff persistence, consumption and slot occupancy are not claimed**. No new socket skill-input test or old source/compiler matrix is run.

The source `pettype` table also proves that `demon` and `binddemon` both belong to **pool 1**. The former code comment saying Bind Demon has a separate pool is corrected. Current behavior is unchanged because Bind Demon's actor is still unfinished; it must join the shared pool when admitted. Older tester notes report that Consume occupies that pool too. Implementing that occupancy remains a concrete next requirement, rather than making the buff a free fourth summon.

The pinned skill entry is right-only, targets pets and allows town casting. It uses state 208/group 9 and a 25-frame periodic/clear-aura path. Current do159's five retained fragments are unreadable; no live/process/protected-memory read was repeated. The approved town-parking rule therefore needs an explicit owned-roster target path for Consume. Do not quietly change it to an outside-town skill or let consumed pets reappear from parking.

Existing integration points are identified for the next component: `BattleOrders.Capture/Change` for percentage-preserving resource transitions; `ClassResources`' additive life-percentage pool; shared class running/walking delay; physical enhancement and element mastery/pierce; actual weapon crushing-blow registration; and saved `BattleCommand.PetOrder` for pool replacement. A buff must not lose its old maximum-life contribution before the removal transaction captures its life percentage. The current Druid form implementation documents and handles that same boundary. These are reuse findings, not unimplemented behaviors claimed as tested.

Next: implement the saved owner buff, actual stat consumers and combined slot replacement; qualify periodic expiry/removal, live and parked targeting, resource/payment/life transitions and eventual Bind Demon integration as one complete rule. Verify actual weapon/spell effects rather than only getters. Final Consume overlay/cast/audio and tree/right-hotkey icons remain separately missing; this capture-only stage emits no effect. The installed 106 artifacts, 38 saves, 20 configurations and 43 client patches are unchanged; no deployment, native/client rebuild, timer, persistence ID or serialization version is added.

The current [author-datamined Consume page](https://diabloforfans.com/skill/consume?lang=enUS) corroborates the imported bonus formulas and caps. [Older original tester notes](https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf) describe additive life, effective run/walk and slot occupancy but are version-unspecified. Those limits remain explicit; no unavailable callback is inferred solely from a tooltip.
