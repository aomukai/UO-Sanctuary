#!/usr/bin/env python3
"""Execute full Lightning Fury search/filter/callback, creator and generic hit route."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 body=s[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';skills='source/D2Game/src/SKILLS/Skills.cpp';path='source/D2Common/src/Path/Path.cpp'
filterBody=routine(skills,'sub_6FD0FA00');area=routine(skills,'sub_6FD0FE80');hit=routine(mode,'MISSMODE_SrvHit20_LightningFury');callback=routine(mode,'MISSMODE_LightningFury_AuraCallback')
def run(name,cpp):
 f=STAGE/(name+'.cpp');f.write_text(cpp);b=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(f),'-o',str(b)],check=True);return json.loads(subprocess.check_output([str(b)]))
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4,PLRMODE_DEAD=17,PLRMODE_DEATH=0,MONMODE_DEAD=12,MONMODE_DEATH=0,STATE_JUSTHIT=1,UNITFLAG_CANBEATTACKED=128,UNITFLAG_ISVALIDTARGET=1024,MONSTATSFLAGINDEX_NOAURA=0,MONSTATSFLAGINDEX_NPC=1;
unsigned gdwBitMasks[32];struct D2GameStrc{};struct D2ActiveRoomStrc;struct D2UnitStrc{int dwUnitType=1,dwClassId=0,dwAnimMode=1,x=50,y=70,id=0,skill=35,rank=1;unsigned dwFlags=1152;bool hostile=true,undead=false,boss=false,prime=false,justHit=false;D2UnitStrc* pRoomNext=nullptr;D2ActiveRoomStrc* room=nullptr;};
struct D2ActiveRoomStrc{D2UnitStrc* pUnitFirst=nullptr;bool town=false;};struct D2DrlgCoordsStrc{int nSubtileX=0,nSubtileY=0,nSubtileWidth=1000,nSubtileHeight=1000;};struct D2CoordStrc{int nX=0,nY=0;};
struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;};D2MonStatsTxt monsters[16];struct D2MissilesTxt{int wHitSubMissile[1]={231},dwHitPar[2]={0,0};unsigned dwMissileFlags=0;};struct D2SkillsTxt{int dwAuraRangeCalc=0,dwCalc[1]={1},dwAuraFilter=42371;};
struct D2AuraCallbackStrc{void* pArgs=nullptr;D2UnitStrc* pOwner=nullptr;D2GameStrc* pGame=nullptr;int nCounter=0;};struct D2HitFunc22ParamStrc{D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nSkillId=0,nLevel=0,nParam=0,nHitSubMissile=0;};
struct D2MissileStrc{int dwFlags=0,nSkill=0,nSkillLevel=0,nMissile=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};D2GameStrc game;D2SkillsTxt skill;D2MissilesTxt row;D2ActiveRoomStrc rooms[2];D2ActiveRoomStrc* adjacent[2];D2UnitStrc caster,parent,targets[6];std::vector<D2MissileStrc> emitted;int searchedX=0,searchedY=0,wall=0;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return &row;}D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return &skill;}D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int id){return &monsters[id];}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &caster;}D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc* u){return u->room;}
int CLIENTS_GetUnitX(D2UnitStrc* u){return u->x;}int CLIENTS_GetUnitY(D2UnitStrc* u){return u->y;}int MISSILE_GetSkill(D2UnitStrc* u){return u->skill;}int MISSILE_GetLevel(D2UnitStrc* u){return u->rank;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc* r){return r&&r->town;}bool MONSTERS_IsUndead(D2UnitStrc* u){return u->undead;}bool MONSTERS_IsBoss(void*,D2UnitStrc* u){return u->boss;}bool MONSTERS_IsPrimeEvil(D2UnitStrc* u){return u->prime;}
bool SUNIT_AreUnitsAligned(D2GameStrc*,D2UnitStrc*,D2UnitStrc* u){return !u->hostile;}bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc* u){return u->hostile;}bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return true;}bool STATES_CheckState(D2UnitStrc* u,int){return u->justHit;}
bool COLLISION_RayTrace(D2ActiveRoomStrc*,D2CoordStrc*,D2CoordStrc*,unsigned){return wall;}void DUNGEON_GetAdjacentRoomsListFromRoom(D2ActiveRoomStrc*,D2ActiveRoomStrc*** out,int* count){*out=adjacent;*count=2;}void DUNGEON_GetRoomCoordinates(D2ActiveRoomStrc*,D2DrlgCoordsStrc*){}
int PATH_ComputeSquaredDistance(int x,int y,int a,int b){return (x-a)*(x-a)+(y-b)*(y-b);}int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int which,int,int rank){return which==0?15:rank+1;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){emitted.push_back(*p);return &parent;}
'''+filterBody+'\n'+area+'\n'+callback+'\n'+hit+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int rank:{1,2,20,255})for(int radius:{0,5})for(int limit:{0,1})for(int reverse:{0,1})for(int variant=0;variant<32;variant++){
 caster={};caster.dwUnitType=0;caster.x=40;caster.y=70;caster.room=&rooms[0];parent={};parent.x=50;parent.y=70;parent.rank=rank;parent.room=&rooms[0];rooms[0]={};rooms[1]={};adjacent[0]=&rooms[reverse];adjacent[1]=&rooms[1-reverse];row={};row.dwHitPar[0]=radius;row.dwHitPar[1]=limit;skill={};emitted.clear();for(auto& m:monsters)m={};
 for(int i=0;i<6;i++){targets[i]={};targets[i].id=i;targets[i].dwClassId=i;targets[i].x=50+i*2;targets[i].y=70;targets[i].room=&rooms[i/3];targets[i].pRoomNext=i%3==2?nullptr:&targets[i+1];}rooms[0].pUnitFirst=&targets[0];rooms[1].pUnitFirst=&targets[3];
 if(variant&1)targets[0].dwAnimMode=MONMODE_DEAD;if(variant&2)targets[1].hostile=false;if(variant&4)monsters[2].dwMonStatsFlags=1;if(variant&8)rooms[1].town=true;if(variant&16)targets[4].dwFlags=0;wall=1;
 int result=MISSMODE_SrvHit20_LightningFury(&game,&parent,&targets[0]);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,[",rank,radius,limit,reverse,variant,result);bool f=true;for(auto& e:emitted){if(!f)std::printf(",");f=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",(e.nTargetX-50)/2,e.dwFlags,e.nSkill,e.nSkillLevel,e.nMissile,e.pOrigin==&parent,e.pOwner==&caster);}std::printf("]]");}std::printf("]\n");}
''';search=run('lightning-fury-search-source-probe',cpp);assert len(search)==1024
for rank,radius,limit,reverse,variant,result,calls in search:
 order=[3,4,5,0,1,2] if reverse else [0,1,2,3,4,5];eligible=[i for i in order if i*2<=(radius or 15) and not(i==0 and variant&1 or i==1 and variant&2 or i==2 and variant&4 or i>=3 and variant&8 or i==4 and variant&16)]
 assert result==3 and [x[0] for x in calls]==eligible[:limit or rank+1]
 assert all(x[1:]==[32,35,rank,231,1,1] for x in calls)
# Complete creator/getters are inherited unchanged; only current pinned skill/owner rows change.
base=(STAGE/'lightning-bolt-creator-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('dwEMaxLev[5]={12,18,28,48,88}','dwEMaxLev[5]={20,30,40,50,50}').replace('return hard*12;','return hard*4;')
main=(STAGE/'lightning-bolt-creator-source-probe.cpp').read_text().split('int main()',1)[1]
main=main.replace('for(int rank:', 'for(int cloud:{0,1})for(int rank:').replace('missile.dwClassId=205','missile.dwClassId=cloud?231:206').replace('row.nSkill=20;row.nSrcDamage=0','row.nSkill=35;row.nSrcDamage=cloud?uint8_t(-1):0').replace('skill.nSrcDam=96','skill.nSrcDam=128').replace('std::printf("[%d,%d,%d,%d,%d,[",rank,points,mastery,item,crit);','std::printf("[%d,%d,%d,%d,%d,%d,[",cloud,rank,points,mastery,item,crit);')
damage=run('lightning-fury-damage-source-probe',base+'int main()'+main);assert len(damage)==576;fields=json.loads((STAGE/'lightning-bolt-source-probe.json').read_text())['fields']
for r in damage:
 d=dict(zip(fields,r[6]));assert bool(d['nFlags']&1)==(r[0]==0),'Only source weapon parent requests missile item events'
# Full generic router with Fury parent/child row flags, not a hand-written replacement.
base=(STAGE/'poison-javelin-routing-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('int32_t MISSMODE_SrvHit20_LightningFury(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return 3;}','int furyCallbacks=0;int32_t MISSMODE_SrvHit20_LightningFury(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){furyCallbacks++;return 3;}')
main=(STAGE/'poison-javelin-routing-source-probe.cpp').read_text().split('int main()',1)[1].split('std::printf("],\\"contacts')[0]
# Use a fresh main to avoid unrelated sequential-contact fixture shape.
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");for(int child:{0,1})for(int victim:{0,1})for(int a4:{0,1})for(int pierce:{0,100})for(int budget:{0,1,4})for(int repeat:{0,1}){
 parent={};owner={};target={};parent.pGame=&game;parent.pMissileData=&parent.md;target.dwUnitType=1;row={};row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|(child?0:gdwBitMasks[MISSILESFLAGINDEX_PIERCE]);row.nCollideKill=child?0:1;row.nToHit=0;row.nAlwaysExplode=0;row.wSrvHitFunc=child?0:20;row.wSrvDmgFunc=0;owner.stats[STAT_SKILL_PIERCE]=pierce;parent.stats[STAT_PIERCE_IDX]=budget;hasRecord=hasSkill=hasOwner=true;arCalls=fillCalls=flagCalls=furyCallbacks=0;if(repeat){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,a4);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",child,victim,a4,pierce,budget,repeat,result,arCalls,fillCalls,furyCallbacks,parent.stats[STAT_PIERCE_IDX]);}std::printf("]\n");}
''';routes=run('lightning-fury-routing-source-probe',cpp);assert len(routes)==96
for child,victim,a4,pierce,budget,repeat,result,ar,fill,callbacks,newbudget in routes:
 rejected=bool(victim and repeat);direct=bool(victim and not rejected);assert ar==0 and fill==int(direct) and callbacks==int(not child and not rejected and (victim or a4))
 expected=1 if rejected else (2 if a4 else 1) if child else 1 if victim and pierce and budget else 2
 assert result==expected
# Execute the complete allocator, preserving its original initial SavedSteps assignment.
allocator=routine(path,'PATH_AllocDynamicPath');classic=routine(path,'PATH_ComputePathClassicMissile')
base=(STAGE/'poison-path-source-probe.cpp').read_text().split('void __fastcall sub_6FDAB810',1)[0]
base=base.replace('int dwUnitType=3,dwFlags=0;','int dwClassId=0,dwUnitType=3,dwFlags=0;').replace('int dwFlags=0,dwPathPoints=1,','D2PathPointStrc tTargetCoord;int nStepNum=0,nDistMax=0,nIDAStarInitFScore=0,unk0x38=0;int dwFlags=0,dwPathPoints=0,')
base+=r'''
#define D2_CALLOC_STRC_POOL(pool,type) new type{}
#define D2_ASSERTM(cond,text) assert(cond)
constexpr int MONSTER_WRAITH1=1,MONSTATSFLAGINDEX_FLYING=0,MONSTATSFLAGINDEX_OPENDOORS=1;unsigned gdwBitMasks[32];struct D2MonStatsTxt{int nBaseId=0;unsigned dwMonStatsFlags=0;};D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){return nullptr;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}int D2Common_11281_CollisionPatternFromSize(D2UnitStrc*,int){return 0;}
void PATH_SetType(D2DynamicPathStrc* p,int type){p->dwPathType=type;}void PATH_AddCollisionFootprintForUnit(D2UnitStrc*){}void UNITROOM_AddUnitToRoom(D2UnitStrc*,D2ActiveRoomStrc*){}
void UNITS_GetCoords(D2UnitStrc* u,D2CoordStrc* c){c->nX=u->pDynamicPath->tGameCoords.wPosX;c->nY=u->pDynamicPath->tGameCoords.wPosY;}int FOG_csprintf(char*,const char*,int){return 0;}bool DungeonTestRoomGame(D2ActiveRoomStrc*,int,int){return true;}
'''+allocator+'\n'+classic+r'''int main(){bool first=true;std::printf("[");D2ActiveRoomStrc room;for(int x:{41,100})for(int y:{71,200})for(int flag:{0,1})for(int dx:{-2,0,2})for(int dy:{-2,0,2}){D2UnitStrc unit;PATH_AllocDynamicPath(nullptr,&room,x,y,&unit,flag);auto* p=unit.pDynamicPath;p->tTargetCoord={(uint16_t)(x+dx),(uint16_t)(y+dy)};int result=PATH_ComputePathClassicMissile(p,&unit);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%u,%u,%d,%d,%d,%d,%d]",x,y,flag,dx,dy,result,p->tGameCoords.dwPrecisionX,p->tGameCoords.dwPrecisionY,p->nSavedStepsCount,p->SavedSteps[0].X,p->SavedSteps[0].Y,p->dwPathPoints,p->dwPathType);delete p;}std::printf("]\n");}
''';initial=run('lightning-fury-initial-path-source-probe',base);assert len(initial)==72
for x,y,flag,dx,dy,result,fx,fy,count,sx,sy,points,typ in initial:assert(result,fx,fy,count,sx,sy,points,typ)==(1,(x<<16)+32768,(y<<16)+32768,1,x,y,1,4)
root=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((root/'sources.json').read_text());pins={};data={}
for name in ['skills','missiles']:
 raw=(root/'json'/(name+'.json')).read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==manifest['files']['json/'+name+'.json']['sha256'];data[name]=json.loads(raw)
fixture=STAGE/'lightning-fury-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,search=search,damage=damage,routes=routes,initial=initial),separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.lightningfury',handlerEnabled=False,legacyCommit=tree['sha'],pinnedHashes=pins,pinnedSkill=data['skills']['35'],pinnedRows={k:data['missiles'][k] for k in ['lightningfury','furylightning']},sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(searchCases=1024,creatorCases=576,routerCases=96,initialAllocatorClassicCases=72,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),sharedEvidence=['tools/reference/lightning-bolt-engine-evidence.json','tools/reference/poison-javelin-factory-engine-evidence.json','tools/reference/poison-javelin-damage-engine-evidence.json','tools/reference/poison-path-engine-evidence.json'],established=['Full hit20/area/filter/callback searches around the parent missile position, in adjacent-room/list order, with squared range15 and rank+1 target cap. Counter increments after every successful callback even if allocation later fails. Primary collider is not excluded; caster/dead/friendly/unattackable/invalid/town/NOAURA recipients are excluded. Current filter0xA583 does not apply the optional owner LOS0x200 gate.','Parent206 and child231 have no ToHit; full route never calls AR. Parent hit20 runs before direct hit, survives successful passive Pierce, and is not AlwaysExplode. Child has no CollideKill or Pierce flag, keeps travelling after contacts without a passive Pierce budget, and uses post-selection LastCollide.','Complete full creator/getters produce source128 parent with weapon/item/critical/species plus skill lightning, and source0 child with retained skill rank/live hard synergy/mastery but no weapon/item/critical. Minimum1 stays unsynergized. No Lightning Bolt srvdmg12 conversion/reset applies.','Full path allocator initializes one saved origin cell and centered FP16 coordinates. Full classic missile path setup leaves it intact and stores target as its single PathPoint. The broader D2Common_10142 reconstruction is marked BROKEN, but its early missile branch delegates to this classic path; the unrelated nonmissile body is not treated as proof.'],limits=['Room adjacency/list, hostility/flags/monster metadata/LOS and allocation are declared services; full search/filter/callback execute. Native sector order still projects source room order. Full modern primary-hit/child overlap, global scheduling, complete seed/stat/item/owner/client/PvP/hireling branches require acceptance.','Full allocator/classic path run with declared allocation/type/collision-room/client/velocity services. They establish initial saved origin data, not complete modern movement/first-update scheduling or every nested vector/path side effect. Native stable path math is compared separately.','Creator and router inherit existing hash-pinned complete component harnesses with declared stat/formula/damage/AR/event services. This probe does not yet establish a native Lightning Fury handler or class completion.'])
compiled=dict(blocks)
reviewed={n:routine(rel,n) for rel,n in [(mode,'MISSMODE_SetDamageFlags'),('source/D2Game/src/UNIT/SUnitDmg.cpp','SUNITDMG_ExecuteEvents')]}
e['sourceHashes']=hashes;e['compiledRoutines']=list(compiled);e['blockHashes']=compiled;e['reviewedRoutineHashes']={k:hashlib.sha256(v.encode()).hexdigest() for k,v in reviewed.items()}
e['established'].append('Full creator source flag1 exists only on weapon parent; complete setter publishes it. Reviewed damage/event bodies use this flag to dispatch attacker/defender missile item events. Source0 Fury children have no flag1 and must not dispatch weapon item callbacks, including Crushing Blow.')
out=ROOT/'tools/reference/lightning-fury-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Lightning Fury source evidence stale'
else:out.write_text(encoded)
print('Lightning Fury:1024 full search/filter/callback,576 complete creator,96 route and72 allocator/classic path cases compiled.')
