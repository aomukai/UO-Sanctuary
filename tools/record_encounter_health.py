#!/usr/bin/env python3
"""Bind encounter FP256 settlement to native saved-life and shield regression."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/encounter-health'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts'] and value['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/encounter-shields-native-evidence.json'
    previous=json.loads(parent.read_text());ref=previous['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/EliteCombat.cs','tests/Sanctuary.Integration/EncounterShieldChecks.cs'}
    added={'tests/Sanctuary.Integration/EncounterHealthChecks.cs'}
    assert set(before)<=set(after) and set(after)-set(before)==added
    assert {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/encounter-shields/birth-native-checks.log'
    assert sha(baseline)==previous['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'encounter-health-check':1,'encounter-health-reload-check':2})
    assert sum(expected.values())==36 and markers(STAGE/'birth-native-checks.log')==expected
    text=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    assert 'Encounter health:physical/magic quarters,256mixed packets' in text
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',861),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/encounter-shields'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    saved=json.loads((STAGE/'encounter-health-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Balance']==saved['Hits']*256
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['encounter-health-checkpoint','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons']
    paths=[parent,baseline,Path(__file__).resolve(),ROOT/'tests/check_encounter_health.py',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','encounter-health-fixtures.json','failed-birth-run-1.log','failed-birth-inputs-1.json']]
    result=dict(referenceVersion='3.3.93847',component='Encounter FP256 life settlement through existing health bridge',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=36,retainedShieldMarkers=33,boots=3,actualReloads=2,saves=3,mixedFractionalPackets=256,actualBoneArmorInputCasts=6,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[861,21],unchangedInstalledArtifacts=106),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Encounter final damage now reaches the existing unchanged SkillHealth.Damage in FP256 instead of being floored to whole HP. Its native damage/death bridge is called once; return values retain native whole-life-loss semantics.',
            'Physical/magic quarter hits,256mixed five-channel FP256 packets,quarter-point Bone Armor overflow,real Shaftstop post-resistance fractions and the existing sub-one-life native death/owner bridge pass. This is native bridge acceptance,not new full current-executable HP/event proof.',
            'A quarter-point injury survives both actual reloads and accumulates to one whole native hit point after four quarter hits. The actual saved player/enemy identities and exact life are checked; the transient fixture Frozen control is restored after load.',
            'All33preceding shield markers,six real Bone Armor casts and original-item/NPC resources remain. Data/kernel/106installed artifacts are unchanged; no deployment or new asset is required.'],
        remaining=['Full modern damage event/stat/reaction/HP/death/PvP/client parity,encounter physical reducer and return/proc/recovery/durability chronology remain separate. Existing native bridge lethal threshold is retained,not newly certified against the executable.',
            'Modern Bone Armor breaking-hit behavior and mixed-shield priority remain unverified. Conventional overflow and fixed shield ordering stay explicitly bounded. No whole skill/class or old-binary migration claim.',
            'The first reload fixture incorrectly required transient Frozen to persist. The corrected fixture preserves actor/life assertions and restores its test control before re-entering the scene. Failed log/inventory retained.'])
    out=ROOT/'tools/reference/encounter-health-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==rendered
    else:out.write_text(rendered)
    print('Encounter health:36markers/3boots/2reloads;four quarters conserve one native HP;4audits/71Python/installed unchanged.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
