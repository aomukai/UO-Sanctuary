# Shadow world admission before NPC casting

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first.

The attempted next connection was a real Shadow Fire Blast cast using the shared startup/do dispatchers and existing lob/elemental spell services. Its current skill row needs no nested start function or LOS test. The first missing prerequisite is earlier than projectile delivery: **the Shadow resource recipient currently requires the body to stay on `Map.Internal`.**

`ShadowEquipmentResources.Require` rejects world maps and activation. `AssassinShadowBody` also reports `CanActivate=false`, stops its AI and rejects ordinary melee. Those safeguards express unfinished world/combat support; they are not a claim about D2R behavior. Removing the map check merely to exercise a spell would expose a body whose incoming damage, events, controller and modifier-42 death behavior have not been qualified.

Before live NPC spells, provide coherent world admission and ownership/parking/death behavior, preserve the original resource/item/passive graph and wounds across moves/reloads, and connect incoming/outgoing combat and actual NPC mode/action events. Then reuse the shared startup/do dispatchers, owned skill ranks, lob travel and elemental receivers. Do not replace the Shadow with a player cast or a Valkyrie source profile.

This is a documented nonblocking skill prerequisite. Shadow Warrior/Master stay unavailable; other class work can proceed. Their body `0x190`/hue `0x455` appearance placeholder and separate effect/icon work remain unchanged. No user question is required to retain these safeguards.
