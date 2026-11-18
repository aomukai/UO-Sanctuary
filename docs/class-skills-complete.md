# Class skills mechanical release

**Mechanically qualified: all eight classes, 240 talents, under the approved Sanctuary rules and current supported PvE/world catalogue.** Reference: **D2R Reign of the Warlock 3.3.93847**. This closes the class-skill implementation goal; it does not claim unverified unpublished D2R behavior or finish the separate artwork and world-content systems. [Release evidence](../tools/reference/class-skill-release-evidence.json), [qualification policy](../tools/reference/class-mechanics-release-policy.json), [supported contexts and limits](class-mechanics-supported-contexts.md).

| Class | Talents | Mechanical evidence |
| --- | ---: | --- |
| Necromancer | 30 | Dedicated class lifecycle/input, approved pet policy and snapshot review, rendered Warrior/Teeth and natural pet combat |
| Sorceress | 30 | Dedicated class lifecycle/input and passive consumers, rendered Fire Bolt damage |
| Paladin | 30 | Dedicated class lifecycle/input and approved aura/contact policies, rendered Holy Bolt damage |
| Barbarian | 30 | Dedicated class lifecycle/input and passive consumers, corrected Bash, approved Find Item, closing Leap/throw edges and rendered Find Item |
| Amazon | 30 | Dedicated class lifecycle/input and passive consumers, approved Pierce/Impale/Lightning Strike rules, rendered Inner Sight |
| Assassin | 30 | Dedicated class lifecycle/input and passive consumers, approved martial/trap/Shadow rules, rendered Psychic Hammer |
| Druid | 30 | Dedicated class lifecycle/input, approved pet/snapshot/parking policy review, rendered original Spirit Wolf |
| Warlock / Witch | 30 | Dedicated class lifecycle/input and passive consumers, approved demon/Hex/Sigil/Entropy rules, rendered original Goatman and both creation variants |

The retained class input audits account for **210 active talents and 30 passive entries**. Every class has dedicated mechanics and saved-lifecycle scopes; current client inspection adds representative casts rather than rerunning all unchanged cohorts. The approved Necromancer and Druid pet changes retain their own explicit consumer evidence. All eight creation choices and both Warlock/Witch submissions are verified.

The final production code includes the corrected post-critical Bash flat damage and shared near-cursor Find Item acquisition. Focused qualifications retain 64 Bash results/24 inputs, 37 combined Find Item results/six inputs, 12 closing Barbarian edge results and their two-reload scopes. The final six rendered classes add 12 consumer results and six disposable saves. Historical receipts remain tied to their original artifacts; the three changed content files and existing Sentinel repair have explicit source bridges.

The matching content and **45-patch client** are installed. Native server binaries are unchanged. The original world loads **4,698 items and 572 mobiles** without saving over it; all **38 original save files, 20 configurations and client settings/data/profiles** retain their hashes. Integration stays out of production. Current rollback packages remain. Historical raw run archives were subsequently retired at the user’s request; see [archive retirement](class-skill-history-retirement.md). Main receives the complete reviewed history and current release checkpoint.

The final ledger records `mechanically-qualified-with-adaptations` for all 240 entries and all eight completed classes. Historical per-skill notes are retained as `historicalReferenceNotes`; they must not be mistaken for a current list of missing handlers. The baseline ledger is archived. This final promotion changes verification metadata only, not numerical balance or another gameplay rule.

Final **effects/audio** and **talent/hotkey icons** remain separate 240-entry registers with identified placeholders: [effects](skill-effects.md), [icons](skill-icons.md). The [supported-context review](class-mechanics-supported-contexts.md) separately identifies unknown modern callback parity, unclassified native species/PvP, unsupported original-item combinations and missing world services. Future global monster spell conversion, world density/players-5 balancing, crafting/charged gear and later melee quality-of-life design can be handled as smaller goals. Charms and runewords remain excluded.

Recheck current source/data and recorded qualification with `python3 tools/verify_class_skill_release.py --check` and `python3 tools/build_skill_roadmap.py --check`. Earlier audit readers remain historical recipes; their raw-run file dependencies are retired. The current checker uses committed release information and does not replay past runs.
