# T40 — Complete wardrobe transactions

A recognized character can now select any of the seven classes and any retained craft at a supported public wardrobe or their valid personal wardrobe. Both lists start with the current choices selected and show saved class levels. Switching is free, alive and out of combat, with no pending targeting, casting, crafting or recovery. Class-only, craft-only, simultaneous and unchanged selections use the same server operation. Existing action deadlines remain intact.

The wardrobe states: **“If you wish to share an item with another class, take it off now.”** Equipped items become the departing class's equipment set. Items left in the shared backpack can be equipped by another class normally.

## Real equipment

Each character owns at most one real stored equipment set per class. These private containers live outside the carried inventory and add no carried weight. Furniture is an access point: removing a house or wardrobe does not erase the character's stored equipment. Sets contain only equipment from the twenty supported wearable layers; backpacks, banks, mounts, appearance and the shared hidden potion-belt container are excluded. Immovable, quest-marked and nonempty container equipment cannot be stored.

The departing set receives the actual equipped objects. The target set restores its actual objects through native equip checks, including class, level, attributes, identification and conflicting layers. Stat-granting equipment can enable another item on a later equip pass. Items that cannot be worn go into the backpack. If the backpack cannot hold them, the operation restores the previous class, real item parents, equipment sets and resource values. A newly attempted class history/storage is removed on rollback.

Missing, sold, moved or lost saved items are skipped and counted in the result. The wardrobe never pulls them from another owner, bank or backpack. One real item cannot occupy two stored sets. Retrieve stored gear through the wardrobe's **Retrieve stored gear** screen to share it; retrieval needs backpack capacity and removes that item from its stored set. The private set cannot be opened or used as an ordinary bag.

Health, mana and stamina fractions are captured **before removing the departing equipment**, then carried into the fully equipped target's maximums, rounded down. A living-health result of zero cancels the swap. Weaker-class encumbrance is allowed and uses normal movement rules. Shared possessions, gold, native skills/caps/locks, effects and elapsed deadlines remain shared.

## Real class summons

Opted-in class summons have a saved owner, class, follower cost and optional absolute UTC lifetime. Inactive summons retain their actual identity, control master and current health, suspend actions on the internal map, and consume no follower slots. They do not think or heal while inactive. Lifetimes continue offline, while another class is active, and across server downtime; expired summons are deleted.

Only the active class's permitted summon set returns. Target skill limits and available follower slots are checked before moving equipment or changing class. Ordinary pets and mercenaries remain ordinary followers; their slots still count. They retain native logout/stabling behavior. A blocked return location leaves a summon internally parked, reserving its restored follower cost, until accepted movement provides a safe nearby position. No overlapping or cloned summon is created.

Resetting the active class dismisses that class's summons. Character death dismisses **all** class summons, including parked ones. Returning to a saved class does not resurrect a missing/dead summon or restore an earlier health snapshot.

## Sessions, persistence and client state

Wardrobe responses carry a one-use session, connection, source, active profile/profession and a quote of histories, equipment, stored sets, class bindings and follower structure. A stale or replaced selection must be reopened. Health and advancing clocks are deliberately not frozen in that quote. Retrieval independently rechecks ownership, current parent, source and capacity.

`SanctuaryWardrobeEquipment` indexes serialized private equipment containers. `SanctuaryClassMinions` stores class ownership, original slot cost and absolute expiry. Both validate identities on load. Existing characters acquire empty indexes without changing their progression or possessions; existing opted-in minions are adopted under their declared class.

Successful swaps send the new stat, combat, belt and class binding state immediately and close stale server talent/binding screens. ClassicUO's S panel, mouse pair and F1–F12 bar follow the active class. Shared belt contents remain in the same storage, with the existing overflow handling for a changed belt capacity.

Validation covers real client selection/retrieval and class-panel refresh, all seven histories, real gear identities/rolls, stat dependencies, ineligible fallback/full-pack rollback, stale quotes, missing ownership, immutable gear, low-life rollback, encumbrance, summon parking/health/slots/expiry, other-class respec and death, plus save/restart fixtures. Operational logs are kept in ignored `.local/logs/t40` and `.local/logs/deploy-t40`.
