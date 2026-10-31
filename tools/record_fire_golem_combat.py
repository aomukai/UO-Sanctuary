#!/usr/bin/env python3
"""Bind the combined Fire unarmed packet, prepared contact and G3 release."""
import argparse
from collections import Counter
import json
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/fire-golem-combat'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/fire-golem-clock-native-evidence.json'
    predecessor = json.loads(previous.read_text())
    ref = predecessor['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) <= set(after)
    added = set(after) - set(before)
    assert added == {'src/Sanctuary.Content/FireGolemContact.cs', 'tests/Sanctuary.Integration/FireGolemCombatChecks.cs'}
    changed = {p for p in before if before[p] != after[p]}
    assert changed == {'src/Sanctuary.Content/FireGolemBody.cs', 'src/Sanctuary.Content/NpcEquipmentElements.cs', 'src/Sanctuary.Content/RaisedSkeletonContact.cs'}
    assert old['copiedData'] == inputs['birth']['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/fire-golem-clock/birth-native-checks.log'
    assert sha(baseline) == predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline) + Counter({'fire-golem-combat-check': 1, 'fire-golem-combat-reload-check': 2, 'fire-golem-combat-live-result': 3})
    assert markers(STAGE / 'birth-native-checks.log') == expected
    assert sum(expected.values()) == 366
    assert sum(markers(baseline).values()) == 360
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
    for name, count in [('named-item-runtime-audit.json', 843), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        prior = json.loads((ROOT / '.local/class-skills/fire-golem-clock' / name).read_text())
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
    proof_path = ROOT / 'tools/reference/fire-golem-mode-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['probeSha256'] == sha(ROOT / 'tools/probe_fire_golem_mode.py')
    assert proof['cases'] == 2880 and proof['ordinaryCases'] == 480 and proof['guardCases'] == 2400
    for path, digest in proof['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    files = [previous, proof_path, Path(__file__), *[ROOT / p for p in changed | added]]
    saved = json.loads((STAGE / 'fire-golem-combat-fixtures.json').read_text())
    seed = (666 << 32) | 173
    for _ in range(3): seed = 0x6AC690C5 * (seed & 0xffffffff) + (seed >> 32)
    assert saved['Generation'] == 2 and saved['Seed'] == seed
    for name in ['npc-equipment-elements-source-evidence.json', 'npc-equipment-critical-source-evidence.json']:
        path = ROOT / 'tools/reference' / name
        source = json.loads(path.read_text())
        for artifact, digest in source['artifacts'].items(): assert sha(ROOT / artifact) == digest, artifact
        files.append(path)
    files += [ROOT / p for p in ['tools/probe_fire_golem_mode.py', 'tools/build_class_skills.py', 'tests/check_fire_golem_combat.py',
              'data/Sanctuary/Balance/necromancer-summons.json', 'tools/reference/fire-golem-world-preflight.json',
              'docs/fire-golem-combat-checkpoint.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
              'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md', 'docs/skeletal-mage-upgrade-plan.md', 'docs/skill-effects.md', 'docs/skill-icons.md',
              'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
              f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
              'audit-native-checks.log', 'python-tests.log', 'fire-golem-birth-fixtures.json', 'fire-golem-absorption-fixtures.json', 'fire-golem-combat-fixtures.json']]
    result = dict(referenceVersion='3.3.93847', component='Learned Fire Golem unarmed elemental packet and prepared contact', installed=False,
                  goalComplete=False, completedClasses=[], build=build, run=run,
                  verified=dict(nativeMarkers=366, retainedPredecessorMarkers=360, boots=3, actualReloads=2, saves=3,
                                auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[843, 21],
                                unchangedInstalledArtifacts=106, sourceContexts=2880, baselineActionFrame=10, baselineEndFrame=17,
                                baselineActionMs=400, baselineCycleMs=680, halfSpeedFrames=[20, 34], cappedRateFrames=[6, 10],
                                observedConnectedFireRelease=True, connectedFireAttacks=3, finalSavedSeed=seed, handlers=186, necromancerHandlers=30),
                  inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
                  changedSourceHashes={path: after[path] for path in sorted(changed | added)},
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=[
                      'New learned Fire bodies share the existing unarmed NPC damage, elemental, critical and saved actor-stream services. Captured rank4/Normal physical range is2560..6911FP and innate fire range9472..14591FP, with exclusive upper bounds6912and14592. Intrinsic5percent critical doubles both channels once; actual pet Enchant accrues into the same fire range. Owner gear,poison,critical and later ranks are excluded.',
                      'The new body bypasses the historical separate follow-up fire hit. Native combined allocation and actual Strike match one packet. Prepared contact captures hit/block and mitigated physical/fire damage at A1start, then commits through shared health/return services without rerolling. Shield,changed mitigation,range,cancel,duplicate and physical-only Life Tap/Thorns ordering controls pass.',
                      'Three connected attacks prepare the exact packet and actor seed at start,have no damage at380ms,and commit once by520ms in the680ms G3cycle. Two actual reloads preserve the unarmed binding,captured endpoints and exact next source draw; deletion clears the stream.',
                      'All360predecessor markers plus6new markers pass across3boots/2reloads/3saves. Four audits/71Python and clean builds pass.573named decisions,runtime data,staged kernel and106installed artifacts remain unchanged.'],
                  remaining=[
                      'The optional monstats fire component remains uncertain because the unchanged older noRatio helper writes fields its caller does not read. This increment adds captured skill fire only; no optional component is guessed. Full mode-entry RNG,including the80percent selection draw,is not integrated or certified.',
                      'Intrinsic Holy Fire weapon-state/pulse behavior and delayed death/replacement explosion remain unfinished. Full current-executable initializer/stat aggregation,initial S1/AI/movement/live-rate/global-frame/client parity and historical migration remain separate.',
                      'Unsupported recipient capabilities explicitly use the reported native bridge before allocation. Allocation faults never replay through it. Full Fire-recipient prepared return and special/PvP reactions remain separate; common services and retained controls do not certify those contexts.',
                      'No complete class,deployment or new broad1180-marker run. Effects/audio and tree/hotkey icons remain separate placeholders; additional pet spells unlocked by hard points remain a later upgrade pass.'])
    destination = ROOT / 'tools/reference/fire-golem-combat-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Fire combined combat:366markers/3boots/2reloads,3prepared400/680ms attacks,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
