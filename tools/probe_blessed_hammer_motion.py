#!/usr/bin/env python3
"""Execute complete Hammer path/vector/step/update routines with declared services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];CACHE=ROOT/'.local/class-skills';STAGE=CACHE/'blessed-hammer-flight/motion-source'
def build(check=False):
 tree=json.loads((CACHE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(CACHE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
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
#include <cmath>
#define __int64 long long
#include <cstdio>
#include <cassert>
#include <algorithm>
#include <limits>
#include <vector>
#include <array>
#define __fastcall
#define __stdcall
#define __forceinline inline
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
struct D2DynamicPathStrc{static constexpr int MAXPATHLEN=78,PATH_MAX_STEP_LEN=10;D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTargetUnit=nullptr;D2ActiveRoomStrc* pRoom=&room;D2FP32_16 tGameCoords;D2CoordStrc tVelocityVector,tDirectionVector;D2PathPointStrc PathPoints[78],SavedSteps[10],tFinalTargetCoord,tPrevTargetCoord;uint32_t dwFlags=0x820;int dwPathPoints=1,dwCurrentPointIdx=0,dwPathType=PATHTYPE_WALL_FOLLOW,nSavedStepsCount=0,nCollidedWithMask=0,nDist=20,dwCollisionPattern=0,nFootprintCollisionMask=0x100,nMoveTestCollisionMask=0x3c01,dwUnitSize=0,dwVelocity=1,dwMaxVelocity=1,dwAcceleration=0,dwAccelerationsCounter=0,unk0x38=0,nStepNum=0;};
struct D2MonStatsTxt{int nVelocity=12,nWalkAnimSpeed=128,nRunAnimSpeed=256;}mon;
struct D2CharStatsTxt{int nWalkSpeed=6;}chr;
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){return &mon;}D2CharStatsTxt* UNITS_GetCharStatsTxtRecord(int){return &chr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int id,int){assert(id==STAT_VELOCITYPERCENT);return u->velocityPercent;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* u,int id,int){assert(id==STAT_ITEM_FASTERMOVEVELOCITY);return u->faster;}
uint32_t PATH_ToFP16Center(int x){return (uint32_t(x)<<16)|0x8000;}uint32_t PATH_FP16FitToCenter(uint32_t x){return (x&0xffff0000)|0x8000;}int PATH_FromFP16(uint32_t x){return x>>16;}
int PATH_NormalizeDirection(int x){return x&63;}void sub_6FDA9720(D2DynamicPathStrc*,int){}
int repaths=0;int D2Common_10236(D2UnitStrc*,bool){repaths++;return 0;}
int D2Common_10399(D2UnitStrc*,D2UnitStrc*){assert(false);return 0;}void sub_6FDAB940(D2PathPointStrc*,D2DynamicPathStrc*){assert(false);}
std::vector<std::array<int,2>> queries,moveQueries;
int barrier=0,footprints=0;
struct GridCoords{int nSubtileX=80,nSubtileY=80,nSubtileWidth=48;};struct D2RoomCollisionGridStrc{GridCoords pRoomCoords;uint16_t* pCollisionMask;};uint16_t cells[48*48];D2RoomCollisionGridStrc grid{{},cells};
D2ActiveRoomStrc* COLLISION_GetRoomBySubTileCoordinates(D2ActiveRoomStrc* p,int x,int y){queries.push_back({x,y});return x>=80&&x<128&&y>=80&&y<128?p:nullptr;}D2RoomCollisionGridStrc* DUNGEON_GetCollisionGridFromRoom(D2ActiveRoomStrc*){return &grid;}
uint16_t COLLISION_CheckCollisionMaskWithAdjacentCells(D2ActiveRoomStrc*,int,int,uint16_t){assert(false);return 0;}uint16_t COLLISION_CheckCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){assert(false);return 0;}
void COLLISION_CreateBoundingBox(D2BoundingBoxStrc*,int,int,unsigned,unsigned){assert(false);}
void COLLISION_SetCollisionMask(D2ActiveRoomStrc*,int,int,uint16_t){footprints++;}void COLLISION_ResetCollisionMask(D2ActiveRoomStrc*,int,int,uint16_t){footprints++;}
void COLLISION_SetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){footprints++;}void COLLISION_ResetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc*,D2BoundingBoxStrc*,uint16_t){footprints++;}
uint16_t COLLISION_TryMoveUnitCollisionMask(D2ActiveRoomStrc*,int,int,int x,int y,int,int,int){queries.push_back({x,y});moveQueries.push_back({x,y});return barrier==3?0x100:barrier==1&&x>=104?COLLIDE_WALL:barrier==2&&y>=104?COLLIDE_MISSILE_BARRIER:0;}void COLLISION_SetUnitCollisionMask(D2ActiveRoomStrc*,int,int,D2ActiveRoomStrc*,int,int,int,int){assert(false);}
void PATH_UpdateClientCoords(D2DynamicPathStrc*){}void PATH_RecacheRoom(D2DynamicPathStrc*,D2ActiveRoomStrc*){assert(false);}
'''+tangent+'\n'+base+'\n'
 for text,name in [(collision,'COLLISION_CheckCollisionMask'),(collision,'COLLISION_CheckMaskWithPattern'),(collision,'COLLISION_SetMaskWithPattern'),(collision,'COLLISION_ResetMaskWithPattern'),(collision,'COLLISION_TryTeleportUnitCollisionMask'),(paths,'PATH_SetVelocity'),(units,'UNITS_SetAnimationSpeed'),(units,'UNITS_GetAnimStatBasePercentage'),(units,'UNITS_GetBaseAnimSpeed'),(units,'UNITS_GetBaseVelocity'),(units,'UNITS_UpdateRunWalkAnimRateAndVelocity'),(step,'PATH_GetDirectionVector'),(misc,'PATH_ComputeVelocityAndDirectionVectorsToNextPoint'),(misc,'sub_6FD5CEB0'),(misc,'sub_6FD5DB70'),(step,'sub_6FDAB810'),(step,'sub_6FDACEC0'),(step,'PATH_RecacheRoomAtCoordIfNeeded'),(step,'PATH_ResetMovement'),(misc,'D2Common_10226')]:cpp+=routine(text,name)
 # Reuse the complete actor probe's scaffold,with a declared missile-grid
 # callback replacing its previously unreachable missile-only service.
 cpp+='\nfloat FOG_10083_Cos_LUT(int a){return (float)std::cos((a&511)*std::acos(-1.0)/256); }\nfloat FOG_10084_Sin_LUT(int a){return (float)std::sin((a&511)*std::acos(-1.0)/256); }\n'
 cpp+=routine(misc,'PATH_ComputePathBlessedHammer_6FDAB3C0')
 cpp+=r'''
void points(const std::vector<std::array<int,2>>& p){printf("[");for(size_t i=0;i<p.size();i++)printf("%s[%d,%d]",i?",":"",p[i][0],p[i][1]);printf("]");}
int main(){assert(PATHTYPE_BLESSEDHAMMER==14);bool first=true;printf("[");
for(int fraction:{0,32768,57344})for(int velocity:{3456,5000,7680})for(int acceleration:{0,250,-250})for(int wall:{0,1,2,3}){
 D2UnitStrc u;D2DynamicPathStrc p;u.dwUnitType=UNIT_MISSILE;u.pDynamicPath=&p;p.pUnit=&u;p.dwFlags=PATH_UNKNOWN_FLAG_0x00020|PATH_MISSILE_MASK|PATH_SAVE_STEPS_MASK;p.dwPathType=PATHTYPE_BLESSEDHAMMER;
 p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;p.dwVelocity=velocity;p.dwMaxVelocity=7680;p.dwAcceleration=acceleration;p.dwUnitSize=1;
 barrier=wall;PATH_ComputePathBlessedHammer_6FDAB3C0(&p);PATH_ComputeVelocityAndDirectionVectorsToNextPoint(&p,TRUE,TRUE);
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d],\"path\":[",fraction,velocity,acceleration,wall);
 for(int i=0;i<p.dwPathPoints;i++)printf("%s[%d,%d]",i?",":"",int(p.PathPoints[i].X)-100,int(p.PathPoints[i].Y)-100);printf("],\"frames\":[");
 for(int frame=0;frame<120;frame++){
  queries.clear();moveQueries.clear();int moving=D2Common_10226(&u,1024);
  if(frame)printf(",");printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,",moving,int(p.tGameCoords.dwPrecisionX)-((100<<16)+fraction),int(p.tGameCoords.dwPrecisionY)-((100<<16)+fraction),p.dwPathPoints,p.dwCurrentPointIdx,p.dwVelocity,p.dwAcceleration,p.dwAccelerationsCounter,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,p.nSavedStepsCount,p.nCollidedWithMask);
  std::vector<std::array<int,2>> saved;for(int i=0;i<p.nSavedStepsCount;i++)saved.push_back({int(p.SavedSteps[i].X)-100,int(p.SavedSteps[i].Y)-100});points(saved);printf(",");points(queries);printf(",");points(moveQueries);printf("]");
 }printf("]}");
}printf("]\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);path=STAGE/'hammer-motion.cpp';path.write_text(cpp);binary=path.with_suffix('')
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr)
 assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
 rows=json.loads(result.stdout);assert len(rows)==108 and all(len(row['frames'])==120 and len(row['path'])==77 for row in rows)
 out=STAGE/'cases.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 # Preserve the same unchanged outer body's optional1.13guard as a conflict
 # control. No complete source routine is edited to force missile execution.
 guarded=STAGE/'hammer-motion-113';subprocess.run(['g++','-std=c++20','-O1','-DD2_VERSION_113C',str(path),'-o',str(guarded)],check=True)
 guarded_rows=json.loads(subprocess.check_output([str(guarded)]));assert len(guarded_rows)==108 and all(not f[0] for r in guarded_rows for f in r['frames'])
 guarded_out=STAGE/'guarded-113-cases.json';guarded_out.write_text(json.dumps(guarded_rows,separators=(',',':'))+'\n')
 # Independent velocity/counter goldens for every still-active clear path.
 for row in rows:
  v,a,c=row['input'][1],row['input'][2],0
  for frame in row['frames']:
   if not frame[0]:break
   if a:
    c+=1
    if c>=5:
     v+=a
     if v>7680:v,a=7680,0
     v=max(0,v);c=0
   assert frame[5:8]==[v,a,c],(row['input'],frame)
 evidence=dict(referenceVersion='3.3.93847',sourceCommit=tree['sha'],movementProfile='unmodified1.10branch',guarded113Profile='108contexts/allmovementrefused',legacyPathType=14,contexts=108,movementFrames=12960,
  sourceHashes=hashes,compiledRoutineHashes=blocks,
  established=['Complete unchanged Hammer77-point initializer,integer tangent direction/waypoint selection,acceleration,full collision step,room recache/reset and outer10226movement execute together across3fractional origins,3starting velocities,3accelerations and4declared collision services for120calls each.','Every frame records FP16relative position,point count/index,velocity/acceleration/counter,direction and velocity vectors,saved cell count/mask and ordered cells/collision queries. Independent velocity/counter goldens verify all moving clear-path prefixes.','Pinned enum identifies BlessedHammer path14;16is the separate missile-stream path. The earlier damage-only harness used an opaque path sentinel16with a call counter,not actual path execution.','The optional D2_VERSION_113C guard refuses all108missile contexts before the movement loop. The primary cases execute the unchanged1.10branch. This reconstruction conflict does not establish modern outer dispatch and is not used to disable moving Hammers.'],
  declaredServices=['Fog sin/cos remain reconstructed single-precision trig,not imported binary LUT/current executable evidence.','The actor-motion scaffold is reused;missile TryMove collision is a declared clear/wall/barrier callback. Room lookup is valid only in the synthetic80..127grid. Actual world/footprint allocation/client facing/target/stat services are not exercised.','The path is prepared at a supplied fractional origin/velocity/counter with save-steps enabled;modern factory/initial dispatch,source InitSteps6and lifetime/collision finder/damage/seed are separate. Additional actor/stat routines compile from the reused scaffold but their unrelated branches are not claimed as covered.'],
  artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(__file__).resolve(),path,binary,out,guarded,guarded_out,STAGE/'sanitizer.log')})
 output=ROOT/'tools/reference/blessed-hammer-motion-engine-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Hammer motion:108complete1.10path contexts/12960movement frames pass ASan/UBSan;1.13guard conflict retained;legacy path14;Fog/collision services remain explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
