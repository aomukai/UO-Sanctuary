#!/usr/bin/env python3
"""Execute complete path step/update bodies that gate poison trail creation."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
def source(rel):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(rel,name):
 text=source(rel);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
cpp=r'''#include <cstdint>
#include <cstdio>
#include <cassert>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_CHECK(x) assert(x)
#define D2_MAYBE_UNUSED(x) (void)(x)
#define HIWORD(x) ((uint16_t)((uint32_t)(x)>>16))
using BOOL=int;using WORD=uint16_t;constexpr int FALSE=0,TRUE=1,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3;
'''
for rel in ['source/D2Common/include/Path/Path.h','source/D2Common/include/D2Collision.h']:
 for m in re.finditer(r'^enum [^\n{]+\s*\{.*?^\};',source(rel),re.M|re.S):cpp+=m.group()+'\n'
cpp+=r'''
struct D2ActiveRoomStrc{};struct D2PathPointStrc{uint16_t X=0,Y=0;bool operator==(const D2PathPointStrc&)const=default;};
struct D2FP32_16{union{uint32_t dwPrecisionX=0;struct{uint16_t fracX,wPosX;};};union{uint32_t dwPrecisionY=0;struct{uint16_t fracY,wPosY;};};};
struct D2CoordStrc{int nX=0,nY=0;};struct D2DynamicPathStrc;struct D2UnitStrc{int dwUnitType=3,dwFlags=0;D2DynamicPathStrc* pDynamicPath=nullptr;};
struct D2DynamicPathStrc{static constexpr int PATH_MAX_STEP_LEN=10;D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTargetUnit=nullptr;D2ActiveRoomStrc* pRoom=nullptr;D2FP32_16 tGameCoords;D2CoordStrc tVelocityVector;D2PathPointStrc PathPoints[10],SavedSteps[10];int dwFlags=0,dwPathPoints=1,dwCurrentPointIdx=0,dwPathType=PATHTYPE_MISSILE,nSavedStepsCount=0,nCollidedWithMask=0,nDist=0,dwCollisionPattern=0,nFootprintCollisionMask=0,nMoveTestCollisionMask=0,dwUnitSize=1,dwVelocity=1;};
int wall=0;uint32_t PATH_ToFP16Center(int x){return (x<<16)|0x8000;}uint32_t PATH_FP16FitToCenter(uint32_t x){return (x&0xffff0000)|0x8000;}int PATH_FromFP16(uint32_t x){return x>>16;}
int D2Common_10236(D2UnitStrc*,bool){return 0;}void COLLISION_SetUnitCollisionMask(D2ActiveRoomStrc*,int,int,D2ActiveRoomStrc*,int,int,int,int){}
uint16_t COLLISION_TryMoveUnitCollisionMask(D2ActiveRoomStrc*,int,int,int,int,int,int,int){return wall;}uint16_t COLLISION_TryTeleportUnitCollisionMask(D2ActiveRoomStrc*,int,int,int,int,int,int,int){return wall;}
D2ActiveRoomStrc* COLLISION_GetRoomBySubTileCoordinates(D2ActiveRoomStrc* p,int,int){return p;}void PATH_UpdateClientCoords(D2DynamicPathStrc*){}void PATH_RecacheRoom(D2DynamicPathStrc*,D2ActiveRoomStrc*){}
bool sub_6FD5DB70(D2DynamicPathStrc*){return true;}void sub_6FD5CEB0(D2DynamicPathStrc*,int){}void PATH_ComputeVelocityAndDirectionVectorsToNextPoint(D2DynamicPathStrc*,int,int){}
'''
step='source/D2Common/src/Path/Step.cpp';misc='source/D2Common/src/Path/PathMisc.cpp'
for rel,name in [(step,'sub_6FDAB810'),(step,'sub_6FDACEC0'),(step,'PATH_RecacheRoomAtCoordIfNeeded'),(step,'PATH_ResetMovement'),(misc,'D2Common_10226')]:cpp+=routine(rel,name)+'\n'
cpp+=r'''int main(){D2ActiveRoomStrc room;D2UnitStrc missile;D2DynamicPathStrc path;bool first=true;std::printf("[");
for(int fraction:{0,8192,32768,57344})for(int vx:{-131072,-65536,-32768,0,32768,65536,131072})for(int vy:{-65536,0,65536})for(int barrier:{0,1,4}){
 path={};path.pUnit=&missile;missile.pDynamicPath=&path;path.pRoom=&room;path.dwFlags=PATH_MISSILE_MASK|PATH_UNKNOWN_FLAG_0x00020|0x20000|8;path.tGameCoords.dwPrecisionX=(100<<16)+fraction;path.tGameCoords.dwPrecisionY=(100<<16)+fraction;path.tVelocityVector={vx,vy};wall=barrier;
 int result=D2Common_10226(&missile,1024);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%d]",fraction,vx,vy,barrier,result,int(bool(path.dwFlags&8)),path.nSavedStepsCount,path.nCollidedWithMask,path.tGameCoords.dwPrecisionX,path.tGameCoords.dwPrecisionY,path.dwCurrentPointIdx);
}std::printf("]\n");}
'''
p=STAGE/'poison-path-source-probe.cpp';p.write_text(cpp);binary=STAGE/'poison-path-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==252
for fraction,vx,vy,wall,result,flag,steps,collision,x,y,index in data:
 if not vx and not vy:assert(result,flag)==(0,0)
 elif collision and steps==0:assert(result,flag)==(0,0)
 elif not collision:
  assert result==1 and flag==int(steps>0)
  assert(x,y)==((100<<16)+fraction+vx,(100<<16)+fraction+vy)
fixture=STAGE/'poison-path-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete10226 clears path-change flag0x8 before each update. Complete movement step sets it only after saved integer-subtile crossings reach its normal completion path. Subtile-fraction movement can leave flag0; several crossed steps still yield one flag for the later do2 creator.','Full step includes its vector downshifts, saved-step loop and wall/missile-barrier early return. Early barrier return can leave saved steps without setting path-change0x8; full wrapper resets stopped movement. Current source rooms and client coordinates are committed by the full recache helper.','Existing complete do2 creates at previous coordinates/flag before this next update. Therefore native integration must retain previous path-change state rather than spawning a cloud per current swept cell.'],limits=['Stable input velocity vectors, collision responses and valid single-room lookup are declared services. Acceleration/direction solver, complete path initialization/initial flag, room transitions, outer global scheduling, actual source finder and modern executable equivalence remain separate.','These252 source movement cases are not native combat geometry acceptance. Collision footprint/path interpolation and initial cloud policy still require explicit integration; no class handler is enabled.'])
reviewed={}
for name in ['PATH_AllocDynamicPath','PATH_SetType','PATH_ComputePathClassicMissile','PATH_MissileToTarget']:
 body=routine('source/D2Common/src/Path/Path.cpp',name);reviewed[name]=hashlib.sha256(body.encode()).hexdigest()
e['reviewedInitialization']={'blockHashes':reviewed,'compiled':False,'finding':'Read-only factory/allocator review: SUNIT_AllocUnitData missile branch calls calloc-backed PATH_AllocDynamicPath. PATH_SetType missile flag table entry4 is0x60000; PATH_MissileToTarget sets0x20 after classic path calculation. These reviewed bodies do not deliberately set0x8 on creation. Verify remaining position/target/velocity setters and full initialization before declaring first-cloud behavior; do not substitute an always-on initial trail.'}
e['blockHashes']={name:e['blockHashes'][name] for name in e['compiledRoutines']}
out=ROOT/'tools/reference/poison-path-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison path evidence stale'
else:out.write_text(encoded)
print('Poison path:252 complete movement/update/flag/collision cases compiled; initial factory state and native geometry remain explicit.')
