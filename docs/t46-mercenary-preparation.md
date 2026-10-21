# Mercenary care preparation

T46's pre-class scope is implemented in staging. This is saved care infrastructure and native integration, not a released mercenary recruitment/combat system. Full mercenary archetypes, skills and gameplay follow complete class skills. T48 play balance and final T49 integration remain deferred.

## Confirmed rules

- Resurrection costs **1,000 gold × the saved mercenary level**, paid from the owner's bank after confirmation. Equipment, nested inventory and the healing reserve survive death and resurrection. Cancel, insufficient funds, stale ownership, changed quote or an out-of-range provider cannot charge. An insufficient-funds quote is spent; reopen it to try again.
- Healing reserve has no gameplay capacity limit. Only the five ordinary healing potion tiers can be deposited, from the owner's backpack or held stack; mana, rejuvenation and combat potions are excluded. Deposited doses are no longer physical inventory and cannot be withdrawn, traded or used as player storage.
- Drink at **50% life or below**, at most **one dose per ten seconds**. Healing ticks using that potion's base amount/duration. Mixed reserves use the lowest available tier first. Existing healing settles before checking the next eligible dose. Warnings fire when drinking leaves **9, 5 or 1** doses: “below10” means9, not10. No extra zero warning was requested. Class-dependent mercenary bonuses remain later work.
- Retirement/replacement destroys all unspent doses and clears ongoing effects. Actual gear remains on an internal retired carrier until returned to its owner; failed recovery into a full backpack retains the original item. Retirement releases follower slots. Registered mercenaries cannot use native pet trading; a system-issued release cannot silently retire them. The native pet loyalty/abandonment cleanup exempts registered carriers.
- **Living healing is free; death is separate.** Merchant trade/conversation and wandering/town healers restore nearby living owned pets and mercenaries to full life outside combat. Foreign, distant, dead and fighting companions are excluded. A healer's Healing context entry offers the same service. These interactions never freely resurrect a mercenary. Ordinary pet resurrection rules remain native.

Owner and provider must be within 3 tiles and line of sight; companions healed as a group must be within 12 tiles of that provider. A current combat target or aggression within 10 seconds blocks treatment. Native vendor access and healer alignment restrictions still apply. The conversation path recognizes greetings, help/heal, buying/selling, “vendor …” and addressing the NPC by name; ambient unrelated speech is not a treatment request.

## Implemented boundary

`MercenaryCare` registers a trusted owned native creature, deposits doses, runs timed consumption, quotes/commits healer treatment, retires a contract and returns retained gear. Registration requires a controlled, non-summoned creature, level 1–99 and no other active registered contract for that character. It is an internal factory API; there is no player recruitment, deposit or retirement interface yet. Native healer contexts and the guarded resurrection confirmation gump are ready for registered mercenaries.

`SanctuaryMercenaryCare` save version 0 stores creature/owner serials, level, retirement state, five 64-bit dose counts and the next drink deadline. Loading rejects duplicate active ownership, invalid levels/counts and malformed records. Runtime timers resolve saved serials; they do not enumerate the world. Automatic drinking requires a connected owner. Native bonded death retains the actual carrier; native `KeepsItemsOnDeath` moves worn items into its backpack rather than dropping them. The future loadout layer must restore those slots before combat resumes.

Patch `0048-mercenary-care-hooks.patch` changes UOContent only. It supplies equipment retention, resurrection authorization, healer contexts, speech/trade callbacks, protected release/transfer behavior and the pet-cleanup exemption. Native veterinary/talisman resurrection calls cannot bypass the paid authorization. No engine or client changes were needed for this preparation.

## Return after class skills

1. Implement mercenary archetypes, recruitment/replacement vendors, actual skills, XP/level updates, stats, AI, orders and combat behavior. Keep the care profile's level synchronized with actual progression before quoting fees.
2. Implement owner deposit and loadout interfaces, explicit retirement/replacement messaging about lost potions, and retained-gear recovery. Preserve real named identities, sockets, affixes and nested items; restore worn slots after resurrection. Do not treat the internal retired carrier as an extra active mercenary.
3. Integrate class/character switching, login/logout, follower limits and dismissal with the completed mercenary system. Existing wardrobe behavior keeps ordinary pets/mercenaries with the character; class summons have their separate parking rules. Trusted mercenary code must retire/recover rather than delete a geared carrier.
4. Give native pet-only resurrection interfaces an early mercenary rejection before any veterinary consumables/skill-loss side effects. The low-level resurrection authorization already prevents a free resurrection; those interfaces are not a mercenary service.
5. Reconcile T49 NPC equipment/stat/effect consumers, including held named items once their prerequisites work. Existing shared item eligibility is unchanged by this pass; charms stay excluded.
6. Run complete mercenary/class gameplay checks, then T48 balance outings and final T49 integration. Preparation checks do not establish mercenary damage, survival, potion demand or gold balance.

## Verification

Disposable native carrier fixtures check real death, bank-funded resurrection, free living merchant/healer treatment, ownership/distance/combat exclusions, potion ticks and cooldown boundaries, a 1,200,001-dose reserve, release/retirement, capacity-safe gear recovery and four-boot persistence. A separate native check forces the hourly abandonment cleanup and preserves registered gear while retaining ordinary pet cleanup. The actual client checks the confirmation gump, insufficient funds, cancellation, exact 12,000-gold payment with retained gear/reserve, and connected-owner automatic drinking/healing with the real ten-second cooldown. Fixtures use a native dog with a humanoid body, not an implemented mercenary class. See the integration fixtures and `.local/t46-t47` logs; installed production remains T45.
