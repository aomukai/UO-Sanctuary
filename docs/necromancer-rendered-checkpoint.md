# Rendered Necromancer combat checkpoint

Verified 2026-11-18 JST in a disposable world using the installed class package and actual ClassicUO client. [Evidence](../tools/reference/necromancer-rendered-evidence.json) binds the client, native/content assemblies, Integration9, fixture sources, screenshots and native results.

The client creates a Necromancer through the ordinary appearance/name packet. The test grants its controlled skill allocation and sends real quick-slot state. Actual F1 input raises a learned Warrior from the displayed corpse. Actual F2 input casts rank-2 Teeth at the displayed enemy. The Warrior is held with the normal native frozen action lock while the test verifies player-attributed damage. The lock is then removed and the same original Warrior approaches and damages the enemy through its natural AI. Five native scene results pass; screenshots show the player, summon, spell placeholder and ordinary pet combat damage. The disposable world saves and shuts down normally.

This is representative rendered input/combat coverage, not a graphical test of every Necromancer talent or final artwork. The existing native lifecycle/input suites retain their separate scopes. Placeholder spell effects and the labelled pet model remain tracked in the separate asset registers. No production source, numeric data or installed binary changes were needed.

The scene needed fixture corrections before acceptance. Two compile failures used an unsupported LINQ/enumeration interface for ModernUO's damage-entry list; the accepted helper uses its collection namespace and enumerator. The first two native attempts left the paperdoll over the cursor; the driver now uses the existing verified minimize control. Manual pet parking did not hold it out of subsequent skill input, so the test uses the existing native frozen action lock instead. Further diagnostics confirmed actual mana payment, correct aim and eligible targets while the health observation still failed. The accepted scene reuses the elevated clear native corridor and normal test-only healing suppression. These controls separate map geometry and regeneration from the damage observation. Rank 2 supplies a centre projectile for the isolated target. Earlier failures do not prove a gameplay defect or independently establish one exact cause for every missed observation.

Failed sources, binaries, data and logs are retained under numbered failure folders in `.local/class-skills/necromancer-rendered`. Failed temporary worlds were cleaned up by their original runners; their saved configuration and available artifacts are retained, not fabricated full-world saves. The successful run is `run-9`, with exact source archive `sources-9.tar.gz`. Original installed saves/configuration/client state remain unchanged.

```bash
python3 tools/record_necromancer_rendered.py --check
```
