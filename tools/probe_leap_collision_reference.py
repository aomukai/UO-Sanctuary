#!/usr/bin/env python3
"""Compile source free-cell order and collision-presence reservation helpers."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=next(v['sha'] for v in tree['tree'] if v['path']==path)
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]+'\n'
collision=source('source/D2Common/src/D2Collision.cpp');paths=source('source/D2Common/src/Path/Path.cpp')
assert re.search(r'gaCollisionPatternsFromSize_6FDD1DE4\[.*?\].*?COLLISION_PATTERN_NONE,\s*COLLISION_PATTERN_SMALL_UNIT_PRESENCE,\s*COLLISION_PATTERN_SMALL_UNIT_PRESENCE,',paths,re.S)
cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <map>
#include <utility>
#define __stdcall
#define __fastcall
#define D2_ASSERT(x) if(!(x))std::abort()
using BOOL=int;
constexpr int COLLISION_PATTERN_NONE=0,COLLISION_PATTERN_SMALL_UNIT_PRESENCE=1,COLLISION_PATTERN_BIG_UNIT_PRESENCE=2,
 COLLISION_PATTERN_SMALL_PET_PRESENCE=3,COLLISION_PATTERN_BIG_PET_PRESENCE=4,COLLISION_PATTERN_SMALL_NO_PRESENCE=5,
 COLLIDE_NO_PATH=0x1000,COLLIDE_PET=0x2000;
struct D2ActiveRoomStrc{} room;
struct D2CoordStrc{int nX=100,nY=100;};
struct D2FieldStrc{};
struct D2BoundingBoxStrc{int nLeft=0,nRight=0,nBottom=0,nTop=0;};
int rule=0,wantedX=0,wantedY=0,calls=0;
uint64_t trace=14695981039346656037ull;
std::map<std::pair<int,int>,uint16_t> grid;
D2FieldStrc* DATATBLS_AllocField(){return new D2FieldStrc;}
void DATATBLS_SetFieldCoordinates(D2FieldStrc*,int,int){}
void DATATBLS_FreeField(D2FieldStrc* p){delete p;}
bool D2Common_11099(D2FieldStrc*,D2ActiveRoomStrc*,int,int,unsigned){return false;}
D2ActiveRoomStrc* COLLISION_GetRoomBySubTileCoordinates(D2ActiveRoomStrc* p,int,int){return p;}
bool COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int x,int y,int size,unsigned mask){
 if(size!=2||mask!=0x1C09)std::abort();calls++;trace^=uint32_t(x);trace*=1099511628211ull;trace^=uint32_t(y);trace*=1099511628211ull;
 int dx=x-100,dy=y-100;bool clear=rule==0 || rule==2&&dx==wantedX&&dy==wantedY || rule==3&&((dx+dy)&1)!=0 || rule==4&&(std::abs(dx)==wantedX||std::abs(dy)==wantedY);
 return !clear;
}
void COLLISION_SetCollisionMask(D2ActiveRoomStrc* p,int x,int y,uint16_t mask){if(p)grid[{x,y}]|=mask;}
void COLLISION_ResetCollisionMask(D2ActiveRoomStrc* p,int x,int y,uint16_t mask){if(p)grid[{x,y}]&=~mask;}
void COLLISION_CreateBoundingBox(D2BoundingBoxStrc* p,int x,int y,int width,int height){p->nLeft=x-width/2;p->nRight=x+width/2;p->nBottom=y-height/2;p->nTop=y+height/2;}
void COLLISION_SetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc* p,D2BoundingBoxStrc* box,uint16_t mask){for(int x=box->nLeft;x<=box->nRight;x++)for(int y=box->nBottom;y<=box->nTop;y++)COLLISION_SetCollisionMask(p,x,y,mask);}
void COLLISION_ResetCollisionMaskForBoundingBoxRecursively(D2ActiveRoomStrc* p,D2BoundingBoxStrc* box,uint16_t mask){for(int x=box->nLeft;x<=box->nRight;x++)for(int y=box->nBottom;y<=box->nTop;y++)COLLISION_ResetCollisionMask(p,x,y,mask);}
'''
for name in ['COLLISION_GetFreeCoordinatesImpl','COLLISION_SetMaskWithPattern','COLLISION_ResetMaskWithPattern']:cpp+=routine(collision,name)
cpp+=r'''int main(){bool first=true;std::printf("{\"freeCases\":[");
for(int test=0;test<5;test++)for(int dx:{-49,-7,-2,-1,0,1,2,7,49})for(int dy:{-49,-7,-2,-1,0,1,2,7,49}){
 rule=test;wantedX=dx;wantedY=dy;calls=0;trace=14695981039346656037ull;D2CoordStrc point;
 auto* found=COLLISION_GetFreeCoordinatesImpl(&room,&point,nullptr,2,0x1C09,0x1C09,1,50,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,\"%llu\"]",test,dx,dy,found?1:0,point.nX,point.nY,calls,(unsigned long long)trace);
}
std::printf("],\"presenceCases\":[");first=true;
for(int present:{0,1})for(int pattern=0;pattern<6;pattern++)for(int mask:{0,0x80,0x100,0x2000,0x8080}){
 grid.clear();for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)grid[{x,y}]=0x408;
 auto* p=present?&room:nullptr;COLLISION_SetMaskWithPattern(p,0,0,pattern,mask);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,[",present,pattern,mask);
 bool cellFirst=true;for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){if(!cellFirst)std::printf(",");cellFirst=false;std::printf("%u",grid[{x,y}]);}
 COLLISION_ResetMaskWithPattern(p,0,0,pattern,mask);std::printf("],[");cellFirst=true;
 for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){if(!cellFirst)std::printf(",");cellFirst=false;std::printf("%u",grid[{x,y}]);}
 std::printf("]]");
}std::printf("]}\n");}
'''
p=STAGE/'leap-collision-source-probe.cpp';p.write_text(cpp);binary=STAGE/'leap-collision-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
def free_case(rule,dx,dy):
 trace=14695981039346656037;calls=0
 def clear(x,y):
  nonlocal trace,calls
  calls+=1
  for value in (100+x,100+y):trace=((trace^(value&0xffffffff))*1099511628211)&0xffffffffffffffff
  return rule==0 or rule==2 and (x,y)==(dx,dy) or rule==3 and (x+y)%2!=0 or rule==4 and (abs(x)==dx or abs(y)==dy)
 if clear(0,0):return (100,100,calls,str(trace))
 for radius in range(1,50):
  best=None;score=100000
  cells=[(x,y) for y in range(-radius,radius+1) for x in (-radius,radius)]
  cells += [(x,y) for x in range(1-radius,radius) for y in (-radius,radius)]
  for x,y in cells:
   if clear(x,y) and abs(x)+abs(y)<score:best=(100+x,100+y);score=abs(x)+abs(y)
  if best:return (*best,calls,str(trace))
 return (100,100,calls,str(trace))
for rule,dx,dy,result,x,y,calls,trace in data['freeCases']:
 assert result==1 and (x,y,calls,trace)==free_case(rule,dx,dy),(rule,dx,dy,result,x,y,calls,trace)
for present,pattern,mask,after,reset in data['presenceCases']:
 expected=[]
 for x in range(-2,3):
  for y in range(-2,3):
   cross=abs(x)+abs(y)<=1;footprint=cross if pattern in (1,3,5) else abs(x)<=1 and abs(y)<=1 if pattern in (2,4) else False
   extra=0x1000 if pattern in (1,2) else 0x2000
   presence=(x==0 and y==0) if pattern in (1,3) else cross if pattern in (2,4) else False
   expected.append(0x408 | (mask if present and footprint else 0) | (extra if present and mask and presence else 0))
 assert after==expected and reset==[0x408]*25,(present,pattern,mask)
outpath=STAGE/'leap-collision-source-probe.json';outpath.write_text(json.dumps(data,indent=2)+'\n')
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},totalCases=sum(map(len,data.values())),dataPath=str(outpath.relative_to(ROOT)),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()}),established=[
 'Leap free-cell search with size2/mask1C09, neighbor rooms allowed, field absent, max50/increment1 checks the original then square rings1..49. On each ring it chooses minimum Manhattan distance, retaining source vertical-edge-before-horizontal-edge tie order.',
 'If no cell is clear, neighbor-room mode can still return the original blocked coordinates/room. The Leap finder must perform its independent final clearance/flying/range checks.',
 'Player size1/2 uses small unit-presence pattern. Setting its nonzero destination mask80 also sets NO_PATH1000 at the center; player/monster movement masks include NO_PATH. Reset removes both. This establishes movement blocking beyond the raw80 bit, supporting the native collision reservation adapter.'
],limits=[
 'Three unchanged legacy routines compiled with declared same-room collision/field/grid services. Field-bearing/neighbor-room geometry and arbitrary size/increment configurations are outside this Leap contract.',
 'Native body/footprint projection, real terrain, door and spawn behavior require their own native checks; the source bit-grid fixture is not complete modern room-engine proof.'
])
(ROOT/'tools/reference/leap-collision-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n')
print('Leap free-cell/presence source cases:',out['probe']['totalCases'])
