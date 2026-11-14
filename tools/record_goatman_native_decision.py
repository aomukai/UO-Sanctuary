#!/usr/bin/env python3
"""Authenticate compiled score fields and the native source decision binding."""
import argparse,json,struct
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-native-decision'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    origin=pin('tools/reference/generic-pet-metadata-data-evidence.json');raw=ROOT/'.local/class-skills/defiler-compiled-formula/skills.bin';pin(raw)
    assert any(n.endswith('skills.bin')and h==e.sha(raw)for n,h in origin['artifacts'].items())
    data=raw.read_bytes();assert struct.unpack_from('<I',data)[0]==429 and len(data)==4+429*748
    rows=[]
    for id in (416,418,417,419):
        r=data[4+id*748:4+(id+1)*748];assert struct.unpack_from('<h',r)[0]==id
        rows.append(dict(id=id,flags37=r[37],score=dict(flags36=r[36],flags39=r[39],quarter532=struct.unpack_from('<h',r,532)[0],
            bonus730=struct.unpack_from('<h',r,730)[0],element644=r[644],state162=struct.unpack_from('<h',r,162)[0],
            class272=struct.unpack_from('<h',r,272)[0],ownerCompare274=struct.unpack_from('<b',r,274)[0],calc276=struct.unpack_from('<i',r,276)[0])))
    assert pin('data/Sanctuary/Balance/goatman-ai-scores.json')==rows
    monster=pin('tools/reference/d2r-named-equipment/json/monstats.json')['wargoatman']
    assert [monster[f'Skill{i}']for i in range(1,5)]==['Goatman Stun','Goatman Berserk','Goatman Frenzy','Goatman Cleave']
    pin('data/Sanctuary/Balance/generic-pet-parameters.json');pin('data/Sanctuary/Balance/generic-pet-skills.json')
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,num in [('content',3),('integration',3)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-npc-contact-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-native-decision-check':1,
        'goatman-native-decision-reload-check':2,'goatman-npc-body-action-check':1,'goatman-npc-body-action-reload-check':2,'goatman-npc-action-check':3})
    e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    saved=pin(STAGE/'goatman-native-decision-fixtures.json');assert saved['Generation']==2 and saved['Random']['Unarmed']and saved['Controller']['Control']is not None
    assert pin(STAGE/'goatman-npc-body-action-fixtures.json')['Generation']==2
    for run in (1,2):
        for name in ('birth-inputs.json','birth-native-checks.log',f'native-run-{run}.log','GoatmanNativeDecisionChecks.cs','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/f'diagnostics-{run}'/name)
    for name in ('GoatmanNativeDecision.cs','Sanctuary.Content.dll'):pin(STAGE/'diagnostics-2'/name)
    assert 'executed=1,prior=1'in (STAGE/'diagnostics-2/birth-native-checks.log').read_text()
    for name in ('src/Sanctuary.Content/GoatmanNativeDecision.cs','tests/Sanctuary.Integration/GoatmanNativeDecisionChecks.cs',
        'tests/check_goatman_native_decision.py','tools/record_goatman_native_decision.py','docs/goatman-native-decision-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Goatman bound source decision root',handlers=221,warlockHandlers=12,completedClasses=[],
        learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,
        contentBuild=3,integrationBuild=3,sourceSnapshot=4,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,
        newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,compiledSkillStride=748,compiledScoreRows=4,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Live captured body/owner/controller words compose existing initialization,gather,search,preparation,target scoring,routing,weighted actions and follow executor.',
            'All5base/unlocked skills selected through repeated real roots with native target stats and actual ordered damage leaves.',
            'Accepted-target path uses direct per-target scoring;new-target path uses category routing.',
            'AI weighted roll and real combat callbacks share one saved actor RNG stream without overwriting nested advances.',
            'Type2event removal,actual follower census and original control/birth/RNG/wounds across2reloads.'],
        limits=['Source AI-entry preprocessing,room/owner traversal,movement/warp and timed skill execution remain mandatory external services.',
            'The fixture traversal/contact/follow leaves are declared acceptance inputs;not an installed full controller or birth-path constructor.',
            'Native world clock/rearm,cap and learned summon still unfinished;no class certified.',
            'Final effects/audio and talent/hotkey icons remain separately pending.'],inputs=inputs)
    path=ROOT/'tools/reference/goatman-native-decision-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Goatman decision receipt stale'
    else:path.write_text(encoded)
    print('Goatman decision root:18markers/2reloads;all5source choices/actual HP/shared RNG;world-entry/movement/timed execution remain unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
