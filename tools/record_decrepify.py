#!/usr/bin/env python3
"""Bind Decrepify four-stat receiver, secondary refresh and native consumers."""
import argparse
from collections import Counter
import json
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/decrepify'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/weaken-native-evidence.json'
    predecessor = json.loads(previous.read_text())
    ref = predecessor['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) <= set(after)
    added = set(after) - set(before)
    assert added == {'tests/Sanctuary.Integration/DecrepifyChecks.cs'}
    changed = {p for p in before if before[p] != after[p]}
    assert changed == {'src/Sanctuary.Content/NecromancerEffects.cs', 'src/Sanctuary.Content/NecromancerCurseCasts.cs'}
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/weaken/birth-native-checks.log'
    assert sha(baseline) == predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline) + Counter({'decrepify-check': 1, 'decrepify-reload-check': 2, 'decrepify-right-check': 3, 'decrepify-right-result': 3, 'decrepify-quick-check': 3, 'decrepify-quick-result': 3})
    assert markers(STAGE / 'birth-native-checks.log') == expected
    assert sum(markers(baseline).values()) == 431 and sum(expected.values()) == 446
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
    for name, count in [('named-item-runtime-audit.json', 848), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        prior = json.loads((ROOT / '.local/class-skills/weaken' / name).read_text())
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
    proof_path = ROOT / 'tools/reference/decrepify-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['receiverContexts'] == 8960
    assert sha(ROOT / proof['reusedReference']['path']) == proof['reusedReference']['sha256']
    for name, digest in proof['pinnedTables'].items():
        assert sha(ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json') == digest
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
    files += [ROOT / p for p in ['tools/probe_decrepify.py', 'tools/build_class_skills.py', 'tests/check_decrepify.py', 'tests/check_iron_golem_birth.py',
              'data/Sanctuary/Balance/source-curse-no-walk.json', 'tools/reference/fire-golem-world-preflight.json',
              'docs/decrepify-checkpoint.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
              'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md', 'docs/skeletal-mage-upgrade-plan.md', 'docs/skill-effects.md', 'docs/skill-icons.md',
              'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
              f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
              'audit-native-checks.log', 'python-tests.log', 'fire-golem-birth-fixtures.json', 'fire-golem-absorption-fixtures.json', 'fire-golem-combat-fixtures.json', 'fire-golem-aura-fixtures.json', 'paladin-element-checks.json', 'fire-golem-death-fixtures.json', 'amplify-damage-fixtures.json', 'inner-sight-fixtures.json', 'cloak-fixtures.json', 'life-tap-fixtures.json', 'weaken-fixtures.json', 'decrepify-fixtures.json']]
    death = json.loads((STAGE / 'fire-golem-death-fixtures.json').read_text())
    assert death['Generation'] == 2 and death['Actor'] > 0 and death['Historical'] > 0
    amp = json.loads((STAGE / 'amplify-damage-fixtures.json').read_text())
    assert amp['Generation'] == 2 and amp['Curse']['Rank'] == 21 and amp['Curse']['PhysicalDelta'] == -100
    assert 'PhysicalDelta' not in amp['Old']
    weakened = json.loads((STAGE / 'weaken-fixtures.json').read_text())
    assert weakened['Generation'] == 2 and weakened['Curse']['Rank'] == 21
    assert 'PhysicalDelta' not in weakened['Curse'] and 'PhysicalDelta' not in weakened['Old']
    decrep = json.loads((STAGE / 'decrepify-fixtures.json').read_text())
    assert decrep['Generation'] == 2 and decrep['Curse']['Rank'] == 21 and decrep['Curse']['PhysicalDelta'] == -50
    assert 'PhysicalDelta' not in decrep['Old']
    result = dict(referenceVersion='3.3.93847', component='Decrepify four-stat application and secondary refresh',
                  installed=False, goalComplete=False, completedClasses=[], build=build, run=run,
                  verified=dict(nativeMarkers=446, retainedPredecessorMarkers=431, boots=3, actualReloads=2, saves=3,
                                auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[848, 21],
                                unchangedInstalledArtifacts=106, receiverSourceContexts=8960,
                                connectedRightQuickCasts=6, naturalExpiryChecks=6, naturalRank1Frames=100,
                                mana256=2816, damagePercent=-50, velocityPercent=-50, attackRate=-50,
                                ordinaryPhysicalDelta=-50, immuneMonsterPhysicalDelta=-10, equalRankSecondaryRewrite=True,
                                handlers=186, necromancerHandlers=30),
                  inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
                  changedSourceHashes={path: after[path] for path in sorted(changed | added)},
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=[
                      'Decrepify shares the learned do30 cast/recipient path,radius6,cost11mana and duration100+15*(rank-1)frames. Current stat slots are velocitypercent,damagepercent,damageresist,attackrate,all-50. Only immune nonplayer/nonhireling physical resistance receives-10.',
                      'Weaker same-skill ranks reject;equal rank retains creator and restarts expiry but rewrites secondary resistance from the current base pool. Higher/different curses replace;Attract/Possessed reject. Captured physical delta extends the existing optional JSON domain with-50/-10,without a new field or Mobile schema.',
                      '8960complete four-stat source receiver/factory contexts pass ASan/UBSan. Native differentials,immunity thresholds,outgoing native weapon damage,element/physical-spell exclusion,movement/attack-delay hooks,source rate and an actual ordinary NPC windup pass. Cold immunity does not suppress the curse.',
                      'Six real right/quick inputs verify unmodified4second expiry and once-only payment. Both reloads retain captured and legacy payloads,original deadlines,resources,gear/bindings and resumed mitigation. Only persistence inspection extends the fixture deadline1day. All431predecessor markers remain verified.',
                      '446native markers/3boots/2reloads/3saves,4audits,71Python,573named decisions and clean builds pass. Runtime data,staged kernel and106installed artifacts remain unchanged.'],
                  remaining=[
                      'Active legacy elite-monster Amplify lacks rank/creator metadata;compound replacement stays reported/skipped. Absent-field legacy Decrepify uses the old dynamic adapter until expiry or supported recast;equal refresh may capture its secondary stat through the actual callback rule.',
                      'Current executable arbitration/custom historical stats,dynamic base/accrued item/proc/slow/haste/cap combinations,live rate rescheduling,full source room/species/flags/allegiance/geometry,AI/events,special/PvP/global phase and client behavior remain open. State60sunderfull=1is a static lead,not proof of modern Sunder math.',
                      'Full do30/radial/formula VM/movement engine are not executed by the receiver probe. No complete class,deployment or broad1180-marker claim. Final effects/audio and tree/right/quick icons stay separate placeholders;hard-point pet upgrades follow baseline skills.'])
    destination = ROOT / 'tools/reference/decrepify-native-evidence.json'

    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Decrepify:446markers/3boots/2reloads,6real4second casts,8960source contexts,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
