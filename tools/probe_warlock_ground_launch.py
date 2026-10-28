#!/usr/bin/env python3
"""Compose shared Abyss/Apocalypse launch and hit23's explicit dispatch boundary."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'warlock-ground-launch-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    treepath = BASE / 'd2moo-source-tree.json'; tree = json.loads(treepath.read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(treepath.relative_to(ROOT)): sha(treepath)}; routines = {}

    def source(name):
        p = BASE / 'd2moo-pinned-source' / name; raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        inputs[str(p.relative_to(ROOT))] = sha(p)
        return raw.decode()

    inherited = BASE / 'volcano-source-probe.cpp'; inputs[str(inherited.relative_to(ROOT))] = sha(inherited)
    priorpath = ROOT / 'tools/reference/volcano-engine-evidence.json'; prior = json.loads(priorpath.read_text()); inputs[str(priorpath.relative_to(ROOT))] = sha(priorpath)
    cpp = inherited.read_text().split('int main()', 1)[0]
    for name, digest in prior['compiledUnchangedRoutines'].items():
        body = function(cpp, name)
        if name == 'SEED_RollRandomNumber':
            original = function(source('source/D2Common/include/D2Seed.h'), name)
            assert hashlib.sha256(original.encode()).hexdigest() == digest and body == original.replace('0x6AC690C5i64', '0x6AC690C5ULL')
        else:
            assert hashlib.sha256(body.encode()).hexdigest() == digest, name
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
    skills = source('source/D2Game/src/SKILLS/Skills.cpp'); sor = source('source/D2Game/src/SKILLS/SkillSor.cpp'); miss = source('source/D2Game/src/MISSILES/MissMode.cpp')
    assert re.search(r'SKILLS_SrvDo123_Volcano', source('source/D2Game/src/SKILLS/SkillDruid.cpp'))
    def replace(a, b):
        nonlocal cpp
        assert cpp.count(a) == 1, a[:100]
        cpp = cpp.replace(a, b, 1)
    replace('struct D2SkillsTxt{', 'struct D2SkillsTxt{int wSrvMissileA=709;')
    replace('struct D2MissilesTxt{', 'struct D2MissilesTxt{int dwHitPar[1]={163};')
    replace('std::vector<D2MissileStrc> requests;', 'std::vector<D2MissileStrc> requests;std::vector<uint64_t> allocationSeeds;int activeSkill=402,activeRank=20,nativeDistance=99,distanceCalls=0,roomLookups=0,townLookups=0,dispatches=0,targetSets=0,dispatchIndex=0,dispatchSkill=0,dispatchRank=0;bool destinationRoom=true,inTown=false;struct Tables{int nMissilesTxtRecordCount=1000;}tables;Tables*sgptDataTables=&tables;')
    replace('assert(id==244);return hasSkill?', 'assert(id==activeSkill);return hasSkill?')
    replace('assert(id==244);return progressive;', 'assert(id==activeSkill);return progressive;')
    replace('requests.push_back(*p);return allocate?', 'requests.push_back(*p);allocationSeeds.push_back(owner.pSeed.lSeed);return allocate?')
    replace('assert(p==&owner&&zero==0&&id==244&&level==20', 'assert(p==&owner&&zero==0&&id==activeSkill&&level==activeRank')
    cpp += r'''
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*r,int,int){assert(r==&room);roomLookups++;return destinationRoom?&room:nullptr;}
int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*r){assert(r==&room);townLookups++;return inTown;}
int UNITS_GetDistanceToCoordinates(D2UnitStrc*p,int,int){assert(p==&owner);distanceCalls++;return nativeDistance;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*p,D2UnitStrc*u){assert(p==&actor&&u==&target);targetSets++;}
void sub_6FD14400(D2GameStrc*,D2UnitStrc*p,int callback,int id,int rank){assert(p==&actor);dispatches++;dispatchIndex=callback;dispatchSkill=id;dispatchRank=rank;}
'''
    for raw, name in [(sor, 'SKILLS_SrvSt14_Hydra'), (skills, 'D2GAME_CreateMissile_6FD115E0'), (sor, 'SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult'), (miss, 'MISSMODE_SrvHit23_Unused')]:
        body = function(raw, name); cpp += body + '\n'; routines[name] = hashlib.sha256(body.encode()).hexdigest()
    cpp += r'''
void Clear(){reset();allocationSeeds.clear();distanceCalls=roomLookups=townLookups=dispatches=targetSets=dispatchIndex=dispatchSkill=dispatchRank=0;destinationRoom=true;inTown=false;}
int main(){bool first=true;printf("{\"start\":[");
for(int roomFlag:{0,1})for(int select:{0,1})for(int x:{0,210})for(int y:{0,220})for(int dest:{0,1})for(int town:{0,1}){
Clear();hasRoom=roomFlag;selected=select;path.x=x+1;target.x=x;path.y=y+1;target.y=y;destinationRoom=dest;inTown=town;
int result=SKILLS_SrvSt14_Hydra(&game,&owner,402,20);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",roomFlag,select,x,y,dest,town,result,roomLookups,townLookups);first=false;}
printf("],\"launch\":[");first=true;
for(int family:{0,1})for(int rank:{1,20,255})for(int guard=0;guard<8;guard++)for(int select:{0,1})for(int x:{0,210})for(int y:{0,220})for(int distance:{99,100,101})for(int alloc:{0,1}){
Clear();activeSkill=family?401:402;activeRank=rank;hasSkill=guard!=1;hasRoom=guard!=3;selected=select;path.x=x+1;target.x=x;path.y=y+1;target.y=y;nativeDistance=distance;allocate=alloc;row.nSize=family?1:3;
skill.wSrvMissileA=guard==2?1000:guard==7?0:709;progressive=guard==2?0:guard==7?-1:722;bits=guard==4?1:guard==5?4:guard==6?0x40:0;
int result=family?SKILLS_SrvDo123_Volcano(&game,&owner,401,rank):SKILLS_SrvDo028_Meteor_Blizzard_Eruption_BaalTaunt_Catapult(&game,&owner,402,rank);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%llu,%u,%d,%d],",first?"":",",family,rank,guard,select,x,y,distance,alloc,result,owner.dwFlags,checks,cx,cy,size,mask,(unsigned long long)owner.pSeed.lSeed,created.targetX,notifications,distanceCalls);first=false;printRequests();printf(",[");for(size_t j=0;j<allocationSeeds.size();j++)printf("%s%llu",j?",":"",(unsigned long long)allocationSeeds[j]);printf("]]");}
printf("],\"hit23\":[");first=true;
for(int present:{0,1})for(int hasrow:{0,1})for(int own:{0,1})for(int id:{-1,0,401})for(int rank:{0,1,255})for(int victim:{0,1}){
Clear();hasRow=hasrow;hasOwner=own;actor.dwClassId=722;actor.skill=id;actor.rank=rank;
int result=MISSMODE_SrvHit23_Unused(&game,present?&actor:nullptr,victim?&target:nullptr);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",present,hasrow,own,id,rank,victim,result,dispatches,targetSets,dispatchIndex,dispatchSkill,dispatchRank);first=false;}
printf("]}\n");}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    path = STAGE / 'source.cpp'; path.write_text(cpp); compiler = STAGE / 'compiler.log'; sanitizer = STAGE / 'sanitizer.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True); sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    data = json.loads(run.stdout); counts = {k: len(v) for k, v in data.items()}; assert counts == dict(start=64, launch=2304, hit23=144)
    directory = ROOT / 'tools/reference/d2r-named-equipment'; manifest = json.loads((directory / 'sources.json').read_text()); tables2 = {}
    for name in ['skills', 'missiles']:
        p = directory / 'json' / (name + '.json'); assert sha(p) == manifest['files']['json/' + name + '.json']['sha256']; inputs[str(p.relative_to(ROOT))] = sha(p); tables2[name] = json.loads(p.read_text())
    localpath = ROOT / 'tools/reference/d2r-local-missile-data-evidence.json'; local = json.loads(localpath.read_text())
    foundationpath = ROOT / 'tools/reference/warlock-foundation-preflight.json'; foundation = json.loads(foundationpath.read_text())
    assert local['referenceVersion'] == foundation['referenceVersion'] == '3.3.93847'
    assert local['comparison']['unexplainedDifferences'] == 0 and foundation['comparedPlayerFields'] == 1709
    assert local['cached']['sha256'] == inputs[str((directory / 'json/missiles.json').relative_to(ROOT))]
    assert foundation['pinnedInputHashes']['skills'] == inputs[str((directory / 'json/skills.json').relative_to(ROOT))]
    ownedmissiles = ROOT / local['local']['path']; ownedskills = ROOT / '.local/research/d2r-3.3.93847/skills.txt'
    assert sha(ownedmissiles) == local['local']['sha256'] and sha(ownedskills) == foundation['licensedSkillSha256']
    for p in [localpath, foundationpath, ownedmissiles, ownedskills]:
        inputs[str(p.relative_to(ROOT))] = sha(p)
    profiles = []
    for id, ability in [(402, 241), (401, 240)]:
        s = tables2['skills'][str(id)]; m = tables2['missiles'][s['srvmissilea']]
        assert m['Range'] in ['par1', 'par2']
        profiles.append(dict(Ability=ability, SkillId=id, StartFunction=s.get('srvstfunc', 0), DoFunction=s['srvdofunc'], Missile=m['*ID'], Size=m['Size'], MissileDo=m['pSrvDoFunc'], MissileHit=m['pSrvHitFunc'], HitDispatch=m.get('sHitPar1', 0), Lifetime=m['Param' + m['Range'][3:]], RadiusFormula=m['Radius']))
    assert [p['MissileDo'] for p in profiles] == [39, 1] and profiles[1]['HitDispatch'] == 163
    dispatch = function(skills, 'sub_6FD14400'); array = re.search(r'gpSkillSrvDoFnTable_6FD40A20\[\]\s*=\s*\{(.*?)\};', dispatch, re.S).group(1)
    slots = [v.strip() for v in array.split(',') if v.strip()]; assert slots[163] == 'nullptr'
    data['profiles'] = profiles; out = BASE / 'warlock-ground-launch-source-probe.json'; encoded = json.dumps(data, separators=(',', ':')) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    profilepath = ROOT / 'data/Sanctuary/Balance/warlock-ground-launch.json'; encoded = json.dumps(profiles, indent=2) + '\n'
    if check:
        assert profilepath.read_text() == encoded
    else:
        profilepath.write_text(encoded)
    inputs[str(Path(__file__).relative_to(ROOT))] = sha(Path(__file__))
    receipt = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Abyss/Apocalypse shared ground launch and hit23 dispatch boundary', handlerEnabled=False, currentExecutableParity=False, nativeConsumerAccepted=False,
        inputs=inputs, compiledUnchangedRoutineHashes=routines, verified=dict(caseCounts=counts, profiles=profiles, legacySkill163IsNull=True), artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, compiler, sanitizer, out, profilepath]},
        established=['Complete source start14/do28/create helper and reused do123/coordinate/placement/RNG routines retain distinct flags and allocation ordering. Abyss sets the do flag before placement and uses the generic100distance check;Apocalypse sets it after placement and consumes one caster RNG draw before allocation,including failed allocation. Requested missile seeds and client notification are retained only on successful allocation.',
            'Complete hit23 requests skill do163 with the retained original skill/rank after optional target assignment;it does not implement do163. The older dispatcher slot163is null. Current Abysscenter/abyssexplode use new missile do39;those bodies are also unavailable here.'],
        remaining=['Actual current do39pull/pulse/explosion and do163area/resistance/roll/filter ordering remain unknown. No learned cast or damage substitute is enabled. Factory/stat/room/geometry/network services and modern outer casting/payment clocks remain explicit. Raw Abyss Radius uses sksrc and is retained without assuming supplier semantics. Source damage/ray/owner lifecycle and final effects/audio/icons remain separate.'])
    p = ROOT / 'tools/reference/warlock-ground-launch-source-evidence.json'; encoded = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert p.read_text() == encoded
    else:
        p.write_text(encoded)
    print('Warlock ground launch:64start/2304launch/144hit23contexts;Abyss do39and Apocalypse do163remain explicit blockers.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
