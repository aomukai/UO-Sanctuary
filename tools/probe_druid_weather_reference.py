#!/usr/bin/env python3
"""Execute unchanged shared weather setup and Hurricane/Armageddon state bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/prepare_druid_weather_reference.py'), '--check'], check=True)
    preflight = json.loads((ROOT / 'tools/reference/druid-weather-preflight.json').read_text())
    path = 'source/D2Game/src/SKILLS/SkillDruid.cpp'
    raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == preflight['sourceHashes'][path]
    source, routines = raw.decode(), {}

    def routine(name):
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        assert routines[name] == preflight['reviewedRoutineHashes'][name]
        return body + '\n'

    cpp = r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <tuple>
#include <vector>
#define __fastcall
constexpr int UNITFLAG_SKSRVDOFUNC=128,EVENTTYPE_REMOVESTATE=1,EVENTTYPE_ACTIVESTATE=2;
constexpr int STAT_MODIFIERLIST_SKILL=0,STAT_MODIFIERLIST_LEVEL=1,STAT_ATTACKRATE=2,STAT_OTHER_ANIMRATE=3;
constexpr int SKILLSFLAGINDEX_INTOWN=8,COLLIDE_MASK_RADIAL_BARRIER=0x805;
uint32_t gdwBitMasks[32];
struct D2GameStrc{void* pMemoryPool=nullptr;int dwGameFrame=1000;}game;
struct D2SkillStrc{int id=250,param=41;}used;
struct D2UnitStrc{int dwUnitType=0,dwUnitId=13,dwFlags=0;uint32_t pSeed=73;}actor;
struct D2SkillsTxt{int nAuraState=144,dwAuraLenCalc=250,dwAuraRangeCalc=9,dwParam[4]={250,0,9,20};uint32_t dwFlags[1]={0};int wAuraStat[6]={-1,-1,-1,-1,-1,-1},dwAuraStatCalc[6]={};int wResultFlags=1,dwHitFlags=2;}row;
struct D2StatListStrc{int expiry=0,state=-1,creatorType=-1,creatorId=-1,stats[4]={};}list;
struct D2ActiveRoomStrc{bool town=false;}room;
struct D2DamageStrc{int dwPhysDamage=0,dwColdDamage=0,wResultFlags=0,dwHitFlags=0;};
struct Tables{int nStatesTxtRecordCount=200;}tables;Tables* sgptDataTables=&tables;
bool hasRow=true,hasUsed=true,hasHighest=true,hasList=false,stateOn=false,hasRoom=true,allocates=true;
int allocs=0,posts=0,removes=0,frees=0,callbackSets=0,statUpdates=0,animUpdates=0,seedCalls=0,lowSeedWrites=0;
int rolls=0,areas=0,areaRange=0,areaFilter=0,areaPhys=0,areaCold=0,areaResult=0,areaHit=0;
int creates=0,notifies=0,createX=0,createY=0,attempts=0,limitedCalls=0;
int selection=577,barrierAt=0,roomAt=0,maskAt=0,x=40,y=50,lookupId=250;
std::vector<std::tuple<int,int,int,int>> events;std::vector<int> deleted;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==lookupId);return hasRow?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc* p){assert(p==&actor);return hasUsed?&used:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* p,const char*,int){return p->id;}
uint32_t ITEMS_RollRandomNumber(uint32_t* p){seedCalls++;*p=0x12345678u;return *p;}
void SEED_InitLowSeed(uint32_t* p,int v){assert(v==used.param);lowSeedWrites++;*p=v;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int v,int id,int rank){assert(p==&actor&&id==used.id&&rank>=0);return v;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){assert(p==&actor&&state==row.nAuraState);return hasList?&list:nullptr;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int expiry,int type,int id){assert(flags==2);allocs++;if(!allocates)return nullptr;list={};list.expiry=expiry;list.creatorType=type;list.creatorId=id;hasList=true;return &list;}
void STATLIST_SetState(D2StatListStrc* p,int state){p->state=state;}
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc* p,decltype(&sub_6FD10E50) f){assert(p==&list&&f==sub_6FD10E50);callbackSets++;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* p,D2StatListStrc* s,int on){assert(p==&actor&&s==&list&&on==1);posts++;}
void STATES_ToggleState(D2UnitStrc* p,int state,int on){assert(p==&actor&&state==row.nAuraState);stateOn=on;if(!on)removes++;}
void D2COMMON_10476(D2StatListStrc* p,int expiry){p->expiry=expiry;}
void EVENT_SetEvent(D2GameStrc* g,D2UnitStrc* p,int type,int frame,int id,int rank){assert(g==&game&&p==&actor);events.emplace_back(type,frame,id,rank);}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc* g,D2UnitStrc* p,int type,int id){assert(g==&game&&p==&actor&&type==2&&id==used.id);deleted.push_back(id);}
void* SKILLS_GetItemStatCostTxtRecord(int id){return id>=0&&id<4?&list:nullptr;}
void STATLIST_SetStat(D2StatListStrc* p,int stat,int v,int layer){assert(layer==0);p->stats[stat]=v;statUpdates++;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int v,int layer){STATLIST_SetStat(p,stat,v,layer);}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc* p,const char*,int){assert(p==&actor);animUpdates++;}
void SKILLS_SetParam1(D2SkillStrc* p,int v){p->param=v;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->param;}
bool STATES_CheckState(D2UnitStrc* p,int state){assert(p==&actor&&state==row.nAuraState);return stateOn;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* p){assert(p==&actor);return hasRoom?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* p){return p->town;}
void D2Common_10474(D2UnitStrc* p,D2StatListStrc* s){assert(p==&actor&&s==&list);hasList=false;}
void STATLIST_FreeStatList(D2StatListStrc* p){assert(p==&list);frees++;}
void D2GAME_RollPhysicalDamage_6FD14EC0(D2UnitStrc* p,D2DamageStrc* d,int id,int rank){assert(p==&actor&&id==used.id&&rank>0);d->dwPhysDamage=rank*7;rolls++;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc* p,D2DamageStrc* d,int id,int rank){assert(p==&actor&&id==used.id&&rank>0);d->dwColdDamage=rank*11;rolls++;}
void sub_6FD10200(D2GameStrc* g,D2UnitStrc* p,int ax,int ay,int r,D2DamageStrc* d,int filter){assert(g==&game&&p==&actor&&ax==0&&ay==0);areas++;areaRange=r;areaFilter=filter;areaPhys=d->dwPhysDamage;areaCold=d->dwColdDamage;areaResult=d->wResultFlags;areaHit=d->dwHitFlags;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc* p,int id){assert(p==&actor&&id==used.id);return selection;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc* p,int id){assert(p==&actor&&id==used.id);return hasHighest?&used:nullptr;}
int ITEMS_RollLimitedRandomNumber(uint32_t* p,int n){assert(p==&actor.pSeed&&n==2*row.dwAuraRangeCalc+1);int v=(limitedCalls*3+1)%n;limitedCalls++;return v;}
int CLIENTS_GetUnitX(D2UnitStrc* p){assert(p==&actor);return x;}
int CLIENTS_GetUnitY(D2UnitStrc* p){assert(p==&actor);return y;}
bool D2Common_11026(int,int,D2UnitStrc* p,int mask){assert(p==&actor&&mask==COLLIDE_MASK_RADIAL_BARRIER);attempts++;return attempts>=barrierAt;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc* p,int,int){assert(p==&room);return attempts>=roomAt?&room:nullptr;}
int COLLISION_CheckMask(D2ActiveRoomStrc* p,int,int,int mask){assert(p==&room&&mask==1);return attempts>=maskAt?0:1;}
D2UnitStrc* D2GAME_CreateMissile_6FD115E0(D2GameStrc* g,D2UnitStrc* p,int id,int rank,int mid,int ax,int ay){assert(g==&game&&p==&actor&&id==249&&rank>0&&mid==selection);creates++;createX=ax;createY=ay;return nullptr;}
void sub_6FCC6300(D2UnitStrc* p,int zero,int id,int rank,int ax,int ay,int seed){assert(p==&actor&&zero==0&&id==249&&rank>0&&ax==createX&&ay==createY&&seed==0);notifies++;}
'''
    # Aura filter is a source record member; keep source bodies unchanged.
    cpp = cpp.replace('int wResultFlags=1,dwHitFlags=2;', 'int wResultFlags=1,dwHitFlags=2;uint32_t dwAuraFilter=42883;')
    for name in ['sub_6FCFE0E0', 'SKILLS_SrvDo124_Armageddon_Hurricane', 'SKILLS_SrvDo145_Unused', 'SKILLS_SrvDo146_Unused']:
        cpp += routine(name)
    cpp += r'''
void reset(int id){game={};actor={};used={};used.id=lookupId=id;row={};if(id==249){row.nAuraState=145;row.dwAuraRangeCalc=row.dwParam[2]=8;row.dwParam[3]=6;}
list={};room={};hasRow=hasUsed=hasHighest=hasRoom=allocates=true;hasList=stateOn=false;
allocs=posts=removes=frees=callbackSets=statUpdates=animUpdates=seedCalls=lowSeedWrites=rolls=areas=areaRange=areaFilter=areaPhys=areaCold=areaResult=areaHit=creates=notifies=createX=createY=attempts=limitedCalls=0;
selection=577;barrierAt=roomAt=maskAt=0;events.clear();deleted.clear();}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"setup\":[");bool first=true;
for(int id:{249,250})for(int prior:{0,1})for(int length:{-4,0,1,250,1250})for(int rank:{1,20,255})for(int guard=0;guard<7;guard++){
 reset(id);hasList=stateOn=prior;list.expiry=1100;row.dwAuraLenCalc=length;
 if(guard==1)hasRow=false;if(guard==2)row.nAuraState=-1;if(guard==3)row.nAuraState=200;if(guard==4)hasUsed=false;if(guard==5)allocates=false;
 if(guard==6)used.id=id+1;
 int result=0;
 result=SKILLS_SrvDo124_Armageddon_Hurricane(&game,&actor,id,rank);
 if(result){assert(list.stats[0]==id&&list.stats[1]==rank);assert(events.size()==2);auto [kind,frame,sid,level]=events[0];assert(kind==1&&frame==1000+std::max(length,1)&&sid==0&&level==0);auto [active,next,skill,castRank]=events[1];assert(active==2&&next==1000+row.dwParam[3]&&skill==id&&castRank==rank);if(!prior)assert(list.state==row.nAuraState&&list.creatorType==0&&list.creatorId==13);}

 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",id,prior,length,rank,guard,result,allocs,posts,callbackSets,seedCalls,used.param,list.expiry,stateOn,statUpdates,animUpdates,(int)events.size(),(int)deleted.size(),actor.dwFlags);first=false;
}
printf("],\"hurricane\":[");first=true;
for(int guard=0;guard<8;guard++)for(int rank:{0,1,20,255})for(int range:{0,1,9})for(int town:{0,1})for(int allowed:{0,1}){
 reset(250);hasList=stateOn=true;row.dwAuraRangeCalc=range;room.town=town;if(allowed)row.dwFlags[0]|=gdwBitMasks[8];
 if(guard==1)hasRow=false;if(guard==2)row.nAuraState=-1;if(guard==3)row.nAuraState=200;if(guard==4)stateOn=false;if(guard==5)hasRoom=false;if(guard==6)hasList=false;if(guard==7){row.wResultFlags=4;row.dwHitFlags=16;}
 int result=SKILLS_SrvDo145_Unused(&game,&actor,250,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,rank,range,town,allowed,result,(int)events.size(),(int)deleted.size(),removes,frees,rolls,areas,areaRange,areaFilter,areaPhys,areaCold,areaResult,areaHit,stateOn);first=false;
 if(!events.empty()){auto [type,frame,id,level]=events.back();assert(type==2&&frame==1020&&id==250&&level==rank);}
}
printf("],\"armageddon\":[");first=true;
for(int guard=0;guard<9;guard++)for(int gate:{0,1,2})for(int success:{1,3,5,6})for(int rank:{0,1,20}){
 reset(249);hasList=stateOn=true;if(gate==0)barrierAt=success;if(gate==1)roomAt=success;if(gate==2)maskAt=success;
 if(guard==1)hasRow=false;if(guard==2)stateOn=false;if(guard==3)selection=0;if(guard==4)hasHighest=false;if(guard==5)row.dwAuraRangeCalc=0;if(guard==6)hasRoom=false;if(guard==7)room.town=true;if(guard==8){room.town=true;row.dwFlags[0]|=gdwBitMasks[8];}
 int result=SKILLS_SrvDo146_Unused(&game,&actor,249,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,gate,success,rank,result,(int)events.size(),(int)deleted.size(),removes,frees,seedCalls,lowSeedWrites,used.param,attempts,limitedCalls,creates,notifies,createX,createY);first=false;
 if(!events.empty()){auto [type,frame,id,level]=events.back();assert(type==2&&frame==1006&&id==249&&level==rank);}
}
printf("]}\n");}
'''
    path = STAGE / 'druid-weather-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'druid-weather-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    for id_, prior, length, rank, guard, result, allocs, posts, callbacks, seeds, param, expiry, state, stats, anim, events, deleted, flags in data['setup']:
        entered = guard in (0, 5)
        valid = entered and (prior or guard != 5)
        assert result == int(valid)
        assert seeds == int(entered) and flags == (128 if entered else 0)
        assert allocs == int(entered and not prior) and posts == callbacks == int(valid and not prior)
        assert (param, expiry, state, stats, anim, events, deleted) == ((0x5678, 1000+max(length, 1), 1, 2, 1, 2, 1) if valid else (41, 1100, prior, 0, 0, 0, 0))
    for guard, rank, range_, town, allowed, result, events, deleted, removes, frees, rolls, areas, radius, filter_, phys, cold, flags, hit, state in data['hurricane']:
        entered = guard not in (1, 2, 3, 4) and rank > 0
        removed = entered and guard != 5 and town and not allowed
        scheduled = entered and not removed and range_ > 0
        damage = scheduled and guard != 5 and not town
        assert (result, events, deleted, removes, frees, rolls, areas) == (int(damage), int(scheduled), 1, int(removed), int(removed and guard != 6), 2*int(damage), int(damage))
        assert (radius, filter_, phys, cold, flags, hit) == ((range_, 42883, rank*7, rank*11, 4 if guard == 7 else 1, 16 if guard == 7 else 2) if damage else (0, 0, 0, 0, 0, 0))
        assert state == int(guard != 4 and not removed)
    for guard, gate, success, rank, result, events, deleted, removes, frees, seeds, low_writes, param, attempts, limited, creates, notifies, ax, ay in data['armageddon']:
        entered = guard not in (1, 2) and rank > 0
        removed = entered and guard == 7
        scheduled = entered and not removed and guard not in (3, 4, 5)
        emits = scheduled and guard not in (6, 8)
        valid = emits and success <= 5
        count = min(success, 5) if emits else 0
        assert (result, events, deleted, removes, frees, seeds, low_writes, param, attempts, limited, creates, notifies) == (int(valid), int(scheduled), 1, int(removed), int(removed), int(emits), int(emits), 0x12345678%1000000 if emits else 41, count, 2*count, int(valid), int(valid))
        assert (ax, ay) == ((40+((2*(count-1)*3+1)%17)-8, 50+(((2*count-1)*3+1)%17)-8) if valid else (0, 0))
    counts = {name: len(rows) for name, rows in data.items()}
    assert counts == dict(setup=420, hurricane=384, armageddon=324)
    fixture = STAGE / 'druid-weather-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    output = dict(referenceVersion='3.3.93847', legacyCommit=preflight['legacyCommit'], handlerEnabled=False, installed=False,
                  pinnedInputHashes=preflight['pinnedInputHashes'], compiledUnchangedRoutines=routines,
                  fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), caseCounts=counts),
                  sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                  established=[
                      'Complete do124 and aura-stat helper preserve duration clamp, create-versus-refresh, uint16 seed capture before allocation, exactly one replaced active-state event and stored skill/rank. Allocation failure draws seed/marks doflag but creates no state or events.',
                      'Complete145 checks state/rank/town/range, removes forbidden-town state, schedules before no-room/allowed-town exit and rolls one fresh physical/cold packet per event. Copied-area request carries current radius/filter and skill flags; per-target area internals are separately proved by Tornado.',
                      'Complete146 preserves gate order, rescheduling, low-seed reset/stored draw, at most5 two-coordinate attempts and early exit after barrier/room/mask acceptance. A declared null missile allocation still notifies/returns success at an accepted site.'
                  ], services=[
                      'Row lookup, formula results, state/stat storage, allocation, event queue, unit/room/town/barrier/collision and seed/random services are declared bounded fixtures. No complete modern state/event dispatcher or source RNG stream is claimed.',
                      'Hurricane raw damage uses rank-scaled sentinels; complete cold damage/length/mastery/defense/health and Armageddon factory/impact/field require separate composition.',
                      'Native learned timing/resources/input, saved state/live-stat captures/natural timers/owner/facet/town/class cleanup and reloads remain unimplemented. No weather handler, class certificate or final asset is enabled.'
                  ], next='After Tornado installation, compose Hurricane source cold endpoints/length and current native area route, then implement saved self weather with dedicated input/timer/reload acceptance.')
    out = ROOT / 'tools/reference/druid-weather-source-evidence.json'
    text = json.dumps(output, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Druid weather:', counts, 'complete source setup/state cases pass; no handlers enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
