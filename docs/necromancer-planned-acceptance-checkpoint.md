# Current Necromancer combined acceptance

Scope follows the explicit no-charms/no-runewords decision and retains all 30 Necromancer handlers. The new `tests/check_necromancer_planned_acceptance.py` composes the existing 115-family class cohort with current Bone Armor, native corpse/Lich and helper coverage. It reuses cached reference files; no old source compiler or newer D2R process is run. Installed build/configuration/saves remain unchanged.

The first combined run passed 206 markers, then failed `bone-prison-self-quick` on its expiry assertion. An isolated run of the same Content/Integration assemblies reproduced it. The old test required the root clock to request death. The approved barrier rule instead collapses the entire formation when **any** segment requests expiry/death.

The updated fixture observes all growing segments, requires a strict source-AI expiry after frame 600, verifies a common linked retirement state and still requires full deletion, no collision and elapsed nominal lifetime. Historical unlinked roots keep their own clock check. Content build 1 is unchanged; only Integration build 2 changes the fixture. Three isolated actual quick casts pass across three boots and two reloads, with logs confirming `rootRequested=False formation=True`. See [focused evidence](../tools/reference/necro-prison-expiry-acceptance-evidence.json).

The combined run has restarted with the corrected fixture. **Full combined acceptance remains pending; no class certificate is claimed.** The dedicated barrier-retirement/deadline-save receipt remains separate, rather than being silently counted as rerun here. Other documented gameplay limits remain open. Effects/audio and talent/hotkey icons stay in their existing separate registers.
