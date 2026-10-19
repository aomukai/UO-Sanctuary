# T02 — first reference/catalogue cohort

## Bounded scope and acceptance

Started 19 October 2026 (JST), after the [T01 baseline](t01-working-baseline.md).

This card authors reference data for **Longsword, WoodenShield, LeatherCap, RingmailChest, GoldRing, Necklace, SkullCap and BoneChest**. Together they cover weapon/shield/head/body/ring/amulet and plain metal/wood/leather/cloth/bone. Existing runtime gear and affix catalogues remain authoritative; this cohort links to those records and records future crafting/material decisions separately.

Acceptance checks:

- Verify pinned D2 source hashes; retain bounded source rows and explicit units for the selected bases and effects.
- Assign maker, physical material and base-tier/progression identity for every selected UO type; show source facts separately from Sanctuary adaptations.
- Enumerate legal current Magic/Rare affixes through the existing eligibility/implemented-effect rules, including level/type/rarity gates and units. White crafting grants no random affixes.
- Record missing/gated consumers and unresolved decisions without enabling effects, sockets, Set/Unique items, new recipes, rewards or gambling.
- Distinguish base tier, crafting quality, item level, affix level, required level, monster difficulty and encounter grade.
- Validate the authoring artefacts against existing data/source and preserve the installed baseline. No save migration or deployment is part of T02.

Status: **complete for this bounded reference cohort**. No recipe, gameplay effect, save migration or deployment was enabled.

## The authoring contract

The [cohort manifest](../tools/reference/first-loop-cohort.json) contains the eight selected UO identities, their existing base codes, intended makers/materials, target socket/weight policies, five plain physical-material families and the supported/gated effect inventory. It is stored under `tools/reference/`, outside runtime balance loading. It does not add a second saved gear model or production recipe book.

The [source evidence](../tools/reference/first-loop-evidence.json) records selected source rows with table/zero-based row index/physical line, source pins, UO source hashes, current observations and pool summaries. The [affix audit](../tools/reference/first-loop-affix-audit.json) contains the exact eligible affix IDs for each query. Resolve those IDs in the existing [affix catalogue](../data/Sanctuary/Balance/affixes.json), which retains ranges, units/modes, groups, weights, requirements and exclusions. None of these files is loaded by gameplay.

## Pinned facts and units

D2 evidence uses the previously selected **LoD 1.13** extraction at commit `45112569deb9384738ccafe5c24ebbb71f41c7c9`. Existing source hashes were verified. Two additional inputs from that same immutable commit were added to [sources.json](../data/Sanctuary/Balance/sources.json):

| Table | Purpose | SHA-256 |
| --- | --- | --- |
| Misc | Ring/amulet base rows and their reference cost fields | `d01a37412cc170b744ae4cde66f5f5130e97f69274bad60b2d3ef0f470d7e207` |
| ItemStatCost | Stat identity/encoding, including resource fixed-point shifts | `4d743b7f3e260a6647f259de25a931fd2554d52315d316eac467f110ee096c27` |

The source extraction is a pinned mechanical-data mirror, not a Blizzard-authored specification. Readable primary cross-checks are Blizzard's [Normal swords](https://classic.battle.net/diablo2exp/items/normal/swords.shtml), [shields](https://classic.battle.net/diablo2exp/items/normal/shields.shtml), [helms](https://classic.battle.net/diablo2exp/items/normal/helms.shtml), [body armour](https://classic.battle.net/diablo2exp/items/normal/armor.shtml), [prefixes](https://classic.battle.net/diablo2exp/items/magic/pre.shtml) and [suffixes](https://classic.battle.net/diablo2exp/items/magic/suf.shtml), checked 19 October. The raw values below agree for the selected normal weapon/armour bases. The UO analogues and maker assignments are Sanctuary decisions.

