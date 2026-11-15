#!/usr/bin/env python3
"""Authenticate scoped Flame Wave suppliers without claiming world implementation."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/flame-wave-world';P='4305ba4b6a3469611308a112c7b2e3133aa62fe1'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-1.json',S/'birth-inputs.json');pin(S/'source-snapshot-1.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/miasma-chain-cast/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/FlameWaveBirth.cs','tests/Sanctuary.Integration/FlameWaveBirthChecks.cs'}and not a.keys()-b.keys()and all(b[n]==h for n,h in a.items())
    assert born['copiedData'].keys()-old['copiedData'].keys()=={'Data/Sanctuary/Balance/flame-wave-route.json'}and all(born['copiedData'][n]==h for n,h in old['copiedData'].items())
    for k in ('content','integration'):
        p=S/f'{k}-build-1.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    parent=pin('tools/reference/miasma-chain-cast-native-evidence.json');expected=Counter(parent['markers']);expected.update({'flame-wave-birth-check':3});log=e.cohort(S,expected,105)
    pin(S/'birth-native-checks.log');pin(S/'native-run-1.log');assert log.count('Client: 127.0.0.1: Connected.')==45
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    source=pin('tools/reference/flame-wave-birth-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(R/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in ('src/Sanctuary.Content/FlameWaveBirth.cs','tests/Sanctuary.Integration/FlameWaveBirthChecks.cs','data/Sanctuary/Balance/flame-wave-route.json','tests/check_flame_wave_world.py','tools/record_flame_wave_birth.py','docs/flame-wave-birth-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Flame Wave captured graph/numeric inputs',flameWaveWorldImplemented=False,learnedEnabled=False,handlers=232,warlockHandlers=23,completedClasses=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=105,markers=dict(expected),authenticatedSessions=45,boots=3,reloads=2,saves=3,actualFlameWaveInputs=0,retainedActualInputs=30,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Isolated708/690/695/716/240/29graph,numeric raw versus prerequisite Ring damage,width/mana/duration and actual hard/effective gear capture pass.','Accepted Chain/Bolt/Mirrored30socket inputs,world/save/binding/resource controls retain across two reloads.'],
        limits=['No Flame Wave world/input/burn or active persistence consumer yet.','Current156/38/42,tail/static overrides,240damage inheritance,overlap/tick windows and ownership remain unfinished;tail8is a proposed additive binding.','No partial approval question,new class certificate or final artwork.'],inputs=inputs)
    target=R/'tools/reference/flame-wave-birth-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Flame Wave suppliers C1/I1/S1/N1:105markers/45connections/30retained inputs/2reloads;world/learned gated,232handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
