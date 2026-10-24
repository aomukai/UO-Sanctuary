#!/usr/bin/env python3
"""Run the complete pinned PathWF translation unit against explicit collision grids."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 wf=source('source/D2Common/src/Path/PathWF.cpp');header=source('source/D2Common/include/Path/Path.h');collision=source('source/D2Common/src/D2Collision.cpp');collision_header=source('source/D2Common/include/D2Collision.h');assert 'static const size_t MAXPATHLEN = 78;' in header
 # Only includes are supplied by a local ABI shim; every routine/constant/array in PathWF is unchanged.
 body=re.sub(r'^#include[^\n]*\n','',wf,flags=re.M)
 def routine(name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',collision,re.M);assert m,name;i=collision.index('{',m.start())+1;depth=1
  while depth:depth+=(collision[i]=='{')-(collision[i]=='}');i+=1
  return collision[m.start():i]+'\n'
 enums='\n'.join(re.search(r'enum '+name+r'[^\{]*\{.*?\};',collision_header,re.S)[0] for name in ['D2C_CollisionPattern','D2C_CollisionMaskFlags'])
 body=routine('COLLISION_CheckAnyCollisionWithPattern')+routine('COLLISION_SetMaskWithPattern')+body

 cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <climits>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <set>
#include <vector>
#define __fastcall
#define __vectorcall
#define __stdcall
#define D2_ASSERT assert
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1;
struct D2PathPointStrc{uint16_t X=0,Y=0;bool operator==(const D2PathPointStrc& o)const{return X==o.X && Y==o.Y;}bool operator!=(const D2PathPointStrc& o)const{return !(*this==o);}int SquaredDistance(const D2PathPointStrc& o)const{int x=o.X-X,y=o.Y-Y;return x*x+y*y;}};
struct D2CoordStrc{int nX,nY;};struct D2UnitStrc{int dwUnitType=UNIT_MONSTER;}actor;
struct D2DynamicPathStrc{static const size_t MAXPATHLEN=78;int nDistMax=20;D2UnitStrc* pUnit=&actor;D2UnitStrc* pTargetUnit=nullptr;D2PathPointStrc PathPoints[MAXPATHLEN]={};}dynamic;
struct D2PathInfoStrc{D2DynamicPathStrc* pDynamicPath=&dynamic;void* pStartRoom=nullptr;int nCollisionPattern=0,nCollisionMask=0x3c01;D2PathPointStrc tStartCoord,tTargetCoord;}info;
std::set<std::pair<int,int>> blocked;std::vector<std::array<int,2>> queries;
using D2ActiveRoomStrc=void;struct D2BoundingBoxStrc{};int footprints=0;
int COLLISION_CheckMask(void*,int x,int y,uint16_t mask){assert(mask==0x3c01);queries.push_back({x,y});return blocked.contains({x,y})?mask:0;}
int COLLISION_CheckAnyCollisionWithAdjacentCells(void*,int,int,uint16_t){assert(false);return 0;}
void COLLISION_CreateBoundingBox(D2BoundingBoxStrc*,int,int,int,int){assert(false);}
int COLLISION_CheckAnyCollisionForBoundingBoxRecursively(void*,D2BoundingBoxStrc*,uint16_t){assert(false);return 0;}
void COLLISION_SetCollisionMask(void*,int,int,uint16_t){footprints++;}
void COLLISION_SetCollisionMaskForBoundingBoxRecursively(void*,D2BoundingBoxStrc*,uint16_t){footprints++;}

'''+enums+'\n'+body+r'''
void points(const std::vector<std::array<int,2>>& p){printf("[");for(size_t i=0;i<p.size();i++)printf("%s[%d,%d]",i?",":"",p[i][0],p[i][1]);printf("]");}
int main(){bool first=true;printf("{\"cases\":[");int index=0;std::vector<std::vector<std::array<int,2>>> grids;
for(int grid=0;grid<24;grid++)for(int dx:{-24,-19,-10,-3,-2,0,2,3,10,19,24})for(int dy:{-19,-3,0,3,19})for(int budget:{4,8,20,40,77})for(int targeted:{0,1}){
 dynamic={};info={};info.tStartCoord={100,100};info.tTargetCoord={uint16_t(100+dx),uint16_t(100+dy)};dynamic.nDistMax=budget;dynamic.pTargetUnit=targeted?&actor:nullptr;blocked.clear();queries.clear();
 if(grid==1)blocked.insert({103,100});
 if(grid==2)for(int y=90;y<=110;y++)blocked.insert({105,y});
 if(grid==3)for(int y=90;y<=110;y++)if(y!=103)blocked.insert({105,y});
 if(grid==4)for(int x=90;x<=110;x++)blocked.insert({x,103});
 if(grid==5)for(int x=97;x<=103;x++)for(int y=97;y<=103;y++)if(x==97||x==103||y==97||y==103)blocked.insert({x,y});
 if(grid==6)for(int y=98;y<=105;y++)blocked.insert({103,y});
 if(grid==7)for(int x=96;x<=108;x++)for(int y=95;y<=105;y++)if(y==98||y==102)blocked.insert({x,y});
 if(grid>=8){uint32_t seed=grid;for(int x=88;x<=112;x++)for(int y=88;y<=112;y++){seed=1664525u*seed+1013904223u;if(seed%100<22)blocked.insert({x,y});}blocked.erase({100,100});}
 if(grid==(int)grids.size()){std::vector<std::array<int,2>> cells;for(auto cell:blocked)cells.push_back({cell.first,cell.second});grids.push_back(cells);}
 footprints=0;COLLISION_SetMaskWithPattern(nullptr,100,100,0,0x100);assert(footprints==0);
 int result=PATH_ComputePathOrSlideAlongObstacles(&info);assert(result>=0 && result<=77);
 std::vector<std::array<int,2>> path;for(int i=0;i<result;i++)path.push_back({dynamic.PathPoints[i].X,dynamic.PathPoints[i].Y});
 printf("%s[%d,%d,%d,%d,%d,%d,",first?"":",",grid,dx,dy,budget,targeted,result);first=false;points(path);printf(",");points(queries);printf("]");index++;
}printf("],\"grids\":[");for(size_t i=0;i<grids.size();i++){if(i)printf(",");points(grids[i]);}printf("]}\n");}
'''
 p=STAGE/'wall-follow-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'wall-follow-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:];data=json.loads(result.stdout);assert len(data['cases'])==13200 and len(data['grids'])==24
 for grid,dx,dy,budget,targeted,count,path,queries in data['cases']:
  assert count==len(path) and len(path)<=77
  distance=max(abs(dx),abs(dy));actual=max(budget,40) if targeted else budget
  if distance<=2 or distance>=actual:assert count==0 and not queries
  if grid==0 and 3<=distance<actual and (dx==0 or dy==0 or abs(dx)==abs(dy)):assert path==[[100+dx,100+dy]]
 out=STAGE/'wall-follow-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,translationUnit='Complete PathWF.cpp plus complete COLLISION_CheckAnyCollisionWithPattern and COLLISION_SetMaskWithPattern, unchanged; collision enums retained from pinned header. Includes only supplied by ABI shim.',fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=13200),sanitizers='g++ -O1 -fsanitize=address,undefined -fno-sanitize-recover=all; zero diagnostic output required.',established=['Full Bresenham, paired left/right obstacle walk, subpath splice/loop/fallback and line simplification run against24 explicit grids,11 horizontal/5 vertical target offsets,budgets4/8/20/40/77 and point/unit target controls. Exact compressed path and collision query sequence are retained.','Unchanged routine rejects initial Chebyshev distances<=2 and distances>=budget; a targeted unit raises budgets below40 to40. Axis/perfect-diagonal unobstructed legal paths simplify to the target endpoint. Other routes are source comparison fixtures, not assumed A-star shortest paths.'],services=['Full collision pattern dispatcher uses pattern0 (current blade size0) and mask0x3c01 (monster path). Its final cell/room lookup remains an explicit masked grid service, not native terrain or complete room collision. Other pattern branches are compiled but not exercised. Full footprint writer with pattern0 makes zero world writes on all13200 invocations. ABI structs retain16-bit coordinates/78-point output.','D2MOO PathWF is reconstructed/annotated source. It explicitly uses INT_MAX for an empty fallback path where its comment notes original-game memory behavior differed. Exact legacy/current binary behavior at that edge remains uncertain; unmodified D2MOO execution is supplementary evidence.','Full mode/velocity/room/path initialization, movement advancement, AI scheduling, native world projection and current executable remain outside this component. No learned blade handler enabled.'])
 p=ROOT/'tools/reference/wall-follow-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Full PathWF:13200 source paths/query traces pass ASan/UBSan and bounded independent line/budget goldens.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
