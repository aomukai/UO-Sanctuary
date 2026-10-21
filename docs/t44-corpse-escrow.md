# T44 — Corpse escrow and banker recovery

Staged T49 update (22 October2026): actual eligible named items now share this path. The [complete return list](named-item-readiness.md) records every held definition and its prerequisites; unavailable outcomes retain their original weight. These changes are isolated staging; production remains T45.

Status: implemented, tested and deployed. All gameplay defaults below were confirmed on 21 October 2026. The deployed evidence below describes the original T44 scope, when Set/Unique equipment was deferred.

## Confirmed rules

Player corpses persist for 24 real elapsed hours from death, including logout and server downtime. Before the deadline, existing physical corpse recovery still re-equips eligible equipment and leaves capacity/weight overflow on the corpse for retry. At the deadline, only the belongings still inside enter private, character-owned escrow. Each death has its own claim, with no further expiry initially. New deaths do not overwrite old claims. Empty corpses create no paid claim.

Insured/blessed equipment follows existing death protection before escrow. Inactive wardrobe equipment remains outside both death and corpse recovery. The flat 20% carried-gold penalty is charged once at death; escrow does not repeat it. Potion-belt reserves follow their normal death rules and can subsequently enter escrow with the other remaining belongings. Monster corpses retain native decay.

Visit any banker and **say `recover`**, or use **`[Recover`** within 3 tiles. The owner must be alive, in sight/range and permitted to bank. Select a claim to review its death time/location, contents and fee. **Confirm recovery** withdraws the full fee from existing bank funds and moves all contents into that character's bank, keeping real item identities and nested containers intact. The fee is not paid out of the yet-unrecovered gold. Claim gold becomes ordinary bank gold when recovered.

Capacity is checked for the whole claim before payment, including nested objects. Insufficient bank space or funds leaves everything untouched and allows retry. Recovery does not partially transfer a claim. Bank-item capacity follows native limits and weight rules; recovery does not bypass limits for staff characters.

**Closing/cancelling, leaving for later, disconnecting, being unable to pay or having a full bank never forfeits anything.** This explicitly supersedes the original O02 refusal/no-funds deletion wording. **Surrender belongings…** opens a separate confirmation explaining permanent deletion of all contents in the selected claim. Only **Confirm forfeiture** deletes them; backing out retains them. Other claims, recovered property and bank balances are untouched.

## Fee valuation

Recovery costs 10% of the aggregate value of remaining contents, rounded down once to whole gold, with a 100-gold minimum once per corpse. Count each real object once, including nested contents; price stack quantities per unit, so splitting/merging never changes the total. Container shells use their own ordinary value independently of their contents.

| Contents | Value per unit |
| --- | --- |
| Gold | Face value: 1 gold |
| Ordinary NPC resale value exists | That resale value |
| No resale value, but an NPC purchase price exists | Normal undiscounted NPC purchase price |
| Neither price, non-stackable | 100 gold |
| Neither price, stackable | 10 gold per unit |

Thus an unpriced non-stackable contributes 10 gold to the fee, an unpriced stackable contributes 1 gold per unit, and a corpse containing 1,000 gold plus an item valued 2,000 gold plus one unpriced non-stackable costs 310 gold.

Equipment uses the existing persistent `TradePrices` offer, respecting its identified/unidentified valuation and purchase-origin rules without identifying or rerolling gear. Gems/Runebark and authored merchant supplies use their established prices. Native shop definitions provide ordinary resale and undiscounted purchase references; when ordinary NPC references differ, use the lowest positive reference consistently. Generic purchase references match item type and graphic. Merely being priced for recovery does not enable NPC sale of restricted goods. `data/Sanctuary/Balance/escrow-values.json` is an initially empty, validated type-to-unit-value override table for valuable unpriced families authored later; no player-market estimates are inferred.

## Persistence and transaction boundaries

`CorpseEscrowProfiles` stores `SanctuaryCorpseEscrow` version 0, priority 135: original corpse serial, owner serial, absolute UTC death time, location/facet and escrow storage serial. New deadlines use absolute DateTime serialization, independent of native corpse anchored-time shifts. The loading hook adopts existing player corpses using their available native death time; saved T44 claims retain their original deadline exactly. Overdue claims enter escrow automatically after loading. No global world-item scan is introduced; loading callbacks and the known claim ledger supply references.

