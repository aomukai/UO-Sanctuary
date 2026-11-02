#!/usr/bin/env python3
"""Compose complete older Energy Shield do23, shared list and passive helper."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/energy-shield-cast-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 names={'source/D2Game/src/SKILLS/Skills.cpp':'sub_6FD10EC0','source/D2Game/src/SKILLS/SkillSor.cpp':'SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay','source/D2Game/src/SKILLS/SkillDruid.cpp':'sub_6FCFE200'}
 blocks={};sources={}
 for rel,name in names.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
  assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  sources[rel]=sha(p);blocks[name]=function(raw.decode(),name)
 prior=ROOT/'tools/reference/enchant-source-evidence.json';proof=json.loads(prior.read_text());original=ROOT/'.local/class-skills/enchant-source/probe.cpp'
 retained=STAGE/'reused-enchant.cpp';expected=proof['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected and function(retained.read_text(),'sub_6FD10EC0')==blocks['sub_6FD10EC0']
 header=retained.read_text().split(blocks['sub_6FD10EC0'])[0]
 def replace(a,b):
  nonlocal header
  assert header.count(a)==1,a;header=header.replace(a,b,1)
 replace('int wAuraStat[6]','int nPassiveStat[5]={-1,-1,-1,-1,-1},dwPassiveCalc[5]={};int wAuraEvent[3]={1,-1,-1},wAuraEventFunc[3]={24,0,0};int wAuraStat[6]')
 replace('nStatesTxtRecordCount=200;','nStatesTxtRecordCount=200;int costs[256]={};int* pItemStatCostTxt=costs;')
 header+=r'''
constexpr int STAT_MODIFIERLIST_SKILL=10,STAT_MODIFIERLIST_LEVEL=11,STAT_ATTACKRATE=12,STAT_OTHER_ANIMRATE=13;
using D2C_UnitEventTypes=int;
int registrations=0,unregisters=0,rates=0;
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){unregisters++;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){registrations++;}
void STATLIST_SetStat(D2StatListStrc* p,int stat,int value,int){writes++;p->values[stat]=value;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){rates++;}
'''
 cpp=header+blocks['sub_6FD10EC0']+'\n'+blocks['sub_6FCFE200']+'\n'+blocks['SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay']
 cpp+=r'''
int main(){printf("[");bool first=true;owner.dwUnitId=202;
for(int guard=0;guard<6;guard++)for(int old:{0,1,10,20,30})for(int rank:{1,10,20})for(int same:{0,1})for(int life:{0,3600,36000})for(int auras:{0,1,3}){
hasRow=guard!=1;row.wAuraStat[0]=guard==2?-2:guard==3?256:-1;row.nAuraState=guard==4?-1:guard==5?200:30;
for(int i=0;i<3;i++)row.wAuraEvent[i]=i<auras?i+1:-1;
mon.dwMonStatsFlags=0;owner.dwUnitType=0;present=old>0;list={};list.rank=old;list.skill=same?58:999;list.state=30;list.owner=101;list.until=9999;
list.values[10]=7;list.values[11]=8;duration=life;writes=allocations=frees=refresh=gfx=events=registrations=unregisters=rates=0;
int ret=SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay(&game,&owner,58,rank);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",guard,old,rank,same,life,auras,ret,writes,allocations,frees,refresh,events,registrations,unregisters,rates,list.rank,list.until,list.owner,list.values[10],list.values[11]);first=false;
}printf("]\n");}
'''
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==1620
 for inputs,out in cases:
  guard,old,rank,same,life,auras=inputs;accepted=guard==0 and not(old and same and old>rank);reused=bool(accepted and old==rank and same);allocated=accepted and not reused
  expected=[int(accepted),2*accepted,int(allocated),int(allocated and old>0),int(allocated),int(accepted and life>0),auras if accepted else 0,int(accepted and auras>0),int(accepted),rank if accepted else old,(100+life if life else 9999 if reused else 0) if accepted else 9999,101 if reused or not accepted else 202,58 if accepted else 7,rank if accepted else 8]
  assert out==expected,(inputs,out,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True)
 log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.loads((table.parent.parent/'sources.json').read_text());assert sha(table)==manifest['files']['json/skills.json']['sha256']
 current=next(x for x in json.loads(table.read_text()).values() if x.get('skill')=='Energy Shield');assert current['*Id']==58 and current['srvdofunc']==23 and current['auraeventfunc1']==24 and not any(k.startswith('passivestat') for k in current)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTable={str(table.relative_to(ROOT)):sha(table)},cases=len(cases),sanitizers='ASan/UBSan clean',established=['Complete do23/shared state-list/passive helper composition preserves equal-rank owner/list with refreshed deadline,refuses weaker same-skill rank,and replaces stronger/different lists. Stored skill/rank modifiers and event registration counts are retained. Current Energy Shield has no passive-stat entries.'],declaredServices=['Older engine,not modern executable proof. Supplied noncurse self state/formula/allocator/events/stat/animation services;ordinary player domain only. Generic payment wrapper,allocator failure,arbitrary subscribers,modern mixed-shield event order and client remain separate.'],artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),prior,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/energy-shield-cast-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Energy Shield do23:1620 complete older cast/list/passive compositions;ASan/UBSan clean;outer payment/modern events remain separate.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
