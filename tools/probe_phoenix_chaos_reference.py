#!/usr/bin/env python3
"""Full chaos-ice do35, elapsed-frame getter, target setters and private source LCG."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def routine(rel,name):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
 base=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
constexpr int64_t operator""i64(unsigned long long x){return int64_t(x);}
constexpr int UNIT_MISSILE=3;
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};D2SeedStrc():lSeed(0){}};
struct D2MissileDataStrc{int nTotalFrames=40,nCurrentFrame=40;struct{int nX=0,nY=0;}pTargetCoords;};
struct D2DynamicPathStrc{int x=0,y=0,builds=0;};struct D2UnitStrc{int dwUnitType=3,dwClassId=569,x=40,y=70;D2SeedStrc pSeed;D2MissileDataStrc* pMissileData=nullptr;D2DynamicPathStrc* pDynamicPath=nullptr;};struct D2GameStrc{};struct D2MissilesTxt{int dwParam[1]={3};}row;
bool record=true;int collisions=0;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return record?&row:nullptr;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
uint32_t SEED_GetLowSeed(D2SeedStrc* p){return p->nLowSeed;}
void D2COMMON_10170_PathSetTargetPos(D2DynamicPathStrc* p,int x,int y){p->x=x;p->y=y;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int){p->builds++;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;return 1;}
'''
 for rel,name in [('source/D2Common/include/D2Seed.h','SEED_RollRandomNumber'),('source/D2Common/src/D2Seed.cpp','SEED_InitLowSeed'),('source/D2Game/src/ITEMS/Items.cpp','ITEMS_RollRandomNumber')]:base+=routine(rel,name)+'\n'
 rel='source/D2Common/src/Units/Missile.cpp'
 for name in ['MISSILE_GetRemainingFrames','MISSILE_GetTargetX','MISSILE_GetTargetY','MISSILE_SetTargetX','MISSILE_SetTargetY']:base+=routine(rel,name)+'\n'
 base+=routine('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvDo35_RoyalStrikeChaosIce')+r'''
int main(){D2GameStrc game;bool comma=false;printf("[");for(int elapsed:{0,1,2,3,4,37,38,39})for(int interval:{-1,1,3})for(int x:{-20,-1,0,1,19})for(int y:{-20,-1,0,1,19})for(int low:{0,7,-1})for(int guard:{0,1,2}){D2UnitStrc u;D2MissileDataStrc data;D2DynamicPathStrc path;u.pMissileData=&data;u.pDynamicPath=&path;data.nCurrentFrame=40-elapsed;data.pTargetCoords.nX=low;data.pTargetCoords.nY=int(uint32_t(uint16_t(x))+(uint32_t(uint16_t(y))<<16));SEED_InitLowSeed(&u.pSeed,123);row.dwParam[0]=interval;record=guard!=2;collisions=0;int result=MISSMODE_SrvDo35_RoyalStrikeChaosIce(&game,guard==0?nullptr:&u);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u]",elapsed,interval,x,y,low,guard,result,collisions,path.builds,path.x,path.y,data.pTargetCoords.nX,data.pTargetCoords.nY,u.pSeed.nLowSeed,u.pSeed.nHighSeed);}printf("]\n");}
'''
 cpp=STAGE/'phoenix-chaos-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==5400
 def signed(v):return (v+2**31)%2**32-2**31
 def trunc(v):return v//4 if v>=0 else -((-v)//4)
 carry=0
 for elapsed,interval,x,y,low,guard,result,collisions,builds,tx,ty,targetSeed,packed,seedLow,seedHigh in rows:
  valid=guard==1;active=valid and elapsed%max(interval,1)==0;expectedPacked=signed((x&65535)+((y&65535)<<16));lo,hi=123,666;ex,ey=x,y
  if active:
   n=666+0x6AC690C5*(low&0xffffffff);lo,hi=n&0xffffffff,n>>32
   flipx=-x if lo&1 else x;flipy=y if lo&1 else -y;ex=trunc(flipy+4*x) or 1;ey=trunc(flipx+4*y) or 1;expectedPacked=signed(ex+(ey<<16));carry+=int(ex<0)
  assert(result,collisions,builds,tx,ty,targetSeed,packed,seedLow,seedHigh)==(1 if valid else 2,int(valid),int(active),40+ex if active else 0,70+ey if active else 0,signed(lo) if active else low,expectedPacked,lo,hi)
 assert carry>0
 fixture=STAGE/'phoenix-chaos-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,sourceHashes=hashes,compiledRoutines=blocks,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),negativeXPackingContexts=carry,sanitizers='ASan+UBSan'),established=['The complete misleadingly named MISSILE_GetRemainingFrames returns total-current,which is elapsed frames under the countdown caller. Full do35therefore rebuilds at elapsed0,3,6,...with current Param1=3,including the very first update,before collision/movement.','Full do35reseeds from saved targetX(high666),draws once to choose which signed vector component flips,and computes truncated quarter-rotated offsets. Each zero component becomes1. Full target setters retain new low seed and the literal signed-add packed vector.','Negative X packing carries into the high signed Y half. The literal older source behavior is preserved in this oracle;current executable parity is explicitly unverified. Native code must label this source-derived behavior rather than silently normalize packed pairs.'],services=['World position and path-target/rebuild/collision callbacks are explicit services;full do35/elapsed getter/target getters-setters/LCG execute. No full path movement,health,NextHit,freeze,owner/world/client certificate.','Valid source unit data/table contexts are supplied. Null missile/missing record are controlled guards of do35before the collision service;the unrelated malformed generic missing-record router remains excluded.'],next='Port literal elapsed-frame/seed/vector transform with explicit current-build packing caveat. Compose source freeze-only damage2,current40frame movement/collision and native saved child lifecycle before enabling Phoenix.')
 out=ROOT/'tools/reference/phoenix-chaos-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix chaos evidence stale'
 else:out.write_text(encoded)
 print('Phoenix chaos:5400 complete curve/elapsed/getter/setter/LCG contexts pass;first update uses elapsed0;packing uncertainty remains explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
