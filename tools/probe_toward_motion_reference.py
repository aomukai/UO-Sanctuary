#!/usr/bin/env python3
"""Execute complete toward initialization and48 movement frames per context."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 import probe_toward_path_reference
 probe_toward_path_reference.build(True)
 original=(STAGE/'toward-path-source-probe.cpp').read_text();cpp=original[:original.index('int main()')]
 # The initializer probe logs pattern-dispatch queries; movement checks the
 # room/grid primitive directly, so its phase records that service instead.
 cpp=cpp.replace('D2ActiveRoomStrc* COLLISION_GetRoomBySubTileCoordinates(', 'int motionPhase=0;D2ActiveRoomStrc* COLLISION_GetRoomBySubTileCoordinates(')
 cpp=cpp.replace('{return x>=80&&x<128&&y>=80&&y<128?p:nullptr;}', '{if(motionPhase)queries.push_back({x,y});return x>=80&&x<128&&y>=80&&y<128?p:nullptr;}')
 grid=original[original.index(' std::fill(std::begin(cells)'):original.index(' queries.clear();int count=')]
 cpp+=r'''
int main(){bool first=true;printf("[");
for(int wall=0;wall<8;wall++)for(int dx:{-19,-8,-2,2,8,19})for(int dy:{-3,0,3})for(int fraction:{0,32768,57344})for(int budget:{1,5,20})for(int type:{2,13}){
 D2UnitStrc u;D2DynamicPathStrc p;u.pDynamicPath=&p;p.pUnit=&u;p.dwFlags=type==13?0x100:0;p.dwPathType=type;p.nDistMax=p.nDist=budget;p.dwVelocity=p.dwMaxVelocity=2304;p.dwPathPoints=1;p.dwCurrentPointIdx=0;p.PathPoints[0]={99,99};p.tFinalTargetCoord={99,99};p.tPrevTargetCoord={98,98};p.tGameCoords.dwPrecisionX=(100<<16)+fraction;p.tGameCoords.dwPrecisionY=(100<<16)+fraction;p.tTargetCoord={uint16_t(100+dx),uint16_t(100+dy)};
'''+grid+r'''
 motionPhase=0;D2Common_10142(&p,&u,0);motionPhase=1;
 if(!first)printf(",");first=false;printf("{\"input\":[%d,%d,%d,%d,%d,%d],\"frames\":[",wall,dx,dy,fraction,budget,type);
 for(int frame=0;frame<48;frame++){
  queries.clear();repaths=0;int moving=D2Common_10226(&u,1024);
  if(frame)printf(",");printf("[%d,%u,%u,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,",moving,p.tGameCoords.dwPrecisionX,p.tGameCoords.dwPrecisionY,p.dwFlags,p.dwPathPoints,p.dwCurrentPointIdx,p.dwVelocity,p.dwMaxVelocity,p.dwAcceleration,p.dwAccelerationsCounter,p.tDirectionVector.nX,p.tDirectionVector.nY,p.tVelocityVector.nX,p.tVelocityVector.nY,p.nDist,p.nSavedStepsCount,p.nCollidedWithMask,repaths);points(queries);printf("]");
 }printf("]}");
}printf("]\n");}
'''
 p=STAGE/'toward-motion-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'toward-motion-sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-4000:]
 data=json.loads(result.stdout);assert len(data)==2592 and all(len(x['frames'])==48 for x in data)
 assert any(f[0] and f[14]==0 for x in data for f in x['frames'])
 assert sum(len(f[18]) for x in data for f in x['frames'])>10000
 out=STAGE/'toward-motion-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,sourceInitializerEvidence='tools/reference/toward-path-engine-evidence.json',sourceMotionEvidence='tools/reference/actor-motion-engine-evidence.json',compiledUnitSha256=hashlib.sha256(cpp.encode()).hexdigest(),fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),contexts=2592,frames=124416),sanitizers='ASan/UBSan; successful exit and zero runtime stderr required',established=['Complete unchanged source initializer, toward/ray/direction kernel and outer actor movement/collision routines execute together for path2/13 across eight grids, six horizontal/three vertical offsets, three fractional poses and budgets1/5/20,48 consecutive movement calls each.','Exact FP16 positions, path count/index, direction/velocity vectors, flags, distance, collision mask, saved-step count, repair requests and collision query order are retained per frame. A distance counter reaching zero does not independently stop these point-target paths.'],limits=['Valid room/grid, zero-footprint monster, point target, prepared velocity2304 and failed repair service remain explicit. Calling the motion primitive after a failed initialization is a component reset control, not a claim that real neutral AI dispatches movement.','This does not execute whole mode/stat collectors, AI/event scheduling, automatic actor/missile/combat/input or current D2R executable. Unchanged legacy reconstruction is supplementary evidence; the general initializer remains marked BROKEN.'])
 p=ROOT/'tools/reference/toward-motion-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Toward motion:2,592 complete initialized paths/124,416 exact movement frames pass sanitizers, including zero remaining distance.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
