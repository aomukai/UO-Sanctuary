# Fire Golem death source review

The later native implementation is tracked in the [delayed-death checkpoint](fire-golem-death-checkpoint.md). The source-only scope described below remains historical evidence, not its native acceptance certificate.

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations taking precedence. This is source preparation; no runtime behavior, installed build or saves change in this increment.

The [reproducible source receipt](../tools/reference/fire-golem-death-source-evidence.json) composes **5,184 complete retained-owner explosion contexts** under ASan/UBSan. It reuses the existing sentry owner/damage harness and verifies unchanged bodies against the pinned source tree. Registry, room enumeration, radial barriers, allocation, block results, random draws, stats and empty event subscribers are declared services. These contexts do not establish current executable or native corpse lifetime equivalence.

The Fire spawn branch assigns monster modifier **31**, `CORPSEBOOMDEATH`. Its dispatch row selects `MONSTERUNIQUE_ColdEnchantedModeChange` for mode entry and `MONSTERUNIQUE_CastCorpseExplode2` for the later event. The complete scheduler queues event type 7 **four frames later** when the actor is in death mode. Spawn/dispatch links are statically reviewed; the callbacks themselves execute. Direct callback controls in other raw modes do not imply those requests are reachable gameplay.

The callback creates missile **117**, `monstercorpseexplode`, then supplies a fixed **100 physical + 100 fire** packet, **radius 6** and find flags **0x583**. The current skill table's three death formulas agree with those constants. The complete radial wrapper explicitly excludes both the golem and its owner, checks allegiance and radial barriers, and gives each recipient a packet copy. Allocation failure or missing source registration prevents damage. Retained golem/master death modes alone do not prevent damage in the declared context.

The current missile row has **no GetHit, SoftHit, KnockBack or server-damage callback field**. Its generic result routine still checks missile blocking and the monster's intrinsic **5% critical chance**; critical doubles both channels and consumes its draw even for a blocked recipient. Declared blocking suppresses health loss. Immunity and partial physical/fire resistance controls independently check final life. The explosion does not inherit ordinary weapon or Enchant damage, and its row does not request ordinary get-hit recovery.

The complete owner, allegiance, result flags, intrinsic critical, resistance/total damage, event health and missile-death routines are executed. This is stronger than treating the three skill-table constants as the whole mechanic. Full arbitrary reactions, absorption/leech contexts, real filter/geometry/enumeration and current executable behavior remain outside this proof.

The replacement source also differs from the native adapter: an undeleted source pet enters kill/death mode; an already deleted pet is removed. This call path is reviewed, while full kill entry, modifier scheduling, stat cleanup and final corpse/GUID removal remain unexecuted here.

The native lifetime dependency remains open. D2's missile lookup requires the dead golem to remain registered. UO's ordinary NPC death calls `Delete()` and clears ownership immediately. The existing sentry review already identifies the same retention requirement. **Do not create an immortal substitute or use the player as the explosion source**: that could change critical, item/event and kill-credit behavior.

Read-only PE metadata from the actual tested kernel confirms that `Server.Mobile.Alive`, `Delete`, `Kill`, `OnBeforeDeath` and `OnDeath` are virtual and non-final. The receipt binds those assembly hashes. This establishes a route to bounded retention of the **original** actor, without a stand-in or player substitution. It does not implement that state or verify its behavior. The next step must preserve normal death bookkeeping, exclude the retired actor from the live roster/actions, retain ownership for damage credit, handle saves and dispose of the original actor exactly once after the callback.

The current immediate burst remains an explicitly unfinished legacy adapter; this source review does not certify it. Proceed with the bounded original-actor retirement path; the other Necromancer curse checks remain available if a specific lifetime rule cannot yet be established. Fire body/explosion/critical effects and audio remain placeholders in [effects](skill-effects.md), and its talent-tree/right/quick icons remain separately pending in [icons](skill-icons.md). All 71 Python tests and generated skill-data checks pass. No complete class or new server build is claimed; the compiled metadata reader only inspects PE tables.

Reproduce from the existing pinned cache:

```sh
python3 tools/probe_fire_golem_death.py --cache-root /media/aomukai/FILES/Sanctuary/.local/class-skills --check
```
