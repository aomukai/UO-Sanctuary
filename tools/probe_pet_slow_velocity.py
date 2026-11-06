#!/usr/bin/env python3
"""Execute complete older movement-speed floor under declared stat/path services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'pet-slow-velocity-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);path=BASE/'d2moo-pinned-source/source/D2Common/src/Units/Units.cpp';raw=path.read_bytes();tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Common/src/Units/Units.cpp')
 body=function(raw.decode(),'UNITS_UpdateRunWalkAnimRateAndVelocity')
 start=raw.decode().index('ANIMMODESTAT_ATTACKRATE');end=raw.decode().index('ANIMMODESTAT_MOVEVELOCITY',start)
 assert [s.strip()for s in raw.decode()[start:end].split(',')if s.strip()]==['ANIMMODESTAT_ATTACKRATE','ANIMMODESTAT_GETHITRATE','ANIMMODESTAT_CASTRATE','ANIMMODESTAT_BLOCKRATE']
 costs=ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json';assert next(r for r in json.loads(costs.read_text())if r['Name']=='velocitypercent')['Id']==67
 cpp=r'''#include <cassert>
#include <cstdio>
#include <initializer_list>
#define __forceinline inline
#define __fastcall
constexpr int ANIMMODESTAT_MOVEVELOCITY=4,STAT_VELOCITYPERCENT=67;
struct Path{}path;struct D2UnitStrc{Path*pDynamicPath=&path;};
int base=0,stat=0,speed=0,velocity=0,animation=0,movement=0,sets=0,paths=0,traces=0;
int UNITS_GetAnimStatBasePercentage(int id,D2UnitStrc*){assert(id==4);return base;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int id,int layer){assert(id==67&&layer==0);return stat;}
int UNITS_GetBaseAnimSpeed(int,int,int){return speed;}
int UNITS_GetBaseVelocity(int,int){return velocity;}
void UNITS_SetAnimationSpeed(D2UnitStrc*,int value){animation=value;sets++;}
void PATH_SetVelocity(Path*,int value,const char*,int){movement=value;paths++;}
void FOG_Trace(const char*,...){traces++;}
'''+body+r'''
int main(){printf("[");bool comma=false;D2UnitStrc unit;
for(int present:{0,1})for(int b:{0,50,100,150,300})for(int s:{-200,-90,-75,-50,-1,0,25,100})for(int a:{100,128,208,256})for(int v:{0,100}){
unit.pDynamicPath=present?&path:nullptr;base=b;stat=s;speed=a;velocity=v;animation=movement=sets=paths=traces=0;
UNITS_UpdateRunWalkAnimRateAndVelocity(&unit,2,1,10,__FILE__,__LINE__);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",comma?",":"",present,b,s,a,v,animation,movement,sets,paths,traces);comma=true;}printf("]\n");}
'''
 assert function(cpp,'UNITS_UpdateRunWalkAnimRateAndVelocity')==body
 src=STAGE/'source.cpp';src.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(src),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
 rows=json.loads(run.stdout);assert len(rows)==640
 out=STAGE/'cases.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,contexts=640,completeRoutineHashes={'UNITS_UpdateRunWalkAnimRateAndVelocity':hashlib.sha256(body.encode()).hexdigest()},inputs={str(path.relative_to(ROOT)):sha(path),str(costs.relative_to(ROOT)):sha(costs),str(Path(__file__).resolve().relative_to(ROOT)):sha(Path(__file__))},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [src,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out]},services=['Animation-base percentage,velocity stat,base animation/base velocity,path/setter/trace are declared services. MOVE selector4is checked against the source enum order;velocity stat67matches the owned cost table.'],established=['The complete body floors combined movement percentage at25before integer animation/path writes. With100base percentage and no haste,nominal90slow yields25percent movement,not10.'],limits=['No current executable,haste supplier composition,full NPC/player path/room/step/order or renderer certificate. The shared native delay projection is checked only for the no-haste case.'])
 target=ROOT/'tools/reference/pet-slow-velocity-source-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Pet slow velocity:640complete unchanged movement-body cases;25percent floor;ASan+UBSan pass;full/current supplier order remains bounded.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
