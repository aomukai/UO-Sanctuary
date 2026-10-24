#!/usr/bin/env python3
"""Execute complete toward/toward-finish paths for the next blade controller step."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 import probe_actor_path_reference
 probe_actor_path_reference.build(True)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  s=text[m.start():i];blocks[name]=hashlib.sha256(s.encode()).hexdigest();return s+'\n'
 misc=source('source/D2Common/src/Path/PathMisc.cpp')
 cpp=(STAGE/'actor-path-source-probe.cpp').read_text();cpp=cpp[:cpp.index('int main()')]
 cpp=cpp.replace('dwSpeed=0;', 'dwSpeed=0,nDirOffset=0;')
 start=misc.index('struct D2UnkPathStrc');end=misc.index('//D2Common.0x6FDAA720');extra=misc[start:end]
 for name in ['sub_6FDAB610','sub_6FDAB6A0','PATH_GetDirections_6FDAB790','sub_6FDAA880','sub_6FDAA720','sub_6FDABA50','PATH_RayTrace','PATH_CheckCollisionsToNextPosition','PATH_Toward_6FDAA9F0']:
  extra+=routine(misc,name)
 marker='using PathFunctionType=int(*)(D2PathInfoStrc*);';assert marker in cpp;cpp=cpp.replace(marker,extra+'\n'+marker)
 cpp=cpp.replace('scpfnPathFunction[18]={nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,PATH_ComputePathOrSlideAlongObstacles,nullptr,nullptr}', 'scpfnPathFunction[18]={nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,PATH_ComputePathOrSlideAlongObstacles,nullptr,nullptr}')
 cpp+=r'''
int main(){bool first=true;printf("[");
for(int wall=0;wall<8;wall++)for(int dx:{-19,-10,-3,-2,0,2,3,10,19})for(int dy:{-10,-3,0,3,10})for(int fraction:{0,32768,57344})for(int budget:{1,5,20})for(int type:{2,13}){
 D2UnitStrc u;D2DynamicPathStrc p;u.pDynamicPath=&p;p.pUnit=&u;p.dwFlags=type==13?0x100:0;p.dwPathType=type;p.nDistMax=budget;p.dwVelocity=p.dwMaxVelocity=2304;p.dwPathPoints=1;p.dwCurrentPointIdx=0;p.PathPoints[0]={99,99};p.tFinalTargetCoord={99,99};p.tPrevTargetCoord={98,98};p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;p.tTargetCoord={uint16_t(100+dx),uint16_t(100+dy)};
 std::fill(std::begin(cells),std::end(cells),0);
 if(wall==1)for(int y=80;y<128;y++)cells[(y-80)*48+23]=1;
 if(wall==2)for(int x=80;x<128;x++)cells[22*48+x-80]=0x100;
 if(wall==3)for(int y=98;y<=102;y++)cells[(y-80)*48+23]=0x1000;
 if(wall==4)for(int x=97;x<=103;x++)for(int y=97;y<=103;y++)if(x==97||x==103||y==97||y==103)cells[(y-80)*48+x-80]=1;
 if(wall==5)for(int y=80;y<128;y++)cells[(y-80)*48+18]=1;
 if(wall==6)for(int x=80;x<128;x++)cells[23*48+x-80]=1;
 if(wall==7){uint32_t seed=19;for(int x=80;x<128;x++)for(int y=80;y<128;y++){seed=1664525u*seed+1013904223u;if(seed%100<22)cells[(y-80)*48+x-80]=1;}cells[20*48+20]=0;}
 queries.clear();int count=D2Common_10142(&p,&u,0);assert(count==p.dwPathPoints && count<=77);
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d,%d,%d],\"state\":[%d,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"points\":",wall,dx,dy,fraction,budget,type,count,p.tGameCoords.dwPrecisionX,p.tGameCoords.dwPrecisionY,p.dwFlags,p.dwCurrentPointIdx,p.dwVelocity,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,p.nCollidedWithMask,p.tTargetCoord.X,p.tTargetCoord.Y,p.tPrevTargetCoord.X,p.tPrevTargetCoord.Y,p.tFinalTargetCoord.X,p.tFinalTargetCoord.Y);
 std::vector<std::array<int,2>> out;for(int i=0;i<p.dwPathPoints;i++)out.push_back({p.PathPoints[i].X,p.PathPoints[i].Y});points(out);printf(",\"queries\":");points(queries);printf("}");
}printf("]\n");}
'''
 p=STAGE/'toward-path-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'toward-path-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
 data=json.loads(result.stdout);assert len(data)==6480
 for x in data:
  wall,dx,dy,fraction,budget,kind=x['input']
  if wall==0 and (dx or dy):assert x['points']==[[100+dx,100+dy]]
  assert x['state'][0]==len(x['points'])
 fixture=STAGE/'toward-path-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeVerifiedByThisSourceProbe=False,nativeEvidence='tools/reference/toward-path-native-evidence.json',sourceHashes=hashes,compiledRoutineHashes=blocks,initializerEvidence='tools/reference/actor-path-engine-evidence.json',fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),contexts=6480),sanitizers='ASan/UBSan; successful exit and zero runtime stderr required',established=['Complete toward-path, ray trace, next-position collision check, direction-index/table lookup, three-direction collision choice and distance heuristic execute unchanged together with the previously verified complete general initializer. Both path2 and13 use this routine with distinct source flags; offset0 is exercised.','Eight collision grids, nine horizontal/five vertical offsets, three fractional poses and budgets1/5/20 retain exact path, final/active/previous targets, direction/velocity/flags and ordered collision queries. Open paths can reach a distant target directly even when the distance budget is1; do not apply wall-follow distance rejection to this kernel.'],limits=['Source proof only; the native toward path and motion component is verified separately. The general initializer is marked BROKEN in D2MOO; source reconstruction and modern executable acceptance remain separate.','Valid synthetic room/grid, zero-footprint monster point target and no target adjustment are explicit services. Direction-offset helper is compiled but only offset0 is exercised. Successful path repair/event/mode/AI/missile consumers are not executed by this probe.'])
 p=ROOT/'tools/reference/toward-path-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Toward path:6,480 complete source initializer/ray/direction/path contexts pass sanitizers; native component is verified separately.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