| Field / effect | Unit and interpretation |
| --- | --- |
| D2 `level`, `levelreq` | Integer base quality level (qlvl), and character equip requirement respectively. Neither is item level or crafting quality. |
| Damage / defense / requirements | Whole damage points, defense points, and attribute points. Defense is a hit-chance input, not damage reduction. |
| Weapon `speed` | Source weapon-speed modifier; Long Sword is **−10**, not −10 ms or an attack rate. Class/family frame timing remains in `ActionTiming`. Armour's same-named source column is retained separately and is not a weapon-speed input. |
| Shield `block` | Base offset in percentage points before the existing class/Dexterity/level formula and 75% cap; Small Shield's **5** is not a final 5% block chance. |
| Flat attributes/pools/AR | Whole attribute/resource/attack-rating points. ItemStatCost's `ValShift=8` for maximum life/mana/stamina describes D2 internal encoding; do not multiply the affix-table whole-point values by 256 in Sanctuary. |
| `dmg%`, `ac%`, `att%` | Percentage modifiers within their actual local/global consumer scope. `ac%` enhances item defense; it is not physical resistance. |
| Resistances / increased block | Percentage points; all-resistance excludes physical resistance. Native Energy is the Lightning transport slot. |
| IAS / FCR / FRW / FHR | Percentage bonus inputs to their respective formulas/breakpoints, not direct percentage reductions in action milliseconds. FHR generation remains disabled. |
| D2 `durability`, `gemsockets` | Reference counts only. Native durability remains the comparison baseline; Sanctuary's five-socket policy supersedes the source cap when later implemented. Misc's numeric socket field alone does not grant ring/amulet socket permission. |
| D2 `cost`, `gamble cost` | Source cost fields, retained as evidence. They are not Sanctuary resale quotes, complete gamble prices, or an approved exchange-rate formula. T10/T11/T19/T45 own those consumers. |

No source fact is treated as a new recipe, material bonus, drop chance or gameplay unlock.

## Selected mappings and adaptation choices

All eight retain their existing **Normal reference base**. The six ordinary equipment entries use the future Normal craft band/item-level policy; the two jewellery entries use the separate jewellery ladder. Jewellery's runtime `Normal` label does not invent Exceptional/Elite jewellery or decide its missing item-level/cost/equip table.

| UO identity | D2 reference / slot | Maker → physical material | Current defense or damage; requirements | Target / current distinction |
| --- | --- | --- | --- | --- |
| Longsword | `lsd` Long Sword / weapon | Blacksmithing → plain iron | Damage **3–19**; Str **55**, Dex **39**; qlvl **20** | One-handed sword; future socket cap 5. |
| WoodenShield | `sml` Small Shield / shield | Bowcraft/Fletching → plain wood | Defense **8–10**; Str **22**; block offset **5**; qlvl **5** | Light. Native maker is Carpentry; the agreed material-based maker is Bowcraft. |
| LeatherCap | `cap` Cap / head | Tailoring → plain leather | Defense **3–5**; no source attribute requirement; qlvl **1** | Light headgear; future socket cap 5. |
| RingmailChest | `rng` Ring Mail / body | Blacksmithing → plain iron | Defense **45–48**; Str **36**; qlvl **11** | Medium remains metal/Blacksmithing. |
| GoldRing | `rin` Ring / ring | Tinkering → plain iron | No inherent damage/defense/attribute requirement; qlvl **1** | Native default is Iron despite its name/art. Target jewellery recipe, ilvl and equip values remain gated by E01/E02. No sockets. |
| Necklace | `amu` Amulet / amulet | Tinkering → plain iron | No inherent damage/defense/attribute requirement; qlvl **1** | Uses amulet statistics despite native Neck layer. Same jewellery gate; no sockets. |
| SkullCap | `cap` Cap / head | Tailoring → cloth | Defense **3–5**; no source attribute requirement; qlvl **1** | UO SkullCap maps to D2 **Cap**, not D2 Skull Cap. Cloth and leather share a stat analogue, not a material identity. |
| BoneChest | `rng` Ring Mail / body | Tailoring → bone | Defense **45–48**; Str **36**; qlvl **11** | Keep existing numerical analogue; agreed target weight **Heavy**, current runtime **Medium**. Apply and measure the heavy penalty in T08/T09. |

