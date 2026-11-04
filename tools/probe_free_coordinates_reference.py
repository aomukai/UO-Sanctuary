#!/usr/bin/env python3
"""Execute complete free-coordinate wrapper/search for Flight and retained Leap."""
import argparse
import ast
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'free-coordinates-source'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    path='source/D2Common/src/D2Collision.cpp';p=BASE/'d2moo-pinned-source'/path;raw=p.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==path)
    scaffold=ROOT/'tools/probe_leap_collision_reference.py'
    assignment=next(n for n in ast.parse(scaffold.read_text()).body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
    cpp=ast.literal_eval(assignment.value)
    bodies={n:function(raw.decode(),n) for n in ('COLLISION_GetFreeCoordinatesImpl','COLLISION_GetFreeCoordinates')}
    prior=json.loads((ROOT/'tools/reference/leap-collision-engine-evidence.json').read_text())
    assert hashlib.sha256(bodies['COLLISION_GetFreeCoordinatesImpl'].encode()).hexdigest()==prior['probe']['blockHashes']['COLLISION_GetFreeCoordinatesImpl']
    cpp+='\n'+ '\n'.join(bodies.values())+r'''
int main(){bool comma=false;printf("[");
for(int allow:{0,1})for(int test=0;test<5;test++)for(int dx:{-50,-49,-7,-2,-1,0,1,2,7,49,50})for(int dy:{-50,-49,-7,-2,-1,0,1,2,7,49,50}){
 rule=test;wantedX=dx;wantedY=dy;calls=0;trace=14695981039346656037ull;D2CoordStrc point;
 auto* found=COLLISION_GetFreeCoordinates(&room,&point,2,0x1C09,allow);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,\"%llu\"]",allow,test,dx,dy,found?1:0,point.nX,point.nY,calls,(unsigned long long)trace);
}printf("]\n");}
'''
    generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    with (STAGE/'compiler.log').open('w') as out:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=out,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()
    data=json.loads(run.stdout);assert len(data)==1210
    def expected(allow,rule,dx,dy):
        calls=0;trace=14695981039346656037
        def clear(x,y):
            nonlocal calls,trace
            calls+=1
            for v in (100+x,100+y):trace=((trace^(v&0xffffffff))*1099511628211)&0xffffffffffffffff
            return rule==0 or rule==2 and (x,y)==(dx,dy) or rule==3 and (x+y)%2!=0 or rule==4 and (abs(x)==dx or abs(y)==dy)
        if clear(0,0):return [1,100,100,calls,str(trace)]
        for radius in range(1,50):
            points=[(x,y) for y in range(-radius,radius+1) for x in (-radius,radius)]
            points += [(x,y) for x in range(1-radius,radius) for y in (-radius,radius)]
            best=None;score=100000
            for x,y in points:
                if clear(x,y) and abs(x)+abs(y)<score:best=(x,y);score=abs(x)+abs(y)
            if best:return [1,100+best[0],100+best[1],calls,str(trace)]
        return [allow,100,100,calls,str(trace)]
    for row in data:assert row[4:]==expected(*row[:4]),row
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],currentExecutableCodeParity=False,
        cases=1210,inputs={str(p.relative_to(ROOT)):sha(p),str(scaffold.relative_to(ROOT)):sha(scaffold)},
        compiledUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        services=['Same existing-room,field-null,size2/mask0x1C09/max50/increment1 collision service. Spatial rules expose all-clear,all-blocked,single-cell,checkerboard and competing-edge candidates. No field-bearing search or actual D2 room topology is certified.',
                  'The declared ABI scaffold is read from the tracked older Leap generator AST without running or rewriting its historical evidence. Full unchanged wrapper selects the actual fallback flag,not a copied arithmetic fragment.'],
        established=['The complete source wrapper passes the same collision mask as both masks,field=null,maxDistance50,increment1. Original point precedes square rings1..49;each ring retains minimum Manhattan score and original vertical-edge-before-horizontal-edge tie order.',
                     'Flag0 returns failure when every candidate is blocked. Flag1 can return the original blocked point/room. Flight uses0;retained Leap uses1 and independently checks final clearance/range/flying. The flag does not stop the ring loop from querying neighboring rooms.',
                     'Coordinate/call-count/FNV-trace controls include radius49success and radius50outside-limit failure. No native map/terrain/height/permission/footprint equivalence follows from this same-room scalar probe.'],
        sanitizers='ASan+UBSan clean',artifacts={str(q.relative_to(ROOT)):sha(q) for q in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',cases,Path(__file__).resolve())})
    out=ROOT/'tools/reference/free-coordinates-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Free-coordinate reference stale'
    else:out.write_text(encoded)
    print('Free coordinates:1210 complete wrapper/search cases;Flight failure versus Leap blocked-origin fallback;radius/tie/call-trace controls;ASan+UBSan clean.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
