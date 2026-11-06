#!/usr/bin/env python3
"""Compile complete damage-to-mana and percentage routines; retain NPC early guards."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-damage-to-mana-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def read(name):
  p=BASE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==name);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 bodies={n:function(read(p),n)for p,n in [('source/D2Game/src/SKILLS/SkillItem.cpp','SKILLITEM_EventFunc13_DamageToMana'),('source/D2Game/src/MONSTER/MonsterUnique.cpp','MONSTERUNIQUE_CalculatePercentage')]}
 cpp='''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,STAT_MANA=8;
struct D2GameStrc{};struct D2UnitStrc{int dwUnitType=1;};struct D2DamageStrc{int dwDmgTotal=0;};
int value=0,mana=0,maximum=10000,reads=0,maxReads=0,manaReads=0,sets=0,overlays=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,uint32_t,int){reads++;return value;}
int STATLIST_GetMaxManaFromUnit(D2UnitStrc*){maxReads++;return maximum;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){manaReads++;return mana;}
void STATLIST_SetUnitStat(D2UnitStrc*,int,int n,int){sets++;mana=n;}
void UNITS_SetOverlay(D2UnitStrc*,int overlay,int){if(overlay!=152)__builtin_trap();overlays++;}
'''+bodies['MONSTERUNIQUE_CalculatePercentage']+'\n'+bodies['SKILLITEM_EventFunc13_DamageToMana']+r'''
int main(){printf("[");bool first=true;D2GameStrc game;D2UnitStrc a;D2DamageStrc d;
for(int present:{0,1})for(int kind:{0,1,4})for(int packet:{0,1})for(int damage:{-1,0,1,255,256,257,10000,1048577})for(int percent:{-1,0,1,20,35,100,200})for(int before:{-1,0,1,9999,10000,10001}){
 a.dwUnitType=kind;d.dwDmgTotal=damage;value=percent;mana=before;reads=maxReads=manaReads=sets=overlays=0;
 int result=SKILLITEM_EventFunc13_DamageToMana(&game,9,present?&a:nullptr,nullptr,packet?&d:nullptr,114<<16,0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",present,kind,packet,damage,percent,before,result,mana,reads,maxReads,manaReads,sets,overlays);first=false;}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 p=STAGE/'probe.cpp';p.write_text(cpp)
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(STAGE/'probe')],capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
 cases=json.loads(run.stdout);assert len(cases)==4032;assert all(r[6:]==[0,r[5],0,0,0,0,0]for r in cases if r[1]!=0)
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n');inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in (p,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizer.log',out)},scope='Complete unchanged callback13 and percentage helper. NPC/item sources return0 before any stat/resource/overlay services,irrespective of target or packet. Player controls retain positive damage/percentage,FP mana/clamps and overlay152;the NPC adapter does not grant player or owner recovery.',limits=['Supplementary older source is not current-executable identity or full damage/event/shield/PvP/client order. Stat/mana/overlay services and declared player/NPC unit-type constants remain explicit bindings.'])
 path=ROOT/'tools/reference/npc-damage-to-mana-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print('NPC damage-to-mana:4032complete callback/helper contexts;NPC reads/writes/overlays0;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
