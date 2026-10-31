#!/usr/bin/env python3
"""Bind original Fire Golem delayed death, replacement and saved retirement."""
import argparse
from collections import Counter
import json
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/fire-golem-death'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/fire-golem-aura-native-evidence.json'
    predecessor = json.loads(previous.read_text())
    ref = predecessor['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) <= set(after)
    added = set(after) - set(before)
    assert added == {'src/Sanctuary.Content/FireGolemDeathBody.cs', 'tests/Sanctuary.Integration/FireGolemDeathChecks.cs'}
    changed = {p for p in before if before[p] != after[p]}
    assert changed == {'src/Sanctuary.Content/NecromancerSummons.cs', 'src/Sanctuary.Content/NecromancerGolems.cs', 'src/Sanctuary.Content/ClassLoot.cs', 'tests/Sanctuary.Integration/ClassSkillChecks.cs', 'tests/Sanctuary.Integration/FireGolemBirthChecks.cs', 'tests/Sanctuary.Integration/GolemRosterChecks.cs'}
    assert old['copiedData'] == inputs['birth']['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/fire-golem-aura/birth-native-checks.log'
    assert sha(baseline) == predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline) + Counter({'fire-golem-death-check': 1, 'fire-golem-death-reload-check': 2, 'fire-golem-death-live-result': 3, 'fire-golem-death-save': 3})
    assert markers(STAGE / 'birth-native-checks.log') == expected
    assert sum(markers(baseline).values()) == 378 and sum(expected.values()) == 387
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    fire = json.loads((STAGE / 'fire-golem-birth-fixtures.json').read_text())
    assert fire['Generation'] == 2 and fire['Maximum'] == 216345 and fire['Life'] == 216345 - 257
    absorption = json.loads((STAGE / 'fire-golem-absorption-fixtures.json').read_text())
    assert absorption['Generation'] == 2 and absorption['Life'] == 84134 - 10000 - 208
    for kind in ['content', 'integration']:
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(x in text for x in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 845), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        prior = json.loads((ROOT / '.local/class-skills/fire-golem-aura' / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert all(audit[k] == v for k, v in prior.items() if k not in ['ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes'])
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down') == 1
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    proof_path = ROOT / 'tools/reference/fire-golem-death-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['probeSha256'] == sha(ROOT / 'tools/probe_fire_golem_death.py')
    assert proof['cases'] == 5184 and proof['modifier'] == 31
    for path, digest in proof['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    files = [previous, proof_path, Path(__file__), *[ROOT / p for p in changed | added]]
    saved = json.loads((STAGE / 'fire-golem-aura-fixtures.json').read_text())
    seed = (666 << 32) | 173
    for _ in range(3): seed = 0x6AC690C5 * (seed & 0xffffffff) + (seed >> 32)
    assert saved['Generation'] == 2 and saved['Seed'] == seed
    for name in ['npc-equipment-elements-source-evidence.json', 'npc-equipment-critical-source-evidence.json']:
        path = ROOT / 'tools/reference' / name
        source = json.loads(path.read_text())
        for artifact, digest in source['artifacts'].items(): assert sha(ROOT / artifact) == digest, artifact
        files.append(path)
    files += [ROOT / p for p in ['tools/probe_fire_golem_death.py', 'tools/build_class_skills.py', 'tests/check_fire_golem_death.py', 'tests/check_iron_golem_birth.py',
              'data/Sanctuary/Balance/necromancer-summons.json', 'tools/reference/fire-golem-world-preflight.json',
              'docs/fire-golem-death-checkpoint.md', 'docs/fire-golem-death-source-review.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
              'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md', 'docs/skeletal-mage-upgrade-plan.md', 'docs/skill-effects.md', 'docs/skill-icons.md',
              'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
              f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
              'audit-native-checks.log', 'python-tests.log', 'fire-golem-birth-fixtures.json', 'fire-golem-absorption-fixtures.json', 'fire-golem-combat-fixtures.json', 'fire-golem-aura-fixtures.json', 'paladin-element-checks.json', 'fire-golem-death-fixtures.json']]
    death = json.loads((STAGE / 'fire-golem-death-fixtures.json').read_text())
    assert death['Generation'] == 2 and death['Actor'] > 0 and death['Historical'] > 0
    result = dict(referenceVersion='3.3.93847', component='Original Fire Golem delayed death and replacement burst', installed=False,
                  goalComplete=False, completedClasses=[], build=build, run=run,
                  verified=dict(nativeMarkers=387, retainedPredecessorMarkers=378, boots=3, actualReloads=2, saves=3,
                                auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[845, 21],
                                unchangedInstalledArtifacts=106, sourceContexts=5184, deathDelayFrames=4, deathDelayMs=160,
                                physicalDamage=100, fireDamage=100, sourceRadius=6, intrinsicCriticalPercent=5,
                                connectedDeathChecks=3, connectedBurstPhases=9, savedRetirementReloads=2, persistenceFixtureHoldMs=60000,
                                historicalBodyReloads=2, handlers=186, necromancerHandlers=30),
                  inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
                  changedSourceHashes={path: after[path] for path in sorted(changed | added)},
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=[
                      'New learned FireGolemDeathBody retains the original dead registered actor and owner for a bounded four-frame delay. Normal NPC death bookkeeping runs; live roster, AI, pending contact and aura are cleared. Natural damage and real golem replacement use this path, then dispose once.',
                      'Connected controls verify no early damage/deletion, fixed100physical+100fire with independent resistance, intrinsic critical doubling both channels, critical RNG consumption on blocked recipients, original-source kill credit after deletion and no added hit recovery. Owner/player gear and ordinary weapon effects are excluded.',
                      'The persistence fixture holds only the volatile retirement clock60seconds through test-only reflection to span asynchronous save preparation,leaving the real UTC expiry unchanged. Both reloads observe that original serial in its restored dead state and verify expiry without offline damage replay or RNG/aura leaks. Unmodified160ms lifetime is verified separately in connected combat. Previous FireGolemBody v0 saves retain their exact type and ownership.',
                      'All378predecessor markers plus9death markers pass over3boots/2reloads/3saves. Four audits,71Python and clean builds pass.573named decisions,runtime data,staged kernel and106installed artifacts remain unchanged.'],
                  remaining=[
                      'The shared native radius/hostility/LOS/enumeration projection is reused; exact D2 room/species/allegiance/collision-mask/distance ordering remains unverified. Native health/mitigation/event delivery is an existing bridge, not full source missile-event/return parity. Unsupported cross-facet emission is reported and skipped.',
                      'Earlier exact FireGolemBody v0 and plain-minion instances retain their legacy immediate death path; they are not silently migrated. Current-executable optional monster-fire/noRatio,mode-entry RNG,initial aura scheduling,full initializer/world/client acceptance remain open.',
                      'No complete class,deployment or new broad1180-marker run. Effects/audio and tree/hotkey icons stay separately pending;hard-point pet ability progression is deferred until all baseline spells/skills are finished.'])
    destination = ROOT / 'tools/reference/fire-golem-death-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Fire death:387markers/3boots/2reloads,9connected phases,5184source contexts,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
