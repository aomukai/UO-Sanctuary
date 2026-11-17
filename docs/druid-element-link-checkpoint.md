# Druid elemental damage and Health Link

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This connects existing spell damage to the approved [Health Link contract](defiler-health-link-rule-proposal.md).

The accepted parent DLL reproduced missing sharing on Twister: full primary damage of 4,096 FP256, secondary damage of 0 instead of 2,170, and an explicit missing-components fault. The baseline was frozen before the gameplay change.

Seven health calls across six spells now retain their known settled non-poison channels: Twister, Fissure, Volcano, Tornado contact and copied area, Shock Wave, and Molten Boulder. Damage formulas, modifiers, shield/avoidance checks, source RNG, status, knockback, hit leases, geometry, timing and resource payment retain their existing paths. Poison stays in the primary total and outside shared non-poison components.

Molten Boulder's burning ground keeps its existing **Periodic** classification and never starts link sharing. Tornado's timed copied-area hits retain their existing **Direct** classification. Firestorm, Hurricane and Armageddon periodic paths are unchanged. Pet melee delivery remains a separate pass.

The matrix has **36 cases per boot**: eight direct receiver paths in normal, potion, resistant-secondary and immune-primary contexts, plus four Boulder ground controls. It checks exact primary and secondary FP256 loss, independent recipient defenses, typed source/target/components, no recursive events, immune-hit suppression, poison exclusion and no added contact-level mana debit. Actual prepared spell actors feed the existing receivers; reflection reaches private contact/area handlers and Fissure's field burn. Original pools and temporary guard overrides are restored after each scene. The guard isolates contact delivery from knockback motion; Boulder still consumes its intrinsic knockback RNG draw.

The driver retains the full existing Druid cohort: elemental source/flight/terrain/cast tests, beasts and their upgrades, vines, spirits, forms, held Arctic Blast, and paid Fury save staging. It reuses cached reference fixtures. There are 156 ordinary wire actions and nine held-Arctic sequences across three boots; the new contact matrix adds no new input protocol.

**Content 1 / Integration 5 / source 5 / native run 3 passed 684 exact results, 204 connections, three saves and two reloads.** Both accepted builds have zero warnings/errors. The gameplay DLL stayed identical across two failed native fixture attempts: first an invalid Fissure parent ID/lifetime, then an incorrect Boulder RNG expectation. One duplicate-local fixture compile error was also frozen before repair. The successful transcript retains five save-pruning warnings and six deliberate scalar-link refusals (two per boot). This is not a warning-free save certificate.

Only remaining-description text changes for the six affected talents. All 240 handlers, numerical tables, completed-class count and 573 named-definition outcomes retain. There is no new save field/version, native/client build or asset. Installed artifacts, existing saves, configuration and client patches remain protected. Effects/audio and talent/hotkey icons stay in their separate registers as identified placeholders.

This does not certify a whole class, exact current D2R event ordering, special/PvP recipients, or graphical behavior. Existing unsupported scalar and native routes remain explicit. Druid pet damage is next; the separately pending Impale, Lightning Strike potion and ordinary-cold corpse questions are unaffected.

[Receipt](../tools/reference/druid-element-link-native-evidence.json), [matrix](../tests/Sanctuary.Integration/DruidElementLinkChecks.cs), [driver](../tests/check_druid_element_link.py). Exact baseline, failed attempts, accepted assemblies and immutable evidence are archived under `/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/druid-element-link`.
