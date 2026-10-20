# T35 — Dungeon encounter grades

T35 adds spawnable encounters and bounded level 60–99 profiles. Permanent dungeon layouts, outdoor populations and automatic placement remain world-authoring work. Existing spawners and the T19 creature/difficulty catalogues retain their current distributions.

## Profiles and identity

Four authored UO counterparts each have four profiles: **Crypt Sentinel** (melee), **Bog Revenant** (tank), **Ossuary Archer** (physical missiles) and **Cinder Hierophant** (fire missiles). Profile IDs combine the family with `-60`, `-70`, `-80` or `-90`, covering 60–69, 70–79, 80–89 and 90–99 respectively.

Each spawn saves its profile, base level, rolled life/attack rating/defense, grade, champion kind, ordered modifiers, aura and leader serial. The existing players-1 archetype curve supplies offense, defense, primary damage, attack/impact/recovery timings, movement and XP. Grade bonuses apply at the resolved level. Higher levels retain non-overlapping life bands and nondecreasing offense/defense. The authored regional resistances, quick notice/scanning and reliable pursuit belong to these profiles alone.

| Grade | Resolved level | Composition | Mould chance |
| --- | --- | --- | --- |
| Ordinary | Base level | One creature | 0.1% |
| Champion | Base +2, capped at 99 | 2–4; all but the last are regular Champions | 1% each |
| Random unique | Base +3, capped at 99 | One boss and exactly five minions | 5% boss |
| Minion | Base +3, capped at 99 | Wild cohort member; saved leader identity | 0.1% each |
| Named super-unique | Authored base +3 | One named encounter | 10% |

The random elite factory chooses champion versus unique packs at 20%/80%. Explicit staff spawning can select either. Groups preflight clear cells within six tiles and common line of sight; failed placement leaves no partial pack. Reload never creates replacement minions. Corpses from these new families are not revivable; the earlier graveyard resurrection family remains separate.

Three named encounters reserve future boss reward hooks: **The Ossuary Warden** (97), **The Carrion Matriarch** (98), and **The Ashen Hierophant** (99). Their identities and signature modifiers are configurable. These are dungeon encounters, with no new act-boss system.

## Full elite roster

Champion kinds are Champion, Ghostly, Fanatic, Berserker and Possessed. Random uniques select one/two/three distinct compatible modifiers in Normal/Nightmare/Hell; selection order is saved. Magic Resistant is excluded from random Normal rolls, and Multishot requires a missile family.

