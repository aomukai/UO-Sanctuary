#!/usr/bin/env python3
"""Pin Bane and execute shared do23/factory/passive routes; event36 is absent."""
import argparse,ast,csv,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'hex-bane'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 inputs={};routines={};ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());tables={}
 for name in ['skills','states','itemstatcost']:
  p=ref/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
 skill=tables['skills']['385'];assert skill['srvdofunc']==23 and [skill['auraevent'+str(i)] for i in range(1,4)]==['domeleeattack','domissiledamage','hextrigger'] and all(skill['auraeventfunc'+str(i)]==36 for i in range(1,4))
 groups={str(g):sorted((s['state'],s['*ID'],s.get('curse',0)) for s in tables['states'].values() if s.get('group')==g) for g in [6,7]}
 assert groups=={'6':[('hexbane',198,0),('hexpurge',202,0),('hexsiphon',200,0)],'7':[('hexbanedebuff',199,0),('hexpurgedebuff',217,0),('hexsiphondebuff',201,0)]}
 assert [tables['itemstatcost'][n]['*ID'] for n in ['magicmindam','magicmaxdam','item_tohit_percent']]==[52,53,119]
 calc=BASE/'warlock-foundation/skillcalc.txt';receipt=BASE/'warlock-foundation/skillcalc-extraction.json';assert sha(calc)==json.loads(receipt.read_text())['sha256']
 rows=list(csv.DictReader(calc.read_text().splitlines(),delimiter='\t'));assert [(i,r['code']) for i,r in enumerate(rows) if r['code'] in ['enma','exma']]==[(49,'enma'),(50,'exma')]
 for p in [calc,receipt]:inputs[str(p.relative_to(ROOT))]=sha(p)
 def body(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  inputs[str(p.relative_to(ROOT))]=sha(p);b=function(raw.decode(),name);routines[name]=hashlib.sha256(b.encode()).hexdigest();return b
 # Copy only the declared ABI prefix; never execute the old producer or alter its accepted artifacts.
 old=ROOT/'tools/probe_inner_sight_reference.py';inputs[str(old.relative_to(ROOT))]=sha(old)
 node=next(x.value for x in ast.walk(ast.parse(old.read_text())) if isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in x.targets))
 while isinstance(node,ast.BinOp):node=node.left
 cpp=ast.literal_eval(node)
 cpp=cpp.replace('#include <initializer_list>','#include <initializer_list>\n#include <map>\n#include <vector>\n#include <array>')
 cpp=cpp.replace('value=-99;','value=-99;std::map<int,int> stats;')
 cpp=cpp.replace('nItemStatCostTxtRecordCount=512;','nItemStatCostTxtRecordCount=512;int pItemStatCostTxt[512]={};')
 cpp=cpp.replace('struct D2SkillsTxt{int dwCalc[1]={0};};','struct D2SkillsTxt{int nAuraState=198,dwAuraLenCalc=0,wAuraStat[6]={-1,-1,-1,-1,-1,-1},nPassiveStat[5]={52,53,119,-1,-1},dwPassiveCalc[5]={1,2,3,0,0},wAuraEvent[3]={1,2,3},wAuraEventFunc[3]={36,36,36};}skill;')
 cpp=cpp.replace('STAT_MODIFIERLIST_LEVEL=7;','STAT_MODIFIERLIST_LEVEL=7,STAT_ATTACKRATE=68,STAT_OTHER_ANIMRATE=69;using D2C_UnitEventTypes=int;')
 cpp=cpp.replace('return state==17?p->list:nullptr;','return p->list&&p->list->state==state?p->list:nullptr;')
 cpp=cpp.replace('p->stat=stat;p->value=value;','p->stat=stat;p->value=value;p->stats[stat]=value;')
 cpp=cpp.replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 0;}','int values[4];int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return values[formula];}')
 cpp+='''\nD2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return guard==6?nullptr:&skill;}
void STATLIST_SetStat(D2StatListStrc* p,int id,int value,int){p->stats[id]=value;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){}
int unregs=0;std::vector<std::array<int,4>> registrations;
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){unregs++;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int event,int id,int rank,int callback,int,int state){registrations.push_back({event,callback,rank,state});}
'''
 for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10EC0']),('source/D2Game/src/SKILLS/SkillDruid.cpp',['sub_6FCFE200']),('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay'])]:
  for n in names:cpp+='\n'+body(rel,n)+'\n'
 callback=body('source/D2Common/src/D2Skills.cpp','SKILLS_GetSpecialParamValue');assert 'case 49:' in callback and 'SKILLS_GetMinElemDamage(pUnit, nSkillId, nSkillLevel, 1) >> 8' in callback
 cpp+=r'''int main(){printf("[");bool first=true;
for(int oldRank:{0,1,20,21})for(int rank:{1,20,21})for(int type:{0,1})for(int mode=0;mode<8;mode++){
 D2StatListStrc old;old.state=198;old.skill=385;old.rank=oldRank;old.stats[52]=77;old.stats[53]=88;old.stats[119]=99;
 D2UnitStrc target;target.dwUnitType=type;target.list=oldRank?&old:nullptr;
 guard=mode;mon.dwMonStatsFlags=mode==3?1:0;mon2.dwFlags=mode==5?0:1;skill={};skill.nAuraState=mode==7?256:198;
 values[0]=3600+300*(rank-1);values[1]=rank+3;values[2]=rank+7;values[3]=20+9*(rank-1);freed=events=unregs=0;registrations.clear();
 int ok=SKILLS_SrvDo023_Blaze_EnergyShield_SpiderLay(&game,&target,385,rank);auto* active=target.list;
 bool descriptors=registrations.size()==3&&registrations[0]==std::array<int,4>{1,36,rank,198}&&registrations[1]==std::array<int,4>{2,36,rank,198}&&registrations[2]==std::array<int,4>{3,36,rank,198};
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",oldRank,rank,type,mode,ok,active?active->rank:0,active?active->expire:0,active?active->stats[52]:0,active?active->stats[53]:0,active?active->stats[119]:0,freed,events,unregs,int(descriptors));first=false;
}printf("]\n");}'''
 p=STAGE/'bane-buff.cpp';p.write_text(cpp);compiler=p.with_suffix('.compiler.log');sanitizer=p.with_suffix('.sanitizer.log')
 with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 cases=json.loads(run.stdout);assert len(cases)==192
 for old,rank,type,mode,ok,current,expiry,minimum,maximum,attack,freed,events,unregs,descriptors in cases:
  invalid=mode in [6,7] or mode==1 or type==1 and mode in [2,3,4,5];success=not invalid and not old>rank
  assert ok==int(success)
  assert (current,expiry,minimum,maximum,attack,freed,events,unregs,descriptors)==((rank,400+3600+300*(rank-1),rank+3,rank+7,20+9*(rank-1),int(old>0 and old!=rank),1,1,1) if success else (old,500 if old else 0,77 if old else 0,88 if old else 0,99 if old else 0,0,0,0,0))
 out=BASE/'hex-bane-source-probe.json';out.write_bytes(run.stdout)
 research=ROOT/'.local/research/warlock-community/warlock-skills-notes.txt';assert 'Does not require a successful hit check to apply hex when applied by a melee weapon' in research.read_text();inputs[str(research.relative_to(ROOT))]=sha(research)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,inputs=inputs,routines=routines,skill=skill,stateGroups=groups,cases=192,
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),p,compiler,sanitizer,out]},
  established=['Local pinned skillcalc aliases49/50are enma/exma. The reviewed complete legacy SkillCalc returns mastered elemental endpoints shifted right8for those callbacks. Bane stores whole-point magicmindam52/magicmaxdam53,not FP256 values. Nonzero modern magic mastery is excluded from prepared capture.',
   'Complete legacy do23/factory/passive writer execute192contexts including lower/equal/higher same-skill state, invalid rows/states,NPC and attackability guards. Lower rank rejects, equal refreshes expiry but rewrites passive values and descriptors, higher replaces. All three event descriptors register36with cast rank. No event36body is available in this legacy engine.',
   'Current noncurse group6contains all three weapon hexes; noncurse group7contains their enemy debuffs. Legacy do23does not call the group-clearing helper used by do18,and the old factory does not implement these modern groups. Cross-hex replacement is not inferred.',
   'Current damage uses hard Consume/HexPurge/MirroredBlades35percent per point,not the unused Param8value50. Buff3600+300per rank;debuff150+5per hard EldritchBlast;AR20+9per rank;defense-minusmin(30+rank-1,50);targetARminus25;mana(12+rank-1)/2.'],
  services=['Formula values,statlist storage,allocation,event queue/registry and source masks are declared services. Formula consumers and actual persistence are checked separately in native C#. No complete current executable or new event36body is claimed.'],
  remaining=['Automatic event36target/eligibility/order,including melee miss versus missile damage and new hextrigger,need integration. Supplied primary research reports melee attempts and successful missile hits but exact executable build is unstated.',
   'User has no Hex trained; their Warlock uses summons,sigils and the BladeWarp path. No respec or points requested. Cross-hex group6/7replacement remains uncertain. Modern mastery/double-dip,death/owner transition nuance,SC/input and complete client acceptance remain separate.'])
 target=ROOT/'tools/reference/hex-bane-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Hex Bane:192complete do23/factory/passive/registration cases; new event36and hex groups remain separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
