#!/usr/bin/env python3
"""Bind approved three-element Mage creation and in-place poison migration checks."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/mage-elements'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-meteor-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/NecromancerSummons.cs','tests/Sanctuary.Integration/MageMissileChecks.cs'}
    added={'tests/Sanctuary.Integration/MageElementChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-meteor/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['mage-elements-check']+=1;expected['mage-elements-reload-check']+=2;expected['mage-elements-live-result']+=3
    assert sum(expected.values())==268 and markers(STAGE/'birth-native-checks.log')==expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage elements:3slot/4096default samples,learned three-element births/zero alias')==1
    assert log.count('Mage elements reload:8 original actors/two poison conversions')==2
    assert log.count('Mage Meteor:hard9+soft1 refusal/hard10 fixed10')==1
    assert log.count('Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts and shared Phoenix offset consumer pass')==3
    assert log.count('Freeze state:12096 application,1296 removal,100 callback and224 length source/native cases passed')==3
    assert log.count('Mage Static:44800 one-draw/exclusive five-percent policy contexts')==1
    assert log.count('Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn')==1
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 875), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
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
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30 and not status['completedClasses']
    docs = ['mage-elements-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_elements.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json', ROOT/'tools/reference/tornado-pulse-source-evidence.json', ROOT/'tools/build_class_skills.py', ROOT/'data/Sanctuary/Balance/necromancer-summons.json']
    names=[f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','pre-build-1.json','content-build-1.log','pre-build-2.json','content-build-2.log','integration-build-2.log','birth-inputs.json','birth-native-checks.log','audit-inputs.json','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','mage-elements-fixtures.json','named-item-runtime-audit.json','first-loop-affix-audit.json']
    files += [STAGE/name for name in names]
    saved=json.loads((STAGE/'mage-elements-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Pets'])==8
    assert Counter(p['OriginalElement'] for p in saved['Pets'])==Counter({0:2,1:2,2:2,3:2})
    assert sum(p['Type'].endswith('.MageWorldBody') for p in saved['Pets'])==4
    assert all(p['ExpectedElement']==(1 if p['OriginalElement']==0 else p['OriginalElement']) for p in saved['Pets'])
    result=dict(referenceVersion='3.3.93847',component='Approved three-element Mage creation and in-place poison-to-ice load migration',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=268,retainedMageMeteorGlacialChainStaticBuffPlayerPetSentryMarkers=262,boots=3,actualReloads=2,saves=3,defaultSamplerCases=4096,savedOriginalMageActors=8,savedPoisonConversions=2,naturalMidWindupMigrationSessions=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[875,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'New Raise Skeletal Mage draws uniformly from three host slots mapped to existing element identities1ice/2fire/3lightning. The legacy zero alias canonicalizes to ice;other unknown values are explicitly unsupported. Non-Mages retain their prior unused four-slot draw and do not migrate.',
            'Existing serialized element field/version0/type/serial is reused. Load migration only changes Mage0to1,without new actor,controller,profile,rank/resource recapture,RNG,healing,roster commit or projectile/DOT rewrite. Existing supported elements remain identical. Converted hard20 Mages acquire the existing intrinsic Frozen Armor20 consumer.',
            'Three slot mappings,4096default samples,actual learned births/zero alias,live idempotence/unknown refusal,non-Mage and existing poison packet/DOT controls pass. Four historical plain Minions and four world Mages preserve owner/type/serial,ranks,Mastery,derived profile/resources,resistances,FIFO order,critical seed and parked clocks across2reloads. Full,one-FP and fractional current life are covered.',
            'A held old poison missile remains byte-equal across conversion/reloads;a copied flight still applies poison after its caster becomes ice. A new missile from that actor uses the existing cold baseline. Three connected natural sessions migrate in place during source windup,retain state/seed/wounds and launch/damage with the cold packet at action35 without extra mana.',
            'All262 predecessor markers,71Python,4audits and named/catalogue checks pass. Older four-element missile controls explicitly seed a legacy poison context rather than represent new poison creation. Runtime data,audit decisions,kernel and106installed hashes remain unchanged.'],
        remaining=[
            'Replacement basic Sorceress spells,hard5triples and the recorded Skeleton Mastery mapping remain required;current basic packets still use the bounded NecromageMissile curves. All fixed-rank secondaries/buffs are already bound for the new world subtype. Historical plain Mage AI rollout remains queued;in-place element conversion does not grant a new controller.',
            'Saved cohorts are candidate version0 legacy-shaped field payloads with original identities,not a real installed old-DLL byte upgrade. Parked controller transport and copied resumption do not certify unheld concurrent saves,all active load/event ordering or exact modern engine/world/client/seed geometry.',
            'Existing poison packet/DOT lifetime rules remain intact;full source-after-removal/PvP/Sunder/event and native glyph/appearance behavior remain broader reference gaps. The three-element pool and poison-to-ice conversion are approved Sanctuary deviations,not D2R baseline claims.',
            'Other approved pets,safe-area parking,barrier phases and remaining class/skill mechanics stay queued. No completed class,deployment,new player binding or final effects/audio/icons;shared placeholder bodies and elemental spell cues remain explicit.'])
    output = ROOT / 'tools/reference/mage-elements-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Mage elements:268markers/3boots/2reloads,8saved actors/2conversions,3natural windup sessions;262retained/4audits/71Python;installed unchanged. Mage package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
