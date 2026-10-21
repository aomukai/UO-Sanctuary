# T42 — housing and personal wardrobes

Status: complete and deployed21 October2026 (JST). All gameplay defaults, including the1,000gold replacement wardrobe, are implemented and verified in the installed runtime.

## Using the implementation

On a GM character, use **[MarkHouse** near the building. Walk to each living-space corner and click its checkbox to record the displayed coordinates; click a checked box to clear and recapture it. Set1–5 floors and use the floor buttons to measure upper floors individually, or leave all four unchecked to inherit floor1's area and a20-unit-per-floor elevation. Select **Has garden** for its separate corners. Mark a clear outside exit at your feet, then choose **Save settings and exit**.

The sale setup shows measured areas, the suggested price and editable final price/storage/lockdown limits. Set the name and Furnished/Empty choice. **Mark original furnishing** targets an empty, immovable original world item; targeting it again removes it from the selection. Original fixtures stay with the property. Fixed map statics remain part of the building; the furnishing selector handles world items. Doors inside the footprint attach automatically; **Mark entrance door** can additionally attach a door at a room's edge. **Sign at your feet** places the sale sign outside the property. Cancelling either target returns to the draft. **List property for sale** validates the complete definition before creating the listing.

The GM chooses suitable town buildings after checking NPC reservations, public routes and map geometry. Publication rejects existing housing, occupied NPC/creature space, public civic/crafting facilities and protected higher-priority regions. Four corners describe a rectangular area for each floor and optional garden. The final price override accommodates authored pricing exceptions; it does not change the security footprint.

Players double-click the sign from within three tiles to review the property and confirm purchase, paid from the buyer's bank. Owners use the same sign for native house management/trading or a separate confirmed sale back to town. Buyback refuses all player-added belongings, garden items and original-container contents; original fixtures remain. Refunds use half the recorded town receipt, paid into the selling character's bank. A private player trade retains the receipt and transfers the actual house/contents through native secure trading.

Recognized characters can buy a1,000gold recipient-bound replacement through their town-hall recognition screen. Backpack capacity and bank funds are checked before delivery/charge. The first ceremony award remains free. A character's own placed wardrobe works in the account's owned home; housing rights alone do not grant another character's class/wardrobe progression.

Native architects are installed idempotently near the22 existing civic offices, using a new `SanctuaryHousing` version0 service index (priority133). They sell native house placement tools/deeds through their normal catalogue. Native placement/confirmation, deeds, house claims and transfer acceptance/commit enforce the account housing entitlement and existing account-house limit. Existing native placed-house prices and placement restrictions remain in use.

`sanctuary.housing.indoorGoldPerTile` defaults to1000; `sanctuary.housing.gardenGoldPerTile` to250; `sanctuary.housing.wardrobeGold` to1000. They are read at startup through the normal server settings. Automatic house decay is disabled by the initial housing policy, while private workshops retain their existing deferred status.

Town properties are serialized `BaseHouse` subclasses, with native account ownership registration, security lists, secure containers and trading. Their regions retain the underlying town/world region as parent, preserving its rules. Native lookup recognizes the measured static-building footprint; gardens remain accessible while private interior access is enforced on movement, forced relocation and login. Ownership changes invalidate sale quotes, clear old-account permissions and remove unauthorized interior visitors.

Town listings explicitly survive native unowned-house retirement. Empty-listing furnishings retain the same objects inside a private serialized container, protecting them from native inaccessible-item cleanup. Original containers default to owner-only storage; releasing an original secure fixture does not make it removable. Class equipment/minion storage and every character's progression remain independent of this property data.

Native integration is reproduced by `patches/ModernUO/0040-housing-content-integration.patch`; ClassicUO and engine source require no new edits for T42. Rollback after a world save requires the matching pre-T42 saves as well as runtime binaries, because the old content cannot deserialize the new town-property/storage item types.

## Established housing rules

- Both placed houses purchased through architects and existing town houses are intended. Recognition grants permission to acquire a house, not a free house. Town-sign purchasing was proposed; its exact player flow remains to be settled.
- Recognition awards remain permanent per character, independent of house or furniture loss/transfer. **T42 supersedes T14's character-only housing eligibility:** once one character unlocks housing through recognition, every character on that account may buy/place the account's home. Public wardrobes remain available. Real class equipment, summon state and progression are character-owned, independent of access furniture.
- No initial rent, tax or upkeep. Private home workshops have not been approved; existing public-facility and portable-processing rules remain.
- Suitable town houses require an audit. Reserve homes for future NPC households and preserve shops, civic services and shared routes.

## Confirmed 21 October 2026 (JST)

- Homes are private by default. Only the owner and invited people may enter, including when a door is open. Entry permission must be enforced independently of door state.
- Storage permissions are separate from entry invitations. An invitation alone does not grant storage access. **Confirmed defaults:** reuse native friends/co-owners and per-container security settings; same-account characters retain full access.
- T42 includes a GM house-marking tool for authoring existing town properties.
- On use, the tool prompts the GM to walk to corner 1 and check its checkbox, then corner 2 and onward until all four corners are captured. The dialogue has four checkboxes and displays the captured coordinates.
- The GM can uncheck a corner and capture it again until satisfied with the footprint.
- Clicking **Save** after reviewing the corners opens the GM sale setup dialogue. The author then sets name, final price/capacity, Furnished/Empty, original fixtures, entrance doors and sign position before listing the property.
- The GM sale setup includes a per-property **Furnished / Empty** choice, confirmed by the user. Which furnishings are included and how built-in/static decoration is handled still require implementation rules; this choice does not authorize deleting arbitrary existing contents.
- Include an editable **storey count** in the GM tool and an automatically suggested, editable price based on usable floor area. Confirmed configurable defaults: **1,000gold per living tile** and **250gold per garden tile**, with a final property-price override.
- Measure the garden separately from the living space. The user's latest layout has four **Living space corner** checkboxes with coordinates, **Floor count**, a **Has garden** checkbox that opens a separate four-corner garden selector, and **Save settings and exit**. Both selectors allow uncheck/recapture. Garden corners are numbered1–4 (the repeated1s in the sketch are labels to normalize).
- Retain the earlier measurement-to-sale flow: **Save settings and exit** exits measurement and opens the GM sale setup dialogue. A garden is optional and admits visitors. Its default rate is250gold/tile, excluding the building's projected footprint. Each measured area is a rectangle with inclusive corner tiles.
- The user accepted separate indoor/garden pricing, with the garden counted once and building-footprint overlap excluded from its charge. Rates and final price are editable. The subsequent per-floor decision below refines the indoor calculation.
- **Confirmed per-floor measurement:** floor1 is measured with its four corner checkboxes. Each additional floor has its own optional four-corner measurement. Leaving its checkboxes unchecked derives its area from floor1, including when a different upper floor has an explicit override. A GM may measure an upper floor separately when it has a different usable area. The effective indoor area is the sum of each floor's explicit area or inherited floor1 area. Partially captured upper-floor overrides must be completed or cleared before saving; they must not silently become inherited measurements.
- **Confirmed garden access:** gardens remain open to visitors. Entering the garden does not grant permission to enter the private building or use its storage. House-entry invitations and storage permissions remain separate.
- **Confirmed ownership limit:** one home per account, shared across placed and town houses. Other characters on the same account have full property access, including storage and management. Once any character unlocks housing, any character on that account may buy/place its home. This shares housing eligibility and access, not recognition certificates or class progression.
- Reuse native placed-house ownership safeguards; town housing is the new custom behavior. Integrate town ownership with the same native account-house accounting so a town home blocks a second placed home, and a placed home blocks a town purchase/transfer. Do not build an independent limit that leaves either direction unprotected.
- **Confirmed town-house disposal:** owners may sell back to the town or transfer to another eligible account. Town buyback refunds **half the recorded price paid to the town**, retaining that basis through player trades; private trade valuations do not change it. Refund into the seller's bank. Transfers should use the existing native UO house-trading system wherever possible.
- **Confirmed empty-before-buyback:** the owner must remove personal belongings before selling back to the town; refuse the sale while belongings remain, rather than deleting or automatically packing them. Check the sold property, including its garden. **Confirmed fixture treatment:** original property fixtures may remain; everything added by players must be removed, including garden items. Track authored original fixtures separately from player additions. Player transfers retain the native contents-in-place behavior, subject to the agreed sale terms.
- **Confirmed storage default:** use limits comparable to a similarly sized native house, with a GM override. Choosing the native comparison/template mapping is an implementation task; no new storage-count formula has been adopted.
- **Confirmed replacement policy:** recognized characters may purchase replacement personal wardrobes at a town hall. Replacing the access furniture does not alter stored class equipment or progression. The original ceremony still awards its first wardrobe free. **Confirmed replacement price:**1,000gold, configurable.
- **Confirmed initial inactivity policy:** disable automatic home decay initially so absence does not destroy homes or belongings. This applies to the T42 housing experience across placed and town homes; no rent/tax/upkeep is introduced.

### Native ownership safeguards inspected

`BaseHouse.HasAccountHouse` checks each character on the account against native owned-house registration (`HasHouse`). The placement tool calls it at selection and placement/confirmation stages, and native house transfer calls it for the recipient. T42 registers static town properties in that same ownership system and extends native owner equality across the account independently of the expansion setting. Character deletion continues to use native owned-house reassignment to another account character.

Native `HouseTransferContract.AllowSecureTrade` checks ownership, living parties, the recipient's account-house limit and transfer position before accepting. On successful native trade, the house changes owner, old keys are removed, locks change and ban/friend/co-owner lists clear. T42 uses native trading, adds account recognition checks at acquisition/transfer stages and clears obsolete access invitations when the owning account changes. Real property contents and the original town receipt survive player trading.

### Requested measurement layout

```text
Floor 1 living space corner 1 [ ]  coordinates
Floor 1 living space corner 2 [ ]  coordinates
Floor 1 living space corner 3 [ ]  coordinates
Floor 1 living space corner 4 [ ]  coordinates

Floor count: _

For each upper floor:
    Corner 1 [ ]  coordinates
    Corner 2 [ ]  coordinates
    Corner 3 [ ]  coordinates
    Corner 4 [ ]  coordinates
    All unchecked: use floor 1 area

Has garden [ ]  -> opens garden corner selector

    Garden corner 1 [ ]  coordinates
    Garden corner 2 [ ]  coordinates
    Garden corner 3 [ ]  coordinates
    Garden corner 4 [ ]  coordinates

[Save settings and exit]  -> sale setup
```

The implementation shows each floor's effective area and whether it is inherited or measured, then the effective garden area and suggested quote before listing. Turning Has garden off retains its draft corners but excludes them from the active definition/price until re-enabled. Four distinct rectangular corners may be captured in any order; each area is at most64×64 tiles and its corners must have consistent elevation. A floor covers its measured20-unit vertical band, considering the entering body's height; inherited floors advance20units. Explicit floors must ascend by at least16units. The GM must measure actual usable rectangular rooms, rather than treating an outer building outline as automatically usable interior space.

## Native price comparison — 21 October 2026 (JST)

The pinned native placement catalogue uses individual fixed prices, not a single usable-area formula. `HousePlacementEntry.ClassicHouses` in `upstream/ModernUO/Projects/UOContent/Multis/Houses/HousePlacementTool.cs` supplies the purchase prices below. Architect stock sells the placement tool for627gold; that tool price is separate from the house purchase cost. `BaseHouse.DefaultPrice` values differ from the placement catalogue and are not substituted for these purchase quotes.

A read-only inspection of the installed `MultiCollection.uop` and `tiledata.mul` estimated flat standing floor tiles on the ordinary room levels. It deduplicated x/y/standing-z positions, checked16units of static headroom and excluded impassable tiles, stairs, sloping roofs and separate rooftop decks. These are rough architectural comparisons: they are not a full movement/reachability audit, and floor counts can include thresholds or balconies. No live world was changed.

| Native classic design | Purchase gold | Estimated flat floor tiles, all room storeys | Approximate gold/tile |
| --- | ---: | ---: | ---: |
| Small stone/plaster cottage | 37,000 | 26 | 1,423 |
| Brick guildhouse | 131,500 | 131 | 1,004 |
| Two-storey wood/plaster house | 162,750 | 169 (88 + 81) | 963 |
| Log cabin | 81,750 | 106 (59 + 47) | 771 |
| Two-storey villa | 113,750 | 116 (62 + 54) | 981 |

**Confirmed default rates:**1,000gold per living tile and250gold per garden tile, both configurable. The confirmed calculation is **sum of effective floor areas × indoor rate + non-overlapping garden area × garden rate**, with a GM final-price override. Each unmeasured upper floor inherits floor1's area; individually measured floors use their own area instead. The four property corners should not automatically imply that every tile is usable room space. No separate furniture, balcony/roof or town-premium surcharge is adopted; authors can explicitly override the final price.

## Deferred world authoring

All gameplay choices needed for T42 are settled. The GM selects suitable town buildings and authors their individual measurements, original fixtures, doors, signs and final price overrides through the tool. T42 installs the native architect catalogue and preserves native wilderness placement rules; it does not silently appropriate existing civic/NPC buildings for sale. Actual town-property availability and the ordinary-play saving timescale can be tuned through those authored listings.

Private workshops remain deferred, preserving current public-facility and portable-processing rules. No furniture surcharge, separate roof/balcony tariff, town premium, recurring charge or automatic belongings recovery has been added. The final price remains the author's explicit override.

## Validation and deployment

- Native, project-content and integration builds pass with zero warnings/errors using the repository's normal custom build targets and package audit. The native patch series reproduces exactly and reverses:40 patches/91 paths; unchanged client series32 patches/40 paths.
- `HousingChecks` covers account entitlement without sharing recognition/wardrobe awards, cross-route ownership caps, inheritance and partial/duplicate corner rejection, real bank payment, original secure fixtures, private/open-garden/forced-entry behavior, original-container and garden belongings, real native house transfer, receipt retention, half-price buyback/relisting and paid recipient wardrobe access. It also removes a GM listing to prove existing doors/furnishings survive and the region unregisters.
- Focused and cumulative source checks and installed cumulative/legacy checks pass through four save/boot cycles. Reload assertions wait11seconds, past native unowned-house retirement. Empty listings retain a real robe inside the serialized furnishing store, exercising the native inaccessible-item cleanup boundary too.
- Actual ClassicUO checks pass for four-corner capture/redo, three floors with inherited/measured areas, garden, target cancellation, Furnished/Empty, listing, purchase, native management, blocked/confirmed buyback and replacement payment. Source and installed UI fixtures each survive four boots. Screenshots are under `.local/logs/t42-*.png` and were inspected for clipping/readability.
- The regenerated first-loop catalogue passes its consistency check; all41 Python tests pass. `git diff --check` passes. Test accounts, GM privileges, houses and the integration assembly remain confined to disposable worlds.
- Two-boot copies of the actual source/installed/post-live saves preserve both existing players, every recorded profile/history/allocation/binding/title/native skill/cap/lock, gear and material identity, and all485 existing gold stacks. The only gold additions are22 native architect wallets; the inspection records their exact NPC/backpack/coin identities and verifies their persistence separately.

Matching rollback backup: `.local/backups/pre-t42-20261021-175550` (complete server/client). The deployment preserved68 protected files byte-for-byte during staging, including configuration, saves and client preferences; only native UOContent/project-content DLLs/PDBs were replaced. Engine/logger, installed application launcher, client binary and production assembly list remain unchanged. Production loads only `UOContent.dll` and `Sanctuary.Content.dll`. The deployment manifest is `.local/logs/deploy-t42/deployment.json`; source/installed/copy evidence is under `.local/logs/t42` and `.local/logs/deploy-t42`.

Live boot1 saved at18:06:50JST and shut down cleanly. Boot2 loaded4446 items/536 mobiles, retained all22 architect identities, and saved5013 entities at18:08:55JST. The server is running at127.0.0.1:2593; the desktop client remains closed. No player home or town building was auto-claimed; the GM can now author town sale listings.

For rollback, stop the server/client and restore the matching backup's runtime **and saves together**. The old project content cannot load new town-property/storage item types once used. Restore the matching `.local/content/Sanctuary.Content.dll` too before using `./dev server`, which automatically stages that working build; alternatively launch the restored runtime directly. Never replace running DLLs or rebuild assemblies while test fixtures symlink them. Source and native patches are included in the T42–T44 Git checkpoint; runtime backups and operational evidence remain local.

References: [roadmap](implementation-roadmap.md), [decision worksheet O06/O07/Q02/Q10](economy-open-questions.md), [recognition](t14-recognition.md), [real class storage](t40-wardrobe-transactions.md), [original NPC housing vision](../Britannia-Diablo-Project-Kickoff.md#13-living-npcs-and-housing).
