#!/usr/bin/env python3
"""Pin Siphon, reuse complete do23 source, and execute generic kill callbacks."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'hex-siphon'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};routines={};ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());tables={}
 for n in ['skills','states','itemstatcost']:
  p=ref/f'json/{n}.json';assert sha(p)==manifest['files'][f'json/{n}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[n]=json.loads(p.read_text())
 s=tables['skills']['386'];assert s['srvdofunc']==23 and all(s['auraeventfunc'+str(i)]==36 for i in range(1,4))
 assert [(s['passivestat'+str(i)],s['passivecalc'+str(i)]) for i in [1,3,5]]==[('item_tohit_percent','toht'),('item_healafterkill',"ln12+(((skill('Engorge'.blvl)) *par9))"),('item_manaafterkill',"ln56+(((skill('Engorge'.blvl)) *par9))")]
 assert s['aurastatcalc1']==-33 and tables['states']['hexsiphon']['group']==6 and tables['states']['hexsiphondebuff']['group']==7 and not tables['states']['hexsiphondebuff'].get('curse')
 for name,id,callback in [('item_healafterkill',86,28),('item_manaafterkill',138,17)]:
  row=tables['itemstatcost'][name];assert row['*ID']==id and row['itemevent1']=='kill' and row['itemeventfunc1']==callback
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def body(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  inputs[str(p.relative_to(ROOT))]=sha(p);b=function(raw.decode(),name);routines[name]=hashlib.sha256(b.encode()).hexdigest();return b
 old=ROOT/'tools/reference/hex-bane-source-evidence.json';proof=json.loads(old.read_text());inputs[str(old.relative_to(ROOT))]=sha(old)
 p=BASE/'hex-bane/bane-buff.cpp';assert sha(p)==proof['artifacts'][str(p.relative_to(ROOT))];inputs[str(p.relative_to(ROOT))]=sha(p)
 cpp=p.read_text().split('int main(){printf(',1)[0]
 for a,b in [('nAuraState=198','nAuraState=200'),('wAuraStat[6]={-1,-1,-1,-1,-1,-1}','wAuraStat[6]={17,-1,-1,-1,-1,-1}'),('nPassiveStat[5]={52,53,119,-1,-1}','nPassiveStat[5]={119,-1,86,-1,138}'),('dwPassiveCalc[5]={1,2,3,0,0}','dwPassiveCalc[5]={3,0,1,0,2}')]:
  assert a in cpp;cpp=cpp.replace(a,b)
 for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10EC0']),('source/D2Game/src/SKILLS/SkillDruid.cpp',['sub_6FCFE200']),('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay'])]:
  for n in names:assert body(rel,n) in cpp
 cpp+=r'''int main(){printf("[");bool first=true;
for(int oldRank:{0,1,20,21})for(int rank:{1,20,21})for(int type:{0,1})for(int mode=0;mode<8;mode++){
 D2StatListStrc old;old.state=200;old.skill=386;old.rank=oldRank;old.stats[86]=77;old.stats[138]=88;old.stats[119]=99;
 D2UnitStrc target;target.dwUnitType=type;target.list=oldRank?&old:nullptr;
 guard=mode;mon.dwMonStatsFlags=mode==3?1:0;mon2.dwFlags=mode==5?0:1;skill={};skill.nAuraState=mode==7?256:200;
 values[0]=3600+300*(rank-1);values[1]=rank+1;values[2]=rank+1;values[3]=20+9*(rank-1);freed=events=unregs=0;registrations.clear();
 int ok=SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay(&game,&target,386,rank);auto* active=target.list;
 bool descriptors=registrations.size()==3&&registrations[0]==std::array<int,4>{1,36,rank,200}&&registrations[1]==std::array<int,4>{2,36,rank,200}&&registrations[2]==std::array<int,4>{3,36,rank,200};
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",oldRank,rank,type,mode,ok,active?active->rank:0,active?active->expire:0,active?active->stats[86]:0,active?active->stats[138]:0,active?active->stats[119]:0,freed,events,unregs,int(descriptors));first=false;
}printf("]\n");}'''
 kill=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_PLAYER=0,STAT_MANA=1,STAT_HITPOINTS=2;
struct D2GameStrc{};struct D2DamageStrc{};struct D2UnitStrc{int dwUnitType=0,current=0,maximum=0;}actor,victim;
int amount=0,overlay=0,expectedStat=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,uint32_t stat,int packed){if(stat!=uint32_t(expectedStat)||packed!=(expectedStat<<16))__builtin_abort();return amount;}
int STATLIST_GetMaxManaFromUnit(D2UnitStrc* p){return p->maximum;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->maximum;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p->current;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int,int value,int){p->current=value;}
void UNITS_SetOverlay(D2UnitStrc*,int value,int){overlay=value;}
'''
 for n in ['SKILLITEM_EventFunc17_ManaAfterKill','SKILLITEM_EventFunc28_HealAfterKill']:kill+='\n'+body('source/D2Game/src/SKILLS/SkillItem.cpp',n)+'\n'
 kill+=r'''int main(){printf("[");bool first=true;
for(int kind:{17,28})for(int type:{0,1})for(int hasActor:{0,1})for(int hasVictim:{0,1})for(int value:{0,1,2,21,275}){
 int maximum=(kind==17?167:251)*256;
 for(int current:{1,255,256,maximum-257,maximum-1,maximum,maximum+1}){
 actor={type,current,maximum};amount=value;overlay=0;expectedStat=kind==17?138:86;
 int ok=kind==17?SKILLITEM_EventFunc17_ManaAfterKill(nullptr,0,hasActor?&actor:nullptr,hasVictim?&victim:nullptr,nullptr,expectedStat<<16,1):SKILLITEM_EventFunc28_HealAfterKill(nullptr,0,hasActor?&actor:nullptr,hasVictim?&victim:nullptr,nullptr,expectedStat<<16,1);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,type,hasActor,hasVictim,value,current,maximum,ok,actor.current,overlay);first=false;
 }}printf("]\n");}'''
 artifacts=[];data={}
 for n,source in [('buff',cpp),('kill',kill)]:
  p=STAGE/(n+'.cpp');p.write_text(source);compiler=p.with_suffix('.compiler.log');sanitizer=p.with_suffix('.sanitizer.log')
  with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
  run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:];data[n]=json.loads(run.stdout);artifacts += [p,compiler,sanitizer]
 assert len(data['buff'])==192 and len(data['kill'])==560
 for old,rank,type,mode,ok,current,expiry,life,mana,attack,freed,events,unregs,descriptors in data['buff']:
  invalid=mode in [6,7] or mode==1 or type==1 and mode in [2,3,4,5];success=not invalid and not old>rank
  assert (ok,current,expiry,life,mana,attack,freed,events,unregs,descriptors)==((1,rank,400+3600+300*(rank-1),rank+1,rank+1,20+9*(rank-1),int(old>0 and old!=rank),1,1,1) if success else (0,old,500 if old else 0,77 if old else 0,88 if old else 0,99 if old else 0,0,0,0,0))
 for kind,type,actor,victim,amount,current,maximum,ok,after,overlay in data['kill']:
  eligible=actor and victim and amount and current<maximum and (kind==28 or type==0)
  assert (ok,after,overlay)==(int(bool(eligible)),min(maximum,current+amount*256) if eligible else current,(152 if kind==17 else 151) if eligible else 0)
 out=BASE/'hex-siphon-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n');artifacts += [out,Path(__file__).resolve()]
 research=ROOT/'.local/research/warlock-community/warlock-skills-notes.txt';inputs[str(research.relative_to(ROOT))]=sha(research)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,inputs=inputs,routines=routines,skill=s,buffCases=192,killCases=560,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
  established=['Current386writes owner item_tohit_percent119,healafterkill86andmanaafterkill138. Kill stats are rank+hardEngorge;debuff duration150+5hardEldritchBlast;buff3600+300per extra rank;mana(25+rank-1)/2. Enemy damagepercent is constant-33;unused Param11/12do not override the actual aurastatcalc1.',
   'Pinned itemstatcost maps life to kill/event28andmana to kill/event17. The complete older routines execute560guard/type/amount/fraction/cap contexts;they add amount<<8to existingFP balances,cap to current maxima and do not inspect victim hex states. Mana requires a player attacker;life permits other source types. Native integration remains scoped to class players.',
   'The fresh copied declared ABI prefix retains complete unchanged do23/factory/passive routines and executes192Siphon sparse-stat/registration contexts. Lower same-skill rank rejects,equal refreshes expiry and rewrites passive values,higher replaces;three event36descriptors retain cast rank. Earlier accepted Bane stage is read/hash-checked and not regenerated.'],
  services=['Formula results,stat storage/allocation,kill-stat getter,maxima,clock/event registry and overlays are declared services. Current formulas and native actual resource/death consumers are checked separately. No new event36body or modern group6/7replacement is supplied.',
   'Positive/zero after-kill modifiers are admitted. Negative signed-stat and integer-overflow contexts are not tested or generalized. Source null/monster/overmaximum guards are separate from the player-only native consumer.'],
  remaining=['Automatic hex application,event36/hextriggerqualifications,current cross-hex group replacement and SC/input remain unresolved. Prepared cross-hex combinations are refused rather than silently stacked or replaced.',
   'Primary supplied research reports dependent kills do not trigger Siphon and owner spell kills do;its exact executable build is unstated. Pinned generic item-kill callbacks have no victim-hex requirement;modern special overrides and full owner/facet/logout/PvP/client behavior remain acceptance work. User has no Hex trained;no points/respec requested.'])
 target=ROOT/'tools/reference/hex-siphon-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Hex Siphon:192complete buff and560complete generic kill contexts;event36/hex groups remain separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
