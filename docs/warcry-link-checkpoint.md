# War Cry, Leap landing and Health Link

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. The existing [Health Link rule](defiler-health-link-rule-proposal.md) requires settled non-poison damage components.

The accepted old DLL reproduced the fault on War Cry: stun and the full 4,097 FP256 primary damage applied, but linked damage was 0 instead of 2,171 because the event had no components. Its single damage draw was correct. The baseline was frozen before repair.

One health call in the existing weapon-independent physical receiver now publishes its settled components. Both War Cry and approved Leap Attack landing damage use that receiver. Damage, shield block, accuracy behavior, resistance, RNG, hit recovery, geometry, timing and payment retain their existing paths. War Cry still applies stun after mitigation and before health, including against physical immunity; a blocked hit applies neither damage nor stun. Leap landing keeps its separate later weapon contact.

**Content 1 / Integration 2 / source 2 / native run 1 passed 57 exact results, twelve connections, nine actual Leap Attack inputs, three saves and two reloads.** Both accepted builds have zero warnings/errors. The first corrected native run passed without fixture or driver repairs. There are no save-pruning warnings; six scalar refusals are intentional controls, two per boot.

Ten new contexts repeat per boot: War Cry's actual hit/stun route and the shared receiver without a stun callback, each with normal, primary resistance, secondary resistance, immunity and shield-block controls. They check exact FP256 primary/share loss, typed root identity, no recursive events, one damage draw, stun-before-health and no added contact-level mana debit. A temporary skeleton uses the existing shield routine; original link/shield state and global hooks are restored afterward.

The landing contact matrix calls the shared receiver directly. Retained tests separately cover the actual approved landing area, distinct weapon strike, flight/sequence/clock, left/right/quick inputs, War Cry source curves, real cast/rays, saved stun/delay/shield/fractional mana and resumed ray damage without payment replay. This checkpoint adds no new War Cry wire-input test and does not certify the full Barbarian class.

**Known unfinished integration:** War Cry currently adds no Magical Penetration elements despite the generic [T29 spell rule](t29-combat-potions.md). This is not an approved exclusion. Integrate its potion payload through its own producer context so the shared Leap weapon-skill receiver does not acquire an unintended blanket change. This health-component fix preserves the existing calculation and records that gap in the roadmap.

Only the War Cry and Leap Attack remaining-description rows change. All 240 handlers, numerical data, completed-class count and 573 named-definition outcomes retain. No new save field/version, native/client build or asset. Installed build, saves, configuration and client remain protected. Final effects/audio and talent/hotkey icons remain separately tracked placeholders. Exact modern ordering, special/PvP recipients and other documented limits remain open.

[Receipt](../tools/reference/warcry-link-native-evidence.json), [matrix](../tests/Sanctuary.Integration/WarCryLinkChecks.cs), [driver](../tests/check_warcry_link.py). Exact baseline, accepted assemblies and immutable evidence: `/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/warcry-link`.
