#!/usr/bin/env python3
"""Bind Fire Golem Holy Fire weapon state, shared pulse and retained combat."""
import argparse
from collections import Counter
import json
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/fire-golem-aura'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/fire-golem-combat-native-evidence.json'
    predecessor = json.loads(previous.read_text())
    ref = predecessor['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) <= set(after)
    added = set(after) - set(before)
    assert added == {'src/Sanctuary.Content/FireGolemAura.cs', 'tests/Sanctuary.Integration/FireGolemAuraChecks.cs'}
    changed = {p for p in before if before[p] != after[p]}
    assert changed == {'src/Sanctuary.Content/FireGolemBody.cs', 'src/Sanctuary.Content/NecromancerSummons.cs', 'src/Sanctuary.Content/PaladinElements.cs', 'src/Sanctuary.Content/SkillCombat.cs'}
    assert old['copiedData'] == inputs['birth']['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/fire-golem-combat/birth-native-checks.log'
    assert sha(baseline) == predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline) + Counter({'fire-golem-aura-check': 1, 'fire-golem-aura-reload-check': 2, 'fire-golem-aura-live-result': 3, 'paladin-element-check': 1, 'paladin-element-result': 1, 'paladin-element-reload-check': 2, 'paladin-element-reload-result': 2})
    assert markers(STAGE / 'birth-native-checks.log') == expected
    assert sum(markers(baseline).values()) == 366 and sum(expected.values()) == 378
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
    for name, count in [('named-item-runtime-audit.json', 844), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        prior = json.loads((ROOT / '.local/class-skills/fire-golem-combat' / name).read_text())
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
    proof_path = ROOT / 'tools/reference/fire-golem-aura-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['probeSha256'] == sha(ROOT / 'tools/probe_fire_golem_aura.py')
    assert proof['cases'] == 384 and proof['ordinaryCases'] == 96 and proof['guardCases'] == 288
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
    files += [ROOT / p for p in ['tools/probe_fire_golem_aura.py', 'tools/build_class_skills.py', 'tests/check_fire_golem_aura.py',
              'data/Sanctuary/Balance/necromancer-summons.json', 'tools/reference/fire-golem-world-preflight.json',
              'docs/fire-golem-aura-checkpoint.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
              'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md', 'docs/skeletal-mage-upgrade-plan.md', 'docs/skill-effects.md', 'docs/skill-icons.md',
              'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
              f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
              'audit-native-checks.log', 'python-tests.log', 'fire-golem-birth-fixtures.json', 'fire-golem-absorption-fixtures.json', 'fire-golem-combat-fixtures.json', 'fire-golem-aura-fixtures.json', 'paladin-element-checks.json']]
    result = dict(referenceVersion='3.3.93847', component='Learned Fire Golem Holy Fire weapon state and pulse', installed=False,
                  goalComplete=False, completedClasses=[], build=build, run=run,
                  verified=dict(nativeMarkers=378, retainedPredecessorMarkers=366, boots=3, actualReloads=2, saves=3,
                                auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[844, 21],
                                unchangedInstalledArtifacts=106, sourceContexts=384, baselineActionFrame=10, baselineEndFrame=17,
                                baselineActionMs=400, baselineCycleMs=680, halfSpeedFrames=[20, 34], cappedRateFrames=[6, 10],
                                connectedAuraChecks=3, connectedRetainedFireAttacks=3, auraRank=10, pulseMin256=2176, pulseExclusiveMax256=3840, weaponRange=[51, 90], accruedFireRange=[88, 147], leaseFrames=51, finalSavedSeed=seed, handlers=186, necromancerHandlers=30),
                  inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
                  changedSourceHashes={path: after[path] for path in sorted(changed | added)},
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=[
                      'Fire Golem now supplies its captured Holy Fire rank to the shared elemental aura path. Captured Fire4grants Holy Fire10with2176..3839FP pulse range and51..90whole-point weapon stat endpoints. The latter accrues with captured37..57innate fire to88..147before the ordinary NPC elemental roll and intrinsic critical. Owner gear and Paladin synergies are excluded.',
                      'Self weapon state is applied before town return and expires after51frames. A nonplayer emitter spends no owner/pet mana. One pre-enumeration pulse roll is shared by recipients with their individual resistance and existing proximity projection. Real connected controls verify equal-distance damage,immunity,range,mana0,town self-refresh without damage/RNG and deletion cleanup.',
                      'Two actual reloads retain the actor-owned shared lease and exact next NPC random draw without offline pulse replay. A deliberately extended fixture lease tests persistence;live controls verify the normal2040msduration. Complete unchanged Do66/recipient callbacks execute384bounded contexts with explicit formula/state/world services.',
                      'All366predecessor markers plus12new/retained-Paladin markers pass over3boots/2reloads/3saves. Three Fire aura controls,three retained connected Fire attacks,4audits/71Python and clean builds pass.573named decisions,runtime data,staged kernel and106installed artifacts remain unchanged.'],
                  remaining=[
                      'Existing Sanctuary proximity/filter projection is reused. Exact modern arithmetic,room/species/LOS/vertical filtering,enumeration and full source event/recipient-return/notification behavior remain uncertain. The inherited first-think/two-second cadence is retained;initial aura scheduling/global AI frames are not certified.',
                      'Full own-item aura/mastery interactions,current executable initializer/stat aggregation and client behavior remain open. The optional monstats-fire/noRatio discrepancy and full mode-entry selection RNG are separate. Delayed death/replacement explosion and historical migration remain unfinished.',
                      'No complete class,deployment or new broad1180-marker run. Effects/audio and tree/hotkey icons stay separate placeholders;additional pet abilities unlocked by hard points remain a later pass.'])
    destination = ROOT / 'tools/reference/fire-golem-aura-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Fire Holy Fire:378markers/3boots/2reloads,3connected aura controls,384source cases,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
