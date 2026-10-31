#!/usr/bin/env python3
"""Compose the complete legacy Wall maker/update bodies with declared services.

Reuse the existing poison collision ABI; this is source evidence, not a native
world or current-executable acceptance, and does not change learned skills.
"""
import argparse
import ast
import hashlib
import json
import subprocess
from pathlib import Path

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'bone-wall-maker-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {}

    def source(rel):
        path = BASE / 'd2moo-pinned-source' / rel
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            row['sha'] for row in tree['tree'] if row['path'] == rel)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return raw.decode()

    mode = source('source/D2Game/src/MISSILES/MissMode.cpp')
    step = source('source/D2Common/src/Path/Step.cpp')
    ai = source('source/D2Game/src/AI/AiThink.cpp')
    collision = source('source/D2Common/include/D2Collision.h')
    for name, value in [('WALL', '0x0001'), ('MISSILE_BARRIER', '0x0004'), ('PLAYER', '0x0080'), ('MONSTER', '0x0100')]:
        assert f'COLLIDE_{name} = {value}' in collision
    bodies = {name: function(text, name) for text, name in [
        (mode, 'MISSMODE_SrvDo13_BoneWallMaker'),
        (mode, 'MISSMODE_HandleMissileCollision'),
        (step, 'D2Common_10234'),
        (ai, 'D2GAME_AI_Unk084_6FCDEC70'),
        (ai, 'AITHINK_Fn084_BoneWall')]}
    reviewed = {name: function(text, name) for text, name in [
        (source('source/D2Game/src/MISSILES/Missiles.cpp'), 'MISSILES_CreateMissileFromParams'),
        (mode, 'MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster'),
        (step, 'sub_6FDAD5E0'),
        (source('source/D2Game/src/SKILLS/SkillNec.cpp'), 'SKILLS_SrvDo060_BoneWall')]}

    refs = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((refs / 'sources.json').read_text())['files']
    tables = {}
    for name in ['skills', 'missiles', 'monstats2']:
        path = refs / f'json/{name}.json'
        assert sha(path) == manifest[f'json/{name}.json']['sha256']
        inputs[str(path.relative_to(ROOT))] = sha(path)
        tables[name] = json.loads(path.read_text())
    maker = next(row for row in tables['missiles'].values() if row['Missile'] == 'bonewallmaker')
    species = next(row for row in tables['monstats2'].values() if row['Id'] == 'bonewall')
    assert (maker['*ID'], maker['pSrvDoFunc'], maker['Vel'], maker['MaxVel'], maker['Range'],
            maker['CollideType'], maker['Size'], maker['CanSlow']) == (207, 13, 12, 12, '7 +  (lvl *  2)', 8, 1, 1)
    assert (species['SizeX'], species['SizeY'], species['SpawnUniqueMod']) == (2, 2, 'hidedead')
    assert tables['skills']['78']['Param2'] == tables['skills']['88']['Param2'] == 600
    assert '/*[8]*/{ MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster' in mode

    # The already-used complete collision harness is composed without importing
    # its top-level executable script or maintaining a second copy of its ABI.
    parent = ROOT / 'tools/probe_poison_collision_reference.py'
    inputs[str(parent.relative_to(ROOT))] = sha(parent)
    node = next(row.value for row in ast.parse(parent.read_text()).body
                if isinstance(row, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'cpp' for t in row.targets))

    def compose(node):
        if isinstance(node, ast.Constant) and isinstance(node.value, str):
            return node.value
        if isinstance(node, ast.Name) and node.id == 'body':
            return bodies['MISSMODE_HandleMissileCollision']
        if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Add):
            return compose(node.left) + compose(node.right)
        raise ValueError('Unsupported shared collision ABI expression')

    cpp = compose(node).split('int main(){', 1)[0]

    def replace(old, new):
        nonlocal cpp
        assert cpp.count(old) == 1, old
        cpp = cpp.replace(old, new)

    replace('#define __fastcall', '#include <cassert>\n#include <string>\n#define __stdcall\n#define __fastcall')
    replace('struct D2GameStrc{};', 'struct D2GameStrc{int dwGameFrame=1000;};')
    replace('int velocity=0,flags=64;', 'int velocity=0,flags=64,dwFlags=0;')
    replace('struct D2UnitStrc{', 'struct D2UnitStrc{int dwUnitId=42,budget=4,skill=78,rank=1,x=100,y=200;')
    replace('Route stru_6FD2E5F8[4]={{nullptr,0},{nullptr,4},{nullptr,0},{findCallback,68}};',
            'Route stru_6FD2E5F8[9]={{nullptr,0},{nullptr,4},{nullptr,0},{findCallback,68},'
            '{nullptr,0},{nullptr,0},{nullptr,0},{nullptr,0},{findCallback,389}};')
    replace('D2ActiveRoomStrc room;', 'std::string events;int ownerPhase=3,ownerCalls=0;\nD2ActiveRoomStrc room;')
    replace('moves++;return moveResult;', 'events+="M";moves++;return moveResult;')
    replace('frameWrites++;p->frame=n;', 'events+="F";frameWrites++;p->frame=n;')
    replace('return &owner;', 'events+="O";int bit=ownerCalls++==0?1:2;return (ownerPhase&bit)?&owner:nullptr;')
    replace('finds++;return hasTarget?', 'events+="C";finds++;return hasTarget?')
    replace('hits++;lastEnd=end;', 'events+="H";hits++;lastEnd=end;')
    replace('int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return mask;}',
            'int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int filter){assert(filter==389);return mask;}')
    cpp += r'''
using BOOL=int;constexpr int FALSE=0,PATH_UNKNOWN_FLAG_0x00008=8,UNIT_MONSTER=1,AISPECIALSTATE_NONE=0,MONMODE_DEATH=0;
struct D2SkillsTxt{int nPetType=3,dwParam[2]={0,600};} skillRow;
struct DataTables{int nPetTypeTxtRecordCount=20;} tables;auto*sgptDataTables=&tables;
struct D2SummonArgStrc{int nAiSpecialState=0,nMonMode=0,dwFlags=0,nHcIdx=0,nPetType=0,nPetMax=0;
 D2UnitStrc*pOwner=nullptr;struct{int nX=0,nY=0;}pPosition;};
D2UnitStrc root,pet;bool skillPresent=true,summonValid=true,rootPresent=true,allocation=true;
int allocations=0,links=0,budgetWrites=0,petType=3;
int MISSILE_GetTargetY(D2UnitStrc*p){return p->budget;}int MISSILE_GetTargetX(D2UnitStrc*){return 42;}
int MISSILE_GetSkill(D2UnitStrc*p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc*p){return p->rank;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){events+="K";assert(id==78);skillRow.nPetType=petType;return skillPresent?&skillRow:nullptr;}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*p,int zero,int id,int rank,int*mode,int a,int b)
 {events+="I";assert(p==&owner&&id==78&&rank==1&&!zero&&!a&&!b);*mode=8;return summonValid?344:-1;}
D2UnitStrc*SUNIT_GetServerUnit(D2GameStrc*,int type,int id){events+="R";assert(type==1&&id==42);return rootPresent?&root:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc*p){return p->y;}
D2UnitStrc*D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc*s)
 {events+="A";allocations++;assert(s->pOwner==&owner&&s->nHcIdx==344&&s->dwFlags==13&&s->nMonMode==8
 &&s->nAiSpecialState==0&&s->pPosition.nX==100&&s->pPosition.nY==200&&s->nPetMax==0
 &&s->nPetType==(petType>=20?0:petType));return allocation?&pet:nullptr;}
void AIGENERAL_SetOwnerData(D2GameStrc*,D2UnitStrc*p,int id,int type,int a,int b)
 {events+="D";assert(p==&pet&&id==42&&type==1&&!a&&!b);}
void AIGENERAL_AllocMinionList(D2GameStrc*,D2UnitStrc*r,D2UnitStrc*p){events+="L";links++;assert(r==&root&&p==&pet);}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*p,int mod,int a){events+="U";assert(p==&pet&&mod==15&&!a);}
void D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc*o,D2UnitStrc*p,int id,int rank,int a)
 {events+="P";assert(o==&owner&&p==&pet&&id==78&&rank==1&&!a);}
void UNITS_StoreOwner(D2UnitStrc*p,D2UnitStrc*o){events+="S";assert(p==&pet&&o==&owner);}
void sub_6FC40280(D2GameStrc*,D2UnitStrc*p,int a,int node){events+="N";assert(p==&pet&&!a&&node==9);}
void MISSILE_SetTargetY(D2UnitStrc*p,int n){events+="B";budgetWrites++;p->budget=n;}
struct D2MonStatsTxt{int nSkill[1]={78};} monstats;
struct D2AiControlStrc{int dwAiParam[3]={0,0,0};} control;
struct D2AiTickParamStrc{D2MonStatsTxt*pMonstatsTxt=&monstats;D2AiControlStrc*pAiControl=&control;};
int deaths=0,idles=0;
void AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc*,D2UnitStrc*,int mode,int x,int y){assert(!mode&&!x&&!y);deaths++;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){assert(frames==15);idles++;}
'''
    cpp += '\n'.join(bodies[name] for name in ['D2Common_10234', 'MISSMODE_SrvDo13_BoneWallMaker',
                                               'D2GAME_AI_Unk084_6FCDEC70', 'AITHINK_Fn084_BoneWall'])
    cpp += r'''
void reset(D2UnitStrc&m,D2DynamicPathStrc&p,int phase,int budget,int changed,int sk,int summon,int rt,int alloc,int end){
 m={};m.pDynamicPath=&p;m.dwMissileMode=8;m.budget=budget;m.frame=end==1?1:9;m.activation=100;
 p={};p.dwFlags=changed;p.velocity=12;p.flags=64;ownerPhase=phase;ownerCalls=0;skillPresent=sk;summonValid=summon;
 rootPresent=rt;allocation=alloc;hasTarget=end==4;mask=end==3?4:end>=4?256:0;moveResult=end==2?2:1;
 events.clear();moves=finds=hits=frameWrites=allocations=links=budgetWrites=0;lastEnd=-1;
}
int main(){D2GameStrc game;D2DynamicPathStrc path;D2UnitStrc missile;bool first=true;printf("{\"maker\":[");
for(int phase:{0,1,3})for(int budget:{0,1,4})for(int changed:{0,8,72})for(int sk:{0,1})for(int summon:{0,1})
for(int rt:{0,1})for(int alloc:{0,1})for(int end=0;end<6;end++){
 reset(missile,path,phase,budget,changed,sk,summon,rt,alloc,end);petType=(end%2)?20:3;
 int result=MISSMODE_SrvDo13_BoneWallMaker(&game,&missile);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%s\"]",first?"":",",phase,budget,changed,sk,summon,rt,alloc,end,
 result,missile.budget,missile.frame,allocations,links,budgetWrites,moves,finds,hits,events.c_str());first=false;
}
printf("],\"sequence\":[");first=true;
// A blocked allocation does not exhaust the success budget or stop movement.
reset(missile,path,3,2,8,1,1,1,0,0);for(int alloc:{0,0,1,0,1,1}){
 allocation=alloc;ownerCalls=0;events.clear();int result=MISSMODE_SrvDo13_BoneWallMaker(&game,&missile);
 printf("%s[%d,%d,%d,%d,\"%s\"]",first?"":",",alloc,result,missile.budget,missile.frame,events.c_str());first=false;
}
printf("],\"ai\":[");first=true;D2AiTickParamStrc tick;
for(int hasMon:{0,1})for(int hasSkill:{0,1})for(int prior:{0,77})for(int frame:{0,1000})for(int delta:{-1,0,1,14,15,16}){
 tick.pMonstatsTxt=hasMon?&monstats:nullptr;skillPresent=hasSkill;control.dwAiParam[0]=prior;game.dwGameFrame=frame;
 D2GAME_AI_Unk084_6FCDEC70(&game,&missile,&tick);int deadline=control.dwAiParam[0];game.dwGameFrame=deadline+delta;
 deaths=idles=0;AITHINK_Fn084_BoneWall(&game,&missile,&tick);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",hasMon,hasSkill,prior,frame,delta,deadline,deaths,idles);first=false;
}printf("]}\n");}
'''
    path = STAGE / 'probe.cpp'
    path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                        '-fno-sanitize-recover=all', str(path), '-o', str(path.with_suffix(''))],
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(path.with_suffix(''))], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    data = json.loads(run.stdout)
    assert len(data['maker']) == 2592 and len(data['ai']) == 96
    for phase, budget, changed, sk, summon, rt, alloc, end, result, out_budget, frame, allocations, links, writes, moves, finds, hits, events in data['maker']:
        prefix = 'O'
        invalid = 2 if phase == 0 or budget == 0 else None
        attempted = False
        if invalid is None and changed & 8:
            prefix += 'O'
            if phase != 3:
                invalid = 2
            else:
                prefix += 'K'
                if not sk:
                    invalid = 2
                else:
                    prefix += 'I'
                    if not summon:
                        invalid = 0
                    else:
                        prefix += 'R'
                        if rt:
                            prefix += 'A'
                            attempted = True
                            if alloc:
                                prefix += 'DLUPSNB'
        success = attempted and bool(alloc)
        assert (allocations, links, writes, out_budget) == (int(attempted), int(success), int(success), budget-int(success))
        if invalid is not None:
            assert (result, frame, moves, finds, hits, events) == (invalid, 1 if end == 1 else 9, 0, 0, 0, prefix)
        else:
            suffix = 'MH' if end == 2 else 'MFH' if end == 1 else 'MFOCH' if end in [3, 4] else 'MFOC' if end == 5 else 'MFO'
            assert events == prefix + suffix
            assert (result, frame, moves, finds, hits) == (
                7 if end in [1, 2, 4] else 2 if end == 3 else 1,
                9 if end == 2 else 0 if end == 1 else 8, 1, int(end >= 3), int(end in [1, 2, 3, 4]))
    assert [row[:4] for row in data['sequence']] == [[0,1,2,8],[0,1,2,7],[1,1,1,6],[0,1,1,5],[1,1,0,4],[1,2,0,4]]
    for has_mon, has_skill, prior, frame, delta, deadline, deaths, idles in data['ai']:
        assert deadline == ((600 if has_skill else prior)+frame if has_mon else prior)
        assert (deaths, idles) == (int(delta > 0), int(delta <= 0))
    (STAGE / 'cases.json').write_text(json.dumps(data, separators=(',', ':')) + '\n')
    receipt = dict(
        referenceVersion='3.3.93847', legacyCommit=tree['sha'], scope='source-review', runtimeChanged=False,
        inputs=inputs, compiledRoutines={name: hashlib.sha256(body.encode()).hexdigest() for name, body in bodies.items()},
        staticallyReviewed={name: hashlib.sha256(body.encode()).hexdigest() for name, body in reviewed.items()},
        cases=dict(maker=2592, blockedThenSuccessfulSequence=6, ai=96),
        reuse=dict(collisionAbi=str(parent.relative_to(ROOT)), update='src/Sanctuary.Content/BasicMissileUpdate.cs',
                   vectors='src/Sanctuary.Content/MissileVectors.cs', steps='src/Sanctuary.Content/SourceMissileSteps.cs',
                   frames='src/Sanctuary.Content/MissileValues.cs', birth='src/Sanctuary.Content/BoneBarrierBirth.cs'),
        established=[
            'Complete unchanged maker13, path-change flag getter and common collision bodies execute together. Owner/budget reject before collision. No changed path skips allocation but still advances the common update. Missing skill returns2; invalid summon ID returns0.',
            'A found root permits one exact flags13 allocation at the current missile integer position. Only successful creation links root/minion, modifier15, summon passives, original caster and node9, then decrements budget. Failed allocation and missing root keep the budget and still update movement/lifetime/collision. No arm-stop instruction occurs on failed allocation.',
            'Maker observes the previous path-change flag and position before common movement and remaining-frame decrement. Thus an allocation can precede a terminal movement/expiry callback; final frame is not a generic spawn-then-stop-at-fixed-length loop.',
            'Mode8 dispatches the existing player-or-monster finder with monster/player/missile-barrier/wall mask. Its full eligibility body is statically reviewed, not executed by these declared-finder cases. Root/species footprint is2x2 source subtiles; one source subtile must not be treated as a whole native tile.',
            'Both complete AI bodies execute with declared skill/time services. Birth stores current frame plus skill0 Param2=600. At think, equality idles15; only deadline<current frame requests DEATH mode. These bodies do not establish first-think scheduling or DEATH duration/deletion.'],
        services=[
            'Reused poison collision ABI declares path movement, room, one saved step, collision mask, candidate finder and ending hit result7. Maker adds declared owner/root/skill/allocation/link/passive/registration services. Ordered service traces and exact summon arguments are checked; this does not execute their internals.',
            'Current tables constrain velocity12, max12, CanSlow1, mode8, size1 and Range=7+2*lvl. Factory75percent/slow ordering and path-cell flag setters are statically reviewed only. No source world/path/event or current-executable run is implied.'],
        remaining=[
            'Native BoneBarriers still uses a normalized whole-tile counter and exhausts an arm after one failed placement. It lacks independent maker lifetime, prior-cell/path flag and mode8 contact state. Fix through shared missile services; do not certify the old geometry or change its footprint based on scale alone.',
            'Exact2x2 collision/room search, source factory/path/slow integration, per-segment birth deadlines, root/minion death/event phase, Prison raster connectors, current executable/client and actual old-save upgrade remain open. Existing native direct24second deletion is an adapter, not the strict AI deadline/death sequence.',
            'No runtime C#/data/serialization changes, native builds/tests, class completion, deployment or asset creation in this source-only increment. Prior native acceptance remains separately bounded.'],
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [Path(__file__).resolve(), path, STAGE/'cases.json', STAGE/'compiler.log', STAGE/'sanitizer.log']})
    out = ROOT / 'tools/reference/bone-wall-maker-source-evidence.json'
    encoded = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Bone Wall maker evidence is stale'
    else:
        out.write_text(encoded)
    print('Bone Wall maker:2592 composed callbacks +6 blocked/success frames +96 AI contexts; clean ASan/UBSan; native growth remains open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
