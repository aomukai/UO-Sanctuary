#!/usr/bin/env python3
"""Bind shared AI67 target wiring and retained prepared Iron Golem behavior."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/necromancer-pet-targets';sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/encounter-health-native-evidence.json';previous=json.loads(parent.read_text());ref=previous['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/IronGolemWorld.cs'}
    added={'src/Sanctuary.Content/NecromancerPetTargets.cs','tests/Sanctuary.Integration/NecromancerPetTargetChecks.cs'}
    assert set(before)<=set(after) and set(after)-set(before)==added and {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    retained=ROOT/'tools/reference/iron-golem-world-native-evidence.json';prior=json.loads(retained.read_text());baseline=ROOT/'.local/class-skills/iron-golem-world/birth-native-checks.log'
    assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'necromancer-pet-targets-check':3})
    assert sum(expected.values())==27 and markers(STAGE/'birth-native-checks.log')==expected
    text=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    assert text.count('AI67:1598 complete source ordered-service/state/draw comparisons')==3
    assert text.count('Necromancer pet target adapter:actual prepared skeleton')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',862),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/encounter-health'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    assert 'Owner combatant precedence' in (STAGE/'failed-birth-run-1.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['necromancer-pet-targets-checkpoint','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons']
    files=[parent,retained,baseline,Path(__file__).resolve(),ROOT/'tests/check_necromancer_pet_targets.py',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','iron-golem-world-fixtures.json','failed-birth-run-1.log','failed-birth-inputs-1.json']]
    result=dict(referenceVersion='3.3.93847',component='Shared melee AI67 target queries with prepared skeleton bindings',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=27,retainedWorldMarkers=24,boots=3,actualReloads=2,saves=3,connectedIronGolemSessions=3,sourcePolicyCasesPerBoot=1598,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[862,21],unchangedInstalledArtifacts=106),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Iron Golem existing melee AI67 owner/primary/selection/eligibility/LOS query wiring is extracted without importing its original-item movement or attack clocks into skeletons. Native14tile scan,previous-target24boundary and serial ties retain their existing adapters.',
            'An actual prepared RaisedSkeleton binds these queries to supplied eligibility/distance/melee/actions. Tests cover current/nearest/ties/scan and distance boundaries,class-owner session selection,eligibility/LOS and exact AI67approach/attack requests. Ranged binding refuses; no automatic skeleton movement/contact or new seed model is implied.',
            'All24retained Iron Golem world/birth/body/original-item/resource markers and1598policy comparisons per boot pass,including three connected natural sessions and two actual reloads. Data/kernel/installed106 remain unchanged; no deployment.'],
        remaining=['Automatic regular skeleton AI67world motion,initial S1,captured contact/event timing,mid-action rescheduling and saved in-flight controller state remain integration work. This is a shared query foundation,not a completed skeleton controller or class.',
            'Native scan/LOS/ties/geometry,full current-executable owner/state/seed/room/AI/event/client parity and final skeleton shield distribution remain unverified. Species eligibility/geometry/actions are supplied for the prepared skeleton tests.',
            'Class-enabled PlayerMobile deliberately nulls native Combatant;the first fixture incorrectly expected it to retain one. The corrected native check uses actual ClassCombat session selection. Historical nonclass Combatant precedence is preserved in code but not newly certified by that fixture.'])
    out=ROOT/'tools/reference/necromancer-pet-targets-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==rendered
    else:out.write_text(rendered)
    print('Necromancer pet targets:27markers/3boots/2reloads,1598policy cases per boot,4audits/71Python;prepared skeleton queries ready for controller integration.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
