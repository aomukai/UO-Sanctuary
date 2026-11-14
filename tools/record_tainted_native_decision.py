#!/usr/bin/env python3
"""Authenticate Tainted native source-root binding and owned ordered skill rows."""
import argparse,csv,json,re,struct
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/tainted-native-decision'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    snaps=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in (1,2,3,4,5)]
    assert all(s['projectInputs']==snaps[0]['projectInputs']for s in snaps)
    assert snaps[0]['copiedData']==snaps[1]['copiedData']
    assert {n for n in snaps[0]['sourceInputs']if snaps[0]['sourceInputs'][n]!=snaps[1]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/TaintedNativeDecisionChecks.cs'}
    assert {n for n in snaps[1]['sourceInputs']if snaps[1]['sourceInputs'][n]!=snaps[2]['sourceInputs'][n]}=={'src/Sanctuary.Content/GoatmanNativeDecision.cs','tests/Sanctuary.Integration/TaintedNativeDecisionChecks.cs'}
    assert {n for n in snaps[1]['copiedData']if snaps[1]['copiedData'][n]!=snaps[2]['copiedData'][n]}=={'Data/Sanctuary/Balance/tainted-ai-scores.json'}
    assert snaps[2]['copiedData']==snaps[3]['copiedData']
    assert {n for n in snaps[2]['sourceInputs']if snaps[2]['sourceInputs'][n]!=snaps[3]['sourceInputs'][n]}=={'tests/Sanctuary.Integration/TaintedNativeDecisionChecks.cs'}
    assert snaps[3]['copiedData']==snaps[4]['copiedData']
    assert {n for n in snaps[3]['sourceInputs']if snaps[3]['sourceInputs'][n]!=snaps[4]['sourceInputs'][n]}=={'src/Sanctuary.Content/GoatmanNativeDecision.cs','tests/Sanctuary.Integration/TaintedNativeDecisionChecks.cs'}
    for kind in ('content','integration'):
        for num in ((1,2,3)if kind=='content'else(1,2,3,4)):
            log=STAGE/f'{kind}-build-{num}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/tainted-ground-movement-native-evidence.json');origin=pin('tools/reference/generic-pet-metadata-data-evidence.json')
    p=ROOT/'.local/class-skills/defiler-compiled-formula/skills.bin';pin(p);assert e.sha(p)in origin['artifacts'].values()
    data=p.read_bytes();assert struct.unpack_from('<I',data)[0]==429 and len(data)==4+429*748
    rows=[]
    for id in (422,420):
        r=data[4+id*748:4+(id+1)*748];assert struct.unpack_from('<h',r)[0]==id
        rows.append(dict(id=id,flags37=r[37],score=dict(flags36=r[36],flags39=r[39],quarter532=struct.unpack_from('<h',r,532)[0],
            bonus730=struct.unpack_from('<h',r,730)[0],element644=r[644],state162=struct.unpack_from('<h',r,162)[0],
            class272=struct.unpack_from('<h',r,272)[0],ownerCompare274=struct.unpack_from('<b',r,274)[0],calc276=struct.unpack_from('<i',r,276)[0])))
    assert pin('data/Sanctuary/Balance/tainted-ai-scores.json')==rows
    owned=pin('tools/reference/d2r-local-sentry-model-evidence.json');entry=owned['files']['monstats'];p=ROOT/entry['path'];pin(p);assert e.sha(p)==entry['sha256']
    monster=next(r for r in csv.DictReader(p.read_text().lstrip('\ufeff').splitlines(),delimiter='\t')if r['Id']=='warbighead')
    assert [monster[f'Skill{i}']for i in range(1,9)]==['Tainted Fire Ball','Tainted Resist Fire','','','','','','']
    assert monster['Sk1mode']=='A2'and monster['Sk2mode']=='S1'
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'tainted-native-decision-check':1,'tainted-native-decision-reload-check':2,
        'tainted-native-decision-check-result':1,'tainted-native-decision-reload-check-result':2})
    text=e.cohort(STAGE,expected,45);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert text.count('Tainted source root:3actual native room/grid/own-fire-resistance decisions;')==3
    assert len(re.findall(r'goatman-cast-(?:right|quick): automatic original demon attacked through saved source host',text))==6
    assert pin(STAGE/'tainted-native-decision-fixtures.json')['Generation']==2
    assert pin(STAGE/'tainted-ground-movement-fixtures.json')['Generation']==2
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','TaintedNativeDecisionChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'Wrong standing collision query'in (STAGE/'diagnostics-1/birth-native-checks.log').read_text()
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','tainted-native-decision-fixtures.json','Sanctuary.Content.dll','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'reviewed-run-2'/name)
    for name in ('src/Sanctuary.Content/GoatmanNativeDecision.cs','src/Sanctuary.Content/GoatmanNativeSearch.cs',
        'tests/Sanctuary.Integration/TaintedNativeDecisionChecks.cs','tests/Sanctuary.Integration/TaintedGroundMovementChecks.cs',
        'tests/check_tainted_native_decision.py','tools/record_tainted_native_decision.py','docs/tainted-native-decision-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Tainted native room/grid/source decision to own A2 and actual projectile damage',handlers=222,warlockHandlers=13,completedClasses=[],
        learnedGoatmanEnabled=True,learnedTaintedEnabled=False,automaticTaintedHostEnabled=False,nativeMarkers=45,markers=dict(expected),
        authenticatedSessions=15,actualInputs=6,boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=4,sourceSnapshot=5,nativeRun=3,
        nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Complete existing source root/owner census/gather/search/preparation/scorer/actions/control/RNG binding reused for original captured Tainted.',
            'Owned ordered422/A2 and420/S1 slots retained;type6 aura rejected as offensive action;neither separately granted Blood Boil nor a base melee descriptor is injected.',
            'Real native room/ranged grid/fire-resistance selection admits original NPC422/A2;existing source release/flight produces real NPC-owned damage with no owner mana payment.',
            'Bonus-disabled gathering keeps assigned rank6 under actual pet Battle Command/effective7;the existing execution getter remains live. Repeated selection commits actor RNG;out-of-range disposition runs actual shared native source path/approach before A2 admission.',
            'Original parked source control/birth/wounds/private RNG survives2reloads;complete current Goatman real input/automatic host and Tainted movement regressions retain.'],
        limits=['Root invocation,outer mode admission and action clock delivery are explicit fixture services;complete autonomous Tainted host/timer and learned cast remain unfinished.',
            'Actual world AI entry/preprocess/think scheduling,saved host lifecycle,cap/payment/input and unsupported incoming/passive contexts remain separate.',
            'Blood Boil do160 remains separately unsupported;current constructor/global caller/client parity unverified.',
            'No class certification or final effects/audio/talent/hotkey artwork;installed files remain unchanged.'],inputs=inputs)
    path=ROOT/'tools/reference/tainted-native-decision-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Tainted native decision evidence stale'
    else:path.write_text(encoded)
    print('Tainted native source root:45markers/6realGoatmaninputs/2reloads;ordered422/420,rangedA2/nativeHP/approach,original saved control;222handlers unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
