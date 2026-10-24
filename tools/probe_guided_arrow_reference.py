#!/usr/bin/env python3
"""Execute unchanged Guided Arrow factory, launch, homing and target selection bodies."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 body=s[m.start():end];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
ama='source/D2Game/src/SKILLS/SkillAma.cpp';miss='source/D2Game/src/MISSILES/Missiles.cpp';mode='source/D2Game/src/MISSILES/MissMode.cpp';skills='source/D2Game/src/SKILLS/Skills.cpp';units='source/D2Common/src/Units/Units.cpp'
bodies={n:routine(rel,n) for rel,n in [(ama,'SKILLS_AddDamagePercentBonus'),(ama,'SKILLS_SrvDo010_GuidedArrow_BoneSpirit'),(miss,'MISSILES_CreateMissileFromParams'),(mode,'MISSMODE_SrvDo07_GuidedArrow_BoneSpirit'),(mode,'MISSMODE_SrvHit10_GuidedArrow_BoneSpirit'),(skills,'sub_6FD10880'),(skills,'sub_6FD107F0'),(units,'UNITS_GetDistanceToCoordinates'),(units,'UNITS_GetDistanceToOtherUnit')]}
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <iterator>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,UNITFLAG_DOUPDATE=1,UNITFLAG_ISVALIDTARGET=8,UNITFLAG_CANBEATTACKED=16,UNITFLAG_SKSRVDOFUNC=32,UNITFLAG_ISDEAD=0x10000,STATE_SLOWMISSILES=1,COLLIDE_MISSILE=4,COLLIDE_NONE=0;
enum{MISSILESFLAGINDEX_CANSLOW,MISSILESFLAGINDEX_CANDESTROY,MISSILESFLAGINDEX_PIERCE};
enum{STAT_DAMAGEPERCENT,STAT_SKILL_HANDOFATHENA,STAT_ITEM_PIERCE,STAT_SKILL_PIERCE,STAT_PIERCE_IDX,STAT_TOHIT,STAT_DAMAGE_FRAMERATE};
unsigned gdwBitMasks[32];struct D2GameStrc{};struct D2ActiveRoomStrc{};struct D2StatListStrc{};struct D2SeedStrc{};struct D2MissileDamageDataStrc{};
struct D2UnitStrc;struct D2DynamicPathStrc{D2UnitStrc* target=nullptr;int x=0,y=0,velocity=0,flags=0;};
struct D2UnitStrc{int dwUnitType=0,dwClassId=86,dwUnitId=0,dwFlags=0,nSeqCurrentFramePrecise=0,x=40,y=70,size=1,skill=22,rank=1,remaining=128,total=128,mode=0,packed=0,missileFlags=0;bool dead=false,valid=true;D2DynamicPathStrc path;D2DynamicPathStrc* pDynamicPath=&path;D2UnitStrc* owner=nullptr;std::map<int,int> stats;};
struct D2MissilesTxt{int nVel=24,nVelLev=0,nSubLoop=0,nSubStop=0,nSubStart=0,wRange=128,wLevRange=0,nActivate=0,nCollideType=3,nCollision=0,wAccel=0,nMaxVel=24,dwDamageRate=0;unsigned dwMissileFlags=1;int dwParam[2]={5,15};};
struct D2SkillsTxt{int wSrvMissileA=86,wSrvMissileB=86;int dwCalc[4]={};};
struct D2MissileStrc{D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;D2UnitStrc* pTarget=nullptr;int dwFlags=0,nX=0,nY=0,nTargetX=0,nTargetY=0,nSkillLevel=0,nSkill=0,nMissile=0,nVelocity=0,nLoops=0,nRange=0,nFrame=0,nActivateFrame=0,nAttBonus=0,pInitArgs=0;void(*pInitFunc)(D2UnitStrc*,int)=nullptr;};
struct D2CoordStrc{int nX,nY;};struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};struct D2_6FD10880_Strc{D2UnitStrc* unk0x00;int32_t unk0x04;D2UnitStrc* unk0x08;int32_t unk0x0C,unk0x10,unk0x14;};
struct Tables{int nMissilesTxtRecordCount=1000;} tables;Tables* sgptDataTables=&tables;
struct Collision{unsigned nCollisionMask=4;} stru_6FD2E5F8[9];
D2GameStrc game;D2ActiveRoomStrc room;D2StatListStrc stat;D2UnitStrc owner,target,other,created,candidates[3];D2MissilesTxt row;D2SkillsTxt skill;D2MissileStrc captured;D2UnitStrc* selected=nullptr;
bool hasSkill=true,hasRecord=true,hasOwner=true,hasRoom=true,allocated=true,coordinates=true,town=false,targetValid=true,slow=false,selectedDead=false;
int aimX=50,aimY=70,calc=12,weaponClass=1,repaths=0,validations=0,collisions=0,distance=10,creates=0,draws=0,creationPercent=0,searches=0,filter=0,searchX=0,searchY=0,searchRadius=0;std::vector<D2UnitStrc*> enumeration;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc* u){return u==&owner?selected:u->pDynamicPath->target;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=selected?selected->x:aimX;*y=selected?selected->y:aimY;return coordinates;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return calc;}
int UNITS_GetWeaponClass(D2UnitStrc*){return weaponClass;}
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->y;}
void UNITS_GetCoords(D2UnitStrc* u,D2CoordStrc* c){c->nX=u->x;c->nY=u->y;}int UNITS_GetUnitSizeX(D2UnitStrc* u){return u->size;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return hasRoom?&room:nullptr;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){return hasRoom?&room:nullptr;}
bool STATES_CheckState(D2UnitStrc*,int){return slow;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return &stat;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 50;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int s,int){return u->stats[s];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* u,int s,int){return u->stats[s];}
int STATLIST_GetUnitBaseStat(D2UnitStrc* u,int s,int){return u->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc* u,int s,int value,int){u->stats[s]=value;}
D2UnitStrc* SUNIT_AllocUnitData(int type,int id,int x,int y,D2GameStrc*,D2ActiveRoomStrc*,int,int,int){creates++;if(!allocated)return nullptr;created={};created.pDynamicPath=&created.path;created.dwUnitType=type;created.dwClassId=id;created.x=x;created.y=y;return &created;}
void MISSILE_SetTotalFrames(D2UnitStrc* u,int v){u->total=v;}void MISSILE_SetCurrentFrame(D2UnitStrc* u,int v){u->remaining=v;}
int MISSILE_GetCurrentFrame(D2UnitStrc* u){return u->remaining;}
void MISSILE_SetActivateFrame(D2UnitStrc*,int){}
void PATH_SetVelocity(D2DynamicPathStrc* p,int v,const char*,int){p->velocity=v;}
void PATH_SetTargetUnit(D2DynamicPathStrc* p,D2UnitStrc* u){p->target=u;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc* p,int x,int y){p->x=x;p->y=y;p->target=nullptr;}
void PATH_SetFootprintCollisionMask(D2DynamicPathStrc*,int){}
void PATH_SetMoveTestCollisionMask(D2DynamicPathStrc*,int){}
int D2Common_10142(D2DynamicPathStrc*,D2UnitStrc*,int){repaths++;return 1;}
void MISSILE_SetOwner(D2UnitStrc*,D2UnitStrc*){}
void PATH_SetAcceleration(D2DynamicPathStrc*,int){}void PATH_SetMaxVelocity(D2DynamicPathStrc*,int){}
void STATLIST_AllocStatListEx(D2UnitStrc*,int,void*,void*){}
void MISSILE_SetSkill(D2UnitStrc* u,int v){u->skill=v;}void MISSILE_SetLevel(D2UnitStrc* u,int v){u->rank=v;}
void sub_6FC7C900(D2UnitStrc*,int){}void sub_6FC7CA10(D2UnitStrc*,int){}
void MISSILE_CalculateDamageData(D2MissileDamageDataStrc*,D2UnitStrc*,D2UnitStrc*,D2UnitStrc* u,int){creationPercent=u->stats[STAT_DAMAGEPERCENT];}
void MISSILE_SetDamageStats(D2UnitStrc*,D2UnitStrc*,D2MissileDamageDataStrc*,int){}
void UNITS_StoreOwner(D2UnitStrc* u,D2UnitStrc* o){u->owner=o;}
void SEED_InitLowSeed(D2SeedStrc*,int){}
unsigned ITEMS_RollRandomNumber(D2SeedStrc*){draws++;return 99;}
void MISSILE_SetFlags(D2UnitStrc* u,int v){u->missileFlags=v;}int MISSILE_GetFlags(D2UnitStrc* u){return u->missileFlags;}
void sub_6FC6E8B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){}
void MISSILE_SetTargetX(D2UnitStrc* u,int v){u->mode=v;}int MISSILE_GetTargetX(D2UnitStrc* u){return u->mode;}
void MISSILE_SetTargetY(D2UnitStrc* u,int v){u->packed=v;}int MISSILE_GetTargetY(D2UnitStrc* u){return u->packed;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* u){return hasOwner?u->owner:nullptr;}
bool SUNIT_IsDead(D2UnitStrc* u){return !u || u->dead;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){validations++;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return targetValid;}
int D2Common_10201(D2DynamicPathStrc* p){return p->flags;}
int MISSILE_GetLevel(D2UnitStrc* u){return u->rank;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;return 1;}
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* u,int x,int y,int radius,unsigned f,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){searches++;filter=f;searchX=x;searchY=y;searchRadius=radius;D2AuraCallbackStrc a{g,u,args};for(auto* c:enumeration)callback(&a,c);}
'''
for name in ['UNITS_GetDistanceToCoordinates','UNITS_GetDistanceToOtherUnit','sub_6FD10880','sub_6FD107F0','SKILLS_AddDamagePercentBonus','MISSILES_CreateMissileFromParams','SKILLS_SrvDo010_GuidedArrow_BoneSpirit','MISSMODE_SrvDo07_GuidedArrow_BoneSpirit','MISSMODE_SrvHit10_GuidedArrow_BoneSpirit']:cpp+='\n'+bodies[name]+'\n'
cpp+=r'''void reset(){owner={};target={};other={};created={};owner.pDynamicPath=&owner.path;target.pDynamicPath=&target.path;other.pDynamicPath=&other.path;created.pDynamicPath=&created.path;owner.dwUnitType=0;target.dwUnitType=other.dwUnitType=1;target.dwUnitId=10;other.dwUnitId=20;target.x=50;target.y=70;row={};skill={};selected=nullptr;hasSkill=hasRecord=hasOwner=hasRoom=allocated=coordinates=targetValid=true;town=slow=false;aimX=50;aimY=70;calc=12;weaponClass=1;repaths=validations=collisions=creates=draws=creationPercent=searches=filter=searchX=searchY=searchRadius=0;enumeration.clear();created.owner=&owner;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("{\"launch\":[");
for(int targeted:{0,1})for(int dx:{-20,-1,1,10,36})for(int dy:{-10,0,7})for(int bonus:{0,12,145})for(int rank:{1,20})for(int cross:{0,1}){
 reset();aimX=owner.x+dx;aimY=owner.y+dy;target.x=aimX;target.y=aimY;selected=targeted?&target:nullptr;calc=bonus;weaponClass=cross?2:1;owner.stats[STAT_ITEM_PIERCE]=owner.stats[STAT_SKILL_PIERCE]=100;
 int result=SKILLS_SrvDo010_GuidedArrow_BoneSpirit(&game,&owner,22,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",targeted,dx,dy,bonus,rank,cross,result,created.mode,created.packed,created.remaining,created.total,created.path.velocity,creationPercent,draws);
}
std::printf("],\"steer\":[");first=true;
for(int flags:{1,2,5,6})for(int remaining:{0,1,3,5,10,125,128})for(int cadence:{0,5,7})for(int kind:{0,1,2,3})for(int dist:{2,3,4,24,25,26}){
 reset();created.mode=flags;created.remaining=remaining;row.dwParam[0]=cadence;created.path.target=kind?&target:nullptr;target.dead=kind==2;targetValid=kind!=3;target.x=created.x+dist+1;target.y=created.y;target.size=2;
 int result=MISSMODE_SrvDo07_GuidedArrow_BoneSpirit(&game,&created);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,remaining,cadence,kind,dist,result,repaths,validations,collisions);
}
std::printf("],\"hit\":[");first=true;
for(int flags:{0,1,2,4,5,6})for(int remaining:{-1,0,1,127})for(int victim:{0,1,2})for(int wall:{0,4})for(int found:{0,1}){
 reset();created.mode=flags;created.remaining=remaining;created.path.flags=wall;created.path.target=&target;created.packed=(uint16_t)-10+((uint32_t)(uint16_t)7<<16);if(found){other.x=45;other.y=70;enumeration={&other};}
 int result=MISSMODE_SrvHit10_GuidedArrow_BoneSpirit(&game,&created,victim==0?nullptr:victim==1?&target:&other);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,remaining,victim,wall,found,result,created.mode,created.remaining,repaths,searches,created.path.target==&other,created.path.x,created.path.y,filter);
}
std::printf("],\"select\":[");first=true;
for(int count:{0,1,2,3})for(int reversed:{0,1})for(int previous:{-1,0,10,20,99}){
 reset();for(int i=0;i<count;i++){candidates[i]={};candidates[i].dwUnitId=(i+1)*10;enumeration.push_back(&candidates[i]);}if(reversed)std::reverse(enumeration.begin(),enumeration.end());int seen=0;auto* result=sub_6FD107F0(&game,&owner,40,70,15,3,previous,&seen);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",count,reversed,previous,result?result->dwUnitId:-1,seen,filter);
}std::printf("]}\n");}
'''
path=STAGE/'guided-arrow-source-probe.cpp';path.write_text(cpp);binary=STAGE/'guided-arrow-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(launch=360,steer=2016,hit=288,select=40),{k:len(v) for k,v in data.items()}
for targeted,dx,dy,bonus,rank,cross,result,mode,packed,remaining,total,velocity,percent,draws in data['launch']:
 distance=max(abs(dx),abs(dy))+min(abs(dx),abs(dy))//2;assert result==1 and mode==(1 if targeted else 2) and remaining==(128 if targeted else (distance<<16)//(16*4608)) and total==128 and velocity==4608 and percent==bonus and draws==0
for flags,remaining,cadence,kind,dist,result,repaths,validations,collisions in data['steer']:
 expected=int(bool(flags&1) and kind==1 and remaining%(cadence if cadence>0 else 5)==0 and 3<dist<25);assert result==1 and repaths==expected and validations==int(bool(flags&1)) and collisions==1
for flags,remaining,victim,wall,found,result,newMode,newRemaining,repaths,searches,targetChanged,x,y,filter in data['hit']:
 if victim==0 and (flags&4 or wall&4):expected=1
 elif flags&1:expected=4 if victim==2 else 3
 elif not flags&2:expected=3
 elif remaining>0:expected=4-int(bool(flags&4))
 elif flags&4:expected=1
 else:expected=4
 seeking=bool(flags==2 and remaining<=0 and not(victim==0 and wall&4))
 assert result==expected and searches==int(seeking) and repaths==int(seeking)
 assert newMode==((5 if found else 6) if seeking else flags) and newRemaining==(128 if seeking else remaining)
 if seeking:assert filter==0xA783 and targetChanged==found and ((x,y)==(30,77) if not found else True)
for count,reversed,previous,result,seen,filter in data['select']:
 ids=[(i+1)*10 for i in range(count)];greater=[x for x in ids if x>previous] if previous>=0 else ids;expected=min(greater) if greater else min(ids) if ids else -1;assert result==expected and filter==0xA783
fixture=STAGE/'guided-arrow-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.guidedarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(cases={k:len(v) for k,v in data.items()},path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete unchanged do10 and factory preserve selected-target mode1 versus ground mode2, packed original signed aim, source128 damage-percent initializer ordering, source velocity4608 and ground remaining-frame floor calculation. Current no-Pierce row does not draw a pierce budget even with owner pierce.','Complete unchanged do7 validates target and repaths only at remaining-frame cadence with size-adjusted source distance strictly between3 and25. Selected target death/hostility failure stops repathing; it does not acquire another target.','Complete unchanged hit10 distinguishes locked-target collision, ignored ground-initial collisions, ground expiry/acquisition mode5 and unacquired extension mode6. Remaining timers reset to current range128. Complete selector chooses next ID after previous with wrap; with previous=-1 it chooses lowest ID independently of enumeration order.','Complete source distance routines are executed by the factory and callbacks. Retarget filter0xA783 includes owner-to-target LOS via the separately reviewed area filter.'],limits=['World allocation/rooms/path movement/hostility/recipient enumeration/stat/damage/critical services are declared adapters. This probe compiles complete launch/factory/callback/selection bodies but stubs the generic collision caller; it does not certify full modern path/collision/steering/seed/world equivalence.','Path classic missile generation, movement and hit-handler result routing are reviewed separately before native integration. Native Q12 vectors, two-subtile geometry and owner/hostile/visibility adapters retain their existing bounded scope. Modern current selection/ground transition behavior and actual-client presentation remain acceptance work.'])
out=ROOT/'tools/reference/guided-arrow-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Guided source evidence stale'
else:out.write_text(encoded)
print('Guided Arrow:360 complete launch/factory,2016 steering,288 hit-state and40 selector cases compiled.')
