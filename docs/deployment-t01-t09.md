# T01–T09 cumulative deployment

The user requested deployment of all completed T01–T09 work on19 October2026 (JST), including features previously left disabled. **Deployment complete: all completed T01–T09 features are installed and active.** The live world passed save, clean shutdown, restart and another save; the server is left running.

The project goal is the complete agreed game, not an MVP. Roadmap cards organize dependencies and verification; they do not limit the final product to a demonstration cohort. Complete known requirements and their gameplay consumers, and identify unfinished work explicitly instead of treating later bug fixing as a substitute for implementation.

Subsequent [T10 deployment](t10-rewards.md) activates manual corpse gold and Gold Find. The MF/GF statements below describe the earlier T01–T09 deployment.

## Card audit

| Card | Included in this deployment |
| --- | --- |
| T01 | Installed-world compatibility and copied-save restart checks; existing progression/preferences preserved. This is verification, not a new gameplay subsystem. |
| T02 | Pinned reference facts, maker/material mappings and audited legal affix inventory. Subsequent T08/T09 work supplies the actual tiered recipe/material consumers. |
| T03 | All confirmed class names and matching server/client labels, preserving class IDs. |
| T04 | Current versioned gear records and deterministic legacy migrations; copy/trade/serialization preservation. |
| T05 | First-profession confirmation, activation/training checks, independent retained crafting skills, ten-profession practice and the profession UI. Enabled in runtime configuration. |
| T06 | All ten tool transactions, tool quality/uses, public town facilities, facility checks and the matching client craft interfaces. |
| T07 | Canonical material weights, generic scraps/strips/chips, native material conversions and protected transactions; material vendor restrictions and bandage-creation exclusions. |
| T08 | All603 approved white-equipment recipes across201 standard-material identities and three tiers; quality, empty sockets, maker/durability perks and potion tabard recipes. |
| T09 | Active material resistance/damage/chill/poison/Verite effects, FHR affixes, consistent broken gear, cosmetic cloth clothing and working leather footwear/belts/tabards. |

All four matching artifacts are installed: Server, UOContent, Sanctuary.Content and ClassicUO. Runtime catalogues are installed alongside them. The integration assembly, test characters, fixture kits and fixture-only commands are excluded from the live server.

## Activation decision

`sanctuary.professions.enabled=True` is set in the installed server configuration. The repository default remains opt-in. This deployment is the user's explicit override of earlier notes that held the completed profession/crafting features disabled until T14/T15. Existing native skills/caps/locks and unconfirmed first-choice defaults are preserved. A confirmed first craft still uses the agreed activation rules; the earned public profession-switch route is now delivered by T14/T15 (see the updates below).

The potion belt UI, storage and hotkeys1–4 were already deployed; they remain included. T08 adds their approved craftable tabards. No second belt stat slot is introduced.

## Explicitly unfinished work

The ten exotic glass/stone/dragon-scale recipes remain excluded by the user's earlier decision. No completed T01–T09 feature is intentionally left disabled in this runtime. Later cards still own their separate requirements: gold/reward and vendor supply, repairs, recognition/wardrobe switching, full jewellery/Imbuing progression, sockets/runes/gems, the potion/food/timed-effect catalogues, broader class abilities and world progression. MF/GF and unsupported affixes remain unavailable until those actual consumers are built. These are unfinished parts of the full game, not optional replacements for its final scope.

## Operations and verification

- Both installed applications were stopped before installation. Full server and client backups were made under `.local/backups/pre-t09-20261019-235519/` before replacing files.
- All54 recorded save/configuration/preference files were byte-identical after build/staging, except the explicitly authorized profession flag in server configuration. Existing client settings and profiles are preserved.
- Native/content/client builds succeed; server/content report no warnings/errors. The client retains its existing upstream warnings. Installed assembly hashes are checked against the cumulative build outputs.
- Installed binaries are checked in disposable worlds with all completed profession/crafting features enabled, full tool/equipment/material suites, gear effects, actual client recipe/equip interactions and save/restart checks.
- A disabled→enabled→enabled copy of the installed save preserves both characters' profiles, allocations, native skills/caps/locks, worn identities, gear and material/bandage stacks.

Supplementary operational evidence and the rollback manifest are in `.local/logs/deploy-t09/`. Live boots and saves passed in `live-boot1.log` and `live-boot2.log`; the first boot shut down cleanly and the second remains running. The post-deployment copied-world inspection matches every pre-deployment player, gear and material record. All13 client preference files are unchanged. The new public facilities persist. The installed-client GUI test and all enabled craft/processing/gear suites pass four disposable save/restart boots. The desktop client is closed and ready for the user to launch. Rollback after new gameplay requires the matching pre-deployment runtime **and save** backup, not only an older content DLL.