Normal source base required level is **0** for these references. That does not waive attributes, rolled-affix requirements, later filled-socket requirements, or the future jewellery table.

Higher-tier **source references only** are Long Sword → Rune Sword (`9ls`) → Cryptic Sword (`7ls`); Small Shield → Round Shield (`xml`) → Luna (`uml`); Cap → War Hat (`xap`) → Shako (`uap`); Ring Mail → Linked Mail (`xng`) → Diamond Mail (`ung`). Their separate source rows/requirements are retained in the evidence. No higher-tier UO variant or recipe is enabled. The reused Cap and Ring Mail analogues do not make different UO material identities interchangeable.

### Physical material and native recipe boundaries

The five manifest material entries explicitly link raw, processed and existing scrap identities:

| Family | Ordinary inputs / processed form | Existing physical salvage | Boundary for later implementation |
| --- | --- | --- | --- |
| Plain metal | IronOre → IronIngot | MetalScraps | No vendor metal supply. Jewellery uses Tinkering; metal equipment uses Blacksmithing. |
| Plain wood | Log → Board | WoodenChips | No vendor wood supply. Furniture stays Carpentry; wooden weapons/shields use Bowcraft. |
| Plain leather | Hides → Leather | LeatherStrips | Native species yields and skill-free butchering; no vendor leather supply. |
| Cloth | Wool/Cotton → Yarn/SpoolOfThread → Cloth through the appropriate native processing | ClothScraps | Expensive vendor cloth allowed. Bandage removal and conversion weight normalization remain T07 work. |
| Bone | Bone directly; native bone armour additionally uses leather | **None in the current prototype** | No bone scrap or salvage quantity is invented. T17 must resolve the physical recovery policy before enabling it. Repair's direct-bone rule remains separate. |

Observed constructor facts show why a single resource enum cannot own the future physical-material identity: WoodenShield has `Resource=Iron` but `MaterialType=Wood`; BoneChest has `Resource=RegularLeather` but `MaterialType=Bone`; cloth SkullCap has `Resource=None`. The current salvage classifier correctly returns Wood for the shield, Cloth for SkullCap, and no physical recovery for BoneChest. T04 must preserve explicit physical identity without laundering origin.

Existing native recipe facts, **not adopted target recipes**: Longsword uses 12 IronIngot at skill 28–78; RingmailChest 18 IronIngot at 21.9–71.9; LeatherCap 2 Leather at 6.2–31.2; SkullCap 2 Cloth at 0–25; WoodenShield is a Carpentry recipe using 9 Logs at 52.6–77.6. BoneChest uses 12 Leather + 10 Bone at 96–121, which cannot simply become a Normal-band (0–80) recipe. Native GoldRing recipes include an IronIngot base and optional legacy-gem variants; they do not settle the new five-step jewellery recipe. There is no exact `Necklace` recipe entry in the native Tinkering catalogue. See native `Engines/Craft/DefBlacksmithy.cs`, `DefTailoring.cs`, `DefCarpentry.cs` and `DefTinkering.cs` for the baseline. T06/T08 must author the target transactions and quantities.

