#!/usr/bin/env python3
"""Execute complete poison application and player/monster HP regeneration bodies."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
application=routine('source/D2Game/src/UNIT/SUnitDmg.cpp','SUNITDMG_ApplyPoisonDamage');player=routine('source/D2Game/src/PLAYER/PlrModes.cpp','EVENTS_HpRegen');monster=routine('source/D2Game/src/MONSTER/MonsterMode.cpp','D2GAME_MONSTER_ApplyStatRegen_6FC63440')
cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <iterator>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STATE_POISON=1,STATE_HEALTHPOT=2,STATE_PREVENTHEAL=3,STATE_OPENWOUNDS=4,STATE_UNINTERRUPTABLE=5,STATE_DEATH_DELAY=6;
constexpr int STAT_HPREGEN=0,STAT_HITPOINTS=1,STAT_LAST_SENT_HP_PCT=2,EVENTTYPE_STATREGEN=1,EVENTTYPE_REMOVESTATE=2,MONMODE_DEATH=0,MONMODE_DEAD=12,UNITEVENT_KILLED=1,UNITEVENT_KILL=2;
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;};struct D2ActiveRoomStrc{};
struct D2UnitStrc{D2GameStrc* pGame=nullptr;int dwUnitType=0,dwUnitId=10,dwAnimMode=1,dwLastHitClass=0,stats[8]={};};
struct D2StatListStrc{int rate=0,expiry=0,ownerType=0,ownerId=0,state=0;};
D2StatListStrc list;D2UnitStrc owner,defender;D2GameStrc game;D2ActiveRoomStrc room;bool hasList=false,lifeMask=false,town=false,prevent=false,uninterrupt=false;
int baseRegen=0,scheduled=0,removed=0,killed=0,killOwner=-1,deferred=0,writes=0,allocations=0;
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){return hasList&&state==STATE_POISON?&list:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc* s,int,int){return s->rate;}void D2COMMON_10476(D2StatListStrc* s,int frame){s->expiry=frame;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* s,int,int v,int){s->rate=v;}void STATES_ToggleState(D2UnitStrc*,int state,int){if(state==STATE_DEATH_DELAY)deferred++;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int expiry,int type,int id){allocations++;list={};list.expiry=expiry;list.ownerType=type;list.ownerId=id;return &list;}
void STATLIST_SetState(D2StatListStrc* s,int state){s->state=state;}void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){hasList=true;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){removed++;}void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){scheduled++;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int){return u->stats[stat];}int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 1024;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*){}void STATLIST_FreeStatList(D2StatListStrc*){}
void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){u->stats[stat]=value;if(stat==STAT_HITPOINTS)writes++;}
void sub_6FCC6270(D2UnitStrc*,int){}int STATES_CheckStateMaskLifeOnUnit(D2UnitStrc*){return lifeMask;}int STATLIST_GetUnitBaseStat(D2UnitStrc*,int,int){return baseRegen;}
int STATES_CheckState(D2UnitStrc*,int state){return state==STATE_PREVENTHEAL?prevent:state==STATE_UNINTERRUPTABLE?uninterrupt:false;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
uint8_t sub_6FC62F50(D2UnitStrc* u){return (u->stats[STAT_HITPOINTS]>>8)*128/(1024>>8);}
int STATLIST_GetOwnerType(D2StatListStrc* s){return s->ownerType;}int STATLIST_GetOwnerGUID(D2StatListStrc* s){return s->ownerId;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int id){owner.dwUnitId=id;return &owner;}
void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc*,D2UnitStrc* source,int){killed++;killOwner=source?source->dwUnitId:-1;}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,int){}
'''+application+'\n'+player+'\n'+monster+r'''
int main(){bool first=true;std::printf("{\"application\":[");
for(int old:{0,32,128})for(int rate:{0,16,32,128})for(int length:{0,1,200,1550})for(int original:{10,20})for(int next:{10,30})for(int kind:{0,1}){
 owner={};owner.pGame=&game;owner.dwUnitId=next;defender={};defender.dwUnitType=kind;list={};list.rate=-old;list.expiry=1500;list.ownerId=original;hasList=old>0;scheduled=removed=allocations=0;
 SUNITDMG_ApplyPoisonDamage(&owner,&defender,rate,length);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",old,rate,length,original,next,kind,hasList,list.rate,list.expiry,list.ownerId,scheduled,allocations);
}
std::printf("],\"regen\":[");first=true;
for(int kind:{0,1})for(int life:{1,128,255,256,257,512,1024})for(int rate:{0,1,32,256,1024})for(int base:{0,8})for(int mask:{0,1})for(int inTown:{0,1})for(int blocked:{0,1})for(int interrupt:{0,1}){
 defender={};defender.dwUnitType=kind;defender.stats[STAT_HPREGEN]=base-rate;defender.stats[STAT_HITPOINTS]=life;list={};list.ownerId=77;hasList=rate>0;baseRegen=base;lifeMask=mask;town=inTown;prevent=blocked;uninterrupt=interrupt;killed=deferred=writes=scheduled=removed=0;killOwner=-1;
 if(kind)D2GAME_MONSTER_ApplyStatRegen_6FC63440(&game,&defender,0,0);else EVENTS_HpRegen(&defender);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,life,rate,base,mask,inTown,blocked,interrupt,defender.stats[STAT_HITPOINTS],killed,killOwner,deferred,writes);
}std::printf("]}\n");}
'''
p=STAGE/'poison-state-source-probe.cpp';p.write_text(cpp);binary=STAGE/'poison-state-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data['application'])==384 and len(data['regen'])==2240
for old,rate,length,original,next,kind,present,out,expiry,source,events,alloc in data['application']:
 valid=rate>0 and length>0;refresh=valid and (not old or rate>=old)
 assert(present,out,expiry,source)==(int(old>0 or valid),-rate if refresh else -old,100+length if refresh else 1500,original if old or not valid else next)
 assert events==(int(valid and kind==1)+int(refresh)) and alloc==int(valid and not old)
for kind,life,rate,base,mask,town,prevent,uninterrupt,after,killed,source,deferred,writes in data['regen']:
 delta=base-rate-(base if kind and mask else 0)
 change=bool(delta and (not kind or (not prevent or delta<0) and not(delta<0 and life<256 and town)))
 expected=max(0 if kind else 256,min(1024,life+delta)) if change else life
 die=bool(kind and change and expected==0);assert(after,killed,deferred,writes)==(expected,int(die and not uninterrupt),int(die and uninterrupt),int(change))
 assert source==(77 if die and not uninterrupt and rate else -1)
fixture=STAGE/'poison-state-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(applicationCases=384,regenCases=2240,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete poison application accepts positive rate and frame length, replaces equal/stronger rate and ignores weaker rate without refreshing expiry. Existing stat-list owner remains the original source on refresh; first creation captures attacker type/ID. Monster stat-regeneration event is rescheduled to next frame on every valid application, even weaker applications.','Complete player HP regeneration clamps to256FP256/one life. Monster regeneration writes exact FP256 life, can retain positive life below256 and kills only at zero; ordinary source kill/kill-credit events occur then. It does not dispatch ordinary damage for each nonlethal poison frame.','Monster life-mask state removes its base regeneration contribution. Prevent-heal does not block negative regeneration. A negative update below one life returns without health mutation in town; source large updates crossing zero from at least one life and delayed-death branches are separately covered.'],limits=['Stats/stat-list allocation/attachment/expiration, room/town/life-mask/state, game/event scheduling, max life, death/event and packet services are declared. Complete application/player/monster bodies execute, not the global modern event queue or final NPC death pipeline.','The combined regen stat is a declared input; source natural regeneration, potion/replenish interactions and current modern poison interruptions/damage-event ordering remain separate. Do not infer global40ms phase or complete modern same-frame source ownership from this fixture.','The old native consumer used whole-HP ordinary Damage, replaced source on refresh and allowed player death. Shared direct FP256 health/original ownership/player-floor and saved source-frame receiver coverage is now recorded in poison-state-native-evidence.json. Earlier aggregate producers, exact mitigation/global phase/interruptions/combined regen, native delayed-death mapping and extended offline catch-up remain separate; no Poison Javelin handler is certified.'])
out=ROOT/'tools/reference/poison-state-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison state evidence stale'
else:out.write_text(encoded)
print('Poison:384 complete application/owner/rate and2240 player/monster FP256 regeneration cases compiled.')
