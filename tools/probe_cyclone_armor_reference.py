#!/usr/bin/env python3
"""Execute complete Cyclone Armor event25; preserve declared source services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(path,name):
  s=source(path);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;end=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
  text=s[m.start():end];blocks[name]=hashlib.sha256(text.encode()).hexdigest();return text+'\n'
 ref=ROOT/'tools/reference/d2r-named-equipment';pins={};tables={};manifest=json.loads((ref/'sources.json').read_text())
 for name in ['skills','states']:
  raw=(ref/'json'/(name+'.json')).read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==manifest['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(raw)
 skill=next(x for x in tables['skills'].values() if x.get('*Id')==235);state=next(x for x in tables['states'].values() if x.get('state')=='cyclonearmor')
 assert skill['srvdofunc']==18 and skill['aurastate']=='cyclonearmor' and skill['aurastat1']=='bonearmor' and skill['aurastat2']=='bonearmormax' and skill['auraevent1']=='absorbdamage' and skill['auraeventfunc1']==25
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __fastcall
struct D2GameStrc{};
struct D2SkillsTxt{int nAuraState=3;int wAuraStat[2]={1,2};}row;
struct D2StatListStrc{int stats[4]={};}list;
struct D2UnitStrc{}actor,other;
struct D2DamageStrc{int dwFireDamage=0,dwColdDamage=0,dwLtngDamage=0,dwPhysDamage=19,dwPoisDamage=23,dwMagDamage=29;};
struct Tables{int nStatesTxtRecordCount=8,nItemStatCostTxtRecordCount=4;}tables;Tables* sgptDataTables=&tables;
constexpr int STAT_UNSENTPARAM1=0;bool rowPresent=true,listPresent=true;int removed=0,freed=0,gfx=0,detached=0;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==235);return rowPresent?&row:nullptr;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){assert(p==&actor&&state==3);return listPresent?&list:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc* p,int stat,int layer){assert(p==&list&&stat>=0&&stat<4&&layer==0);return p->stats[stat];}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int value,int layer){assert(p==&list&&stat>=0&&stat<4&&layer==0);p->stats[stat]=value;}
void STATES_ToggleState(D2UnitStrc* p,int state,int on){assert(p==&actor&&state==3&&on==0);removed++;}
void D2Common_10474(D2UnitStrc* p,D2StatListStrc* s){assert(p==&actor&&s==&list);detached++;}
void STATLIST_FreeStatList(D2StatListStrc* p){assert(p==&list);freed++;}
void STATES_ToggleGfxStateFlag(D2UnitStrc* p,int state,int on){assert(p==&actor&&state==3&&on==1);gfx++;}
'''
 cpp+=routine('source/D2Game/src/MONSTER/MonsterUnique.cpp','MONSTERUNIQUE_CalculatePercentage')
 cpp+=routine('source/D2Game/src/SKILLS/SkillDruid.cpp','D2GAME_EventFunc25_6FD00140')
 cpp+=r'''
int main(){D2GameStrc game;bool first=true;printf("[");
 for(int guard=0;guard<=10;guard++)for(int capacity:{0,1,127,256,512,1024,8192})for(int maximum:{0,8192})
 for(int fire:{0,1,128,256,513,4096})for(int cold:{0,1,128,256,513,4096})for(int lightning:{0,1,128,256,513,4096})for(int old:{0,25,100}){
 if(guard && (capacity!=256||maximum!=8192||fire!=513||cold!=513||lightning!=513||old!=25))continue;
 row={};rowPresent=guard!=3;listPresent=guard!=6;if(guard==4)row.nAuraState=-1;if(guard==5)row.nAuraState=8;
 if(guard==7)row.wAuraStat[1]=-1;if(guard==8)row.wAuraStat[1]=4;if(guard==9)row.wAuraStat[0]=-1;if(guard==10)row.wAuraStat[0]=4;
 list={};list.stats[0]=old;list.stats[1]=capacity;list.stats[2]=maximum;removed=freed=gfx=detached=0;
 D2DamageStrc damage;damage.dwFireDamage=fire;damage.dwColdDamage=cold;damage.dwLtngDamage=lightning;
 int result=D2GAME_EventFunc25_6FD00140(&game,0,guard==1?nullptr:&actor,&other,guard==2?nullptr:&damage,235,1);
 assert(damage.dwPhysDamage==19&&damage.dwPoisDamage==23&&damage.dwMagDamage==29&&removed==freed&&freed==detached);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,capacity,maximum,fire,cold,lightning,old,result,list.stats[1],damage.dwFireDamage,damage.dwColdDamage,damage.dwLtngDamage,removed,freed,gfx,list.stats[0]);first=false;
 }printf("]\n");}
'''
 p=STAGE/'cyclone-armor-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('')
 subprocess.run(['g++','-std=c++17','-O1','-g','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-Wno-misleading-indentation','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'cyclone-armor-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 rows=json.loads(run.stdout);assert len(rows)==9082
 for g,cap,maximum,f,c,l,old,result,left,of,oc,ol,removed,freed,gfx,new in rows:
  valid=g==0 and maximum>0;remaining=cap;outputs=[]
  for amount in [f,c,l]:
   absorb=min(remaining,amount) if valid else 0;remaining-=absorb;outputs.append(amount-absorb)
  assert result==int(valid) and [of,oc,ol]==outputs and left==remaining
  assert removed==freed==int(valid and remaining==0)
  percent=100*remaining//maximum if maximum else 0;updates=valid and remaining>0 and percent-old>=5
  assert gfx==int(updates) and new==(percent if updates else old)
 out=STAGE/'cyclone-armor-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');assert damage.index('SUNITEVENT_Trigger(pGame, UNITEVENT_ABSORBDAMAGE')<damage.index('SUNITDMG_ApplyResistancesAndAbsorb(&damageInfo')
 fields=['skill','*Id','srvdofunc','aurastate','aurastat1','aurastatcalc1','aurastat2','aurastatcalc2','auraevent1','auraeventfunc1','auralencalc','anim','manashift','mana','lvlmana','Param1','Param2','Param8']
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes=blocks,pinnedInputHashes=pins,skill={k:skill.get(k) for k in fields},state=state,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),established=['Complete event25 and source percentage helper execute9072positive/nonpositive-maximum contexts and10distinct missing/invalid actor/damage/row/state/list/stat guards.','A shared FP256 pool consumes fire, then cold, then lightning; physical/poison/magic remain untouched. Zero remaining with positive maximum removes/unlinks/frees once.','Current player235 uses shared defensive-buff do18, blank auralencalc and equal current/maximum formula with hard Twister/Tornado/Hurricane7percent synergies; lifetime/creation still need composition.','Inspected complete damage reducer dispatches ABSORBDAMAGE before resistances/MDR; this ordering inspection is separate from compiled event scope.'],limits=['Stat/state storage, row lookup, layout and unlink/free/gfx services are declared fixtures, not the modern executable or complete defensive-buff caster.','The older gfx percentage update tests new-old>=5, so decreasing values do not refresh that parameter. Do not silently reinterpret it as absolute difference or infer modern display behavior.','Native incoming weapon/spell/area producer coverage, shield priority, persistence, casting/payment/cancellation and client self-targeting remain to implement/verify. Missing final effects/icons stay separate.'],sanitizers='ASan/UBSan exit0 and empty stderr required')
 ep=ROOT/'tools/reference/cyclone-armor-source-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert ep.read_text()==text
 else:ep.write_text(text)
 print('9082complete Cyclone Armor event/guard cases pass; no native handler enabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
