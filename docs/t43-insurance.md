# T43 — Equipment insurance

Status: implemented, tested and deployed. User approved the defaults on 21 October 2026 and explicitly deferred Set and Unique equipment/support until later. Production runs the matching T43 native/content build.

## Approved policy

| Rarity | Normal | Exceptional | Elite |
| --- | ---: | ---: | ---: |
| White | 100 | 200 | 300 |
| Magic | 250 | 500 | 750 |
| Rare | 500 | 1,000 | 1,500 |

Pay from the character's bank upfront. Each item purchase covers its next insured death without a time expiry. Covered equipment moves into the character's backpack on death; uninsured belongings follow ordinary corpse rules. Automatic renewal is optional and defaults off. With renewal on, protection is consumed and the next death purchased from the bank at the current item premium. Insufficient renewal funds never revoke the death just covered; that item's future protection lapses and the player receives a warning. Renewals occur per item through native death processing. No insurance cash reward goes to a player killer. The separate flat 20% backpack-gold death penalty remains unchanged.

Insurance is character-owned per real item. Moving between the character's own equipment, backpack, bank or inactive wardrobe does not transfer ownership. Equipment outside the dying character's carried/equipped inventory is not consumed or renewed by that death. An item or nested bag received by another character loses old protection and unused credit, including transfers to another character on the same account. Copies never inherit paid policies. Cancelling coverage retains unused prepaid credit on the same item/character; reactivating it does not charge a second time. Transfers do not refund premiums.

Supported catalogued weapons, armor/shields, clothing/belts and jewelry are eligible while worn or in the backpack, including nested bags. Containers and their contents as a package, gold, stackable ammunition, consumables, mounts, already blessed/newbied or cursed objects and unsupported item families are excluded. A potion tabard's own equipment can be insured; its potion reserve remains subject to the ordinary belt-death rules. Identify equipment before insuring it, so premiums cannot reveal hidden rarity. Native crafted quality is distinct from Normal/Exceptional/Elite base tier.

Set and Unique are deliberately absent from the implementation and price data. The older Unique price row remains design reference for a later item-family task; it does not enable Unique insurance or create fake Set/Unique drops. Existing unsupported-rarity drop behavior remains intact.

## Player flow and implementation

Use **[Insurance** or the character's native **Open Item Insurance Menu** context entry. The menu lists eligible carried equipment, current premiums, unused credit, bank funds and auto-renew status. Select equipment, review the total immediate charge, then confirm. The batch validates ownership, eligibility, premiums and policy state together and requires the full bank payment before changing any policies. Refresh rebuilds the list after equipment changes. Native single-item targeting remains available through Toggle Item Insurance.

`data/Sanctuary/Balance/insurance.json` is the configurable 3×3 table, with strict supported-rarity/positive-price validation. `EquipmentInsurance` supplies price/eligibility, acquisition, ownership cancellation and death settlement. `InsuranceProfiles` stores real item and character serials plus prepaid/active state in `SanctuaryInsurance`, version 0, priority 134. Deleted-item/owner records are omitted when saving. Existing supported paid native flags are lazily adopted without a second initial charge. The native character auto-renew preference and item insurance flags continue to provide persistence/tooltips.

`InsuranceGump` uses connection-bound, one-use session validation and fresh quote checks. Death/disconnect invalidates pending responses. Native `InsuranceContentHooks` delegates pricing, eligibility, menu/target actions, before/item/after-death settlement and received-item/deletion cleanup to project content. Death captures the protected item set before native nested-item flattening, then each item settles once and returns the native MoveToBackpack result. Replayed callbacks cannot buy another renewal. Ownership checks use native player receiving paths, including nested transferred bags. Existing item-copy hooks clear insurance credit on copies.

Native integration is reproduced by `patches/ModernUO/0041-equipment-insurance.patch`. It edits UOContent only; no engine or ClassicUO changes are required. [T44 corpse escrow](t44-corpse-escrow.md) implements later banker recovery separately; it does not consume or charge equipment already protected by insurance.

## Validation and deployment

Native, project and integration builds pass with zero warnings/errors. Focused, cumulative and legacy source checks, actual-client insurance checks and source/installed two-boot copies pass. The suite covers all nine prices, excluded/unidentified items, exact/insufficient batch funds, stale quotes, cancellation/reactivation, direct/nested ownership transfer, equipped/nested real death protection, carried-gold loss, renewal/failure/later death, existing paid flags, copy behavior, inactive wardrobe, no PvP insurance payout, the separately insured belt equipment/uninsured potion reserve and policy/gear persistence.

References: [roadmap T43](implementation-roadmap.md), [O03 decisions](economy-open-questions.md), [death-gold rule](t13-death-gold.md), [class storage](t40-wardrobe-transactions.md).


The native patch series reproduces exactly and reverses: 41 ModernUO patches over 93 paths and the unchanged 32 ClassicUO patches over 40 paths. The regenerated first-loop catalogue passes its consistency check; all 41 Python tests pass. Source and installed cumulative/legacy suites and client fixtures exercise selection, preview/back, exact bank payment, optional renewal, real death and four save/boot cycles. Screenshots under `.local/logs/t43-insurance-*.png` were inspected for readability and clipping. Test accounts/items remain confined to disposable worlds.

Deployment uses the matching complete server/client backup `.local/backups/pre-t43-20261021-200133`. T42 saved at 20:01:23 JST and stopped cleanly at 20:01:33 before replacement. Staging preserved all 69 protected files byte-for-byte, including saves, configuration and client preferences. Only native UOContent/project-content DLLs/PDBs and project data changed; engine/logger, the installed application launcher, client and production assembly list remain unchanged. Live loads only `UOContent.dll` and `Sanctuary.Content.dll`, with no integration assembly. Source and installed copies retain both original players, their full class histories/profiles/allocations/bindings/titles/native skills/caps/locks, real gear/material identity, all 507 gold stacks and all 22 architect identities.

Evidence: `.local/logs/t43`, `.local/logs/deploy-t43/deployment.json` and its staging/preservation verifier. Live boot1 saved at 20:05:50 JST and shut down cleanly at 20:05:58. Boot2 loaded the same 4446 items/536 mobiles and saved 5014 entities at 20:06:47. The added persistence record is the new insurance ledger. Post-live two-boot copies and the final verifier preserve every tracked player/item/material/gold/architect record and all protected non-save payloads. The server remains running on 127.0.0.1:2593, with the client closed.

For rollback, stop server/client and restore the matching backup's runtime and saves together. Restore `.local/content/Sanctuary.Content.dll` to the same version before using `./dev server`, which auto-stages that working build, or launch the restored runtime directly. Keep the new insurance ledger and native insurance flags consistent. Never replace running assemblies or rebuild files used by active test fixtures. Source and native patches are included in the T42–T44 Git checkpoint; runtime backups and operational evidence remain local.