## T13 update — 20 October 2026 JST

[T13](t13-death-gold.md) is deployed: flat20% of recursively counted backpack gold on death, aggregate downward rounding and a saved once-only corpse claim. Bank gold and XP remain safe. The full pre-T13 server/client backup is `.local/backups/pre-t13-20261020-023011`; artifact hashes and preservation records are in `.local/logs/deploy-t13/deployment.json`.

All59 protected payloads survived staging unchanged. Both live boots and saves pass, with476 existing merchants restored and none newly created. Installed four-boot cumulative checks and actual ClassicUO resurrection/recovery/minion/reconnect checks pass. Pre/post copied-world comparisons preserve both characters, gear/materials and all485 gold-stack identities/amounts/parents/origins. All13 client preference files are unchanged. Server engine/logger/client binaries are unchanged; matching application/content are installed with no integration assembly or fixtures in production. The second server boot remains running and the client is closed. T14 is next.


## T14 update — 20 October 2026 JST

[T14](t14-recognition.md) recognition and second-ceremony active-class respec are deployed and enabled. Both routes grant permanent character-owned wardrobe/housing rights, one certificate per honor and one bookshelf wardrobe; respec after both honors costs5,000gold, refunds class skill/stat points and closes the UI while preserving class/level/XP/native trades. No inactive class is reset and no switch is performed. Public craft switching remains T15, full class histories T40 and housing acquisition T42.

Full rollback backup: `.local/backups/pre-t14-20261020-030051`. All59 protected payloads survive installation unchanged; both live boots save successfully,22 civic offices persist without duplicates and all476 existing merchants remain. Source/installed four-boot cumulative and actual ClassicUO ceremony/respec checks pass. Pre/post snapshots preserve both characters/raw attributes/skills/recognition, gear/materials and485 gold stacks; all13 client preference files are unchanged. Artifact hashes/preservation records are in `.local/logs/deploy-t14/deployment.json`. Engine/logger/client binaries are unchanged and production registers only UOContent.dll and Sanctuary.Content.dll. The second live server boot remains running; the client is closed.


## T15 update — 20 October 2026 JST

[T15](t15-wardrobe.md) public craft switching is deployed and enabled under the existing profession flag. Either completed ceremony grants free switching among ten crafts at all22 public wardrobes. The current class/craft open preselected; native radio groups and server validation require exactly one of each. The current class stays fixed. Native training/caps/locks, class points, gear, summons, pools and timers remain intact. Combat and pending action checks are shared with respec; old trainer/crafting requests cannot survive a switch roundtrip. Second-ceremony5,000gold active-class respec remains separate and closes/invalidates all wardrobe responses.

Full backup: `.local/backups/pre-t15-20261020-031851`. All60 protected files survive staging;34 non-save configuration/client/assembly-list payloads, including13 client preferences, remain unchanged after both live saves/restart. Source/installed four-boot cumulative checks, actual ClassicUO switching/respec and all16 Python checks pass. Pre/staged/post-live copies retain both characters, their skills/allocations/raw attributes/recognition, gear/materials and485 gold stacks. No merchants/civic objects are duplicated. Engine/logger/client binaries are unchanged; production registers only UOContent.dll and Sanctuary.Content.dll. `.local/logs/deploy-t15/deployment.json` records artifact and preservation evidence. The second live boot remains running and the client is closed. T16 is next.


## T16 update — 20 October 2026 JST

[T16](t16-salvage.md) corresponding-tool salvage is deployed. Smithing hammers/fletching tools/tailoring kits/tinker's tools/scribe's pens cover the equipment families; anyone can use them without matching skill/active craft, tool-use charge, gold fee or town workshop. The common Essence menu binds the same matching tool. Source/connection/content snapshots and one-use sessions reject stale/replaced/cancelled/reconnected responses. Independent rarity/ilvl rolls remain; filled contents are lost without extra yield, with capacity checked before host destruction. Native recycling bypasses remain blocked; Unique recovery stays inactive.

Full rollback backup: `.local/backups/pre-t16-20261020-033914`. All60 protected files survive staging.34 non-save configuration/client/assembly-list payloads, including13 client preferences, remain byte-identical after both live saves/restart. Source/installed four-boot cumulative, legacy economy/item-state, actual ClassicUO and16 Python checks pass. Pre/staged/post-live copies retain both characters, skills/allocations/raw attributes/recognition, gear/materials and485 gold stacks. Engine/logger/client binaries are unchanged, production registers only UOContent.dll and Sanctuary.Content.dll, and no service objects duplicate. `.local/logs/deploy-t16/deployment.json` records evidence. The second live server boot remains running; client closed. T17 is next.


