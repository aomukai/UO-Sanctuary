# Learned Iron Golem casting checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations taking precedence. Iron Golem now has a learned handler and right/quick item-target casting. Coverage is **186 handlers / Necromancer30**. All30Necromancer talents have handlers; mechanical parity and class completion remain separate, unproven requirements. The candidate is not installed.

The shared ability model identifies Iron Golem as an item-target skill. Client patch0037 preserves the hovered non-corpse item's serial for right-held casts and requires an item for quick use. Server combat and hotkey routes keep that serial, enforce right-only binding, and derive the cast aim from the real original rather than quoted coordinates.

Both windup and release check the identified movable ground original, ownership, range/LOS, native placement, pinned start20/do57 row, numeric birth and current event capabilities. Unsupported originals refuse before consumption. Release allocates the existing prepared world actor, rechecks its binding and current capabilities, pays35mana once, and then commits the shared golem replacement. Allocation failure or insufficient mana restores the exact original, removes the unsuccessful candidate and preserves the old paid golem. Movement or competing item ownership cancels the pending attempt. The existing ground-pet search is reused for a blocked item tile; cast aim remains on the item while the available spawn position can be nearby.

## Verified scope

The [receipt](../tools/reference/iron-golem-casting-evidence.json), reproduced with `python3 tools/record_iron_golem_casting.py --check`, binds build6/run5 to:

- 94native markers across three boots, two actual reloads and three saves, including three real right-input casts and three real quick-input casts.
- Unpaid SC windup, authoritative item aim, exactly35mana on success, original identity/private storage, cap1replacement and once-only release.
- Three allocation/payment rollback controls, three moved-item/caster/lost-mana cancellations, and unsupported proc refusal before windup.
- Active original/full gear rolls, world controller, shared roster, injured FP256life, fractional mana, RNG and bindings across both reloads. Unpaid pending input does not replay. The fixture observes mana at WorldSave/WorldLoad boundaries and permits subsequent native regeneration; real Energy allocations persist through load.
- Retained original/body/world/ordered contact/return/retaliation/property tests, including45840writer cases per boot and the actual removed-pet owner reward exchange.
- Four separate Paladin/catalogue markers and71Python tests. All573named decisions and106installed artifacts are unchanged; audits cover817/21fingerprints.

Content and integration compile with zero warnings/errors. The isolated client builds successfully; its74warning messages match an unmodified isolated baseline. Patch0037applies cleanly to the existing client source. The installed client/source/build/saves are not modified. Compilation and actual server packet tests are not graphical playtesting.

Historical component assertions that Iron Golem must remain unregistered were removed when the learned route was added; their numeric/state controls remain. Failed-run logs retain two runtime gaps fixed here (left binding and blocked target placement) and fixture corrections for native mana regeneration and persisted Energy stats.

## Remaining acceptance and assets

The prior explicit unsupported item/proc/conditional-hit/Iron Maiden/Sunder/mode/recipient guards remain. Baranar's unrecorded independent cold duration and Hellplague's unsupported fireskill writer are refused without inventing values or consuming their originals. Full modern initial-poison/Thorns behavior, native geometry/nearby search/RNG/AI/display/mode adapters, complete exceptional-callback transactions and class-wide lifecycle/client acceptance remain open. No new full-class regression, old-content migration, deployment or class completion is claimed.

Final body/summon/contact/return/audio effects and talent/right-hotkey icons remain separate pending assets. Native summon0x376Aand sound0x1E5, the prepared Iron body and existing contact/overlay markers are identified placeholders. Asset creation does not block these mechanics. Revive still inherits its living monster's repertoire; global UO-to-D2 spell conversion remains a separate task.
