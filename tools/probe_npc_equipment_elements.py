#!/usr/bin/env python3
"""Run the complete source fixed-point element-range and percentage routines."""
import hashlib,json,os,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
CACHE=Path(os.environ.get('SANCTUARY_REFERENCE_CACHE',ROOT/'.local/class-skills'))
STAGE=ROOT/'.local/class-skills/npc-equipment-elements-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
 STAGE.mkdir(parents=True,exist_ok=True)
 tree=json.loads((CACHE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 inputs={}
 def read(name):
  p=CACHE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name)
  inputs[str(p)]=sha(p);return raw.decode()
 damage=read('source/D2Game/src/UNIT/SUnitDmg.cpp');unique=read('source/D2Game/src/MONSTER/MonsterUnique.cpp')
 cpp='''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
struct D2UnitStrc{int pSeed=0;};
int edge=0,bound=0;
int ITEMS_RollLimitedRandomNumber(int*,int n){assert(n>0);bound=n;return edge?n-1:0;}
'''
 routines={}
 for source,name in [(unique,'MONSTERUNIQUE_CalculatePercentage'),(damage,'SUNITDMG_RollDamageValueInRange')]:
  body=function(source,name);routines[name]=hashlib.sha256(body.encode()).hexdigest();cpp+=body+'\n'
 cpp+='''int main(){printf("[\\n");bool first=true;D2UnitStrc unit;
for(int low:{0,1,256,3000000})for(int choice:{0,1,2})for(int a:{-100,0,37,100})for(int b:{0,37,100})for(int initial:{-257,0,512})for(int roll:{0,1}){
int high=choice==0?0:choice==1?low:low+1024;edge=roll;bound=0;
int result=SUNITDMG_RollDamageValueInRange(&unit,low,high,a,b,initial);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d]]",first?"":",\\n",low,high,a,b,initial,roll,result,bound);first=false;}
printf("\\n]\\n");}
'''
 p=STAGE/'source.cpp';p.write_text(cpp)
 run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],capture_output=True,text=True)
 (STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
 cases=json.loads(run.stdout);assert len(cases)==864,len(cases)
 (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
 result=dict(referenceVersion='3.3.93847',olderEngineCommit=tree['sha'],cases=len(cases),inputs=inputs,compiledUnchangedRoutineHashes=routines,
  scope='Complete older range and percentage routines with controlled first/last random services. Includes zero/equal/reversed endpoints,independent endpoint percentages,initial damage and large-value rounding. This does not compile complete FillDamageValues or prove modern executable parity or item birth.',
  artifacts={str(x.relative_to(ROOT)):sha(x) for x in [p,STAGE/'compiler.log',STAGE/'sanitizer.log',STAGE/'cases.json']})
 (ROOT/'tools/reference/npc-equipment-elements-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
 print('Source element roll:864complete range/percentage contexts pass ASan/UBSan.')
if __name__=='__main__':build()
