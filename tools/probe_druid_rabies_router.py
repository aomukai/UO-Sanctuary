#!/usr/bin/env python3
"""Compose complete Rabies child callbacks with the shared complete missile hit router."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath = ROOT / 'tools/reference/druid-rabies-preflight.json'
    pin = json.loads(pinpath.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    hashes, routines = {}, {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def routine(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        if name in pin['completeRoutineHashes']:
            assert routines[name] == pin['completeRoutineHashes'][name]
        return body + '\n'

    mode = 'source/D2Game/src/MISSILES/MissMode.cpp'
    common = 'source/D2Common/src/Units/Missile.cpp'
    router = routine(mode, 'MISSMODE_SrvDmgHitHandler')
    enum = re.search(r'enum D2C_MissilesTxtFlagIndices\s*\{.*?\};', source('source/D2Common/include/DataTbls/MissilesTbls.h'), re.S).group()
    row = pin['currentRows']['missiles']['rabiescontagion']
    assert row['pSrvHitFunc'] == '53' and row['pSrvDmgFunc'] == '11' and row['LastCollide'] == '1'
    assert not any(row.get(key) for key in ['ToHit', 'Pierce', 'CollideKill', 'NextHit', 'AlwaysExplode', 'Explosion'])
    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <iterator>
#include <map>
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,D2UnitInvalidGUID=-1,TRUE=1,FALSE=0;
constexpr int MISSMODE_NOCOLLIDE=0,MISSMODE_PLAYERKILL=1,MISSMODE_MONSTERKILL=2;
constexpr int STATE_JUSTHIT=1,EVENTTYPE_REMOVESTATE=1,UNITEVENT_HITBYMISSILE=1;
constexpr int STAT_SKILL_PIERCE=3,STAT_ITEM_PIERCE=4,STAT_PIERCE_IDX=5,STAT_TOHIT=6,STAT_ITEM_DAMAGETARGETAC=7,STAT_ARMORCLASS=8;
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2ActiveRoomStrc{}room;
struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;};
struct D2UnitStrc{int dwUnitType=3,dwUnitId=10,dwClassId=516,dwMissileMode=3,skill=238,rank=1,targetX=0,targetY=0;std::map<int,int> stats;D2GameStrc* pGame=&game;D2MissileDataStrc md;D2MissileDataStrc* pMissileData=nullptr;};
struct D2MissilesTxt{unsigned dwMissileFlags=0;int nCollideKill=0,nToHit=0,nNextHit=0,nNextDelay=0,nAlwaysExplode=0,wSrvHitFunc=53,wSrvDmgFunc=11,nCollideFriend=0;}row;
struct D2SkillsTxt{int wAuraTargetState=112;}skill;
struct Tables{int nStatesTxtRecordCount=200;}tables;auto*sgptDataTables=&tables;
struct D2StatListStrc{int state=112,expires=6000;}marker;
struct D2MissileStrc{int dwFlags=0,nSkill=0,nSkillLevel=0,nMissile=0,nRange=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
struct D2DamageStrc{int dwPhysDamage=0,dwPoisDamage=0,dwPoisLen=0;};
D2UnitStrc parent,owner,target,secondary;unsigned gdwBitMasks[32];
bool hasOwner=true,friendly=false,marked=false,allocationFails=false;
int fills=0,flags=0,resets=0,events=0,allocations=0,posts=0,lengthReads=0,spawns=0,spawnRange=0,rateSeen=-1,lengthSeen=-1,markedAtFill=-1;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){assert(id==516);return &row;}
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int id){return SKILLS_GetMissilesTxtRecord(id);}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==238);return &skill;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* m){assert(m==&parent);return hasOwner?&owner:nullptr;}
int MISSILE_GetSkill(D2UnitStrc* m){return m->skill;}int MISSILE_GetLevel(D2UnitStrc* m){return m->rank;}
int MISSILE_GetTargetX(D2UnitStrc* m){return m->targetX;}
void MISSILE_SetTargetX(D2UnitStrc* m,int value){m->targetX=value;}void MISSILE_SetTargetY(D2UnitStrc* m,int value){m->targetY=value;}
int SKILLS_GetElementalLength(D2UnitStrc* m,int id,int rank,int mode){assert(m==&owner&&id==238&&mode==1);lengthReads++;return 100+10*(rank-1);}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc* m,int id){assert(m==&owner&&id==238);return 515;}
int STATLIST_UnitGetStatValue(D2UnitStrc* m,int stat,int){return m->stats[stat];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* m,int stat,int){return m->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc* m,int stat,int value,int){m->stats[stat]=value;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc* m,D2UnitStrc* t){assert(m==&owner&&t==&target);return !friendly;}
bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int,int){std::abort();} // Current child has no ToHit.
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* m,int state){assert(m==&target&&state==112);return marked?&marker:nullptr;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned f,int deadline,int type,int id){assert(f==2&&type==target.dwUnitType&&id==target.dwUnitId);allocations++;if(allocationFails)return nullptr;marker.expires=deadline;return &marker;}
void STATLIST_SetState(D2StatListStrc* list,int state){assert(list==&marker&&state==112);list->state=state;}
void sub_6FD10E50(){}void MISSMODE_ToggleStateOff(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* m,D2StatListStrc* list,int f){assert(m==&target&&list==&marker&&f==1);marked=true;posts++;}
void STATES_ToggleState(D2UnitStrc* m,int state,int value){assert(m==&target&&state==112&&value==1);}
void D2COMMON_10476(D2StatListStrc* list,int deadline){list->expires=deadline;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc* m,int kind,int frame,int,int){assert(m==&target&&kind==EVENTTYPE_REMOVESTATE&&frame==marker.expires);}
void sub_6FCFE0E0(D2UnitStrc* m,D2StatListStrc* list,D2SkillsTxt* s,int id,int rank){assert(m==&owner&&list==&marker&&s==&skill&&id==238&&rank==parent.rank);}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* args){assert(args->nMissile==515&&args->dwFlags==32768&&args->pOwner==&target&&args->pOrigin==&target&&args->nSkill==238&&args->nSkillLevel==parent.rank);spawns++;spawnRange=args->nRange;return &secondary;}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,void*){events++;}
void MISSMODE_FillDamageParams(D2UnitStrc* m,D2UnitStrc* t,D2DamageStrc* damage){assert(m==&parent&&t==&target);fills++;markedAtFill=marked;damage->dwPoisDamage=678;damage->dwPoisLen=99;}
void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc* m,D2UnitStrc* t,D2DamageStrc* damage){assert(m==&parent&&t==&target);flags++;rateSeen=damage->dwPoisDamage;lengthSeen=damage->dwPoisLen;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}int CLIENTS_GetUnitX(D2UnitStrc*){return 31;}int CLIENTS_GetUnitY(D2UnitStrc*){return 47;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}
void COLLISION_ResetMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned mask){assert(mask==0x40);resets++;}
'''
    cpp += enum + '\n'
    cpp += routine(common, 'MISSILE_SetOwner')
    cpp += routine(common, 'MISSILE_CheckUnitIfOwner')
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'sub_6FCFEDD0')
    cpp += routine(mode, 'MISSMODE_SrvHit53_RabiesContagion')
    cpp += routine(mode, 'MISSMODE_SrvDmg11_RabiesContagion')
    selected = {'MISSMODE_SrvDmgHitHandler', 'MISSMODE_SrvHit53_RabiesContagion', 'MISSMODE_SrvDmg11_RabiesContagion'}
    for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+', router)) - selected):
        damage = 'SrvDmg' in name
        cpp += ('void' if damage else 'int32_t') + ' ' + name + '(D2GameStrc*,D2UnitStrc*,D2UnitStrc*' + (',D2DamageStrc*' if damage else '') + '){std::abort();}\n'
    cpp += router
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
 for(int rank:{1,3,20,28,60,255})for(int remain:{-1,0,1,9,10,100+10*(rank-1)-1,100+10*(rank-1),100+10*(rank-1)+1})
 for(int victim:{0,1})for(int end:{0,1})for(int repeat:{0,1})for(int own:{0,1})for(int exists:{0,1})for(int failed:{0,1})for(int hostile:{0,1})for(int type:{0,1}){
 parent={};owner={};target={};secondary={};parent.pMissileData=&parent.md;parent.skill=238;parent.rank=rank;parent.targetX=1000+remain;
 owner.dwUnitType=0;owner.dwUnitId=1;target.dwUnitType=type;target.dwUnitId=10;
 row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];skill={};marker={};
 if(repeat){parent.md.nOwnerType=type;parent.md.dwOwnerGUID=10;}
 hasOwner=own;friendly=!hostile;marked=exists;allocationFails=failed;fills=flags=resets=events=allocations=posts=lengthReads=spawns=spawnRange=0;rateSeen=lengthSeen=markedAtFill=-1;
 int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,end);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,remain,victim,end,repeat,own,exists,failed,hostile,type,result,fills,flags,resets,events,allocations,posts,lengthReads,spawns,spawnRange,rateSeen,lengthSeen,markedAtFill,int(marked),marker.expires,parent.md.nOwnerType,parent.md.dwOwnerGUID,secondary.targetY);first=false;
 }printf("]\n");}
'''
    generated = BASE / 'druid-rabies-router-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-rabies-router-compiler.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=log, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-rabies-router-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 12288
    for case in cases:
        rank, remaining, victim, end, repeat, owner, existed, failed, hostile, kind, result, fills, flags, resets, events, allocations, posts, reads, spawns, timeout, rate, frames, at_fill, marked, expiry, last_type, last_guid, source_guid = case
        rejected = victim and (repeat or owner and not hostile)
        callback = not rejected and (victim or end)
        valid = bool(callback and victim and owner and 10 <= remaining <= 100 + 10 * (rank - 1))
        new = valid and not existed
        posted = new and not failed
        expected = 1 if rejected or not callback else (2 if end else 1) if valid else 2
        assert (result, fills, flags, resets, events) == (expected, int(valid), int(valid), int(expected == 2), int(callback))
        assert (allocations, posts, spawns, timeout) == (int(new), int(posted), int(posted), remaining if posted else 0)
        assert reads == int(callback and victim and owner and remaining >= 10) + int(valid)
        assert (rate, frames, at_fill) == ((678, remaining, int(existed or posted)) if valid else (-1, -1, -1))
        assert (marked, expiry) == (int(existed or posted), 1000 + remaining if posted else 6000)
        assert (last_type, last_guid) == ((kind, 10) if victim or repeat else (0, -1))
        assert source_guid == int(posted)
    fixture = BASE / 'druid-rabies-router-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], preflightSha256=sha(pinpath), contexts=len(cases), completeRoutineHashes=routines, sourceHashes=hashes,
        flagEnumSha256=hashlib.sha256(enum.encode()).hexdigest(), generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        columns=['rank', 'remainingFrames', 'hasTarget', 'ending', 'repeatLastTarget', 'hasCaster', 'existingMarker', 'allocationFailure', 'hostile', 'targetType', 'routeResult', 'damageFills', 'damageFlags', 'collisionResets', 'hitEventServiceCalls', 'markerAllocations', 'markerPosts', 'lengthReads', 'plagueCreations', 'plagueTimeout', 'poisonRate', 'poisonFrames', 'markedAtFill', 'marked', 'markerDeadline', 'lastTargetType', 'lastTargetGuid', 'plagueCasterGuid'],
        established=[
            'Six complete unchanged older routines compose12288bounded contacts under ASan/UBSan: generic hit router,original LastCollide setter/checker,Rabies hit53/damage11and secondary marker/emitter helper. Current LastCollide flag uses its original enum index. Unselected callback table entries abort if accidentally reached; current child has no AR,Pierce,NHD or CollideKill.',
            'Valid lifetime hit53returns2: marker/emitter processing precedes direct FillDamageParams,then damage11sets the remaining poison frames,then SetDamageFlags. The child continues after normal contact. Explicit ending/a4can deliver that final valid packet and then return removal2. An existing marker is not refreshed but a valid poison packet still passes; allocation failure also does not suppress that valid packet.',
            'An initial oracle expected no hit-event service call for a null-target ending. The unchanged complete router calls that service before hit53even with a null recipient; the oracle now records the call. This does not assert downstream null-target event effects.',
            'Invalid remaining time or missing caster makes hit53return1: the real router skips both damage fill and damage11and removes the child on that contact. Thus the standalone damage11zeroing controls in the predecessor are not actual invalid-window damage deliveries.',
            'Consecutive most-recent-target contacts are rejected before callbacks or damage. Target type/GUID is stored before hostility checks. No global permanent per-child hit set is introduced. Declared nonhostile controls and null-contact/end paths retain their exact source decisions without claiming all are spatially reachable.',
        ], declaredServices=[
            'Damage fill/flags,stat-list allocation/events,poison mitigation/application,hostility,room/collision masks,elemental-length lookup and missile creation remain declared services. This is complete routing around those boundaries,not native HP/poison ticks,flight/candidate selection,full ABI or modern executable equivalence. Valid missile/current row inputs are assumed; unsafe unrelated null-row branches are not exercised.',
            'The current row supplies only LastCollide among selected router flags. Other stat/event constants are surrogate service identifiers; the flag enum and six executed function bodies are original pinned source. Current player versus monster recipients are control inputs; native PvP/exceptional-recipient permission remains separate.',
        ], remaining=[
            'Compose initial start57/do121,separate ordinary weapon and poison packets,payment,full skill/child damage creation and relative-origin/zero-aim factory semantics. Reuse existing poison state/mitigation and most-recent collision consumers only where these exact rules match.',
            'No learned Rabies handler or installed change is enabled here. Native independent marker/emitter persistence,poison cure/replacement,owner/history/logout/facet policy,actual input and graphical acceptance remain open. Final effects/icons stay separate; preserve the running Hunger candidate.',
        ])
    path = ROOT / 'tools/reference/druid-rabies-router-engine-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Rabies router:12288contexts/6complete routines; marker-before-poison,deadline damage/removal and LastCollide prepared; no native handler.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
