#!/usr/bin/env python3
"""Compose full Phoenix chain initializer with source direction lookup and unit-coordinate helpers."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 parent=json.loads((ROOT/'tools/reference/phoenix-chain-init-source-evidence.json').read_text());path=STAGE/'phoenix-chain-init-source-probe.cpp';assert hashlib.sha256(path.read_bytes()).hexdigest()==parent['fixture']['cppSha256'];base=path.read_text().split('int main()',1)[0];tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(rel,name):
  s=source(rel);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
 old='int UNITS_GetDirectionToCoords(D2UnitStrc*,int,int){return startDirection;}';assert base.count(old)==1;base=base.replace(old,'int UNITS_GetDirectionToCoords(D2UnitStrc*,int,int);')
 base+=r'''using DWORD=uint32_t;constexpr bool TRUE=true,FALSE=false;
struct D2CoordStrc{int nX=0,nY=0;};struct TanToDirectionLutEntryStrc{D2CoordStrc dirVector;uint32_t nAngle;};
void UNITS_GetCoords(D2UnitStrc* p,D2CoordStrc* c){c->nX=p->x;c->nY=p->y;}
uint32_t PATH_ToFP16Center(int x){return (uint32_t(x)<<16)+32768;}
'''
 step='source/D2Common/src/Path/Step.cpp';s=source(step);m=re.search(r'TanToDirectionLutEntryStrc lutTanToDirection\[\] = \{[\s\S]+?\n\};',s);assert m;table=m.group();base+=table+'\n'
 for rel,name in [(step,'PATH_GetDirectionVector'),('source/D2Common/src/Path/PathMisc.cpp','PATH_ComputeDirection'),('source/D2Common/src/Units/Units.cpp','UNITS_GetDirectionToCoords')]:base+=routine(rel,name)+'\n'
 base+=r'''int main(){bool comma=false;printf("[");for(int dx=-32;dx<=32;dx++)for(int dy=-32;dy<=32;dy++){D2UnitStrc u;D2DynamicPathStrc path;u.pDynamicPath=&path;u.x=u.y=1024;u.total=u.current=60;path.PathPoints[0]={(uint16_t)(1024+dx),(uint16_t)(1024+dy)};D2CoordStrc vector;int direction=0;PATH_GetDirectionVector(&vector,&direction,PATH_ToFP16Center(1024),PATH_ToFP16Center(1024),PATH_ToFP16Center(1024+dx),PATH_ToFP16Center(1024+dy));sub_6FCF7390(&u,7);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%u,%u,[",dx,dy,direction,vector.nX,vector.nY,path.dwPathPoints,u.pSeed.nLowSeed,u.pSeed.nHighSeed);for(int i=0;i<path.dwPathPoints;i++)printf("%s[%u,%u]",i?",":"",path.PathPoints[i].X,path.PathPoints[i].Y);printf("]]");}printf("]\n");}'''
 cpp=STAGE/'phoenix-direction-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==4225
 fixture=STAGE/'phoenix-direction-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');angles=[int(x) for x in re.findall(r'\}\s*,\s*(\d+)\s*\}',table)];assert len(angles)==128
 for dx,dy,direction,vx,vy,n,lo,hi,points in rows:
  x,y=abs(dx),abs(dy);i=127*min(x,y)//max(x,y) if max(x,y) else 0;angle=angles[i]
  if x>y:angle=(-1-angle)&15
  if dy<0:angle=(-1-angle)&31
  if dx>=0:angle=(-1-angle)&63
  assert direction==(angle+8)&63 and n==60 and len(points)==60
 e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,reusedCompleteComposition='tools/reference/phoenix-chain-init-source-evidence.json',sourceHashes=hashes,compiledAdditionalRoutines=blocks,directionTableSha256=hashlib.sha256(table.encode()).hexdigest(),angles=angles,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Full UNITS_GetDirectionToCoords,PATH_ComputeDirection,PATH_GetDirectionVector and exact128-entry vector/angle lookup execute before the complete Phoenix charged initializer. No generic eight-direction Heading substitute is used.','All integer delta pairs in[-32,32]including zero are covered at valid1024origin with seed7and current60frame lifetime. Every resulting point and final seed are retained for native comparison.'],services=['Unit coordinates,FP16center conversion,first-target-point/frame/type accessors reuse declared scalar services. Direction computation/lookup and charged path initializer execute unchanged.','This bounded domain avoids the older signed127*tangent numerator overflow region. Source actor direction overflow remains separately documented;do not silently claim wide-arithmetic equivalence for arbitrary coordinates.'],next='Port exact source angle and path generator,compare all4225composed points/native seeds,then integrate saved child movement/collision. No learned handler enabled.')
 out=ROOT/'tools/reference/phoenix-direction-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix direction evidence stale'
 else:out.write_text(encoded)
 print('Phoenix direction:4225 complete angle/vector/chain-path compositions pass;bounded delta domain recorded.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
