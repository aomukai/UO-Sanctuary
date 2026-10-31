#!/usr/bin/env python3
"""Bind Amplify Damage captured curse state, learned casting and retained mechanics."""
import argparse
from collections import Counter
import json
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/amplify-damage'
sha = inventory.sha


def record(build, run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / (name + '-inputs.json')) for name in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    previous = ROOT / 'tools/reference/fire-golem-death-native-evidence.json'
    predecessor = json.loads(previous.read_text())
    ref = predecessor['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before) <= set(after)
    added = set(after) - set(before)
    assert added == {'src/Sanctuary.Content/AmplifyDamageCasts.cs', 'tests/Sanctuary.Integration/AmplifyDamageChecks.cs'}
    changed = {p for p in before if before[p] != after[p]}
    assert changed == {'src/Sanctuary.Content/NecromancerEffects.cs', 'src/Sanctuary.Content/NecromancerCombat.cs', 'tests/Sanctuary.Integration/ClassSkillChecks.cs'}
    assert set(inputs['birth']['copiedData']) - set(old['copiedData']) == {'Data/Sanctuary/Balance/source-curse-no-walk.json'}
    assert all(inputs['birth']['copiedData'][p] == h for p, h in old['copiedData'].items())
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/fire-golem-death/birth-native-checks.log'
    assert sha(baseline) == predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline) + Counter({'amplify-damage-check': 1, 'amplify-damage-reload-check': 2, 'amplify-damage-right-check': 3, 'amplify-damage-right-result': 3, 'amplify-damage-quick-check': 3, 'amplify-damage-quick-result': 3, 'inner-sight-check': 1, 'inner-sight-result': 1, 'inner-sight-reload-check': 2, 'cloak-check': 1, 'cloak-reload-check': 2, 'cloak-result': 1, 'cloak-reload-result': 2, 'life-tap-check': 1, 'life-tap-reload-check': 2, 'life-tap-result': 1})
    assert markers(STAGE / 'birth-native-checks.log') == expected
    assert sum(markers(baseline).values()) == 387 and sum(expected.values()) == 416
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
    for name, count in [('named-item-runtime-audit.json', 847), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        prior = json.loads((ROOT / '.local/class-skills/fire-golem-death' / name).read_text())
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
    proof_path = ROOT / 'tools/reference/amplify-damage-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['cases'] == dict(receiver=8960, gates=1024) and proof['noWalkSpecies'] == 135
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
    files += [ROOT / p for p in ['tools/probe_amplify_damage.py', 'tools/build_class_skills.py', 'tests/check_amplify_damage.py', 'tests/check_iron_golem_birth.py',
              'data/Sanctuary/Balance/source-curse-no-walk.json', 'tools/reference/fire-golem-world-preflight.json',
              'docs/amplify-damage-checkpoint.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md',
              'docs/necromancer-mechanical-audit.md', 'docs/skill-mechanical-reference.md', 'docs/skeletal-mage-upgrade-plan.md', 'docs/skill-effects.md', 'docs/skill-icons.md',
              'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
              f'run-{run}.log', f'audit-run-{run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log',
              'audit-native-checks.log', 'python-tests.log', 'fire-golem-birth-fixtures.json', 'fire-golem-absorption-fixtures.json', 'fire-golem-combat-fixtures.json', 'fire-golem-aura-fixtures.json', 'paladin-element-checks.json', 'fire-golem-death-fixtures.json', 'amplify-damage-fixtures.json', 'inner-sight-fixtures.json', 'cloak-fixtures.json', 'life-tap-fixtures.json']]
    death = json.loads((STAGE / 'fire-golem-death-fixtures.json').read_text())
    assert death['Generation'] == 2 and death['Actor'] > 0 and death['Historical'] > 0
    amp = json.loads((STAGE / 'amplify-damage-fixtures.json').read_text())
    assert amp['Generation'] == 2 and amp['Curse']['Rank'] == 21 and amp['Curse']['PhysicalDelta'] == -100
    assert 'PhysicalDelta' not in amp['Old']
    result = dict(referenceVersion='3.3.93847', component='Amplify Damage captured resistance, curse arbitration and learned casts',
                  installed=False, goalComplete=False, completedClasses=[], build=build, run=run,
                  verified=dict(nativeMarkers=416, retainedPredecessorMarkers=387, boots=3, actualReloads=2, saves=3,
                                auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[847, 21],
                                unchangedInstalledArtifacts=106, receiverSourceContexts=8960, eligibilitySourceContexts=1024,
                                connectedRightQuickCasts=6, naturalExpiryChecks=6, naturalRank1Frames=200,
                                mana256=1024, ordinaryPenalty=-100, immuneMonsterPenalty=-20, noWalkSpecies=135,
                                handlers=186, necromancerHandlers=30),
                  inputInventories={name: dict(path=str((STAGE / (name + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (name + '-inputs.json'))) for name in inputs},
                  changedSourceHashes={path: after[path] for path in sorted(changed | added)},
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=[
                      'Learned Amplify Damage uses the existing SC/FCR/target tracking/interruption path and pays4mana once at release,including empty/resisted areas. Radius3+rank-1 and duration200+75*(rank-1)frames use effective item ranks. Its filter3 area does not add an unrequested recipient LOS gate.',
                      'New applications capture physical resistance delta:-100normally,-20against immune nonplayer/nonhireling monsters. Weaker same-skill ranks reject,equal rank refreshes expiry retaining original caster/stat,and higher rank replaces. Attract and possessed champions reject.135pinned stationary species lack required walk-mode bit2.',
                      '8960complete source receiver/factory contexts and1024eligibility contexts pass under ASan/UBSan. Native source-policy comparisons,physical immunity thresholds,element exclusion,rank/owner/stat retention,caster deletion,Attract/Possessed,obstructed area ray,moving target,cancel,low mana,actual Wormskull rank21 and legacy payloads pass.',
                      'Six real right/quick casts verify unmodified8second expiry and once-only payment. Both reloads preserve new captured and absent-field legacy states,deadlines,mana,gear/bindings and resumed damage. Persistence inspection alone extends the fixture deadline1day. Inner Sight,Cloak,Life Tap and all387Fire death predecessor markers remain verified.',
                      '416native markers/3boots/2reloads/3saves,4audits,71Python,573named decisions and clean builds pass. Only one generated135species data list is added;prior runtime data,staged kernel and106installed artifacts remain unchanged.'],
                  remaining=[
                      'The older elite-monster Amplify store lacks skill rank/caster data. Replacing that active legacy curse is reported/skipped pending a shared adapter. Legacy ordinary curses without captured delta keep their old dynamic adapter until replacement/expiry;equal-rank refresh does not invent historical stats.',
                      'Current-executable multicaster arbitration,full source base/accrued item graphs,item/proc casts,room/species/flags/allegiance/geometry,AI reactions/events,PvP,FCR/global phase and graphical-client acceptance remain open. Native physical resistance and encounter traits are declared world projections.',
                      'Do30/radial traversal are hash-reviewed,not executed by the receiver probe. No complete class,deployment or broad1180-marker claim. Final effects/audio and tree/right/quick icons remain separate placeholders;hard-point pet upgrades follow all baseline skills.'])
    destination = ROOT / 'tools/reference/amplify-damage-native-evidence.json'

    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Amplify Damage:416markers/3boots/2reloads,6real8second casts,9984source contexts,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.check)
