#!/usr/bin/env python3
"""Compile full Fire Blast landing callbacks, hit router, update and copied-area wrappers."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  b=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(b).hexdigest();return b.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  body=text[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 mode=source('source/D2Game/src/MISSILES/MissMode.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');ass=source('source/D2Game/src/SKILLS/SkillAss.cpp');common=source('source/D2Common/src/Units/Missile.cpp');units=source('source/D2Common/src/Units/Units.cpp');header=source('source/D2Game/include/MISSILES/MissMode.h')
 assert re.search(r'MISSMODE_NOCOLLIDE,\s*MISSMODE_PLAYERKILL,\s*MISSMODE_MONSTERKILL,\s*MISSMODE_ALLKILL,\s*MISSMODE_NOKILL,\s*MISSMODE_SAFE,\s*MISSMODE_BARRIERCOLLIDE,',header)
 router=routine(mode,'MISSMODE_SrvDmgHitHandler')
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <iterator>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,D2UnitInvalidGUID=-1;
constexpr int MISSMODE_NOCOLLIDE=0,MISSMODE_PLAYERKILL=1,MISSMODE_MONSTERKILL=2,MISSMODE_BARRIERCOLLIDE=6;
constexpr int STATE_JUSTHIT=1,STATE_UNINTERRUPTABLE=2,EVENTTYPE_REMOVESTATE=1,UNITEVENT_HITBYMISSILE=1,DAMAGERESULTFLAG_SUCCESSFULHIT=1;
enum{MISSILESFLAGINDEX_EXPLOSION,MISSILESFLAGINDEX_PIERCE,MISSILESFLAGINDEX_LASTCOLLIDE};
enum{STAT_SKILL_PIERCE,STAT_ITEM_PIERCE,STAT_PIERCE_IDX,STAT_TOHIT,STAT_ITEM_DAMAGETARGETAC,STAT_ARMORCLASS};
unsigned gdwBitMasks[32];struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2ActiveRoomStrc{}room;struct D2StatListStrc{};struct D2CoordStrc{int nX=0,nY=0;};struct D2PathPointStrc{uint16_t X=40,Y=70;}points[1];
struct D2DynamicPathStrc{int velocity=2304,flags=64;};struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;};
struct D2UnitStrc{int dwUnitType=3,dwClassId=385,dwUnitId=10,dwMissileMode=6,x=40,y=70,skill=251,rank=1,targetX=73,pSeed=0,frame=50,activation=50,fire=3141;std::map<int,int> stats;D2GameStrc* pGame=&game;D2MissileDataStrc md;D2MissileDataStrc* pMissileData=&md;D2DynamicPathStrc path;D2DynamicPathStrc* pDynamicPath=&path;int defense=0;bool unt=false;}owner,parent,child,target,other;
struct D2MissilesTxt{uint32_t dwMissileFlags=0,wResultFlags=5,dwHitFlags=2;int nNextHit=0,nNextDelay=0,nCollideFriend=0,nCollideKill=0,nToHit=0,nAlwaysExplode=1,wSrvHitFunc=36,wSrvDmgFunc=0,wHitSubMissile[1]={386},dwHitPar[1]={0};}row;
struct D2SkillsTxt{int dwAuraRangeCalc=5;}skill;
struct D2MissileStrc{int nSkill=0,nMissile=0,dwFlags=0,nX=0,nY=0,nSkillLevel=0;D2UnitStrc* pOwner=nullptr;}captured;
struct D2DamageStrc{uint32_t wResultFlags=0,dwHitFlags=0;int dwPhysDamage=0,dwFireDamage=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2MissilesTxt* pMissilesTxtRecord;};
using Finder=int(*)();int findCallback(){return 0;}struct Route{Finder pfUnitFindCallback;int nCollisionMask;};Route stru_6FD2E5F8[9];
bool hasRecord=true,hasSkill=true,hasOwner=true,hasRoom=true,factory=true;int creates=0,fills=0,flags=0,areas=0,filter=0,noAura=-1,areaRadius=0,events=0,commits=0,defenses=0,moves=0,finds=0,moveResult=1,frameWrites=0,ar=0;
std::vector<D2UnitStrc*> victims;std::vector<int> resultFlags,fireValues;D2DamageStrc* original=nullptr;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &row;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return nullptr;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}int MISSILE_GetTargetX(D2UnitStrc* p){return p->targetX;}void MISSILE_SetTargetX(D2UnitStrc* p,int x){p->targetX=x;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}void UNITS_GetCoords(D2UnitStrc* p,D2CoordStrc* c){c->nX=p->x;c->nY=p->y;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*){return 40;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*){return 70;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates++;captured=*p;child.x=p->nX;child.y=p->nY;child.skill=p->nSkill;child.rank=p->nSkillLevel;return factory?&child:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int rank){assert(id==251 && rank>0);return f;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}void STATLIST_SetUnitStat(D2UnitStrc* p,int s,int v,int){p->stats[s]=v;}
bool STATES_CheckState(D2UnitStrc* p,int state){return state==STATE_UNINTERRUPTABLE && p->unt;}bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return true;}
bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int,int){ar++;return true;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}void STATLIST_SetState(D2StatListStrc*,int){}
void MISSMODE_ToggleStateOff(D2UnitStrc*,int,D2StatListStrc*){}void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}void STATES_ToggleState(D2UnitStrc*,int,int){}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,void*){}
void MISSMODE_FillDamageParams(D2UnitStrc* p,D2UnitStrc*,D2DamageStrc* d){fills++;d->dwFireDamage=p->fire;}
void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){flags++;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return hasRoom?&room:nullptr;}void COLLISION_ResetMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned){}
int SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,int a,int b){assert(a==1 && b==0);defenses++;return p->defense;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*){events++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d){commits++;resultFlags.push_back(d->wResultFlags);fireValues.push_back(d->dwFireDamage);}
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* o,int x,int y,int radius,unsigned f,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int check,const char*,int){areas++;filter=f;noAura=check;areaRadius=radius;original=(D2DamageStrc*)args;D2AuraCallbackStrc a{g,o,args};for(auto* v:victims)callback(&a,v);assert(((D2DamageStrc*)args)->wResultFlags==5);}
int PATH_GetVelocity(D2DynamicPathStrc* p){return p?p->velocity:0;}int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){moves++;return moveResult;}
int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->frame;}void MISSILE_SetCurrentFrame(D2UnitStrc* p,int f){p->frame=f;frameWrites++;}int MISSILE_GetActivateFrame(D2UnitStrc* p){return p->activation;}
int D2Common_10201(D2DynamicPathStrc* p){return p->flags;}int D2COMMON_10198_PathGetSaveStep(D2DynamicPathStrc*,D2PathPointStrc** p){*p=points;return 1;}
int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return 4;}D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,Finder,D2MissileUnitFindArgStrc*,int){finds++;return &target;}
'''
 for text,name in [(units,'UNITS_GetDistanceToCoordinates'),(skills,'D2GAME_CreateMissile_6FD115E0'),(common,'MISSILE_SetOwner'),(common,'MISSILE_CheckUnitIfOwner'),(skills,'sub_6FD10140'),(ass,'sub_6FCF5DE0'),(skills,'sub_6FD10200'),(mode,'MISSMODE_SrvHit44_ExplodingJavelin'),(mode,'MISSMODE_SrvHit03_ExplosivePotion_BombOnGround'),(mode,'MISSMODE_SrvHit36_MissileInAir')]:cpp+=routine(text,name)
 actual={'MISSMODE_SrvHit44_ExplodingJavelin','MISSMODE_SrvHit03_ExplosivePotion_BombOnGround','MISSMODE_SrvHit36_MissileInAir','MISSMODE_SrvDmgHitHandler'}
 for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+',router))-actual):
  kind='void' if 'SrvDmg' in name else 'int32_t';params='D2GameStrc*,D2UnitStrc*,D2UnitStrc*'+(',D2DamageStrc*' if kind=='void' else '');cpp+=f'{kind} {name}({params}){{'+('' if kind=='void' else 'return 3;')+'}\n'
 cpp+=router+routine(mode,'MISSMODE_HandleMissileCollision')+routine(mode,'MISSMODE_SrvDo01_BasicMissile')
 cpp+=r'''
void reset(int kind){owner={};parent={};child={};target={};other={};owner.dwUnitType=0;target.dwUnitType=other.dwUnitType=1;other.dwUnitId=20;parent.dwClassId=kind?386:385;parent.targetX=137;parent.path.velocity=kind?0:2304;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_EXPLOSION]|gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];row.wSrvHitFunc=kind?3:36;row.nCollideKill=kind;hasRecord=hasSkill=hasOwner=hasRoom=factory=true;creates=fills=flags=areas=events=commits=defenses=moves=finds=frameWrites=ar=0;filter=areaRadius=0;noAura=-1;moveResult=1;victims={&target,&other};resultFlags.clear();fireValues.clear();captured={};skill.dwAuraRangeCalc=5;}
void emit(){printf(",%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",creates,fills,flags,areas,filter,noAura,areaRadius,events,commits,defenses,captured.dwFlags,captured.nMissile,captured.nSkill,captured.nSkillLevel,child.targetX,child.x,child.y,ar);}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"callbacks\":[");
for(int kind:{0,1})for(int exists:{0,1})for(int record:{0,1})for(int victim:{0,1})for(int own:{0,1})for(int sub:{-1,386})for(int rank:{1,21}){reset(kind);hasRecord=record;hasOwner=own;row.wHitSubMissile[0]=sub;parent.rank=rank;int result=kind?MISSMODE_SrvHit03_ExplosivePotion_BombOnGround(&game,exists?&parent:nullptr,victim?&target:nullptr):MISSMODE_SrvHit36_MissileInAir(&game,exists?&parent:nullptr,victim?&target:nullptr);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d",first?"":",",kind,exists,record,victim,own,sub,rank,result);first=false;emit();printf("]");}
printf("],\"routes\":[");first=true;
for(int kind:{0,1})for(int victim:{0,1})for(int ending:{0,1})for(int repeat:{0,1})for(int own:{0,1})for(int skillRow:{0,1})for(int radius:{0,3}){reset(kind);hasOwner=own;hasSkill=skillRow;row.dwHitPar[0]=radius;if(repeat){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,ending);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d",first?"":",",kind,victim,ending,repeat,own,skillRow,radius,result);first=false;emit();printf("]");}
printf("],\"updates\":[");first=true;
for(int kind:{0,1})for(int frame:{0,1,2,5,50})for(int movement:{1,2})for(int pathFlags:{0,1,2,4,64})for(int roomExists:{0,1})for(int defense:{0,1,2,4,16}){reset(kind);parent.frame=frame;parent.path.flags=pathFlags;moveResult=movement;hasRoom=roomExists;target.defense=defense;int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",first?"":",",kind,frame,movement,pathFlags,roomExists,defense,result,parent.frame,moves,finds,frameWrites);first=false;emit();printf(",[");for(size_t i=0;i<resultFlags.size();i++)printf("%s%d",i?",":"",resultFlags[i]);printf("],[");for(size_t i=0;i<fireValues.size();i++)printf("%s%d",i?",":"",fireValues[i]);printf("]]");}
printf("]}\n");}
'''
 path=STAGE/'fire-blast-landing-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(callbacks=128,routes=128,updates=1000)
 out=STAGE/'fire-blast-landing-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 def verify_tail(tail,create,fill,area,rank=1,radius=5,blocked=0):
  creates,fills,flags,areas,filter_,no_aura,area_radius,events,commits,defenses,factory_flags,missile_id,skill_id,level,child_target,x,y,ar=tail
  assert (creates,fills,flags,areas,events,commits,defenses,ar)==(create,fill,0,area,area*(2-int(blocked!=0)),area*2,area*2,0),tail
  assert (filter_,no_aura,area_radius)==((0x8583,0,radius) if area else (0,-1,0))
  assert (factory_flags,missile_id,skill_id,level,child_target,x,y)==((1,386,251,rank,137,40,70) if create else (0,0,0,0,73,40,70))
 for row in data['callbacks']:
  kind,exists,record,victim,own,sub,rank,result,*tail=row
  active=exists and record
  create=int(not kind and active and not victim and own and sub>=0)
  fill=int(kind and active and not victim)
  assert result==(0 if victim and (kind or active and sub>=0) else 1)
  verify_tail(tail,create,fill,fill*own,rank)
 for row in data['routes']:
  kind,victim,ending,repeat,own,skill_row,radius,result,*tail=row
  rejected=victim and repeat
  create=int(not rejected and not kind and not victim and own)
  fill=int(not rejected and kind and not victim and (skill_row or radius>0))
  assert result==(1 if rejected or victim and not ending else 2)
  verify_tail(tail,create,fill,fill*own,radius=radius or 5)
 for row in data['updates']:
  kind,frame,movement,path_flags,room_exists,defense,result,out_frame,moves,finds,writes,*tail=row
  packet_flags,packet_fire=tail[-2:];tail=tail[:-2]
  early=not kind and movement==2;ending=early or frame<=1
  assert (result,out_frame,moves,finds,writes)==(2 if ending or not room_exists or path_flags&5 else 1,frame-int(not early),int(not kind),0,int(not early))
  create=int(ending and not kind);fill=int(ending and kind)
  verify_tail(tail,create,fill,fill,blocked=defense)
  expected={0:5,1:20,2:100,4:132,16:4}[defense]
  assert packet_flags==([expected,5] if fill else []) and packet_fire==([3141,3141] if fill else [])

 e=dict(referenceVersion='3.3.93847',talentId='assassin.fireblast',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),caseCounts={k:len(v) for k,v in data.items()}),status='128 callback,128 complete router and1000 complete update cases pass independent guard/frame/routing/packet assertions; no native handler enabled.',established=['Hit36 creates one child386 through the complete coordinate factory helper for a null victim; direct actor contact returns0 and causes neither damage nor a child. Child keeps original owner, skill/rank, position and targetX137; a missing owner prevents allocation.', 'Ground hit3 only invokes full hit44 for a null victim. It fills one shared packet, uses pinned radius5 through aurarangecalc, sets row flags and calls full area dispatcher with filter0x8583 and NoAura checking0. Actor contacts return0 without direct or area damage.', 'Barrier collision mode is6 as pinned by the source enum, and skips actor finders. Full do1 moves first, then decrements lifetime; movement-end or frame<=0 goes through the real hit router. Synthetic room/flag early-removal controls remain distinct from landing callbacks.', 'Complete copied-area and impact wrappers copy the original packet per victim, apply individual defenses and preserve the shared original. Shield/dodge/weapon-block controls suppress successful-hit/events for one victim while the other receives the same raw3141 fire packet. WeaponBlock high flag is dropped by the original0xFE mask, as in the existing shared proof.'],services=['Declared room/path advancement, current collision flags/steps, table/skill/radius, child allocator and item-independent damage-reader sentinels.','Area search is a declared victim enumeration service recording filter/radius/NoAura; per-victim defense is a declared flag service. Full copied packet/impact wrappers execute unchanged.','Unselected callback table entries are declared stubs. Full damage factory/getters/stat writer, world room/finder geometry, seed/critical/events/commit and modern executable acceptance remain separate.'])
 p=ROOT/'tools/reference/fire-blast-landing-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Fire Blast landing:128 callbacks,128 complete routes and1000 complete updates pass; native handler remains disabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