The roster includes Aura Enchanted, Cursed, Cold Enchanted, Extra Fast, Fire Enchanted, Lightning Enchanted, Magic Resistant, Mana Burn, Multishot, Spectral Hit, Stone Skin, Extra Strong and Teleport. Elemental additions, chill/poison, mana loss, Amplify Damage, reactive lightning and death bursts execute in combat. Possessed rejects curses. Added resistance bonuses cannot create a third immunity or increase one of two existing immunities. Existing immunities remain; Ghostly retains its explicit physical-resistance override. Grade and modifier formulas follow the [Arreat Summit monster bonuses](https://classic.battle.net/diablo2exp/monsters/bonus.shtml).

Aura choices are Might, Holy Fire, Blessed Aim, Holy Freeze, Holy Shock, Conviction and Fanaticism. Friendly cohort members receive applicable bonuses; players and their controlled/summoned creatures receive hostile effects. The strongest emitter of each aura applies once, while different auras combine. Elemental pulses select one emitter per aura and target. Death, deletion, departure and loss of sight remove the aura. Holy Freeze slowing is independent of cold immunity. Amplify Damage saves an absolute UTC expiry, advances offline and clears on character death without occupying the food or potion slot.

Hits combine immediate components before resistance, flat magic reduction, Protection and hit recovery. Spectral poison uses the existing timed poison lane. Reactive lightning has eight traced bolts; Multishot launches a three-missile fan. Missiles follow their launch direction, collide with enemies and respect walls. A volley hits a given unit at most once. Deletion cancels outstanding missiles and actor timers.

## Loot

`encounter-treasure.json` is a separate projection of pinned LoD `MonStats` and `TreasureClassEx` tables. Reproduce it with:

```sh
python3 tools/build_encounters.py --source-dir .local/research/foundation-pinned --check
```

It retains positive and ordered negative Picks, NoDrop, weights, nested classes, level-based group upgrades outside Normal, inherited quality factors and fixed-point gold multipliers. Quality factors apply after the ordinary MF/minimum calculation. Existing ordinary treasure projections are unchanged.

Champion trees reserve one gear/gold draw and two potion draws. Random unique trees reserve one item draw and four potion draws. Authored named bosses reserve two item draws and four potion draws, within six terminal outcomes. Unmapped outcomes and successful Set/Unique **item** rolls stay empty; they never reroll or substitute. This leaves their item-quality slots ready for later content. Real monster level supplies item level.

Moulds remain one separate, MF-independent roll after ordinary treasure, sharing the saved reward-life claim with XP and gear. Minions use ordinary mould odds despite their increased XP/life. The dropped mould saves its encounter grade and uses the existing affix-frequency weights; levels 90–99 supply tier 8.

## Explicit UO adaptations

- Pack size is the agreed five minions, and all resolved levels cap at 99.
- Aura/curse radii map three LoD subtiles to one UO tile and cap at sight range. Fire death radii are 3/3/4 tiles; the cold nova is a six-tile, sight-checked burst. Source damage and duration come from the corresponding LoD formulas/tables.
- Teleport uses an eight-second cooldown, a clear visible landing within six tiles and the home leash. At low life it restores 25% maximum life. These three values are configurable in `encounters.json`.
- Fan side angles are ±0.22 radians; missiles travel at most 18 tiles with 40 ms collision steps. Holy Fire rounds to UO HP after applying the attack multiplier.
- Mana Burn implements intended mana damage and difficulty scaling without reproducing the legacy melee ×256 bug.

The selected skill/missile constants were checked against pinned `Skills.txt` and `Missiles.txt` at commit `45112569deb9384738ccafe5c24ebbb71f41c7c9`. SHA-256: Skills `59601208f3052205c175753038cedb6e338ef9bdc268f4e92a74b9c7b69cdc07`; Missiles `9d6794a50e8b1931e476a4d666bfe4048d7d1dada454f19659d74a8825adef53`. Cold death uses **coldunique**, rather than the Sorceress Frost Nova row.

## Staff testing

Examples (Game Master access; target clear ground within 12 tiles):

```text
[SpawnEncounter crypt-sentinel-60 Nightmare Champion
[SpawnEncounter ossuary-archer-90 Hell Unique
[SpawnEncounter ashen-hierophant Hell SuperUnique
```

`EncounterChecks` exercises all profiles/grades/difficulties, champion formulas, modifier compatibility, resistance ordering, aura effects and non-stacking, native damage/chill/curse/mana, projectiles/walls/dodging, native death effects, corner pursuit, unlocked doors, pet targeting, pack rollback, treasure structure, all four exact mould chances, duplicate reward claims and four-boot identity/injury/curse/mould persistence. `test_encounters.py` verifies reproducible source projection and bounded profile/tree structure. Cumulative and copied-live-save validation must pass before installation.

## Deployment checkpoint — 21 October 2026

Installed and live-save/restart verified. Source cumulative/legacy suites and the actual-client allied movement regression passed; installed cumulative/legacy suites passed. All 41 Python tests, catalogue reproduction and the existing native patch round trips passed. Pre-install, installed and post-live disposable save comparisons preserved both characters and all gear/material/gold snapshots, including skills/caps/locks. The complete runtime backup is `.local/backups/pre-t35-20261021-034521`; local evidence is under `.local/logs/t35` and `.local/logs/deploy-t35`. Engine, native content, client, configuration and preferences are unchanged. Permanent encounter placement remains deferred.
