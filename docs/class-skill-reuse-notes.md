# Class skill reuse notes

Reference: D2R **3.3.93847, Reign of the Warlock**, with Sanctuary's approved deviations taking precedence. These notes identify reuse candidates; they do not certify unimplemented skills.

## Blade Sentinel and Miasma Chain

The user highlighted Miasma Chain as a future beneficiary of the Blade Sentinel work. Preserve that connection when the roadmap reaches Warlock/Witch.

The pinned rows distinguish their actual routes:

- Blade Sentinel creates a blade actor, whose first AI evaluation creates a following collision missile (`pSrvDoFunc=20`, `pSrvHitFunc=37`). Its damage packet captures the player's weapon before ownership transfers to the blade. Movement and AI observe the source frame clock.
- Miasma Chain (`sourceKey=Miasma Chains`, skill399) uses skill do167 and `miasmachainsmaker`. Its missile rows declare maker do43, travelling link do45/hit41, a separate cloud maker do43 and a cloud field do41. Link anchor/length, attached-unit death and location options are separate table inputs. These new engine routes are not present in the older reconstructed source and still need current-build evidence.

Reuse the frame conductor, persistent effect identities and clocks, immutable damage packets, ownership separation, ordered collision/footprint helpers and contact-delay machinery where the actual contracts match. Miasma Chain has magic damage and link/cloud behavior; do not copy Blade Sentinel's weapon fraction, NPC attack rating, monster AI, reversal rules or lifetime merely because the skills look related. Keep link motion, target acquisition, cloud placement, duration and current hit-delay scope explicit until verified.

Source rows: [class skills](../data/Sanctuary/Balance/class-skills.json), [pinned missiles](../tools/reference/d2r-named-equipment/json/missiles.json). Original table provenance and hashes are pinned by [the mechanical reference](skill-mechanical-reference.md). Required art remains in the independent [effects](skill-effects.md) and [icons](skill-icons.md) registers.

## Blade Shield weapon-area callback

[Compiled source orchestration](../tools/reference/blade-shield-source-evidence.json) separates Blade Shield's gameplay from its cosmetic attachment. Each periodic area pulse rolls its skill damage once and copies that packet per target; each target then gets its own hit resolution, current weapon fill and successful-hit durability/event delivery. Reuse the established weapon damage, poison, critical, defense/block and hit consumers where their contracts match. Blade Sentinel's immutable first-missile weapon snapshot is a different contract and must not become a Blade Shield snapshot by accident.

The periodic driver keeps the cast rank in its state list, while hard synergies and weapon suppliers need their own live-source treatment. The attachment's `Half2HSrc` flag does not by itself modify the separate area callback's weapon fraction. Source-start mana payment, initial animation/pulse order and native saved self-state/input remain explicit implementation work. The independent skill-effects and skill-icons inventories continue to hold the required placeholders.

## Raven summon stats and blindness

Raven reuses the existing 111-row summon level table through `ValkyrieCreation.BaseAtLevel`. Its explicit `max(1, owner level - 2 + skill rank)` stays on the pet; only the stat-table lookup clamps. Valkyrie and the earlier default-level summons retain their existing level formula through the original wrapper. Do not duplicate the table or cap Raven's level to 99.

Keep birth stats separate from the monster attack-mode callback. The inspected source refreshes physical endpoints and flat skill attack rating from the owner's live Raven rank. Its first AI evaluation separately initializes the finite attack budget. Current animation metadata and source AI branches still require actor/event integration; neither a skeleton attack cycle nor a sentry firing sequence establishes Raven timing.

The local current-build Raven property supplies signed `stupidity=-1`. The ordinary player blind entry requires a positive bonus, while the prepared summon entry accepts any nonzero signed value and draws before downstream curse eligibility. Both share the existing blindness state and duration consumer. Prepared source/native comparisons and the explicitly named NPC proxy are recorded in [component evidence](../tools/reference/raven-stats-native-evidence.json); they do not enable a playable Raven or prove its flight, attack-event delivery or saved lifecycle.
