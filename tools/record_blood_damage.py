#!/usr/bin/env python3
"""Bind Blood150percent endpoints and shared Clay/NPC packet acceptance."""
import argparse
import json
import re
import subprocess
from collections import Counter
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/blood-damage'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / f'{name}-inputs.json') for name in ('birth', 'audit')}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{name}-inputs.json')
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/clay-upgrades-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in (
        'BloodGolemBody', 'ClayGolemBody', 'ClayGolemUpgrades', 'NecromancerSummons', 'NpcEquipmentDamage')} | {
        'tests/Sanctuary.Integration/' + n + '.cs' for n in ('BloodGolemCombatChecks', 'BloodGolemWorldChecks')}
    added = {'src/Sanctuary.Content/GolemPetDamage.cs', 'tests/Sanctuary.Integration/BloodGolemDamageChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/clay-upgrades/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['blood-damage-check'] += 1
    expected['blood-damage-reload-check'] += 2
    expected['blood-damage-live-result'] += 3
    assert sum(expected.values()) == 301 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Blood damage:shared captured Skeleton/Golem Mastery150percent endpoints') == 1
    assert log.count('Blood combat: FP410..820,5percent intrinsic') == 1
    assert log.count('Clay combat: FP273..546,5percent intrinsic') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        build_log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in build_log for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 879), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
        prior_audit = json.loads(subprocess.check_output(['git', 'show', 'deb6ef5:tools/reference/' + name], cwd=ROOT))
        strip = lambda x: {k: v for k, v in x.items() if k not in ('ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes')}
        assert strip(audit) == strip(prior_audit)
    assert markers(STAGE / 'audit-native-checks.log') == Counter({
        'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down') == 1
    log = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in log and '\nOK\n' in log
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30
    assert not status['completedClasses']
    # The replacement adds no actor saved fields or versions.
    for name in ('NecromancerSummons', 'ClayGolemBody', 'BloodGolemBody', 'BloodGolemWorldBody'):
        path = f'src/Sanctuary.Content/{name}.cs'
        original = subprocess.check_output(['git', 'show', 'deb6ef5:' + path], cwd=ROOT, text=True)
        current = (ROOT / path).read_text()
        for pattern in (r'\[SerializableField\([^]]+\)\]\s*private[^;]+;', r'\[SerializationGenerator\([^]]+\)\]'):
            assert re.findall(pattern, original) == re.findall(pattern, current)
    saved = json.loads((STAGE / 'blood-damage-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Body'] != saved['Legacy'] and saved['Rank'] == 20
    assert saved['Packet']['MinimumFraction256'] == 128
    docs = ('blood-golem-damage-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_blood_damage.py',
             ROOT / 'tools/review_blood_pet_events.py', ROOT / 'tools/reference/blood-pet-event-reuse-review.json',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{name}.md' for name in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
             f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'birth-native-checks.log',
             'audit-inputs.json', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log',
             'catalogue-check.log', 'blood-damage-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Blood150percent captured Clay/Skeleton curve with fractional intrinsic endpoints',
        installed=False, goalComplete=False, completedClasses=[], bloodUpgradePackageComplete=False, build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=301, retainedClayWarriorMagePlayerPetSentryMarkers=295, boots=3, actualReloads=2, saves=3,
            naturalBloodContacts=3, originalSavedBloodTypes=2, actualFacetTransitions=3, actualOriginalItemCaptureControls=2,
            normalRank4NoMasteryMinimum256=410, normalRank4NoMasteryExclusiveMaximum256=821,
            auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[879, 21],
            unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)),
            sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Clay and Blood share GolemPetDamage on the captured Skeleton curve with Golem Mastery. Blood intrinsic endpoints are150percent before ordinary percentages;optional0..255FP endpoint fractions retain odd half damage. Clay remains100percent with zero fractions. Old innate Blood/passive Fire synergy damage is replaced.',
            'NpcPhysicalStats default-zero endpoint fractions extend the existing NPC roll before ordinary percentages/SrcDam without changing zero-fraction arithmetic or draw count. Invalid fractions refuse before RNG. Real original shield preserves intrinsic fractions;two-hand weapon displacement clears them. These controls cover packet capture,not unsupported NPC item proc events.',
            'Actual soft/captured ranks,distinct Skeleton Mastery,owner training/respec/relearn,no rank/resource recapture,wounds/order/stream/10action16end clock and three owner-facet transitions pass. Older physical JSON defaults to zero fractions. Retained Clay remains273..546FP at rank4;approved Blood is410..820FP rather than4748..13567.',
            'Modern exact and original plain Blood actors retain types,owners and wounds across two reloads. Exact packet fractions/maximum/captured ranks/order/stream persist. Three natural learned contacts release the approved captured physical packet. Retained Blood healing/contact/source/world controls exercise the new damage with their existing pre-health healing. All295 predecessor marker scopes remain.',
            'Both clean builds,71Python,4audits,573named and eight-class240checks pass. Decision payloads,data,kernel and106installed hashes remain unchanged;879/21fingerprints are fresh. Actor saved fields/versions are unchanged. Complete older event/proc functions/current item registrations are separately pinned by a static reuse review,not certified as executed.'],
        remaining=[
            'Blood hard10OpenWounds and hard20five-percent LifeTap5 remain unimplemented. NpcCombatEvents explicitly refuses their item registrations. Next execute/compose complete callback/factory/target-cast routes and real pet-source fixed-rank area curse context,including draw/subscriber order and same-hit healing;do not invent an after-HP hook.',
            'Intrinsic150percent scaling before ordinary percentages is an explicit Sanctuary composition with FP quantization. Original plain Minions keep their older native controller/whole-number fallback;their complete FP/source-frame migration,actual installed old-DLL upgrade and concurrent/unheld-save acceptance remain separate.',
            'Shared hit/result/health/death/event order,modern world/geometry/global RNG/PvP/Sunder/client boundaries retain prior evidence limits. Capturing shield/two-hand fractions does not enable unsupported NPC item effects/events or prove the entire item system.',
            'Iron/Fire replacement damage/upgrades,Mage basic spells/triples and Skeleton Mastery mapping,barriers,safe-area parking,Druid upgrades and other class mechanics remain unfinished. No class completion/deployment/player binding. Blood body/cast/contact/healing and pending wound/curse feedback remain placeholders;effects/audio and talent/right/quick icons stay separate.'])
    output = ROOT / 'tools/reference/blood-golem-damage-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Blood damage:301markers/3boots/2reloads,3natural contacts;295retained/4audits/71Python;installed unchanged. Blood proc upgrades remain pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
