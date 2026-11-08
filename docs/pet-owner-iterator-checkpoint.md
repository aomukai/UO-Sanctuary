# GenericPet player-owner iterator

2026-11-09 JST. Mechanical reference: **D2R Reign of the Warlock 3.3.93847**. Coverage remains **213/240 handlers, Warlock 4/30; no class certified**. This checkpoint prepares the player pet-store iterator used by demon initialization. Automatic AI and learned summon/Death Mark routes remain unfinished.

The complete current outer routine `35e6d0` (386 bytes, six fragments) and bucket routine `35e510` (446 bytes) establish the following behavior:

- Require a player owner with player data. A missing pet store returns immediately, before consulting the game or selector.
- Iterate pet-type buckets from 1 up to, but excluding, the supplied table count. Skip bucket 0. Read the game selector again after each completed bucket.
- Within a bucket, cache each node's next link before lookup; skip nodes whose low flag bit is set. Resolve other IDs in the monster registry using the existing inline ID/next158 traversal.
- Collect all resolved handles before invoking that bucket's callbacks. Preserve linked-list order and duplicates; ignore callback return values. Changes to the current bucket during callbacks cannot rewrite its snapshot, while later buckets and selector/table changes remain visible.
- Apply no additional life, ownership, facet, distance or LOS filter inside these routines.

`GenericPetOwnerIterator` implements this policy over explicit store services and binds the existing initializer's `OwnerUnits` dependency. It reuses `GenericPetRegistry`; it does not substitute the separate monster-owner iterator `GenericPetTraversal.Owned`. Managed storage replaces the temporary inline-64 pointer vector. The 65th entry is compared against the source with a declared growth service; raw allocator equivalence is not claimed.

Two bounded, read-only captures obtained the previously missing complete routines, totaling 2,784 bytes read including validation. Offline Unicorn executes all 832 routine bytes across **33 focused contexts**. Controls cover missing owner/store/game, non-player owner, invalid selector, empty/count boundaries, inactive flags, missing registry units, duplicate IDs, cached-next mutation during lookup, same/later-bucket mutation during callbacks, selector/count changes, bucket 21 and 65-entry growth. Seven controls stop at original assertion services. A separate null-game control encounters the original dereference at `35e715`; Sanctuary refuses that unsupported input rather than attempting an invalid read. It is not counted as an original assertion.

The first probe attempt stopped on that null-game dereference. Its exact script and log are retained under `.local/class-skills/pet-owner-iterator-source/failed-probe-1`; the corrected probe distinguishes it from assertions. No product or native server failure is implied by that probe diagnostic.

The existing original-demon cohort now passes an explicit pet-store graph through the new iterator into actual registered Goatman/Tainted/Defiler initialization, role-qualified native registry, command consumption/restoration and life producers. It covers Warlock and Witch owners, original birth snapshots and fractional wounds through two real reloads. Supplied inactive and unresolved nodes are omitted. The fixture recreates its store graph after load; **this does not prove a persisted or automatically maintained native pet store**.

The pinned JSON has 21 named pet types with IDs 1–21 (`demon` 20, `binddemon` 21). The native fixture explicitly supplies an upper bound of 22 from those IDs. The live compiled table-count supplier and the native insertion/removal/order producer still need binding. Fixture input order is not asserted to be D2's creation order or Sanctuary serial order. The approved Death Mark roster ordering remains specific to Death Mark.

Accepted **Content 1 / Integration 1 / snapshot 1 / native 1** passed **12 native markers across three boots, two reloads and three saves**, with zero build warnings/errors. Build/native results are recorded in the [native receipt](../tools/reference/pet-owner-iterator-native-evidence.json); current-byte comparisons are in the [source receipt](../tools/reference/pet-owner-iterator-source-evidence.json). Verification uses the existing command/three-species reload cohort rather than rerunning the large historical AI matrices. Installed build, configuration, saves and current client patches remain protected. No new effects, audio or icons were generated; those remain in their separate asset registers.

Next: bind the actual player pet-store producer, persistent AI-control words and timed AI event scheduling, then remaining live search/path/movement/combat/action services. Keep unsupported context and raw survey +8 lookup uncertainty explicit. Prepared routines alone do not enable automatic combat or learned casts.
