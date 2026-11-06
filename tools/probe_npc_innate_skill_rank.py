#!/usr/bin/env python3
"""Exercise both complete older skill getters with NPC item stat services."""
import hashlib,json,random,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-innate-skill-rank-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 skill=source('source/D2Common/src/D2Skills.cpp');bodies={n:function(skill,n)for n in ['SKILLS_GetBonusSkillLevel','SKILLS_GetSkillLevel']}
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __stdcall
#define __fastcall
using BOOL=int;constexpr int UNIT_PLAYER=0,D2UnitInvalidGUID=-1,STATE_SHRINE_SKILL=50;
enum{STAT_ITEM_ALLSKILLS,STAT_ITEM_ADDCLASSSKILLS,STAT_ITEM_ADDSKILL_TAB,STAT_ITEM_NONCLASSSKILL,STAT_ITEM_ELEMSKILL,STAT_ITEM_SINGLESKILL};
struct D2SkillDescTxt{int nSkillPage=1;};struct D2SkillsTxt{int nSkillId=9,nCharClass=0,wSkillDesc=0,nEType=0;};
struct D2SkillStrc{D2SkillsTxt*pSkillsTxt=nullptr;int nSkillLevel=0,nLevelBonus=0,nOwnerGUID=-1;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=0;int stats[6]{};bool shrine=false;};
struct Tables{D2SkillsTxt*pSkillsTxt=nullptr;}table;Tables*sgptDataTables=&table;
D2SkillsTxt row;D2SkillDescTxt desc;bool valid=true;int maximum=99;
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int){return valid?&row:nullptr;}
D2SkillDescTxt*DATATBLS_GetSkillDescTxtRecord(int){assert(false);return &desc;}
int DATATBLS_GetMaxLevel(int kind){assert(kind==0);return maximum;}
bool STATES_CheckState(D2UnitStrc*u,int state){assert(state==50);return u&&u->shrine;}
int STATLIST_UnitGetStatValue(D2UnitStrc*u,int stat,int){assert(stat!=STAT_ITEM_ADDCLASSSKILLS&&stat!=STAT_ITEM_ADDSKILL_TAB);return u?u->stats[stat]:0;}
'''+ '\n'.join(bodies.values())+r'''
int main(){int count;assert(scanf("%d",&count)==1);printf("[");for(int i=0;i<count;i++){
int a[15];for(int&v:a)assert(scanf("%d",&v)==1);D2UnitStrc u;D2SkillStrc s;s.pSkillsTxt=a[13]?&row:nullptr;s.nSkillLevel=a[0];s.nLevelBonus=a[1];s.nOwnerGUID=a[7]?123:D2UnitInvalidGUID;
u.stats[STAT_ITEM_ALLSKILLS]=a[2];u.stats[STAT_ITEM_NONCLASSSKILL]=a[3];u.stats[STAT_ITEM_SINGLESKILL]=a[4];u.stats[STAT_ITEM_ELEMSKILL]=a[5];u.shrine=a[9];row.nEType=a[6];maximum=a[10];valid=a[14];
printf("%s%d",i?",":"",SKILLS_GetSkillLevel(a[11]?&u:nullptr,a[12]?&s:nullptr,a[8]));}printf("]\n");}
'''
 rng=random.Random(0x10968);cases=[]
 for i in range(8192):
  a=[rng.choice([-10,0,1,3,20,98,99,255]),rng.choice([-2,0,2]),rng.choice([-5,0,1,10]),rng.choice([-5,0,1,3,9]),rng.choice([-5,0,1,10]),rng.choice([-5,0,9]),rng.randrange(2),int(i%7==0),int(i%11!=0),int(i%5==0),99,int(i%31!=0),int(i%29!=0),int(i%23!=0),int(i%19!=0)]
  cases.append(dict(Args=a))
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe';request=STAGE/'input.txt';request.write_text(' '.join(map(str,[len(cases)]+[n for c in cases for n in c['Args']]))+'\n')
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=STAGE/'output.json';output.write_bytes(run.stdout);observed=json.loads(run.stdout);assert len(observed)==len(cases)
 for c,o in zip(cases,observed):c['Rank']=o
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 prior=ROOT/'tools/reference/oak-skill-rank-engine-evidence.json';e=json.loads(prior.read_text());assert e['experience']['maximumLevels']['Amazon']==99
 inputs[str(prior.relative_to(ROOT))]=sha(prior)
 proof=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,
  scope='Complete older bonus/level getters with NPC unit kind1;class/tree branches abort if reached. Explicit item stat,row validity,shrine,item ownership and pinned class0MaxLvl99 services;no skill entry creation or passive-stat-list refresh claimed.',
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,request,output,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]})
 (ROOT/'tools/reference/npc-innate-skill-rank-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n');print('NPC innate skill rank:8192contexts,two complete getters;ASan/UBSan clean.')
if __name__=='__main__':main()
