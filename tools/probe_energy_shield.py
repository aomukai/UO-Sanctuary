#!/usr/bin/env python3
"""Prepare complete older Energy Shield callback/percentage comparisons; no runtime acceptance."""
import hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
from review_weapon_event_order import function
S=ROOT/'.local/class-skills/energy-shield-source-preparation'
tree=json.load(open(ROOT/'.local/class-skills/d2moo-source-tree.json'))
assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blocks={};hashes={}
for rel,name in [('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_EventFunc24_EnergyShield'),('source/D2Game/src/MONSTER/MonsterUnique.cpp','MONSTERUNIQUE_CalculatePercentage')]:
 raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 blocks[name]=function(raw.decode(),name);hashes[rel]=hashlib.sha256(raw).hexdigest()
cpp=r'''
#include <cstdint>
#include <cstddef>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_MONSTER=1,STAT_MANA=1;
struct D2GameStrc{} game;
struct D2UnitStrc{int dwUnitType=0,mana=0,hire=0;} owner,other;
struct D2DamageStrc{int32_t dwPhysDamage,dwFireDamage,dwLtngDamage,dwColdDamage,dwMagDamage,dwLifeLeech,dwManaLeech,dwStamLeech;};
static_assert(sizeof(D2DamageStrc)==8*sizeof(int32_t));
struct D2SkillsTxt{int nAuraState=17,dwCalc[2]={0,1},wPrgOverlay=0;} row;
struct D2StatListStrc{} list;
struct D2EnergyShieldDataStrc{int32_t nDamageOffset,bPlayerOnly;};
struct Tables{int nStatesTxtRecordCount=200,nOverlayTxtRecordCount=100;} tables,*sgptDataTables=&tables;
bool hasRow=true,present=true;int multiplier=20,divisor=32,freed=0,toggled=0,overlay=-1,reads=0,writes=0;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return present?&list:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int){return f?divisor:multiplier;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int,int){reads++;return u->mana;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* u){return u->hire;}
void STATLIST_SetUnitStat(D2UnitStrc* u,int,int n,int){u->mana=n;writes++;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 10;}int CLIENTS_GetUnitY(D2UnitStrc*){return 20;}
int UNITS_GetDirectionToCoords(D2UnitStrc*,int,int){return 3;}int D2Common_11053(int n){return n;}
void UNITS_SetOverlay(D2UnitStrc*,int n,int){overlay=n;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*){present=false;}
void STATLIST_FreeStatList(D2StatListStrc*){freed++;}
void STATES_ToggleState(D2UnitStrc*,int,int){toggled++;}
'''
cpp+=blocks['MONSTERUNIQUE_CalculatePercentage']+'\n'+blocks['SKILLS_EventFunc24_EnergyShield']
cpp+=r'''
int32_t& Field(D2DamageStrc& p,int i){return *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(&p)+i*sizeof(int32_t));}
int main(int argc,char** argv){
if(argc>1){printf("[");for(int g=0;g<8;g++){
owner={};owner.mana=777;other={};row={};row.nAuraState=g==3?-1:g==4?200:17;
hasRow=g!=2;present=g!=5;multiplier=g==6?0:g==7?-1:20;divisor=32;
freed=toggled=reads=writes=0;overlay=-1;D2DamageStrc packet={1000,1000,1000,1000,1000,1000,1000,1000};
int ret=SKILLS_EventFunc24_EnergyShield(&game,0,g==0?nullptr:&owner,&other,g==1?nullptr:&packet,58,3);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",g?",":"",g,ret,owner.mana,reads,writes,freed,toggled,overlay);
}printf("]\n");return 0;}
printf("[");bool first=true;
for(int mana:{0,1,15,255,256,257,1048577,12800000})for(int mult:{1,20,95})for(int div:{-1,0,1,12,31,32})for(int actor=0;actor<4;actor++)for(int pattern=0;pattern<11;pattern++)for(int art:{-1,0,100,101}){
owner={};owner.mana=mana;other={};other.dwUnitType=actor>=2?1:0;other.hire=actor==3?1:0;
multiplier=mult;divisor=div;row={};row.wPrgOverlay=art;hasRow=present=true;freed=toggled=reads=writes=0;overlay=-1;
D2DamageStrc packet={};auto p=[&](int i)->int32_t&{return Field(packet,i);};
if(pattern>=1&&pattern<=8)p(pattern-1)=1001;
if(pattern==9){int v[8]={1001,513,257,2049,1025,401,999,1777};for(int i=0;i<8;i++)p(i)=v[i];}
if(pattern==10){p(0)=1048577;p(1)=1001;p(5)=1048577;}
int ret=SKILLS_EventFunc24_EnergyShield(&game,0,&owner,actor?&other:nullptr,&packet,58,3);
printf("%s[[%d,%d,%d,%d,%d,%d],[%d",first?"":",",mana,mult,div,actor,pattern,art,ret);
for(int i=0;i<8;i++)printf(",%d",p(i));printf(",%d,%d,%d,%d,%d,%d]]",owner.mana,freed,toggled,overlay,reads,writes);first=false;
}printf("]\n");}
'''
p=S/'probe.cpp';p.write_text(cpp)
subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(S/'probe')],check=True)
cases=json.loads(subprocess.check_output([str(S/'probe')]))
guards=json.loads(subprocess.check_output([str(S/'probe'),'guards']))
assert guards==[[g,0,777,0,0,0,0,-1] for g in range(8)]
(S/'guards.json').write_text(json.dumps(guards,separators=(',',':'))+'\n')
def percent(a,b,c):
 if a<=1048576:
  if b<=65536:return a*b//c
  if c<=b>>4:return a*(b//c)
 elif c<=a>>4:return b*(a//c)
 return a*b//c
mismatches=0;damage_only_differences=0
for args,out in cases:
 mana,mult,div,actor,pattern,art=args;div=max(div,1)
 raw=[0]*8
 if 1<=pattern<=8:raw[pattern-1]=1001
 if pattern==9:raw=[1001,513,257,2049,1025,401,999,1777]
 if pattern==10:raw=[1048577,1001,0,0,0,1048577,0,0]
 expected=raw[:];old=raw[:];m=mana;om=mana;absorbed=0
 for i,v in enumerate(raw):
  if v<=0 or (i>=5 and actor not in (0,2)):continue
  n=min(percent(v,mult,100),percent(m,16,div));m=max(0,m-percent(n,div,16));expected[i]-=n;absorbed+=n
  n=min(v*mult//100,om*16//div);om-=n*div//16;old[i]-=n
 assert out==[1,*expected,m,int(m==0),0,(art+3 if actor and absorbed>0 and 0<=art<=100 else -1),1,1],(args,out,expected)
 mismatches+=expected!=old or m!=om
 damage_only_differences+=actor==1 and pattern in (0,1,2,3,4,5,10) and (expected!=old or m!=om)
assert len(cases)==25344
(S/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(S/'probe-san')],check=True)
with (S/'sanitizer.log').open('wb') as log:assert json.loads(subprocess.check_output([str(S/'probe-san')],stderr=log))==cases
assert not (S/'sanitizer.log').read_bytes()
with (S/'guard-sanitizer.log').open('wb') as log:assert json.loads(subprocess.check_output([str(S/'probe-san'),'guards'],stderr=log))==guards
assert not (S/'guard-sanitizer.log').read_bytes()
result=dict(legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},cases=len(cases),guardCases=len(guards),naiveExactDivisionDifferences=mismatches,damageOnlyDifferences=damage_only_differences,sanitizers='ASan/UBSan clean',limitations=['Complete older callback/percentage helper only,not modern executable proof. Supplied formula/state/stat/overlay-direction/hireling services;cast helper,arbitrary drain adapters and modern event order not yet accepted.'])
table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.load(open(table.parent.parent/'sources.json'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(table)==manifest['files']['json/skills.json']['sha256']
row=next(r for r in json.load(open(table)).values() if r.get('skill')=='Energy Shield')
assert row['*Id']==58 and row['srvdofunc']==23 and row['auraeventfunc1']==24
result.update(referenceVersion='3.3.93847',currentTable={str(table.relative_to(ROOT)):sha(table)},currentRow=row,artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),p,S/'probe',S/'probe-san',S/'sanitizer.log',S/'cases.json',S/'guards.json',S/'guard-sanitizer.log')})
encoded=json.dumps(result,indent=2)+'\n'
(S/'preparation.json').write_text(encoded)
out=ROOT/'tools/reference/energy-shield-source-evidence.json'
if '--check' in sys.argv:assert out.read_text()==encoded
else:out.write_text(encoded)
print('Energy Shield preparation:',len(cases),'complete callback/percentage cases;',mismatches,'differ from naive exact multiplication/division;sanitizers clean.')
