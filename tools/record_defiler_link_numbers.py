#!/usr/bin/env python3
"""Authenticate Defiler's raw link suppliers and accepted adaptation decision."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/defiler-link-numbers'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];second=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['copiedData']==second['copiedData']and first['projectInputs']==second['projectInputs']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=second['sourceInputs'][n]}=={'tests/Sanctuary.Integration/DefilerLinkNumberChecks.cs'}
    for kind in ('content','integration'):
        p=STAGE/f'{kind}-build-{1 if kind=="content"else 2}.log';pin(p);assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/tainted-cast-native-evidence.json');oracle=pin('tools/reference/defiler-link-formula-evidence.json')
    for kind in ('inputs','artifacts'):
        for name,h in oracle[kind].items():assert e.sha(ROOT/name)==h,name;pin(name)
    assert oracle['fogCases']==198
    approved=pin('tools/reference/defiler-health-link-rule-approval.json');assert approved['approved']and e.sha(ROOT/approved['proposal'])==approved['proposalAtApprovalSha256'];pin(approved['proposal'])
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'defiler-link-numbers-check':1,'defiler-link-numbers-reload-check':2})
    text=e.cohort(STAGE,expected,69);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Defiler Health Link:510 source/getter contexts;198rawFogcomparisons;')==3
    assert pin(STAGE/'defiler-link-numbers-fixtures.json')['Generation']==2
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','DefilerLinkNumberChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'FileNotFoundError'in (STAGE/'diagnostics-1/native-run-1.log').read_text()
    for name in ('src/Sanctuary.Content/DefilerLinkNumbers.cs','tests/Sanctuary.Integration/DefilerLinkNumberChecks.cs','tests/check_defiler_link_numbers.py',
        'tools/build_defiler_link.py','tools/probe_defiler_link_numbers.py','tools/record_defiler_link_numbers.py','data/Sanctuary/Balance/defiler-health-link.json',
        'docs/defiler-link-numbers-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Defiler Health Link raw numeric suppliers',handlers=223,warlockHandlers=14,completedClasses=[],
        learnedDefilerEnabled=False,healthLinkRuntimeEnabled=False,healthLinkRuleApproved=True,sourceFogCases=198,nativeGetterContextsPerBoot=510,
        nativeMarkers=69,markers=dict(expected),actualInputs=12,authenticatedSessions=24,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Owned NPC405 retained separately;common innate getter supplies own405/377ranks without recapturing owner gear.',
            'Exact raw Fog expressions including EOF imbalance compile/evaluate without repair;source multiplication/rounding and user25tooltip55percent/5rawlinks match.',
            'Raw99results61percent/6links preserved separately from approved runtime caps60/5;missing skill refuses.',
            'Current learned Goatman/Tainted actual-input/automatic host/cap/mana/body/source/RNG/save regression retains.',
            'Complete Health Link proposal explicitly approved;no repeat question. Implementation of state/damage/flying host/cast remains.'],
        limits=['No do162/event38 execution,current callback cap equivalence or automatic Defiler is claimed.',
            'Approved adaptation settles the behavior but does not replace implementation/verification;other skills/whole-class certification/assets remain open.'],inputs=inputs)
    path=ROOT/'tools/reference/defiler-link-numbers-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Defiler link number evidence stale'
    else:path.write_text(encoded)
    print('Defiler Link:198exactFog/510nativegetter contexts perboot;69markers/12currentpetinputs/2reloads;complete Health Link rule approved,implementation pending.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
