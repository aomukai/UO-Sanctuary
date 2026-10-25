#!/usr/bin/env python3
"""Execute complete Phoenix chain path initializer with explicit initial direction inputs."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 parent=json.loads((ROOT/'tools/reference/martial-lightning-source-evidence.json').read_text());p=STAGE/'martial-lightning-source-probe.cpp';assert hashlib.sha256(p.read_bytes()).hexdigest()==parent['fixture']['cppSha256'];base=p.read_text().split('std::array<D2UnitStrc,80> units;',1)[0];tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={};bodies={}
 def routine(rel,name):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();bodies[name]=body;return body
 base=base.replace('struct D2DynamicPathStrc{','struct D2PathPointStrc{uint16_t X=0,Y=0;};struct D2DynamicPathStrc{D2PathPointStrc PathPoints[78];int dwPathPoints=0;')
 base+=r'''
int startDirection=0;int UNITS_GetDirectionToCoords(D2UnitStrc*,int,int){return startDirection;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->PathPoints[0].X;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->PathPoints[0].Y;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*){std::abort();}
'''
 for rel,name in [('source/D2Common/src/Monsters/Monsters.cpp','D2Common_11053'),('source/D2Common/src/Path/Path.cpp','PATH_GetPathPoints'),('source/D2Common/src/Path/Path.cpp','PATH_SetNumberOfPathPoints'),('source/D2Game/src/SKILLS/SkillAss.cpp','sub_6FCF7390')]:base+=routine(rel,name)+'\n'
 base+=r'''int main(){bool comma=false;printf("[");for(int frames:{0,1,13,59,60,77,78,98})for(int dir=0;dir<64;dir++)for(int low:{0,7,-1,2147483647})for(int origin:{10,65530}){D2UnitStrc u;D2DynamicPathStrc path;u.pDynamicPath=&path;u.x=origin;u.y=origin;u.total=u.current=frames;startDirection=dir;sub_6FCF7390(&u,low);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,[",frames,dir,low,origin,u.total,u.current,path.type,path.nDist,path.dwPathPoints,u.pSeed.nLowSeed,u.pSeed.nHighSeed);for(int i=0;i<path.dwPathPoints;i++)printf("%s[%u,%u]",i?",":"",path.PathPoints[i].X,path.PathPoints[i].Y);printf("]]");}printf("]\n");}'''
 cpp=STAGE/'phoenix-chain-init-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==4096
 def array(body,name):return [int(x) for x in re.search(name+r'\[\]\s*=\s*\{([^}]+)',body,re.S)[1].split(',') if x.strip()]
 lut=array(bodies['D2Common_11053'],'byte_6FDE4F68');dx=array(bodies['sub_6FCF7390'],'dword_6FD29668');dy=array(bodies['sub_6FCF7390'],'dword_6FD29688')
 for frames,direction,seed,origin,total,current,typ,dist,count,endLow,endHigh,points in rows:
  n=min(frames,77);assert(total,current,typ,dist,count)==(n,n,6,n,n)
  lo,hi=seed&0xffffffff,666
  def roll():
   nonlocal lo,hi
   value=hi+0x6AC690C5*lo;lo,hi=value&0xffffffff,value>>32;return lo
  sign=2*(roll()&1)-1;step=2+roll()%3;x=y=origin;expected=[]
  for i in range(n):
   direction+=sign*step;index=lut[direction&63];x+=dx[index];y+=dy[index];expected.append([x&65535,y&65535])
   if i%15==0:sign=-sign;step=2+roll()%3
  assert points==expected and(endLow,endHigh)==(lo,hi)
 fixture=STAGE/'phoenix-chain-init-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,reusedCompleteComposition='tools/reference/martial-lightning-source-evidence.json',sourceHashes=hashes,compiledAdditionalRoutines=blocks,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete sub6FCF7390caps frames>=78to77,initializes the supplied low seed/high666,sets charged path and source distance,then writes exactly one point per frame. Current Phoenix chain Range60therefore keeps60frames.','Full source direction64-to8lookup and private LCG execute. Two initial draws choose sign and2..4direction step; after point0and every15thpoint,the sign flips and one fresh step draw occurs. Final seed and every uint16path coordinate are compared,including wrap controls.','Complete path-point access/count helpers execute; precomputed paths retain origin,initial direction,child seed,points/count and source frame cap for future native reuse.'],services=['Initial direction0..63is an explicit service input. UNITS_GetDirectionToCoords/PATH_ComputeDirection,outer factory/velocity movement/collision/NextHit/packet/event/owner/client are not compiled here.','Frame accessors,type setter and first-point coordinate getters reuse bounded parent services. Complete path distance setter is inherited. Generic missile allocation aborts because this isolated initializer must not allocate.'],next='Compose the producer-selected seven chains with real factory/damage/first-point direction/movement and current4frame hit-delay before enabling Phoenix. Meteor and chaos-ice paths remain separate.')
 out=ROOT/'tools/reference/phoenix-chain-init-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix chain initializer evidence stale'
 else:out.write_text(encoded)
 print('Phoenix chain initializer:4096 complete path/LCG/frame/direction-table contexts pass;initial-direction/factory/movement/damage/native gates remain.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
