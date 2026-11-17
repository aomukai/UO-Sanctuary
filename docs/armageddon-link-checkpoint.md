# Armageddon terminal damage and Health Link

Reference: **D2R Reign of the Warlock 3.3.93847**, approved Sanctuary deviations first. This completes the known Druid direct-damage producers for the approved [Health Link contract](defiler-health-link-rule-proposal.md), without certifying the class's other documented limits.

The old accepted DLL dealt its full 4,096 FP256 terminal damage but shared 0 instead of 2,170. Its direct event lacked settled components and correctly faulted the link. That baseline was frozen before repair.

One health call now publishes the terminal blast's existing settled physical/fire and supported potion components. Primary damage and poison additions retain their previous behavior; poison is excluded from shared components. The child fire field still captures its own current owner stats at the retained rank. Burning ground remains **Periodic** and does not start sharing. No damage formula, RNG, event geometry, timing, payment or save format changes.

The matrix contains eight contexts per boot: terminal and ground paths, each with normal, potion, resistant-secondary and immune-primary controls. It checks exact primary/secondary FP256 damage, source/target/components, nonrecursive direct events, zero events for immunity or periodic damage, independent child capture and no extra contact-level mana debit. Actual actor factories feed the existing private terminal/ground callbacks; retained tests separately exercise their natural timing.

**Content 1 / Integration 2 / source 2 / native run 1 passed 33 exact results, nine connections, three wire casts, three natural-weather checks, three saves and two reloads.** Eight new contexts repeat after each reload; the cached source suite checks 55,080 endpoint combinations per boot. Both accepted builds have zero warnings/errors. The first corrected native run passed without fixture, compile or driver repairs. There are no save-pruning warnings; six scalar-link refusals are intentional controls, two per boot.

The focused driver retains Armageddon's cached endpoint/emitter comparisons, casting/payment, lifecycle, geometry, malformed-worker checks, actual input, natural weather, and original saved actor/field/seed/wound/binding/unpaid-windup checks. It also retains shared Defiler matrices and named-item readiness. Unchanged full Druid form/vine suites and old reference compilation are not repeated.

Only Armageddon's remaining-description row changes. All 240 handlers, numerical data, completed-class count and 573 named-definition outcomes retain. Installed build, saves, configuration and client remain protected. Final effects/audio and tree/hotkey icons remain separately tracked placeholders. No deployment or full-class certificate is implied; exact engine ordering, special/PvP recipients and graphical acceptance remain bounded by the existing records.

[Receipt](../tools/reference/armageddon-link-native-evidence.json), [matrix](../tests/Sanctuary.Integration/ArmageddonLinkChecks.cs), [driver](../tests/check_armageddon_link.py). Exact baseline, accepted assemblies and immutable evidence: `/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/armageddon-link`.
