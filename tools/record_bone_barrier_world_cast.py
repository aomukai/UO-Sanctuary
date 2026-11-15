#!/usr/bin/env python3
"""Bind approved normal learned barrier-world acceptance and legacy compatibility."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/bone-barrier-world-cast';P='e596fd4532cf2ad31b9b99a0960d235fd9e7e138'

def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str) else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p) if p.suffix=='.json' else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-1.json',S/'birth-inputs.json');pin(S/'source-snapshot-1.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/bone-barrier-retirement/birth-inputs.json')
    a={n:h for n,h in old['sourceInputs'].items() if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    changed={'src/Sanctuary.Content/BoneBarriers.cs','src/Sanctuary.Content/BoneBarrierRetirement.cs',
        'tests/Sanctuary.Integration/BoneBarrierRetirementChecks.cs','tests/Sanctuary.Integration/BoneBarrierChecks.cs','tests/Sanctuary.Integration/BoneBarrierClockChecks.cs'}
    assert a.keys()==b.keys() and {n for n in a if a[n]!=b[n]}==changed
    assert born['copiedData']==old['copiedData']
    for kind in ('content','integration'):
        p=S/f'{kind}-build-1.log';pin(p);assert all(x in p.read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    base=pin('tools/reference/bone-barrier-retirement-native-evidence.json');expected=Counter(base['markers'])
    expected.update({'bone-barriers-check':1,'bone-barriers-result':1,'bone-barriers-reload-check':2,
        'bone-barrier-clock-check':1,'bone-barrier-clock-result':1,'bone-barrier-clock-reload-check':2,'bone-barrier-clock-reload-result':2})
    log=e.cohort(S,expected,49);pin(S/'birth-native-checks.log');pin(S/'native-run-1.log');assert log.count('Client: 127.0.0.1: Connected.')==6
    for mode in ('prison-right','wall-quick'):
        assert log.count(f'barrier-retirement-{mode}: windup')==3 and log.count(f'barrier-retirement-{mode}: collapse')==3
    saved=pin(S/'bone-barrier-retirement-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==len(saved['Expiring'])==6
    assert all(n['Life']%256 for n in saved['Actors'])
    assert pin(S/'bone-barrier-birth-fixtures.json')['Generation']==2
    assert len(pin(S/'bone-barrier-clock-fixtures.json'))==5
    for name in ('bone-barrier-cast-fixtures.json','bone-wall-maker-fixtures.json','bone-barrier-checks.json'):pin(S/name)
    approval=pin('tools/reference/bone-barrier-world-rule-approval.json')
    assert approval['approved'] and approval['candidateCommit']==P and approval['answer']=='Approve the complete barrier-world rule (Recommended)'
    assert approval['questionItemId']=='["request_user_input_async","call_0fcfa3489a944202871a3b9b31f5c2d2",0]'
    assert e.sha(R/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',P+':'+approval['proposal']],cwd=R)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending approval; ordinary learned allocation remains unchanged.**',
        '**Approved by the user on 2026-11-16 JST.** [Exact decision](../tools/reference/bone-barrier-world-rule-approval.json). The body below is the approved proposal; learned acceptance is recorded separately.',1)==(R/approval['proposal']).read_text()
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in (*sorted(changed),'tests/check_bone_barrier_world_cast.py','tools/record_bone_barrier_world_cast.py',approval['proposal'],
        'docs/bone-barrier-world-cast-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/necromancer-coverage-matrix.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md',
        'docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Bone Wall/Prison world and retirement rule',approved=True,
        retirementEnabledForLearned=True,handlers=236,necromancerHandlers=30,completedClasses=[],contentBuild=1,integrationBuild=1,
        contentSourceSnapshot=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=49,markers=dict(expected),authenticatedSessions=6,actualInputs=6,
        inputCases=dict(prisonRightLethalWeaponCollapse=3,wallQuickNaturalExpiry=3),boots=3,reloads=2,saves=3,
        artifacts=born['artifacts'],preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),
        newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,
        newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Whole barrier-world proposal approved with original candidate/proposal/evidence hashes;normal learned allocation now uses the new world/death types.',
            'Historical roots retain historical children instead of mixing in the new rule;old plain/prepared/thinking resource/clock/load paths retain scoped coverage.',
            'Six actual learned socket casts use the default allocator without a candidate override;AOS lethal weapon damage triggers Prison formation collapse,and Wall grows then naturally expires.',
            'Actual movement/placement,hostile projectile interception and immediate collision release,immutable600ms deadline and explicit near/far cleanup pass.',
            'Original inspected actor resources/links/state and actual in-progress collapse saves with automatic overdue removal pass two reloads.',
            'Earlier full barrier collision/growth/expiry and five-actor clock/historical-save cohorts now pass with the approved collapse expectations.'],
        limits=['Modern native geometry/owner/hidedead/order remain explicit approved adaptations,not recovered current executable equivalence.',
            'No class certification,graphical playtest,old-binary byte-upgrade or unheld concurrent snapshot guarantee.',
            'Unrelated Warlock/Amazon input cohorts remain historical evidence;four older Warlock proposals remain frozen.',
            'Final cast/growth/collapse/removal effects/audio and talent/hotkey icons remain separate placeholders.'],inputs=inputs)
    p=R/'tools/reference/bone-barrier-world-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Approved learned barriers C1/I1/S1/N1:49markers/6real inputs/2reloads;236handlers,Necromancer30.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
