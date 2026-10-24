# Class skill reuse notes

Reference: D2R **3.3.93847, Reign of the Warlock**, with Sanctuary's approved deviations taking precedence. These notes identify reuse candidates; they do not certify unimplemented skills.

## Blade Sentinel and Miasma Chain

The user highlighted Miasma Chain as a future beneficiary of the Blade Sentinel work. Preserve that connection when the roadmap reaches Warlock/Witch.

The pinned rows distinguish their actual routes:

- Blade Sentinel creates a blade actor, whose first AI evaluation creates a following collision missile (`pSrvDoFunc=20`, `pSrvHitFunc=37`). Its damage packet captures the player's weapon before ownership transfers to the blade. Movement and AI observe the source frame clock.
- Miasma Chain (`sourceKey=Miasma Chains`, skill399) uses skill do167 and `miasmachainsmaker`. Its missile rows declare maker do43, travelling link do45/hit41, a separate cloud maker do43 and a cloud field do41. Link anchor/length, attached-unit death and location options are separate table inputs. These new engine routes are not present in the older reconstructed source and still need current-build evidence.

Reuse the frame conductor, persistent effect identities and clocks, immutable damage packets, ownership separation, ordered collision/footprint helpers and contact-delay machinery where the actual contracts match. Miasma Chain has magic damage and link/cloud behavior; do not copy Blade Sentinel's weapon fraction, NPC attack rating, monster AI, reversal rules or lifetime merely because the skills look related. Keep link motion, target acquisition, cloud placement, duration and current hit-delay scope explicit until verified.

Source rows: [class skills](../data/Sanctuary/Balance/class-skills.json), [pinned missiles](../tools/reference/d2r-named-equipment/json/missiles.json). Original table provenance and hashes are pinned by [the mechanical reference](skill-mechanical-reference.md). Required art remains in the independent [effects](skill-effects.md) and [icons](skill-icons.md) registers.
