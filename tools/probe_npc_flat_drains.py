#!/usr/bin/env python3
"""Execute full total-damage and ExecuteEvents NPC drain composition with declared services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from probe_npc_damage_fill import enum
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-flat-drains-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=BASE/'monster-weapon-phases-source-probe.cpp';proof=ROOT/'tools/reference/monster-weapon-phases-source-evidence.json';prior=json.loads(proof.read_text());assert sha(parent)==prior['generatedSourceSha256']
 cpp=parent.read_text().split('int main(){for(int i=0;i<32;i++)')[0];tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 stats=enum(read('source/D2Common/include/D2StatList.h'),'D2C_ItemStats');events=enum(read('source/D2Game/include/UNIT/SUnitEvent.h'),'D2C_UnitEventTypes: uint8_t // Events.txt')
 # Replace fixture-local stat/event ordinals with actual independently pinned IDs.
 old=re.search(r'enum\{STAT_ARMORCLASS,.*?\};',cpp).group();names=old.removeprefix('enum{').removesuffix('};').split(',');bound={n:stats[n]for n in names if n.startswith('STAT_')}
 cpp=cpp.replace(old,'\n'.join('constexpr int '+n+'='+str(v)+';'for n,v in bound.items())+'\nconstexpr int MASTERY_DAMAGE=10000,MASTERY_CRITICAL=10001;')
 for prefix,values in [('STAT_',stats),('UNITEVENT_',events)]:
  def bind(m):
   name=m.group(1);value=values[name];bound[name]=value;return 'constexpr int '+name+'='+str(value)+';'
  cpp=re.sub(r'constexpr int ('+prefix+r'[A-Z0-9_]+)=\d+;',bind,cpp)
 cpp=cpp.replace('ITEMTYPE_BLUNT=2','ITEMTYPE_BLUNT=57')
 source=read('source/D2Game/src/UNIT/SUnitDmg.cpp');percentage=read('source/D2Game/src/MONSTER/MonsterUnique.cpp');bodies={}
 for text,names in [(source,['SUNITDMG_ExecuteEvents','SUNITDMG_CalculateTotalDamage','SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_FillDamageValues']),(percentage,['MONSTERUNIQUE_CalculatePercentage'])]:
  for n in names:bodies[n]=function(text,n);assert function(cpp,n)==bodies[n],n
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){__builtin_trap();}','void D2COMMON_11013_ConvertMode(D2UnitStrc*u,int*kind,int*,int*,const char*,int){assert(u==&attacker&&*kind==UNIT_MONSTER);}')
 replace('int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 100*256;}','int actorMaximum=1000;int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*u){assert(u==&attacker);return actorMaximum;}')
 replace('void UNITS_SetOverlay(D2UnitStrc*,int n,int){assert(n==54);overlays++;}','void UNITS_SetOverlay(D2UnitStrc*u,int n,int){assert(n==54||n==151||n==152);overlays++;trace.push_back({32,u->dwUnitId,n});}')
 replace('if(stat==STAT_HITPOINTS)trace.push_back({21,u->dwUnitId,value});','trace.push_back({21,u->dwUnitId*1000+stat,value});')
 replace('void STATLIST_AddUnitStat(D2UnitStrc*u,int stat,int n,int){u->stats[stat]+=n;}','void STATLIST_AddUnitStat(D2UnitStrc*u,int stat,int n,int){u->stats[stat]+=n;trace.push_back({21,u->dwUnitId*1000+stat,u->stats[stat]});}')
 main=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int physical:{0,257,12864})for(int hp:{1,257,51200})for(auto drains:{std::array<int,3>{0,0,0},{1,0,0},{0,1,0},{0,0,513},{0,1,513},{513,517,521}})for(int mana:{0,255,1337})for(int stam:{0,255,1537})for(int drain:{0,50,100,150})for(int missing:{0,1,1000})for(int flags:{1,33}){
Reset(416,1);mon={};victimHire=victimBoss=victimUnique=victimStunPresent=tapOn=deathDelay=false;
actorMaximum=1000+missing;attacker.stats[STAT_HITPOINTS]=1000;defender.stats[STAT_HITPOINTS]=hp;defender.stats[STAT_MANA]=mana;defender.stats[STAT_STAMINA]=stam;mon.nDrain[0]=drain;
D2DamageStrc p;p.dwPhysDamage=physical;p.dwLifeLeech=drains[0];p.dwManaLeech=drains[1];p.dwStamLeech=drains[2];p.wResultFlags=flags;
SUNITDMG_CalculateTotalDamage(&game,&attacker,&defender,&p);SUNITDMG_ExecuteEvents(&game,&attacker,&defender,0,&p);
printf("%s{\"input\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"output\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"writes\":[",first?"":",",physical,hp,mana,stam,missing,drains[0],drains[1],drains[2],drain,flags,attacker.stats[STAT_HITPOINTS],defender.stats[STAT_HITPOINTS],defender.stats[STAT_MANA],defender.stats[STAT_STAMINA],p.dwPhysDamage,p.dwDmgTotal,p.dwLifeLeech,p.dwManaLeech,p.dwStamLeech,p.wResultFlags,overlays);first=false;
bool comma=false;for(auto t:trace)if(t.kind==21||t.kind==32){printf("%s[%d,%d,%d]",comma?",":"",t.kind,t.a,t.b);comma=true;}printf("]}");}printf("]\n");}
'''
 cpp='#include <array>\n'+cpp+main
 for n,b in bodies.items():assert function(cpp,n)==b
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 cases=json.loads(run.stdout);assert len(cases)==11664,len(cases);out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 # Independently replay the exact full-filler-to-native acceptance vector.
 composed=cpp.split('int main(){for(int i=0;i<32;i++)')[0]+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;Reset(416,1);mon={};mon.nCrit=0;victimHire=victimBoss=victimUnique=victimStunPresent=tapOn=deathDelay=false;actorMaximum=256000;
attacker.stats[STAT_HITPOINTS]=1000;defender.stats[STAT_HITPOINTS]=51200;defender.stats[STAT_MANA]=1337;defender.stats[STAT_STAMINA]=1537;
attacker.stats[STAT_LIFEDRAINMINDAM]=attacker.stats[STAT_LIFEDRAINMAXDAM]=2;
attacker.stats[STAT_MANADRAINMINDAM]=attacker.stats[STAT_MANADRAINMAXDAM]=attacker.stats[STAT_STAMDRAINMINDAM]=attacker.stats[STAT_STAMDRAINMAXDAM]=1;attacker.stats[STAT_STUNLENGTH]=40;
D2DamageStrc p;p.dwPhysDamage=257;p.dwHitFlags=1;p.wResultFlags=1;
SUNITDMG_FillDamageValues(&game,&attacker,&defender,&p,0,128);SUNITDMG_CalculateTotalDamage(&game,&attacker,&defender,&p);SUNITDMG_ExecuteEvents(&game,&attacker,&defender,0,&p);
printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]\n",attacker.stats[STAT_HITPOINTS],defender.stats[STAT_HITPOINTS],defender.stats[STAT_MANA],defender.stats[STAT_STAMINA],p.dwPhysDamage,p.dwDmgTotal,p.dwLifeLeech,p.dwManaLeech,p.dwStamLeech,p.dwStunLen,victimStun.until,overlays);}
'''
 for n,b in bodies.items():assert function(composed,n)==b,n
 composedpath=STAGE/'composed.cpp';composedpath.write_text(composed);composedbinary=STAGE/'composed'
 with(STAGE/'composed-compiler.log').open('w')as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(composedpath),'-o',str(composedbinary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(composedbinary)],capture_output=True,timeout=120);(STAGE/'composed-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 result=json.loads(run.stdout);assert result==[2281,49919,825,1025,257,1281,1024,512,512,40,1040,1],result
 composedout=STAGE/'composed.json';composedout.write_bytes(run.stdout)
 inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve());inputs[str(parent.relative_to(ROOT))]=sha(parent)
 record=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=len(cases),fullFillerNativeComposition=result,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},boundStatAndEventIds=bound,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in(path,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log',composedpath,composedbinary,composedout,STAGE/'composed-compiler.log',STAGE/'composed-sanitizer.log')},scope='Five complete unchanged older routines:ordinary total/mitigation and ExecuteEvents including positive NPC life/mana/stamina drains,plus an exact full Fill-to-total-to-ExecuteEvents native acceptance vector. Ordinary non-hireling non-transformed monster-kind conversion,empty subscribers,unresisted channels,room/alignment/monster records,resource setters,overlays and state sinks are explicit declared services. Positive life drain joins direct HP total;stamina alone deducts stamina but cannot open recovery gate. Resource write order is captured. This is not a current executable or full stat/world model.')
 target=ROOT/'tools/reference/npc-flat-drains-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'NPC flat-drain source proof stale'
 else:target.write_text(encoded)
 print('NPC flat drains:11664full total/ExecuteEvents contexts;five complete routines plus full-filler acceptance vector;header-bound stat/event IDs;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