## T17/T18 update — 20 October2026 JST

[T17](t17-imbuing.md) tiered Imbuing, successful-action training and ten white jewellery recipes are deployed, followed by [T18](t18-identification.md) single-use Identify scrolls. The scroll recipe is1BlankScroll+1BlackPearl with pen/public desk, Inscription0–50; scribes sell80gold. Only successful own-backpack identification consumes it. New magical drops are hidden until identified; ordinary crafting/Imbuing/shop items are known. Existing item states are retained. Charged tomes and production encounter gear drops remain T34/T19 respectively.

Full backup: `.local/backups/pre-t17-t18-20261020-041104`. The install preserves all60 protected files;34 configuration/client/list payloads, including13 client preferences, stay byte-identical after both live saves/restart. Source/installed four-boot cumulative, legacy economy/item-state, actual ClassicUO,16 Python checks and refreshed24-pool/1536-roll authoring audit pass.25 ModernUO/53 paths and22 ClassicUO/36 paths reproduce/reverse exactly; native revisions and engine/logger/client binaries are unchanged. Pre/staged/post-live snapshots retain both characters, their progression/skills/rights,1 tracked gear/material record and485 gold stacks. Production loads only UOContent.dll and Sanctuary.Content.dll. No merchants/civic service objects duplicate. `.local/logs/deploy-t17-t18/deployment.json` records evidence. The second live boot remains running; client closed. T19 is next.


## T19 update — 20 October 2026 JST

[T19](t19-gear-drops.md) production gear drops and Magic Find are deployed for the existing mapped Normal reward roster. Pinned treasure/base/quality tables drive one shared gold/gear draw; unsupported branches and successful Set/Unique rolls remain empty without rerolls. Inferior/superior white outcomes become ordinary white bases. Magic/Rare drops are unidentified. Working equipped numeric/material MF affects quality selection and is displayed in the matching protocol-v5 client.

Full rollback backup: `.local/backups/pre-t19-20261020-044429`. All 60 protected files survive staging; 34 non-save payloads, including 13 client preferences, remain unchanged after both live saves/restart. Source/installed four-boot cumulative, legacy server/client, actual installed-client drop flow, 19 Python checks and refreshed 34-code/24-pool/1,536-roll audit pass. All 7,935 variant/level/quality cases and seeded quality distributions pass. Pre/staged/post-live comparisons retain both characters, gear/material records and all 485 gold stacks. Engine/logger and native pins remain unchanged; the client is updated for MF. Production loads only UOContent.dll and Sanctuary.Content.dll, with no duplicated merchants/civic objects. `.local/logs/deploy-t19/deployment.json` records evidence. The second live boot remains running; client closed. T20 is next.


## T19 Nightmare/Hell extension — 20 October 2026 JST

The [higher-difficulty extension](t19-difficulties.md) is deployed: persistent spawn identity, corresponding numeric stats/XP and source treasure tables for the 324 mapped counterpart types. Staff targeting/factory APIs create explicit variants; source treasure groups upgrade by the reviewed monster level. Normal catalogue output remains unchanged. Set/Unique rolls and unsupported branches remain empty. The scoped AOS hook preserves higher-difficulty immunity without the native one-point floor or negative-channel cancellation. Native abilities/AI and equipment/status modifiers remain their existing implementations.

Full rollback backup: `.local/backups/pre-t19-difficulties-20261020-050811`. All 60 protected payloads survive staging; 34 non-save payloads including 13 client preferences remain unchanged after both live saves/restart. Source/installed four-boot cumulative, legacy Normal systems, actual Hell and installed Nightmare client paths, 21 Python checks and refreshed 34-code/24-pool/1,536-roll audit pass. All 26 ModernUO/53 paths and 23 ClassicUO/36 paths reproduce and reverse exactly. Engine/logger/client binaries and native revision pins are unchanged. Pre/source/staged/post-live copies preserve both characters, gear/material records and all 485 gold stacks. The new live difficulty file contains zero assignments, so existing populations remain Normal and placement is deferred. Only UOContent.dll and Sanctuary.Content.dll load in production. `.local/logs/deploy-t19-difficulties/deployment.json` records evidence; the second live boot remains running, client closed. T20 is still the next roadmap card.
