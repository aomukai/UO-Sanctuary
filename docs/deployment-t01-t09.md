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

`sanctuary.professions.enabled=True` is set in the installed server configuration. The repository default remains opt-in. This deployment is the user's explicit override of earlier notes that held the completed profession/crafting features disabled until T14/T15. Existing native skills/caps/locks and unconfirmed first-choice defaults are preserved. A confirmed first craft still uses the agreed activation rules; the earned profession-switch route remains unfinished in T14/T15.

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
