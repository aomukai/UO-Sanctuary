#!/usr/bin/env python3
"""Complete kill recovery callbacks: NPC life/demon life and player-only mana."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-kill-recovery-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 path=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillItem.cpp';raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']=='source/D2Game/src/SKILLS/SkillItem.cpp')
 names=['SKILLITEM_EventFunc28_HealAfterKill','SKILLITEM_EventFunc17_ManaAfterKill','SKILLITEM_EventFunc18_HealAfterDemonKill'];bodies={n:function(raw.decode(),n)for n in names}
 cpp='''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,STAT_HITPOINTS=6,STAT_MANA=8;
struct D2GameStrc{};struct D2DamageStrc{};struct D2UnitStrc{int dwUnitType=1;};
int value=0,balance=0,maximum=10000,reads=0,maxReads=0,poolReads=0,writes=0,overlay=0,demonChecks=0;bool demon=false;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,uint32_t,int){reads++;return value;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){maxReads++;return maximum;}
int STATLIST_GetMaxManaFromUnit(D2UnitStrc*){maxReads++;return maximum;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){poolReads++;return balance;}
void STATLIST_SetUnitStat(D2UnitStrc*,int,int v,int){writes++;balance=v;}
void UNITS_SetOverlay(D2UnitStrc*,int v,int){overlay=v;}
bool MONSTERS_IsDemon(D2UnitStrc*){demonChecks++;return demon;}
'''+''.join(bodies[n]+'\n'for n in names)+r'''
int main(){printf("[");bool first=true;D2GameStrc g;D2UnitStrc a,t;using Fn=int(*)(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*,int,int);Fn funcs[]={SKILLITEM_EventFunc28_HealAfterKill,SKILLITEM_EventFunc17_ManaAfterKill,SKILLITEM_EventFunc18_HealAfterDemonKill};
for(int fn:{0,1,2})for(int src:{0,1})for(int dst:{0,1})for(int kind:{0,1,4})for(int amount:{0,1,3,20})for(int before:{-1,0,1,9999,10000,10001})for(int d:{0,1}){
 a.dwUnitType=kind;value=amount;balance=before;demon=d;reads=maxReads=poolReads=writes=overlay=demonChecks=0;
 int result=funcs[fn](&g,10,src?&a:nullptr,dst?&t:nullptr,nullptr,(fn==0?86:fn==1?138:139)<<16,0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",fn,src,dst,kind,amount,before,d,result,balance,reads,maxReads,poolReads,writes,overlay,demonChecks);first=false;}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 p=STAGE/'probe.cpp';p.write_text(cpp)
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(STAGE/'probe')],capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
 cases=json.loads(run.stdout);assert len(cases)==1728;out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs={str(path.relative_to(ROOT)):sha(path),str(Path(__file__).resolve().relative_to(ROOT)):sha(Path(__file__))},artifacts={str(p.relative_to(ROOT)):sha(p)for p in (p,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizer.log',out)},scope='Complete unchanged callbacks28/17/18 with explicit stat/resource/overlay and demon-predicate services. Flat whole-point addition preserves original FP balances before clamp;mana17is player-only. Stat zero and full pools have no write/overlay. Negative bonus shifts are outside this checked domain.',limits=['No current-executable/global kill/retired-source/PvP/geometry/client certificate. Native ordinary contact and direct-killer ownership require separate acceptance. Demon metadata and stat/mana/life service bindings remain explicit.'])
 target=ROOT/'tools/reference/npc-kill-recovery-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('NPC kill recovery:1728complete three-callback contexts;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
