#!/usr/bin/env python3
"""Compose complete item executor, missile helper and alternate spell do bodies.

The final missile allocator remains a traced service. This distinguishes the
outgoing item routes from ordinary casts without claiming current binary parity.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'npc-item-spell-origins-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def probe(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    bodies, sources = {}, {}
    groups = {
        'Skills.cpp': ['sub_6FD11420', 'sub_6FD14170', 'D2GAME_SKILLS_Handler_6FD12BA0'],
        'SkillSor.cpp': ['SKILLS_SrvDo022_NovaAttack', 'SKILLS_SrvDo026_ChainLightning', 'SKILLS_SrvDo151_Unused'],
        'SkillAss.cpp': ['SKILLS_SrvDo036_ClawsOfThunder_ProgressiveFn2'],
    }
    for filename, names in groups.items():
        relative = 'source/D2Game/src/SKILLS/' + filename
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            r['sha'] for r in tree['tree'] if r['path'] == relative)
        sources[relative] = sha(path)
        for name in names:
            bodies[name] = function(raw.decode(), name)

    refs = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((refs / 'sources.json').read_text())
    tables, pins = {}, {}
    for name in ('skills', 'missiles'):
        path = refs / f'json/{name}.json'
        assert sha(path) == manifest['files'][f'json/{name}.json']['sha256']
        pins[str(path.relative_to(ROOT))] = sha(path)
        tables[name] = json.loads(path.read_text())
    selected = {sid: tables['skills'][str(sid)] for sid in (36, 44, 48, 53)}
    for sid, row in selected.items():
        assert row.get('srvdofunc', 0) == (0 if sid == 36 else 26 if sid == 53 else 22)
        assert row['ItemEffect'] == (1 if sid == 36 else 151 if sid == 53 else 36)
        assert not any(row.get(k) for k in ('ItemTarget', 'ItemTgtDo', 'ItemCheckStart', 'srvstfunc', 'UseManaOnDo', 'lob', 'decquant', 'delay'))

    cpp = r'''
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_SKSRVDOFUNC=64,PLRMODE_TOWNNEUTRAL=5,PLRMODE_SEQUENCE=18;
constexpr int SKILLSFLAGINDEX_INTOWN=8,SKILLSFLAGINDEX_LOB=1,SKILLSFLAGINDEX_DECQUANT=0,SKILLSFLAGINDEX2_USEMANAONDO=5,STAT_TOHIT=19;
uint32_t gdwBitMasks[32];
struct D2GameStrc{} game;
struct D2ActiveRoomStrc{};
struct D2SkillStrc{};
struct D2DynamicPathStrc{int x=420,y=710;};
struct D2UnitStrc{int dwUnitType=1,dwUnitId=231,x=380,y=680,budget=0;uint32_t dwFlags=0x8000,dwSeqFrame=0;D2DynamicPathStrc*pDynamicPath=nullptr;}actor,target,child;
struct D2SkillsTxt{uint16_t wSrvDoFunc=0,wItemEffect=1;int16_t wSrvMissile=-1,wSrvStartFunc=0,wSrvMissileA=-1;uint8_t nAnim=10;uint32_t dwDelay=0,dwFlags[2]={},dwCalc[1]={1};}record;
struct Tables{int nMissilesTxtRecordCount=742;int pMissilesTxt[742]={};}tables;auto*sgptDataTables=&tables;
struct D2MissileStrc{uint32_t dwFlags=0;D2UnitStrc*pOwner=nullptr;int nX=0,nY=0,nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0,nVelocity=0,nAttBonus=0;};
std::vector<D2MissileStrc> emitted;
int currentSkill=0,targetMode=0,failure=-1,attackRating=0,evals=0;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int sid){assert(sid==currentSkill);return &record;}
D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){return nullptr;}
uint32_t SKILLS_GetFlags(D2SkillStrc*){__builtin_trap();}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*){__builtin_trap();}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){__builtin_trap();}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){__builtin_trap();}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){__builtin_trap();}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){__builtin_trap();}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){__builtin_trap();}
D2SkillStrc*SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){__builtin_trap();}
int sub_6FD10CE0(D2UnitStrc*){__builtin_trap();}
using SkillDoFunc=int(*)(D2GameStrc*,D2UnitStrc*,int,int);
std::array<SkillDoFunc,191>gpSkillSrvDoFnTable_6FD40A20{};
std::array<int(*)(),128>gpSkillSrvStartFnTable_6FD408B0{};
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*u){assert(u==&actor);return targetMode>=1&&targetMode<=4?&target:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc*u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc*u){return u->y;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*p){return p->x;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*p){return p->y;}
int STATLIST_UnitGetStatValue(D2UnitStrc*u,int stat,int layer){assert(u==&actor&&stat==19&&layer==0);return attackRating;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){__builtin_trap();}
D2UnitStrc*MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){assert(p->pOwner==&actor);int ordinal=emitted.size();emitted.push_back(*p);return ordinal==failure?nullptr:&child;}
D2UnitStrc*sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){__builtin_trap();}
void D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int){__builtin_trap();}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*u,int formula,int sid,int rank){assert(u==&actor&&sid==currentSkill&&formula==1);evals++;return sid==53?(rank+25)/5:0;}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){__builtin_trap();}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*u,int sid){assert(u==&actor&&sid==currentSkill);return record.wSrvMissileA;}
int DATATBLS_GetMissileVelocityFromMissilesTxt(int,int){return 24;}
void MISSILE_SetTargetX(D2UnitStrc*u,int count){assert(u==&child);u->budget=count;}
'''
    cpp += '\n'.join(bodies.values())
    cpp += '\nvoid setup(int sid){record={};switch(sid){\n'
    for sid, row in selected.items():
        direct = tables['missiles'][row['srvmissile']]['*ID'] if row.get('srvmissile') else -1
        alternate = tables['missiles'][row['srvmissilea']]['*ID'] if row.get('srvmissilea') else -1
        cpp += f'case {sid}:record.wSrvDoFunc={row.get("srvdofunc", 0)};record.wItemEffect={row["ItemEffect"]};record.wSrvMissile={direct};record.wSrvMissileA={alternate};break;\n'
    cpp += r'''default:__builtin_trap();}}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
gpSkillSrvDoFnTable_6FD40A20[22]=SKILLS_SrvDo022_NovaAttack;
gpSkillSrvDoFnTable_6FD40A20[26]=SKILLS_SrvDo026_ChainLightning;
gpSkillSrvDoFnTable_6FD40A20[36]=SKILLS_SrvDo036_ClawsOfThunder_ProgressiveFn2;
gpSkillSrvDoFnTable_6FD40A20[151]=SKILLS_SrvDo151_Unused;
bool comma=false;printf("[");
for(int sid:{36,44,48,53})for(int rank=1;rank<=63;rank++)for(int outgoing:{0,1})for(int mode=0;mode<7;mode++)for(int fail:{-1,0})for(int ar:{0,77}){
currentSkill=sid;targetMode=mode;failure=fail;attackRating=ar;setup(sid);actor={};target={};child={};D2DynamicPathStrc path;actor.pDynamicPath=&path;target.dwUnitId=500;target.x=400;target.y=700;
if(mode==2)target.x=0;if(mode==3)target.y=0;if(mode==4){target.x=actor.x;target.y=actor.y;}if(mode==5)path.x=0;if(mode==6)path.y=0;
evals=0;emitted.clear();int result=D2GAME_SKILLS_Handler_6FD12BA0(&game,&actor,sid,rank,0,1,outgoing);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%u,%d,%d],[",comma?",":"",sid,rank,outgoing,mode,fail,ar,result,actor.dwFlags,evals,child.budget);comma=true;
for(size_t i=0;i<emitted.size();i++){auto&p=emitted[i];printf("%s[%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.pOwner->dwUnitId,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nVelocity,p.nSkill,p.nSkillLevel,p.nMissile,p.nAttBonus);}printf("]]");}
printf("]\n");}
'''
    for name, body in bodies.items():
        assert function(cpp, name) == body
    generated, binary = STAGE / 'probe.cpp', STAGE / 'probe'
    generated.write_text(cpp)
    run = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', '-fno-pie', '-no-pie', str(generated), '-o', str(binary)], capture_output=True)
    compiler = STAGE / 'compiler.log'
    compiler.write_bytes(run.stdout + run.stderr)
    assert run.returncode == 0, run.stderr.decode()
    run = subprocess.run([str(binary)], capture_output=True)
    sanitizer = STAGE / 'sanitizer.log'
    sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()
    cases = json.loads(run.stdout)
    assert len(cases) == 14112
    qx = [30,29,29,28,27,26,24,23,21,19,16,14,11,8,5,2]
    qy = [0,2,5,8,11,14,16,19,21,23,24,26,27,28,29,29]
    directions = list(zip(qx,qy)) + list(zip([-y for y in qy],qx)) + list(zip([-x for x in qx],[-y for y in qy])) + list(zip(qy,[-x for x in qx]))
    for args, state, actual in cases:
        sid, rank, outgoing, mode, failure, ar = args
        has_target = 1 <= mode <= 4
        tx, ty = (0 if mode == 2 else 380 if mode == 4 else 400), (0 if mode == 3 else 680 if mode == 4 else 700)
        px, py = (0 if mode == 5 else 420), (0 if mode == 6 else 710)
        aim = (tx,ty) if has_target else (px,py)
        result, flags, evals, budget, expected = 1, 0x8040, 1, 0, []
        if sid in (44,48):
            if outgoing and not has_target:
                result, flags, evals = 0, 0x8000, 0
            else:
                x,y = (tx,ty) if outgoing else (380,680)
                expected = [[7,231,x,y,dx,dy,24,sid,rank,tables['missiles'][selected[sid]['srvmissilea']]['*ID'],0] for dx,dy in directions]
                # Do36 does not set server-do; the item wrapper restores it.
                if outgoing:
                    flags = 0x8000
        else:
            x,y = (aim if sid == 36 and outgoing and all(aim) else (380,680))
            if sid == 53 and outgoing and has_target:
                x,y = tx,ty
            destination = (2*aim[0]-380,2*aim[1]-680) if sid == 36 and outgoing and all(aim) else aim
            if all(destination):
                expected = [[33 | (4096 if ar else 0),231,x,y,*destination,0,sid,rank,58 if sid==36 else 93,ar]]
            if sid == 36:
                evals = 0
            else:
                result = int(bool(expected) and failure != 0)
                budget = (rank+25)//5 if result else 0
        assert actual == expected and state == [result,flags,evals,budget], (args,state,[result,flags,evals,budget],actual[:1],expected[:1])
    fixture = STAGE / 'cases.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    receipt = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], currentExecutableParity=False,
        cases=len(cases), completeRoutines=len(bodies), sourceHashes=sources,
        routineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()}, currentTable=pins,
        inputs={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(), refs/'sources.json')},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in (generated, compiler, sanitizer, fixture)},
        established=[
            'Seven complete older bodies compose common item dispatch, missile coordinate helper, ordinary do22/do26, alternate item do36/do151 and all64 ring emissions. Four pinned current rows/all nonzero six-bit ranks, both outgoing flags, target/path/zero/same-position edges, allocation failure and own NPC attack-rating flags are independently checked.',
            'Outgoing Fire Bolt starts at a nonzero target/path point and aims at twice that point minus caster position. Incoming Fire Bolt starts at the caster. A direct missile route returns success even if its allocator returns null.',
            'Outgoing Nova/Frost Nova do36 requires a target and centers all64 missiles on that target, retaining the NPC owner. It does not set server-do. Incoming do22 centers on the caster and sets server-do. Ring allocation failure does not stop later allocation attempts or change successful do return.',
            'Outgoing Chain do151 starts at its actual target position and supplies that same position as destination; without a target it starts at the caster and uses the path point. Incoming do26 starts at the caster. Failed root allocation returns zero; successful roots retain floor((rank+25)/5) budget.'
        ], services=[
            'Current skill/missile tables, progressive missile lookup, zero ring formula/current Chain count, positions, own attack-rating and final factory allocation are declared services. No selected/known ordinary skill or owner rank, mana or quantity is borrowed.',
            'The allocator is traced, not executed here. The separate complete factory evidence owns zero-vector correction, world/room allocation, seed, path initialization, damage capture and later collision. Invalid pointers/overflow/out-of-range override indices are excluded.'
        ], remaining=[
            'Prepared ordinary projectile launchers must not be treated as outgoing item launchers until these origins and return semantics are bound and verified. In particular, current prepared rings start at the actor, and prepared original Chain roots implement ordinary do26.',
            'Charged Bolt incoming do17, original event subscription history/order, nested callback dispatch, actual world seed allocation, shield paths, current executable parity and learned Valkyrie cast remain separate.'
        ])
    target = ROOT / 'tools/reference/npc-item-spell-origins-source-evidence.json'
    encoded = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print(f'NPC item origins: {len(cases)} complete seven-routine contexts; outgoing/incoming origins, allocation failures and flags; ASan+UBSan clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    probe(parser.parse_args().check)
