#!/usr/bin/env python3
"""Record prepared barrier-world adaptation without enabling learned allocation."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/bone-barrier-retirement';P='24208319efdb60e6921bdb9141ae8112d6bd824d'

def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str) else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p) if p.suffix=='.json' else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-4.json',S/'birth-inputs.json');pin(S/'source-snapshot-4.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/slow-missiles-learned/birth-inputs.json')
    a={n:h for n,h in old['sourceInputs'].items() if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/BoneBarrierRetirement.cs','tests/Sanctuary.Integration/BoneBarrierRetirementChecks.cs'} and not a.keys()-b.keys()
    assert {n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/BoneBarriers.cs','src/Sanctuary.Content/SkillHealth.cs'}
    assert born['copiedData']==old['copiedData']
    for kind in ('content','integration'):
        p=S/f'{kind}-build-3.log';pin(p);assert all(x in p.read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    base=pin('tools/reference/slow-missiles-learned-native-evidence.json')
    expected=Counter({n:v for n,v in base['markers'].items() if n.startswith(('iron-golem-birth-','iron-golem-items-','npc-resource-callbacks-'))});assert sum(expected.values())==9
    for f in ('bone-barrier-retirement','bone-barrier-cast','bone-barrier-birth','bone-wall-maker'):
        expected.update({f+'-check':1,f+'-reload-check':2})
    expected.update({'bone-wall-maker-result':1,'bone-wall-maker-reload-result':2,'bone-barrier-retirement-save':3})
    for mode in ('prison-right','wall-quick'):
        expected.update({f'barrier-retirement-{mode}-check':3,f'barrier-retirement-{mode}-result':3})
    log=e.cohort(S,expected,39);pin(S/'birth-native-checks.log');pin(S/'native-run-4.log')
    assert log.count('Client: 127.0.0.1: Connected.')==6
    for mode in ('prison-right','wall-quick'):
        assert log.count(f'barrier-retirement-{mode}: windup')==3 and log.count(f'barrier-retirement-{mode}: collapse')==3
    saved=pin(S/'bone-barrier-retirement-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==len(saved['Expiring'])==6
    assert all(n['Life']%256 and n['Retirement']['CompletesUtc']>n['Retirement']['EnteredUtc'] for n in saved['Actors'])
    assert pin(S/'bone-barrier-birth-fixtures.json')['Generation']==2
    for name in ('bone-barrier-cast','bone-wall-maker'):pin(S/(name+'-fixtures.json'))
    for run in (1,2,3):pin(S/f'failed-run-{run}/birth-inputs.json');pin(S/f'failed-run-{run}/birth-native-checks.log')
    unchanged=pin(S/'failed-run-2/birth-inputs.json');runner=pin(S/'failed-run-3/birth-inputs.json');assert unchanged['artifacts']==runner['artifacts']
    for n in ('tools/reference/bone-barrier-retirement-source-evidence.json','tools/reference/bone-barrier-hidedead-reference-evidence.json'):pin(n)
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in ('src/Sanctuary.Content/BoneBarriers.cs','src/Sanctuary.Content/BoneBarrierRetirement.cs','src/Sanctuary.Content/SkillHealth.cs',
        'tests/Sanctuary.Integration/BoneBarrierRetirementChecks.cs','tests/check_bone_barrier_retirement.py','tools/record_bone_barrier_retirement.py',
        'docs/bone-barrier-world-rule-proposal.md','docs/bone-barrier-retirement-checkpoint.md','docs/necromancer-mechanical-audit.md',
        'docs/necromancer-coverage-matrix.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Prepared complete Bone Wall/Prison world and retirement adaptation',approved=False,
        retirementEnabledForLearned=False,baselineBarrierSkillsAvailable=True,handlers=236,completedClasses=[],contentBuild=3,integrationBuild=3,
        contentSourceSnapshot=4,sourceSnapshot=4,nativeRun=4,nativeMarkers=39,markers=dict(expected),authenticatedSessions=6,actualCandidateInputs=6,
        inputCases=dict(prisonRightDamageCollapse=3,wallQuickNaturalExpiry=3),boots=3,reloads=2,saves=3,
        inspectedSavedActors=6,naturalPendingActorsPerSave=6,naturalOverdueActorsVerifiedAfterLoad=12,
        artifacts=born['artifacts'],preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),
        newTimers=0,newPersistenceProfiles=[],newSavedSubtypes=['BoneWallRetiringBody','BoneBarrierRetiringBody'],existingSerializationVersionsChanged=False,
        clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Default learned allocator and historical types retain prior behavior;prepared candidate explicitly selected only for tested owners.',
            'Actual child Kill publishes one formation deadline,immediately releases collision/movement/placement,stops life/growth and preserves the deadline on repeat.',
            'Real hostile firebolts damage living barriers before collapse and pass through to the player afterward;all root/near/far members remove at completion.',
            'Six real socket casts cover paid public Prison-right damage entry and Wall-quick natural AI expiry with automatic600ms collapse completion.',
            'Six original inspected typed actors preserve root links,fractional wounds,AI clocks and pending state through two reloads.',
            'Actual newly entered600ms collapses are saved without extending deadlines;12expired actors reload original metadata and automatically remove after downtime.',
            'Retained barrier birth/cast/maker and default Iron/resource fixtures pass on the same assemblies.'],
        limits=['Whole world/geometry/linked-death/hidedead policy remains an explicit proposal,not current D2R parity or approved learned retirement.',
            'No graphical client test,class certification,old-binary byte upgrade or unheld concurrent snapshot guarantee.',
            'Earlier accepted Warlock/Amazon learned input cohorts are historical receipts,not rerun by this affected-system suite.',
            'Four older Warlock proposals remain frozen;final effects/audio and icons remain separate placeholders.'],inputs=inputs)
    p=R/'tools/reference/bone-barrier-retirement-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Prepared barrier world C3/I3/S4/N4:39markers/6candidate inputs/2reloads;whole rule pending,236handlers.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
