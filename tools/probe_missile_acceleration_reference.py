#!/usr/bin/env python3
"""Run complete source five-update acceleration and velocity-vector function."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Common/src/Path/PathMisc.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();start=text.index('void sub_6FD5CEB0(');i=text.index('{',start)+1;depth=1
 while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
 body=text[start:i];cpp=r'''#include <cstdio>
#include <initializer_list>
struct Vector{int nX=0,nY=0;};struct D2DynamicPathStrc{int dwAcceleration=0,dwAccelerationsCounter=0,dwMaxVelocity=0,dwVelocity=0;Vector tVelocityVector,tDirectionVector;};
'''+body+r'''
int main(){bool comma=false;printf("[");for(int velocity:{0,1024,2304,4608,6144})for(int acceleration:{-1000,0,1000})for(int counter=0;counter<5;++counter)for(int maximum:{3072,6144})for(int base:{0,512,1024})for(auto direction:{Vector{4096,0},Vector{-2896,2896},Vector{0,-4096},Vector{3999,879}}){D2DynamicPathStrc p;p.dwVelocity=velocity;p.dwAcceleration=acceleration;p.dwAccelerationsCounter=counter;p.dwMaxVelocity=maximum;p.tDirectionVector=direction;sub_6FD5CEB0(&p,base);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",velocity,acceleration,counter,maximum,base,direction.nX,direction.nY,p.dwVelocity,p.dwAcceleration,p.dwAccelerationsCounter,p.tVelocityVector.nX,p.tVelocityVector.nY);}printf("]\n");}
''';path=STAGE/'missile-acceleration-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==1800
 fixture=STAGE/'missile-acceleration-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledFunctionHash=hashlib.sha256(body.encode()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Full source function adds acceleration on the fifth velocity update,then resets its counter. It clamps negative resulting speed to0; an overshoot of maximum clamps to maximum and clears acceleration.','Nonpositive base velocity selects1024; source magnitude and direction multiplications use signed shifts. Negative vector components retain that truncation behavior.'],services=['All dynamic-path fields,base velocity and Q12 direction are explicit inputs; this does not compile the outer scheduler/path updater.','Native ordinary missile adapter uses base0/default1024 and one call per active source frame. Charged-path waypoint movement remains a separately labelled existing adapter.'],limits=['Source/native velocity-kernel evidence,not current executable movement or full collision equivalence.','Thunder nova current table Accel=-1000/MaxVel24 needs this component; previous constant-speed candidate failed its guard before installation.']);out=ROOT/'tools/reference/missile-acceleration-source-evidence.json';s=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==s,'Missile acceleration evidence stale'
 else:out.write_text(s)
 print('Missile acceleration:1800 full source velocity/acceleration/counter/vector contexts pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
