#!/usr/bin/env python3
"""Authenticate source/native walking,room lists and sub-tile geometry."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/goatman-native-walking'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['copiedData']==last['copiedData']and first['projectInputs']==last['projectInputs']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/GoatmanNativeWalkingChecks.cs'}
    for kind,num in [('content',1),('integration',2)]:
        log=STAGE/f'{kind}-build-{num}.log';pin(log)
        assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/goatman-native-decision-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'goatman-native-walking-check':1,
        'goatman-native-walking-reload-check':2,'goatman-native-decision-check':1,'goatman-native-decision-reload-check':2,
        'goatman-npc-body-action-check':1,'goatman-npc-body-action-reload-check':2})
    e.cohort(STAGE,expected,18);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    saved=pin(STAGE/'goatman-native-walking-fixtures.json');assert saved['Generation']==2 and saved['Path']['Size5c']==2 and saved['Path']['Flags']&1
    assert saved['Maximum']-saved['Life']==257 and saved['Path']['ActorHandle']==saved['Controller']['Handle']
    assert pin(STAGE/'goatman-native-decision-fixtures.json')['Generation']==2 and pin(STAGE/'goatman-npc-body-action-fixtures.json')['Generation']==2
    for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','GoatmanNativeWalkingChecks.cs','Sanctuary.Integration.dll','reason.txt'):pin(STAGE/'diagnostics-1'/name)
    assert 'No independent fixture corridor'in (STAGE/'diagnostics-1/birth-native-checks.log').read_text()
    for name in ('src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/ReviveMeleeGeometry.cs','src/Sanctuary.Content/GoatmanNativeWalking.cs',
        'tests/Sanctuary.Integration/GoatmanNativeWalkingChecks.cs','tests/check_goatman_native_walking.py','tools/record_goatman_native_walking.py',
        'docs/goatman-native-walking-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Goatman source walking/native position-room-geometry transport',
        handlers=221,warlockHandlers=12,completedClasses=[],learnedDemonSummonsEnabled=False,automaticPetAiEnabled=False,
        nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Actual native walking driven by the existing FP16 source motion kernel on a declared route.',
            'Cross-room movement updates source coordinate/client fields,room memberships and update links using existing qualified policies.',
            'Melee geometry uses actual registered source cells,including odd cells hidden by native tile rounding.',
            'Native occupancy,active-path/motion mismatch refusals and no wound/RNG mutation.',
            'Original walked path/room/controller/ownership/wounds/RNG across2reloads;parked geometry has no live room.',
            'Bound decision root and persistent-body action regressions pass.'],
        limits=['Source route/birth-path/mode/velocity/clock/warp and AI-entry/event integration are still separate host requirements.',
            'Fixture paths/flags/routes are explicit acceptance inputs,not production defaults or current D2R constructor proof.',
            'Goatman-only2x2ground transport does not admit Tainted/Defiler movement.',
            'No learned summon/cap/cast route;no complete class certified;final effects/audio/icons remain separate.'],inputs=inputs)
    p=ROOT/'tools/reference/goatman-native-walking-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Walking transport evidence stale'
    else:p.write_text(encoded)
    print('Goatman walking:18markers/2reloads;real movement/room links/subtile geometry/native occupancy;full host remains unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
