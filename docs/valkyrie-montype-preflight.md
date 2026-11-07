# Valkyrie monster-type property review

The mechanical pin remains **D2R Reign of the Warlock 3.3.93847**. The property table defines `dmg-mon%` through function24 and stat `damage_vs_montype` (180), but explicitly labels it “Currently Not Used.” Enabled property metadata alone does not prove an item can roll it.

The pinned NPC catalogue's **991 affixes, 692 bases and 19 equipment rows** contain no producer of that property or stat. The current raw magic-prefix, magic-suffix, unique-item and set-item tables also contain no property reference. The current class-skill table contains no stat producer. The [preflight receipt](../tools/reference/valkyrie-montype-preflight.json) binds the exact inputs and scan scope.

Keep the existing defensive refusal for supplied/future nonzero stat180. Do not invent a mapping from native UO species to D2 monster subtypes or remove the generic allocator's real type service. This unsupported input does not block learned Valkyrie casting with the current generated-equipment catalogue. Generic target taxonomy, current executable parity and future item/skill sources remain separate work; this review changes no runtime code.
