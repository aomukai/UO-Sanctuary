# Iron Golem ordered live contact checkpoint

Reference: **D2R Reign of the Warlock 3.3.93847**, with approved Sanctuary deviations taking precedence. The prepared Iron Golem world controller now uses the allocated contact/HP/return pipeline. Learned Iron Golem casting remains disabled, the candidate is not installed, and no class completion is claimed.

The action frame now captures hit/block results, original-item damage and mitigation, then dispatches qualified contact, native notifications and responsible-owner credit before one FP256 HP write. It commits captured cold/poison states, dispatches attack-attempt/flat retaliation and clipped percentage return, and lets the source return policy select native death, get-hit, block, knockback or refresh. The immediate `NpcEquipmentDamage.TryResolve` call is removed from this controller. Block presentation now belongs to return rather than the hit-result roll.

An exact-packet transient owner record preserves the Necromancer's entitlement if retaliation deletes the attacking golem and clears its master before the victim's deferred death. It is accepted only alongside the real native responsible entry, is tied to the same attacker, and is not serialized or inferred from another damager's history. Existing native ledger persistence remains unchanged.

## Verification

The [receipt](../tools/reference/iron-golem-ordered-contact-evidence.json), reproduced with `python3 tools/record_iron_golem_ordered_contact.py --check`, binds build9/run5 to:

- Six real original-item world deliveries per boot: Broadsword, Battlebranch, Lance of Yaggai, Rakescar, Gleamscythe and Shaftstop. Allocated totals, offensive RNG, pre-HP native notification, captured cold/poison, packet consumption and absence of next-frame replay pass.
- A real Graveyard Zombie exchange in which victim HP reaches zero before flat retaliation destroys the golem/original; victim death follows once, with actual owner XP and once-only rewards after source removal.
- Five pre-roll refusals: no-heal, Crushing Blow, Iron Maiden, unresolved uninterruptible completion and a native Evil Omen marker. Two original-preserving birth refusals cover an unrecorded Baranar cold-duration fallback and Hellplague's unsupported fireskill writer.
- 79 native markers over three boots, two actual reloads and three saves. Retained scopes include 45,840 property-writer cases per boot, complete source contact/return/phases, allocation, native notification, original equipment, retaliation and connected natural world combat.
- Four separate Paladin/catalogue markers, all 71 Python tests, unchanged 573 named decisions and all 106 installed artifacts. Fresh audits cover 815/21 fingerprints. No full-class regression or old-content-binary migration is claimed.

The metadata generator reproduces 21 item-event stat registrations and 751 class/base/model identities from the pinned primary tables. Active numeric original stat lists are checked before hit/damage RNG. Only flat ATD's actual shared event6/stat78 consumer is allowed; other active registrations and unsupported conditional stun/defense/no-heal paths refuse explicitly. Native recipient damage modifiers/overrides, unresolved mode completion, Paragon/Lightning Enchanted recovery callbacks and unreviewed identities also refuse. Owner Sunder is checked both at birth and live release, including later equipment changes.

The shared return fixture now observes the real startup reset and continues preceding consumers' hit-class cursor. It no longer assumes its command is the first live consumer. Its saved packet retains the selected class through both reloads. Failed-run logs retain the unrepresented item discoveries and two fixture corrections: level99 gives no XP, and notification-recording subclasses have no exact-type reward policy. The final reward check uses a real monster and a level47 owner.

## Remaining mechanics and assets

Finish learned targeting/placement/payment and full lifecycle/client acceptance. Implement the refused item/curse/Sunder/mode consumers and repair missing original stat storage; never reroll an existing Baranar duration. Native room/geometry, defender RNG, AI/display and block timing remain disclosed adapters, not exact current-executable certificates. Modern initial-poison applicability and Thorns coexistence remain unobserved. Callback/region rejection is fail-fast rather than transactional rollback.

Coverage remains **185 handlers / Necromancer29; no class complete**. Revive retains living-monster abilities; global UO-to-D2 spell conversion is separate. [Effects/audio](skill-effects.md) and [talent/hotkey icons](skill-icons.md) stay separate: the prepared body/contact/block/get-hit/overlay presentation remains identified placeholder artwork, including overlay0x37B9. No new independent icon is introduced. Working installation and saves are unchanged.
