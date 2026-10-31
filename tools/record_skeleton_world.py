#!/usr/bin/env python3
"""Bind learned skeleton AI67 world delivery and historical summon compatibility."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/skeleton-world';sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/necromancer-pet-targets-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['IronGolemWorld','RaisedSkeletonContact','SkeletonPets']}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs' for n in ['SkeletonBirthChecks','SkeletonCombatChecks','SkeletonContactChecks']}
    added={'src/Sanctuary.Content/SkeletonWorldBody.cs','tests/Sanctuary.Integration/SkeletonWorldChecks.cs'}
    assert set(before)<=set(after) and set(after)-set(before)==added and {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/necromancer-pet-targets/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)
    for family in ['skeleton-contact','skeleton-world','skeleton-birth','skeleton-roster','skeleton-combat','summon-difficulty']:
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    expected['skeleton-world-live-result']+=3;expected['skeleton-combat-live-result']+=3
    for n in ['skeleton-birth-right','skeleton-birth-quick']:expected[n+'-check']+=3;expected[n+'-result']+=3
    assert sum(expected.values())==63 and markers(STAGE/'birth-native-checks.log')==expected
    text=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    assert text.count('AI67:1598 complete source ordered-service/state/draw comparisons')==3
    assert 'Skeleton world:shared AI67 S1end24/think25/A1action37/end44' in text
    assert 'Skeleton contact' in text and 'existing native rules' in text
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',863),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/necromancer-pet-targets'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    saved=json.loads((STAGE/'skeleton-world-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Life']==69247 and saved['TargetLife']==5120000 and saved['LegacySeed']==173
    assert len({saved[n] for n in ['Owner','Body','Target','Legacy']})==4
    assert markers(STAGE/'prepared-birth-run-1.log')['skeleton-world-live-result']==3
    assert sum(markers(STAGE/'learned-birth-run-3.log').values())==63
    assert 'Build FAILED.' in (STAGE/'content-build-1.log').read_text() and 'Corpse' in (STAGE/'integration-build-5.log').read_text()
    assert 'FileNotFoundError' in (STAGE/'run-2.log').read_text() and '/dev/shm/' in (STAGE/'run-2.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[x for x in status['skills'] if x['implementation']=='handler-present']
    assert len(handlers)==186 and sum(x['characterClass']=='Necromancer' for x in handlers)==30 and not status['completedClasses']
    docs=['skeleton-world-checkpoint','necromancer-pet-targets-checkpoint','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons']
    files=[parent,baseline,Path(__file__).resolve(),ROOT/'tests/check_skeleton_world.py',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','skeleton-world-fixtures.json','skeleton-contact-fixtures.json','skeleton-birth-fixtures.json','skeleton-roster-fixtures.json','skeleton-combat-fixtures.json','summon-difficulty-fixtures.json','prepared-birth-run-1.log','prepared-birth-inputs-1.json','learned-birth-run-3.log','learned-birth-inputs-3.json','content-build-1.log','integration-build-5.log','run-2.log']]
    result=dict(referenceVersion='3.3.93847',component='Learned regular skeleton shared AI67 world controller',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=63,retainedPetTargetWorldMarkers=27,boots=3,actualReloads=2,saves=3,learnedRightQuickCasts=6,learnedNaturalSkeletonSessions=3,retainedIronNaturalSessions=3,historicalNativeSkeletonSessions=3,sourcePolicyCasesPerBoot=1598,birthSourceContexts=11475,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[863,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['New learned Raise Skeleton casts opt into a separate SkeletonWorldBody with saved owner/controller JSON and explicit native-timer suppression. Historical RaisedSkeleton/SkeletonGuardian formats remain on their prior paths. Corpse/payment,FIFO,birth/resources,difficulty and six real right/quick inputs pass.',
            'The existing Iron conductor is shared through explicit species bindings. Skeletons use current SKS1end24/firstthink25,Velocity13/Run15/WL128 and A1speed224/action12/end19. AI67entry25 captures a mapped unarmed packet;action37 releases it once;end44 does not replay. Live learned sessions use the natural40ms clock.',
            'Source-cell melee delegates preserve contact precision;historical unarmed callers retain their default native-coordinate predicate. Captured mitigation/RNG,no early HP,recovery interruption,follow,exact regeneration,offline suspension and malformed-state refusal pass. Iron original-item clocks/world remain independently verified.',
            'Unmapped native recipients keep the existing reported pre-packet native bridge. Skeleton-only fallback acquisition uses explicit size2/native RangeFight adapters;an actual Dog enters and completes that path without controller fault. No source packet allocation failure retries through fallback.',
            'Both reloads retain new actor/owner/wounds/seed and a historical RaisedSkeleton. Transient in-flight contact is canceled by existing suspension/load policy rather than replayed. The inspected cohort next observation is held60seconds for snapshot staging;natural unheld timing is separately verified.'],
        remaining=['Full current-executable room/seed/owner/AI/mode/event parity,native scan/ties/LOS/fit/cell projection,live mid-animation rate rescheduling,unheld concurrent snapshots,PvP/special-recipient adapters and final shield/component distribution remain open. Host/default actor seed transport is an explicit existing adapter,not a recovered D2 world-seed contract.',
            'Pending damage packets remain intentionally transient on suspension/load;this does not certify saved in-flight action resumption. Native fallback mechanics and damaging-hit metrics do not claim source-equivalent blocking/geometry/packet chronology. All unsupported scopes stay explicit.',
            'Prepared run1 and learned run3 passed before the native-recipient compatibility refinement. Build1 naming/shadow issues and build5 missing fixture import are archived. Run2 was mistakenly started after failed integration and stopped before boot for a missing DLL. Only final build7/run4/audit1is acceptance.',
            'No deployment,actual old-binary migration,complete skill/class or final-art claim. New Name/body/native A1,emergence/step/hit/death/audio and tree/hotkey art remain identified placeholders in separate registers.'])
    out=ROOT/'tools/reference/skeleton-world-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==rendered
    else:out.write_text(rendered)
    print('Learned skeleton world:63markers/3boots/2reloads,6real casts/3auto sessions;legacy and Iron retained,4audits/71Python/installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
