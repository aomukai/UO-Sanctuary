#!/usr/bin/env python3
"""Execute complete actor velocity/vector/step/update routines against explicit rooms."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 step=source('source/D2Common/src/Path/Step.cpp');misc=source('source/D2Common/src/Path/PathMisc.cpp');paths=source('source/D2Common/src/Path/Path.cpp');units=source('source/D2Common/src/Units/Units.cpp');collision=source('source/D2Common/src/D2Collision.cpp');header=source('source/D2Common/include/Path/Path.h');ch=source('source/D2Common/include/D2Collision.h')
 enums='\n'.join(m[0] for t in [header,ch] for m in re.finditer(r'^enum [^\n{]+\s*\{.*?^\};',t,re.M|re.S))
 tangent=step[step.index('struct TanToDirectionLutEntryStrc'):step.index('//1.00:  D2Common.0x1005E6D0')]
 base=units[units.index('struct D2UnitAnimModeStatBasePercentageStrc'):units.index('//1.10f: Inlined',units.index('D2UnitAnimModeStatBasePercentageStrc gaAnimModeStatBasePercentage'))]
 # Preserve both the record/enum and exact five stat-base table above the helper.
 if 'gaAnimModeStatBasePercentage[5]' not in base:
  end=units.index('};',units.index('D2UnitAnimModeStatBasePercentageStrc gaAnimModeStatBasePercentage'))+2;base=units[units.index('struct D2UnitAnimModeStatBasePercentageStrc'):end]
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <cassert>
#include <algorithm>
#include <limits>
#include <vector>
#include <array>
#define __fastcall
#define __stdcall
#define __forceinline inline
#define D2_VERSION_113C
#define D2_CHECK(x) assert(x)
#define D2_ASSERT(x) assert(x)
#define D2_MAYBE_UNUSED(x) (void)(x)
#define ARRAY_SIZE(x) (sizeof(x)/sizeof(x[0]))
#define HIWORD(x) ((uint16_t)((uint32_t)(x)>>16))
using BOOL=int;using WORD=uint16_t;using DWORD=uint32_t;constexpr int FALSE=0,TRUE=1,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,MONMODE_RUN=15,MONMODE_WALK=2,PLRMODE_RUN=3,UNITFLAG_ISASYNC=1<<23;
constexpr int STAT_ITEM_FASTERATTACKRATE=1,STAT_ITEM_FASTERGETHITRATE=2,STAT_ITEM_FASTERCASTRATE=3,STAT_ITEM_FASTERBLOCKRATE=4,STAT_ITEM_FASTERMOVEVELOCITY=5,STAT_VELOCITYPERCENT=6;
template<class T>T D2Clamp(T x,T a,T b){return std::clamp(x,a,b);}void FOG_Trace(const char*,...){}
'''+enums+r'''
struct D2ActiveRoomStrc{} room;struct D2BoundingBoxStrc{};
struct D2PathPointStrc{uint16_t X=0,Y=0;bool operator==(const D2PathPointStrc&)const=default;};
struct D2FP32_16{union{uint32_t dwPrecisionX=0;struct{uint16_t fracX,wPosX;};};union{uint32_t dwPrecisionY=0;struct{uint16_t fracY,wPosY;};};D2PathPointStrc ToPathPoint()const{return {wPosX,wPosY};}};
struct D2CoordStrc{int nX=0,nY=0;};struct D2DynamicPathStrc;
struct D2UnitStrc{int dwUnitType=UNIT_MONSTER,dwClassId=413,dwFlags=0;void* pStatListEx=(void*)1;D2DynamicPathStrc* pDynamicPath=nullptr;int velocityPercent=75,faster=0;int16_t wAnimSpeed=0;};
struct D2DynamicPathStrc{static constexpr int PATH_MAX_STEP_LEN=10;D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTargetUnit=nullptr;D2ActiveRoomStrc* pRoom=&room;D2FP32_16 tGameCoords;D2CoordStrc tVelocityVector,tDirectionVector;D2PathPointStrc PathPoints[78],SavedSteps[10],tFinalTargetCoord,tPrevTargetCoord;uint32_t dwFlags=0x820;int dwPathPoints=1,dwCurrentPointIdx=0,dwPathType=PATHTYPE_WALL_FOLLOW,nSavedStepsCount=0,nCollidedWithMask=0,nDist=20,dwCollisionPattern=0,nFootprintCollisionMask=0x100,nMoveTestCollisionMask=0x3c01,dwUnitSize=0,dwVelocity=1,dwMaxVelocity=1,dwAcceleration=0,dwAccelerationsCounter=0,unk0x38=0,nStepNum=0;};
struct D2MonStatsTxt{int nVelocity=12,nWalkAnimSpeed=128,nRunAnimSpeed=256;}mon;
struct D2CharStatsTxt{int nWalkSpeed=6;}chr;
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){return &mon;}D2CharStatsTxt* UNITS_GetCharStatsTxtRecord(int){return &chr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int id,int){assert(id==STAT_VELOCITYPERCENT);return u->velocityPercent;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* u,int id,int){assert(id==STAT_ITEM_FASTERMOVEVELOCITY);return u->faster;}
uint32_t PATH_ToFP16Center(int x){return (uint32_t(x)<<16)|0x8000;}uint32_t PATH_FP16FitToCenter(uint32_t x){return (x&0xffff0000)|0x8000;}int PATH_FromFP16(uint32_t x){return x>>16;}
int PATH_NormalizeDirection(int x){return x&63;}void sub_6FDA9720(D2DynamicPathStrc*,int){}
int repaths=0;int D2Common_10236(D2UnitStrc*,bool){repaths++;return 0;}
int D2Common_10399(D2UnitStrc*,D2UnitStrc*){assert(false);return 0;}void sub_6FDAB940(D2PathPointStrc*,D2DynamicPathStrc*){assert(false);}
std::vector<std::array<int,2>> queries;
int barrier=0,footprints=0;
struct GridCoords{int nSubtileX=80,nSubtileY=80,nSubtileWidth=48;};struct D2RoomCollisionGridStrc{GridCoords pRoomCoords;uint16_t* pCollisionMask;};uint16_t cells[48*48];D2RoomCollisionGridStrc grid{{},cells};
D2ActiveRoomStrc* COLLISION_GetRoomBySubTileCoordinates(D2ActiveRoomStrc* p,int x,int y){queries.push_back({x,y});return x>=80&&x<128&&y>=80&&y<128?p:nullptr;}D2RoomCollisionGridStrc* DUNGEON_GetCollisionGridFromRoom(D2ActiveRoomStrc*){return &grid;}
uint16_t COLLISION_CheckCollisionMaskWithAdjacentCells(D2ActiveRoomStrc*,int,int,uint16_t){assert(false);return 0;}uint16_t COLLISION_CheckCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){assert(false);return 0;}
void COLLISION_CreateBoundingBox(D2BoundingBoxStrc*,int,int,unsigned,unsigned){assert(false);}
void COLLISION_SetCollisionMask(D2ActiveRoomStrc*,int,int,uint16_t){footprints++;}void COLLISION_ResetCollisionMask(D2ActiveRoomStrc*,int,int,uint16_t){footprints++;}
void COLLISION_SetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){footprints++;}void COLLISION_ResetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){footprints++;}
uint16_t COLLISION_TryMoveUnitCollisionMask(D2ActiveRoomStrc*,int,int,int,int,int,int,int){assert(false);return 0;}void COLLISION_SetUnitCollisionMask(D2ActiveRoomStrc*,int,int,D2ActiveRoomStrc*,int,int,int,int){assert(false);}
void PATH_UpdateClientCoords(D2DynamicPathStrc*){}void PATH_RecacheRoom(D2DynamicPathStrc*,D2ActiveRoomStrc*){assert(false);}
'''+tangent+'\n'+base+'\n'
 for text,name in [(collision,'COLLISION_CheckCollisionMask'),(collision,'COLLISION_CheckMaskWithPattern'),(collision,'COLLISION_SetMaskWithPattern'),(collision,'COLLISION_ResetMaskWithPattern'),(collision,'COLLISION_TryTeleportUnitCollisionMask'),(paths,'PATH_SetVelocity'),(units,'UNITS_SetAnimationSpeed'),(units,'UNITS_GetAnimStatBasePercentage'),(units,'UNITS_GetBaseAnimSpeed'),(units,'UNITS_GetBaseVelocity'),(units,'UNITS_UpdateRunWalkAnimRateAndVelocity'),(step,'PATH_GetDirectionVector'),(misc,'PATH_ComputeVelocityAndDirectionVectorsToNextPoint'),(misc,'sub_6FD5CEB0'),(misc,'sub_6FD5DB70'),(step,'sub_6FDAB810'),(step,'sub_6FDACEC0'),(step,'PATH_RecacheRoomAtCoordIfNeeded'),(step,'PATH_ResetMovement'),(misc,'D2Common_10226')]:cpp+=routine(text,name)
 cpp+=r'''
void points(const std::vector<std::array<int,2>>& p){printf("[");for(size_t i=0;i<p.size();i++)printf("%s[%d,%d]",i?",":"",p[i][0],p[i][1]);printf("]");}
int main(){bool first=true;printf("[");for(int route=0;route<6;route++)for(int fraction:{0,32768,57344})for(int percent:{0,25,75,100,150})for(int faster:{0,50})for(int wall:{0,1,2})for(int accel:{0,-256,256})for(int save:{0,1}){
 D2UnitStrc u;D2DynamicPathStrc p;u.pDynamicPath=&p;p.pUnit=&u;u.velocityPercent=percent;u.faster=faster;p.dwFlags|=save?0x20000:0;p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;
 std::vector<std::array<int,2>> routePoints;if(route>=4)routePoints={{108,100}};if(route==0)routePoints={{108,100}};if(route==1)routePoints={{92,100}};if(route==2)routePoints={{108,95}};if(route==3)routePoints={{100,100},{103,102},{103,102},{106,99}};
 p.dwPathPoints=routePoints.size();for(size_t i=0;i<routePoints.size();i++)p.PathPoints[i]={uint16_t(routePoints[i][0]),uint16_t(routePoints[i][1])};p.tFinalTargetCoord=p.PathPoints[p.dwPathPoints-1];
 std::fill(std::begin(cells),std::end(cells),0);if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;if(wall==2)for(int x=80;x<128;x++)cells[22*48+x-80]=0x100;
 UNITS_UpdateRunWalkAnimRateAndVelocity(&u,MONMODE_WALK,UNIT_MONSTER,413,__FILE__,__LINE__);p.dwAcceleration=accel;PATH_ComputeVelocityAndDirectionVectorsToNextPoint(&p,1,1);
 if(route>=4){p.dwVelocity=p.dwMaxVelocity;p.dwCurrentPointIdx=p.dwPathPoints;p.tFinalTargetCoord=route==4?D2PathPointStrc{120,100}:p.tGameCoords.ToPathPoint();}
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d,%d,%d,%d],\"velocity\":%d,\"animation\":%d,\"path\":",route,fraction,percent,faster,wall,accel,save,p.dwMaxVelocity,u.wAnimSpeed);points(routePoints);printf(",\"frames\":[");
 for(int frame=0;frame<20;frame++){queries.clear();footprints=0;repaths=0;int moving=D2Common_10226(&u,1024);assert(footprints==0);if(frame)printf(",");printf("[%d,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,",moving,p.tGameCoords.dwPrecisionX,p.tGameCoords.dwPrecisionY,p.dwFlags,p.dwPathPoints,p.dwCurrentPointIdx,p.dwVelocity,p.dwMaxVelocity,p.dwAcceleration,p.dwAccelerationsCounter,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,p.nDist,p.nSavedStepsCount,p.nCollidedWithMask,repaths);points(queries);printf(",");std::vector<std::array<int,2>> saved;if(save)for(int i=0;i<p.nSavedStepsCount;i++)saved.push_back({p.SavedSteps[i].X,p.SavedSteps[i].Y});points(saved);printf("]");}
 printf("]}");}printf("]\n");}
'''
 p=STAGE/'actor-motion-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'actor-motion-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
 data=json.loads(result.stdout);assert len(data)==3240 and all(len(x['frames'])==20 for x in data)
 for x in data:
  _,_,percent,faster,_,_,_=x['input'];effective=max(25,percent+(150*faster//(150+faster) if faster else 0));assert x['velocity']==effective*3072//100 and x['animation']==effective*128//100
 fixture=STAGE/'actor-motion-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=blocks,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),contexts=3240,frameComparisons=64800),sanitizers='ASan/UBSan, zero stderr and successful exit required',established=['Complete actor run/walk velocity updater, stat-base conversion, base-speed/velocity getters, direction lookup/waypoint selection, acceleration, path availability, full collision step and outer10226 update run together for four routes plus two explicit path-exhaustion/repath controls, duplicate points, three fractional starts, five velocity percentages, two faster-move values, three collision grids, three accelerations and both saved-step flag states.','Complete pattern0 check/teleport/footprint routines use a declared valid room/grid lookup. No source footprint writes occur. Exact FP16 position, point count/index, flags, speed/acceleration/vector/distance/collision/step state and collision query order are retained every frame.','Default base12/75percent/no faster move gives FP8 velocity2304 and axial FP16 advance36864 per moving update before waypoint clamp; independent Python velocity/animation goldens match all contexts.'],services=['Current monstats base velocity12 is supplied with synthetic base animation speed128 (spatial velocity is independent). Item/skill faster-move and velocity-percent stats are explicit inputs, not a live actor stat collector. Full top-level mode selector and current animation-rate acceptance remain separate.','Uses1.13 optimization with server-style monster flags (no0x10/no ISASYNC), point targets and path15. The source server allocator passes0 to PATH_AllocDynamicPath bSetFlag; initialization/mode selection remains separately reviewed. Other path types/target-unit contexts are outside this component.','Repath callback is declared to fail and the exhausted-path controls verify its call/no-call boundary; target-unit distance/target update and invalid footprint branches assert if reached. Valid room/grid lookup, client position output and facing/turn consumer are declared services. General path initialization, native world/height/presence projection, AI event schedule and current executable remain separate.','The original unused saved-step count-to-pointer casts produce64-bit compiler warnings; those pointers are never dereferenced. No source body was repaired for those warnings. This source proof does not enable a learned Blade Sentinel handler or claim production actor persistence.'])
 p=ROOT/'tools/reference/actor-motion-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Actor motion:3240 contexts/64800 full velocity/vector/collision/update frames pass ASan/UBSan and independent speed goldens.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