`CorpseRecoveryHooks` suppresses native player-corpse decay, registers creation/loading/deletion, checks expiry before native opening/lifting/item use/carving and routes banker speech to project content. Class corpse recovery checks that same deadline. Bone conversion cannot reset the clock. Timers are cancelled on corpse deletion/settlement; the ledger stores serials rather than persistent object references. Native UOContent integration is reproduced by `patches/ModernUO/0042-corpse-escrow.patch`; no engine/client changes or corpse serialization-version change is required.

At expiry, real top-level contents move into a generated-serialization `CorpseEscrowStorage` before the empty corpse is removed. Private internal storage rejects ordinary display/lifting/dropping and survives native cleanup. Bank recovery transfers those same top-level items without merging or flattening them. The storage container is then deleted only after it is empty. Confirmed forfeiture deletes the selected storage and its contents together.

`CorpseEscrowGump` binds responses to the connection, owner, banker, one-use session and claim snapshot. Death/disconnect invalidates pending sessions. Each transaction rechecks claim ownership/reference and the complete recursive serial/parent/amount/valuation snapshot; bank fee and capacity are revalidated before any payment. Removed claims cannot be recovered or forfeited again. A stale response cannot delete newly changed contents or another claim.

## Validation and deployment

Source, installed and live validation complete. Source focused/cumulative/legacy and installed cumulative/legacy/client suites pass through four save/boot cycles; source/installed two-boot copies preserve the existing world. Source native/content/integration builds pass with zero warnings/errors. The focused suite covers exact deadline boundaries, bone/decay attempts, minimum/aggregate/stack fees, price fallbacks, nested identities, insufficient funds, full bank, foreign/remote/stale/replayed transactions, explicit forfeiture isolation, insurance interaction, separate claims, saved deadlines and automatic expiry across downtime. Actual ClassicUO checks exercise banker speech, review, closing/reopening, no-funds/full-bank safety, exact payment/nested recovery, cancelling forfeiture and explicitly confirming it, plus deferred claim persistence.

Preserve the existing live world, accounts, inventories and client preferences. Deploy only after a clean save/stop and a complete matching server/client backup. Production must load only UOContent and Sanctuary.Content, with all fixtures and the integration assembly confined to disposable worlds. Matching rollback must restore runtime and saves together, because old content cannot load the new escrow storage type; restore the matching working `.local/content` build before using `./dev server`, which auto-stages it.


The final native patch series reproduces and reverses exactly: 42 ModernUO patches/95 paths and the unchanged 32 ClassicUO patches/40 paths. All 41 Python tests pass, along with first-loop catalogue generation/check and root/native/client whitespace checks. Source and installed screenshots are under `.local/logs/t44-escrow-*.png`; the review and forfeiture screens were visually checked for clipping/readability. The focused suite also proves the pre-deadline physical recovery path and rejects item use from an already-open corpse window after expiry.

T43 saved at 21:12:42 JST and stopped cleanly at 21:13:20. Complete matching rollback backup: `.local/backups/pre-t44-20261021-211320`. Staging preserved all 70 protected save/configuration/client-preference files byte-for-byte and every client file. Only native UOContent/project-content DLLs/PDBs and project data changed; engine/logger/application, client and production assembly list remain unchanged. Production loads only `UOContent.dll` and `Sanctuary.Content.dll`. The manifest and verifier are under `.local/logs/deploy-t44`; source evidence is under `.local/logs/t44`.

Source/installed two-boot copies preserve both original players, their complete recorded class histories/profiles/allocations/bindings/titles/native skill values/caps/locks, real gear/material identity, all 507 gold stacks and all 22 architect identities. The existing world has no player corpses requiring legacy adoption. Live boot1 saved at 21:17:07 JST and stopped cleanly at 21:17:15. Boot2 loaded the same 4446 items/536 mobiles and saved 5015 entities at 21:18:17. The additional persistence record is the new escrow ledger. Post-live two-boot copies and the final verifier preserve all tracked player/item/material/gold/architect records and all protected non-save payloads. The server remains running at 127.0.0.1:2593; the desktop client remains closed. Source and native patches are included in the T42–T44 Git checkpoint; runtime backups and operational evidence remain local.
