# T16 — corresponding-tool equipment salvage

T16 puts the existing salvage transaction on the equipment's matching production tool. Double-click the tool and choose **Salvage gear**, target an eligible item in your backpack, then confirm its destruction. The older `[Essence` / Imbuing menu remains a convenience entry but requires and binds the same matching tool; it cannot perform tool-free salvage.

| Equipment family | Tool | Physical output |
| --- | --- | --- |
| Metal armour/weapons/shields | Smithing hammer | Metal Scraps |
| Wooden armour/shields, bows, wooden melee/staff/wand bases | Fletching tools | Wooden Chips |
| Leather and cloth armour/clothing/footwear/tabards | Tailoring kit | Leather Strips or Cloth Scraps |
| Bone armour and the authored bone weapon family | Tailoring kit | No physical output; Magic/Rare essence remains possible |
| Jewellery | Tinker's tools | Metal Scraps |
| Magical spellbooks | Scribe's pen | No physical output; Magic/Rare essence remains possible |

Native wood-family weapons defaulting to iron still recover wood. Coloured materials do not change the generic recovery family. The ten deferred exotic material recipes remain deferred: glass/stone/scale bases retain their existing essence-only recovery, using fletching tools for staff-family bases and a smithing hammer for sword/hard-armour bases. Native artifact names do not activate Unique rarity.

Anyone with a class profile can salvage, including an unconfirmed or different active craft and zero matching skill. The operation requires an accessible, movable, functional production tool with uses remaining; it consumes no uses, gold, ingredients or skills and grants no skill gains. Salvage is portable and needs no town facility. Existing native resource processing keeps its separate equipment requirements. Repair-only tools, scissors, saws and Imbuing ladles do not substitute for the selected production tool.

Dropped, crafted and gambling-loot equipment is eligible. Gambling uses the existing Drop origin; a gambling vendor is not introduced here. Vendor, starter/newbie, blessed, insured, quest/nontransferable, inaccessible, immovable/locked/secure, equipped, held and foreign items are rejected. Native equipment scissors, forge resmelting and salvage bags remain blocked from deterministic equipment recycling, including otherwise eligible dropped/crafted gear. Non-equipment resource processing remains available under its own rules.

Recovery values are unchanged:

- Magic:80% chance of1–2 Magic Essence; Rare:90% chance of2–5.
- Physical material:85% chance of`[1+s,3+s]`, where`s=floor((ilvl−1)/10)` and ilvl is clamped to1–99.
- Essence and physical recovery use independent chance/quantity rolls after confirmation. White gear has only its physical entitlement. A valid attempt can yield nothing and still consumes the host.
- Unique95%/5–12 is reserved and inactive. Salvage gold cost is fixed at zero; the retained compatibility field must remain zero.

The confirmation shows the selected item/icon, rarity/ilvl, recovery ranges/chances, zero cost and warnings that the host and filled contents are lost. Attached real contents are recursively destroyed with the host, including when every recovery roll fails; contents, native wand charges, stored spells and empty/filled socket counts never increase yield. Socket insertion itself remains a later card. Character-owned potion-belt storage is independent of a backpack tabard and is not treated as that tabard's contents.

Quotes are one-use, per-character and connection-bound; a newer salvage quote replaces the older one. Cancel consumes the quote without consuming items. Disconnect/restart invalidates pending quotes. Commit rechecks actual tool identity/ownership/parent/uses and item ownership, native material/wear/charges, stored spells, hue/art/weight, origin, saved gear state and the entire contents graph. Moving, changing, replacing, losing or using a quoted item/tool requires a fresh confirmation.

All rolled outputs must fit before gear destruction. The existing conservative spare-slot/weight reservation remains: it does not count the host's freed space, and may reject a space-neutral operation in a full pack. Rejection delivers nothing and preserves the host, contents, gold and tool. A successful operation creates real stackable Crafted-origin resources and merges compatible backpack stacks. Save/reload cannot repeat a completed transaction or restore its destroyed host.

## Verification

Integration checks cover all five tool families, physical material routing and exotic/bone exceptions; zero skill/unconfirmed and different active crafts; matching-tool lookup through the common menu; origin/protection/equipped/dead/foreign rejection; native scissors/resmelt/salvage-bag bypasses; level1/30/99 and Magic/Rare/white ranges; independent misses, zero yields, filled equipment, charged wands and spellbooks; changed nested gear/contents/native wear/tool ownership/uses; newer/replayed/reconnected quotes; full-slot/overweight atomicity and four-boot output/item/tool/skill persistence.

Actual ClassicUO checks open the production tool outside a public workshop with zero matching skill and another selected craft. They exercise target/confirmation/cancel, wrong-tool and vendor rejection, losing the tool before confirmation, successful destruction of filled gear, zero gold/use consumption, unchanged skills/craft, and replay rejection. Local screenshots are `t16-salvage-*.png`.

Status: implemented, tested and deployed20 October2026 (JST).

## Deployment evidence

Content/integration builds have zero warnings/errors. Source and installed four-boot cumulative suites pass salvage, wardrobe/recognition/respec, death gold, repairs, town markets, reward lives, effective gear, tool/equipment crafting and processing. The older economy/item-state comparison passes four boots with the matching tools added to its fixtures. Actual ClassicUO interaction and all16 Python checks pass. The refreshed authoring evidence retains24 affix pools and1536 sampled rolls with the tested content hash.

The live world was saved and cleanly stopped before the full server/client backup at `.local/backups/pre-t16-20261020-033914`. All60 protected files survive staging unchanged. Engine/logger/client binaries and34 configuration/client/assembly-list payloads, including13 client preference files, remain unchanged after both live saves/restart. Matching tested application/content is installed, with only UOContent.dll and Sanctuary.Content.dll registered; no integration fixtures/assembly are installed in production.

Both live boots/save pass with all22 civic offices and476 merchants retained without additions. Pre/staged/post-live copied-world snapshots preserve both characters/allocations/raw attributes/native skills/caps/locks/recognition, gear/materials and485 physical gold stacks with their amounts/parents/origins. `.local/logs/deploy-t16/deployment.json` records artifact hashes, rollback and preservation evidence. Source/client evidence is in `.local/logs/t16/`. The second live boot remains running; the client is closed. Source changes remain local for the next requested Git checkpoint. T17 is next.