Coloured ore/wood/leather identities remain recorded in [crafting B04](crafting.md#material-bonus-directions), outside this plain-material cohort. Their arithmetic, random element pool, typed damage, MF/GF and regeneration consumers are still gated. This catalogue does not import native UO resource bonuses or grant undeclared bone/cloth bonuses.

## Independent progression axes

| Axis | Meaning in this cohort |
| --- | --- |
| Base tier | Normal reference selected here. Exceptional/Elite rows are future references; they are independent of rarity and crafting quality. |
| Craft quality | Average/High/Superior, rolled after successful crafting under A05. Native UO `Exceptional` quality is not D2 Exceptional tier. No crafting quality roll is added in T02. |
| Crafted ilvl / base equip floor | Future ordinary Normal/Exceptional/Elite targets are **35/70/99** and **0/60/80**, with source attribute/affix/other requirements still applying. Jewellery values remain unset. |
| Affix level | Derived by existing `AffixBalance.Level` from ilvl, qlvl and magic level. For Long Sword ilvl35 yields **alvl25**, not alvl35. |
| Final equip requirement | Existing base/selected-affix requirement; later target base floors and filled-socket rules must be incorporated by their implementation cards. ilvl35 is not required character level35. |
| Rarity | Only current Normal/Magic/Rare are supported. Set/Unique are future content; no fallback policy or inert item is invented. |
| Monster difficulty | Normal active; Nightmare/Hell data remain preparatory. Numeric curve source bands do not activate those difficulties or choose item tiers. |
| Encounter grade | Ordinary/champion/random unique/named super-unique is separate from difficulty, monster level and Unique item rarity. Grade-dependent mould rewards remain T21/T35 work. |

## Legal effects and query results

All **29** numeric effect codes accepted by the current generation guard have a unit/scope/consumer entry in the manifest. The exporter queries `ClassGear.Implemented`, `AffixBalance.Eligible` and the real roller; it does not replace their rules. Every modifier of a compound affix must pass the guard. Source frequency/group/prefix/suffix constraints and rolled numerical ranges remain authoritative.

There are **25** codes in complete currently spawnable source affixes. `att%`, `block`, `cast2` and `vit` have accepted numeric consumers but occur in no fully enabled spawnable affix record in the current catalogue. A consumer entry therefore does not promise an obtainable affix. Type restrictions further narrow each base's pool: for example these selected bases gain no FRW affix merely because FRW has a working consumer elsewhere.

| Base(s) | ilvl35 → alvl | Eligible single affixes, Magic / Rare at ilvl35 | Magic / Rare at ilvl99 → alvl99 |
| --- | --- | --- | --- |
| Longsword (`lsd`) | 25 | 31 / 31 | 63 / 58 |
| WoodenShield (`sml`) | 33 | 29 / 29 | 39 / 36 |
| LeatherCap, SkullCap (`cap`) | 35 | 26 / 25 | 31 / 29 |
| RingmailChest, BoneChest (`rng`) | 30 | 27 / 27 | 41 / 37 |
| GoldRing (`rin`) | 35 | 40 / 40 | 58 / 53 |
| Necklace (`amu`) | 35 | 48 / 48 | 65 / 58 |

These are pool sizes, not an item's affix count or magnitude. The ilvl99 queries establish reachable reference boundaries; they enable no item source. Jewellery's ilvl35 query is a diagnostic example, not its adopted crafted item level. Exact IDs and source ranges/requirements are linked above. Gear rolls retain random eligible values; the rune category-minimum rule is not applied to gear. Ordinary crafted whites remain affix-free until a separate valid Imbuing transaction.

Still disabled or incomplete:

- FHR `balance1/2/3`: the recovery consumer exists, but generation remains excluded. Physical resistance also has a hook without an enabled affix generation path.
- MF/GF, leech, item regeneration, typed elemental/poison damage and most ability effects: missing/incomplete consumers; no tooltip-only promises or coloured-material activation.
- Socket affixes/state/insertion, procs, charges and parameter-driven effects: excluded from live rolls. The new source socket counts do not bypass that exclusion.
- Set/Unique generation, jewellery progression values, gem family/extra-flat mapping and upgrades, mould property weights, gold/gamble formulas, vendor stock-level meaning and vendor-origin resale: left to their gated later cards.

## Reproduction, outcomes and remaining cohorts

From the repository root, fetch/check the 13 pinned tables, build only the integration assembly and export current eligibility using the disposable harness:

```bash
python3 tools/fetch_balance_sources.py --destination .local/research/foundation
.tools/dotnet/dotnet build tests/Sanctuary.Integration/Sanctuary.Integration.csproj -c Debug -o .local/integration --nologo
SANCTUARY_TEST_SERVER="$PWD/upstream/ModernUO/Distribution" \
SANCTUARY_TEST_CREATION=1 SANCTUARY_TEST_FIRST_LOOP=1 \
SANCTUARY_FIRST_LOOP_ROOT="$PWD" \
SANCTUARY_FIRST_LOOP_AUDIT="$PWD/tools/reference/first-loop-affix-audit.json" \
python3 -u tests/check_britain.py
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation
python3 tools/build_first_loop_catalogue.py --source-dir .local/research/foundation --check
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

The [export fixture](../tests/Sanctuary.Integration/FirstLoopCatalogueChecks.cs) queries six distinct existing base codes at ilvl35/99 and Magic/Rare: **24 pools, 1,536 seeded rolls**. It validates every roll with `GearProfiles.Validate` (groups/counts, type/level/rarity, exact source modifiers/ranges and final requirements), observes non-minimum random values, and verifies that white conversion has no affixes. It constructs/deletes only disposable items. The existing harness still covers class/gear/origin persistence and v1/v0 migration over **four successful boots, explicit saves and clean shutdowns**, preserving town identities.

The [evidence builder](../tools/build_first_loop_catalogue.py) verifies source SHA-256 pins, manifest/content/input-source freshness, source/base equality, material/maker/slot/socket declarations, consumer paths, exact query coverage and numeric-only live pools. `--check` passed without rewriting. The new [negative checks](../tests/test_first_loop_catalogue.py) reject modified source tables, obsolete manifest evidence and insertion of an unsupported reference affix into a live pool. All **12 Python tests passed**, with no skips on this workspace. The integration build reported **zero warnings/errors**.

The authoring exporter records input hashes for runtime catalogues, guard/roller/persistence code and effect/material consumers, plus the exact unchanged content DLL hash. Altering those inputs requires a fresh export; do not hand-edit its affix ID lists. The existing foundation generator's source-manifest list was extended to retain the two new pins on future regeneration; the broad foundation/creature catalogues were **not** regenerated for this card.

Local supporting logs: `.local/logs/t02-reference-cohort-20261019/integration-build.log`, `server-summary.log`, `server.log`, `python-tests.log`, `before.json`, `after.json` and `preservation.json`. The committed reference files and this report retain the essential evidence without those ignored logs.

No production C# source, protocol, upstream patch, numerical runtime catalogue, live binary, saved payload or client preference changed. The only change under runtime balance data is source provenance in `sources.json`; it was not staged into the installation. Live PID **348733** stayed running, the desktop client stayed closed, and the integration assembly remained outside the live runtime. No new client interaction or player feedback is claimed; T01's client baseline remains the comparison evidence.

Remaining content is explicitly outside completion of this bounded card:

- [ ] T08: author usable Normal recipes for these materials, including WoodenShield's maker change and a legal Normal-band BoneChest recipe; apply/measure heavy bone. Add tools and remaining gear slots/families as separate recipe cohorts.
- [ ] T08/T19: author and implement Exceptional/Elite UO variants from the selected source ladders, then extend beyond these four analogue families.
- [ ] E01/E02 and the jewellery recipe cohort: decide jewellery ilvl/equip/costs and author its five steps. The selected ring/amulet references do not complete that acquisition loop.
- [ ] B04/T09: settle coloured-material arithmetic and implement each advertised effect before enabling its cohort.
- [ ] T17: resolve/implement bone salvage; retain existing origin and transaction protections.
- [ ] T21–T26, T10/T19/T35/T45: expand mould/gem, reward/grade/difficulty and gamble references at the consuming cards. Set/Unique, proc/binary effects and unresolved rarity fallbacks remain unavailable.

**Completion record:** T02's prerequisite T01 was verified. Changed files are the linked authoring manifest/evidence/audit, builder/export fixture and its harness flag/negative tests, two source pins and the generator's source list, this report and documentation pointers. No save version/default changed, no deployment/rollback is needed, and no wider recipe/progression milestone is claimed. **T03** is the next recommended card; T04 is also dependency-ready within this cohort's recorded boundaries.
