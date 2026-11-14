#!/usr/bin/env python3
"""Authenticate learned Defiler source host,actual input and saved gameplay."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/defiler-cast'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-1.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/defiler-link-casting-native-evidence.json');source=pin('tools/reference/defiler-ai-source-evidence.json')
    assert source['sourceClass']==744 and source['slots']==[405]and source['modes']==[8]
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name;pin(name)
    assert pin('data/Sanctuary/Balance/defiler-ai-scores.json')==source['rows']
    parameters=pin('data/Sanctuary/Balance/generic-pet-parameters.json');raw=next(r['Parameters']for r in parameters if r['Key']=='warputriddefiler')
    assert raw[:12]==[30,30,60,10,0,2,0,0,1,4,2,0]
    for name in ('tools/reference/defiler-health-link-rule-approval.json','tools/reference/defiler-flight-world-approval.json'):
        approval=pin(name);assert approval['approved']and e.sha(ROOT/approval['proposal'])==approval['proposalAtApprovalSha256']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'defiler-cast-check':1,'defiler-cast-reload-check':2,'defiler-cast-check-result':1,'defiler-cast-reload-check-result':2,
        'defiler-cast-right-check':3,'defiler-cast-right-result':3,'defiler-cast-quick-check':3,'defiler-cast-quick-result':3})
    text=e.cohort(STAGE,expected,114);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('automatic original Defiler linked/shared through saved source host;')==6
    assert text.count('Client: 127.0.0.1: Connected.')==45
    assert pin(STAGE/'defiler-cast-fixtures.json')['Generation']==2
    assert pin(STAGE/'defiler-link-casting-fixtures.json')['Generation']==2
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text();assert len(re.findall(r'CombatAbility\.\w+',code.split('public static bool Implemented')[1].split(';',1)[0]))==15
    for name in ('src/Sanctuary.Content/DefilerHealthLink.cs','src/Sanctuary.Content/DefilerLinkCasting.cs',
        'src/Sanctuary.Content/DemonNativeState.cs','src/Sanctuary.Content/GoatmanNativeDecision.cs',
        'src/Sanctuary.Content/GoatmanNativeSearch.cs','src/Sanctuary.Content/GoatmanNativeHostCandidate.cs',
        'src/Sanctuary.Content/WarlockDefilerBody.cs','src/Sanctuary.Content/WarlockGoatmanWorld.cs',
        'src/Sanctuary.Content/WarlockGoatmanCasting.cs','src/Sanctuary.Content/WarlockCombat.cs','src/Sanctuary.Content/GrimWardCurseTransport.cs',
        'tests/Sanctuary.Integration/DefilerCastChecks.cs','tests/Sanctuary.Integration/DefilerLinkCastingChecks.cs','tests/check_defiler_cast.py',
        'tools/build_defiler_ai.py','tools/record_defiler_cast.py','docs/defiler-cast-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Learned Summon Defiler with automatic original source host/S1/HealthLink',handlers=224,warlockHandlers=15,completedClasses=[],
        learnedDefilerEnabled=True,automaticDefilerAiEnabled=True,healthLinkRuleApproved=True,flightWorldApproved=True,
        nativeMarkers=114,markers=dict(expected),actualInputs=18,defilerActualInputs=6,authenticatedSessions=45,boots=3,reloads=2,saves=3,
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['OwnedNPC405/type2 scorer row and rawbase4/type2 retained;actual source root/search/state210/213/action system selects nearest unmarked enemies without ordinary melee.',
            'Learned SC route commits captured birth/one debit/shared three-species limit;cancel/late mana/placement failure preserve existing roster.',
            'Automatic ownS1 marks and actual typed damage shares pass on direct/real right/quick input,including Warlock/Witch and captured gear rank.',
            'Lost original target records a warned refused release,no invented fallback/no marks,then future source decisions continue without permanent controller fault.',
            'Shared flying path/follow/warp/clock/recovery/parking and optional saved Defiler controller retain original source state,wounds,RNG/birth and real login across two reloads;legacy bodies stay unadopted.',
            'Current learned Goatman/Tainted and Health Link/S1/flight-world/saved-pending-action regressions remain green.'],
        limits=['Whole-class certification,remaining15Warlock plusAmazonSlowMissiles and broader source/property/recipient coverage remain unfinished.',
            'Current D2R path-point fallback remains unverified and is not executed;the refused release follows approved target eligibility.','Scalar-only direct damage still explicitly faults link sharing;no element is guessed.',
            'Final summon/S1/link/shared-damage/expiry effects/audio and talent/right/quick icons remain separately pending.'],inputs=inputs)
    path=ROOT/'tools/reference/defiler-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Learned Defiler evidence stale'
    else:path.write_text(encoded)
    print('Learned Defiler:C1/I1/S1/N1,114markers/18realinputs/2reloads;224handlers/Warlock15;installed build/saves protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
