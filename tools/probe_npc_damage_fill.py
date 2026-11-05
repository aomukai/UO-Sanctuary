#!/usr/bin/env python3
"""Run the full pinned damage filler with explicit stat/hand/target/event services."""
import argparse,hashlib,itertools,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-damage-fill-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def enum(text,name):
 raw=re.search(r'enum '+re.escape(name)+r'\s*\{(.*?)\};',text,re.S).group(1);raw=re.sub(r'/\*.*?\*/|//[^\n]*','',raw,flags=re.S);out={};value=-1
 for part in raw.split(','):
  if not part.strip():continue
  key,sep,n=part.strip().partition('=');value=int(n.strip(),0)if sep else value+1;out[key.strip()]=value
 return out
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 stats=enum(read('source/D2Common/include/D2StatList.h'),'D2C_ItemStats');hits=enum(read('source/D2Game/include/UNIT/SUnitDmg.h'),'D2DamageHitFlags');elements=enum(read('source/D2Common/include/D2Combat.h'),'D2C_ElemTypes')
 results=enum(read('source/D2Game/include/UNIT/SUnitDmg.h'),'D2DamageResultFlags');assert results['DAMAGERESULTFLAG_CRITICALSTRIKE']==8192 and results['DAMAGERESULTFLAG_32']==32
 source=read('source/D2Game/src/UNIT/SUnitDmg.cpp');unique=read('source/D2Game/src/MONSTER/MonsterUnique.cpp');mode=read('source/D2Game/src/MONSTER/MonsterMode.cpp')
 for text,names in [(unique,['MONSTERUNIQUE_CalculatePercentage']),(source,['SUNITDMG_ApplyDamageBonuses','SUNITDMG_RollDamageValueInRange','SUNITDMG_SetHitClass','SUNITDMG_FillDamageValues']),(mode,['D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70'])]:
  for name in names:bodies[name]=function(text,name)
 bound=set(re.findall(r'\bSTAT_[A-Z0-9_]+','\n'.join(bodies.values())))
 cs=ROOT/'src/Sanctuary.Content/NpcDamageFill.cs';inputs[str(cs.relative_to(ROOT))]=sha(cs)
 typed=dict((k,int(v))for k,v in re.findall(r'^    ([A-Z_]+)=(\d+),',cs.read_text(),re.M));assert len(typed)==36 and all(stats['STAT_'+n]==v for n,v in typed.items())
 # Literal bit/element branches in the port are checked against the actual
 # pinned enum namespaces,independent of a fixture's supplied services.
 assert {k:hits['DAMAGEHITFLAG_'+k]for k in ['1','32','LIFEDRAIN','MANADRAIN','STAMINADRAIN','BYPASSUNDEAD','BYPASSDEMON','BYPASSBEASTS']}=={'1':1,'32':32,'LIFEDRAIN':4,'MANADRAIN':8,'STAMINADRAIN':16,'BYPASSUNDEAD':256,'BYPASSDEMON':512,'BYPASSBEASTS':1024}
 assert [elements['ELEMTYPE_'+k]for k in ['FIRE','LTNG','MAGIC','COLD','POIS','RAND','BURN','FREEZE']]==[1,2,3,4,5,10,11,12]
 alignment=enum(read('source/D2Common/include/Units/Units.h'),'D2C_UnitAlignment');events=enum(read('source/D2Game/include/UNIT/SUnitEvent.h'),'D2C_UnitEventTypes: uint8_t // Events.txt');itemtypes=enum(read('source/D2Common/include/D2Items.h'),'D2C_ItemTypes')
 assert alignment['UNIT_ALIGNMENT_GOOD']==2 and events['UNITEVENT_ATTACKEDINMELEE']==3 and itemtypes['ITEMTYPE_BLUNT']==57
 statnames=sorted(bound);constants='\n'.join('constexpr int '+n+'='+str(stats[n])+';'for n in statnames)+'\n'+'\n'.join('constexpr int '+n+'='+str(v)+';'for n,v in hits.items())+'\n'+'\n'.join('constexpr int '+n+'='+str(v)+';'for n,v in elements.items())+'\n'
 constants+='\nconstexpr int UNIT_ALIGNMENT_GOOD='+str(alignment['UNIT_ALIGNMENT_GOOD'])+',ITEMTYPE_BLUNT='+str(itemtypes['ITEMTYPE_BLUNT'])+',UNITEVENT_ATTACKEDINMELEE='+str(events['UNITEVENT_ATTACKEDINMELEE'])+';\n'
 cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define UNIT_PLAYER 0
#define UNIT_MONSTER 1
constexpr int DAMAGERESULTFLAG_CRITICALSTRIKE=8192,DAMAGERESULTFLAG_32=32;
struct D2UnitStrc{int dwUnitType=1,dwClassId=0,pSeed=0;void*pInventory=nullptr;std::array<int,368>stats{};};
struct D2DamageStrc{int dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwColdDamage=0,dwMagDamage=0,dwPoisDamage=0,dwColdLen=0,dwPoisLen=0,dwStunLen=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0,dwEnDmgPct=0;uint32_t dwHitFlags=0;int wResultFlags=0,nConvType=0,dwConvPct=0,dwFrzLen=0,dwBurnLen=0;uint32_t dwHitClass=0;};
struct D2GameStrc{};struct D2StatStrc{int nLayer,nValue;};struct D2MonStatsTxt{int nCrit=0,wMonType=0;};
D2UnitStrc actor,target,weapon;D2MonStatsTxt mon,targetMon;int style=0,draws=0,edge=0,overlay=0,alignment=0,hire=0,armed=0,two=0,strength=100,dexterity=75,masteryDamage=50,masteryCrit=35,blunt=0,mutate=0,eventChange=0;
bool demon=false,undead=false;std::vector<std::array<int,3>>trace;
'''+constants+r'''
uint32_t ITEMS_RollRandomNumber(int*){const uint32_t v[4][4]={{0,0,0,0},{99,0,99,0},{35,50,45,5},{99,99,99,99}};uint32_t value=v[style][draws++%4];trace.push_back({1,int(value),0});if(mutate)actor.stats[STAT_PASSIVE_FIRE_MASTERY]+=7;return value;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){assert(bound>0);int value=edge?bound-1:0;trace.push_back({0,bound,value});if(mutate){actor.stats[STAT_PASSIVE_CRITICAL_STRIKE]=100;actor.stats[STAT_POISON_COUNT]=3;}return value;}
int STATLIST_UnitGetStatValue(const D2UnitStrc*u,int stat,int){return u->stats[stat];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*u,int stat,int layer){return STATLIST_UnitGetStatValue(u,stat,layer);}
void STATLIST_SetUnitStat(D2UnitStrc*u,int stat,int value,int){u->stats[stat]=value;trace.push_back({4,stat,value});}
int STATLIST_GetUnitAlignment(D2UnitStrc*){return alignment;}
int STATLIST_CopyStats(D2UnitStrc*,int stat,D2StatStrc*out,size_t cap){assert(stat==STAT_DAMAGE_VS_MONTYPE&&cap==128);for(int i=0;i<128;i++)out[i]={i+1,i%3==0?-5:10};return 128;}
int SUNITDMG_CheckMonType(int layer,int type){return layer%3==type%3;}
bool MONSTERS_IsDemon(D2UnitStrc*){return demon;}bool MONSTERS_IsUndead(D2UnitStrc*){return undead;}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int id){return id==10000?&targetMon:&mon;}
D2UnitStrc*sub_6FC7C7B0(D2UnitStrc*){return armed?&weapon:nullptr;}
void sub_6FC7C900(D2UnitStrc*,int ranged){trace.push_back({2,ranged,0});}
void sub_6FC7CA10(D2UnitStrc*,int ranged){trace.push_back({3,ranged,0});}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){return two?2:1;}
int ITEMS_GetStrengthBonus(D2UnitStrc*){return strength;}int ITEMS_GetDexBonus(D2UnitStrc*){return dexterity;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*w,int,int kind){assert(kind==1||kind==2);return w?(kind==1?masteryDamage:masteryCrit):0;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int type){assert(type==ITEMTYPE_BLUNT);return blunt;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return hire;}
void UNITS_SetOverlay(D2UnitStrc*,int value,int){overlay=value;trace.push_back({6,value,0});}
void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*p){assert(event==UNITEVENT_ATTACKEDINMELEE);trace.push_back({5,event,0});if(eventChange){p->dwPhysDamage+=257;p->nConvType=ELEMTYPE_RAND;p->dwConvPct=37;}}
int32_t SUNITDMG_RollDamageValueInRange(D2UnitStrc*,int32_t,int32_t,int32_t,int32_t,int32_t);
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*);
'''+ '\n'.join(bodies.values())
 main=r'''
int main(){printf("[");bool first=true;
for(int kind:{0,1})for(int hired:{0,1})for(int good:{0,1})for(int range:{0,1})for(int share:{0,1,64,128,255})for(int variant=0;variant<16;variant++)for(int roll=0;roll<4;roll++){
actor={};target={};weapon={};weapon.dwUnitType=4;target.dwClassId=10000;actor.dwUnitType=kind;target.dwUnitType=variant==15?0:1;actor.pInventory=&actor;alignment=good?UNIT_ALIGNMENT_GOOD:0;hire=hired;mon={variant%3==0?0:variant%3==1?5:100,0};targetMon={0,variant%2?7:0};demon=variant%2;undead=variant%3==0;armed=variant%4!=0;two=variant%3==0;blunt=variant%5==0;style=roll;draws=0;edge=variant%2;overlay=0;mutate=variant==14;eventChange=variant==13;masteryDamage=50;masteryCrit=35;strength=100;dexterity=75;trace.clear();
auto Set=[&](int stat,int value){actor.stats[stat]=value;};
Set(STAT_MINDAMAGE,3);Set(STAT_MAXDAMAGE,11);Set(STAT_SECONDARY_MINDAMAGE,13);Set(STAT_SECONDARY_MAXDAMAGE,21);Set(STAT_ITEM_NORMALDAMAGE,2);Set(STAT_STRENGTH,17);Set(STAT_DEXTERITY,11);Set(STAT_DAMAGEPERCENT,25);Set(STAT_ITEM_MINDAMAGE_PERCENT,37);Set(STAT_ITEM_MAXDAMAGE_PERCENT,51);
Set(STAT_ITEM_DAMAGETARGETAC,-25);Set(STAT_ITEM_DEMONDAMAGE_PERCENT,50);Set(STAT_ITEM_UNDEADDAMAGE_PERCENT,-25);target.stats[STAT_ARMORCLASS]=20;
Set(STAT_PASSIVE_CRITICAL_STRIKE,50);Set(STAT_ITEM_DEADLYSTRIKE,45);
Set(STAT_FIREMINDAM,2);Set(STAT_FIREMAXDAM,6);Set(STAT_LIGHTMINDAM,3);Set(STAT_LIGHTMAXDAM,7);Set(STAT_COLDMINDAM,5);Set(STAT_COLDMAXDAM,8);Set(STAT_MAGICMINDAM,1);Set(STAT_MAGICMAXDAM,3);Set(STAT_COLDLENGTH,31);Set(STAT_STUNLENGTH,47);
Set(STAT_PASSIVE_FIRE_MASTERY,37);Set(STAT_PASSIVE_LTNG_MASTERY,27);Set(STAT_PASSIVE_COLD_MASTERY,51);Set(STAT_PASSIVE_MAG_MASTERY,13);Set(STAT_PASSIVE_POIS_MASTERY,25);
Set(STAT_POISONMINDAM,769);Set(STAT_POISONMAXDAM,1537);Set(STAT_POISONLENGTH,103);Set(STAT_POISON_COUNT,2);
Set(STAT_LIFEDRAINMINDAM,1);Set(STAT_LIFEDRAINMAXDAM,3);Set(STAT_MANADRAINMINDAM,2);Set(STAT_MANADRAINMAXDAM,4);Set(STAT_STAMDRAINMINDAM,3);Set(STAT_STAMDRAINMAXDAM,5);
Set(STAT_SKILL_BYPASS_UNDEAD,1);Set(STAT_SKILL_BYPASS_DEMONS,1);Set(STAT_SKILL_BYPASS_BEASTS,1);
if(variant==1){for(int stat:{STAT_FIREMAXDAM,STAT_LIGHTMAXDAM,STAT_COLDMAXDAM,STAT_MAGICMAXDAM,STAT_POISONMAXDAM,STAT_LIFEDRAINMAXDAM,STAT_MANADRAINMAXDAM,STAT_STAMDRAINMAXDAM})Set(stat,0);}
if(variant==2)Set(STAT_SKILL_POISON_OVERRIDE_LENGTH,10);if(variant==3)Set(STAT_POISON_COUNT,0);if(variant==4)Set(STAT_POISON_COUNT,3);
if(variant==5){for(int stat:{STAT_PASSIVE_FIRE_MASTERY,STAT_PASSIVE_LTNG_MASTERY,STAT_PASSIVE_COLD_MASTERY,STAT_PASSIVE_MAG_MASTERY,STAT_PASSIVE_POIS_MASTERY})Set(stat,-100);}
D2DamageStrc p;if(variant%2){p.dwPhysDamage=257;p.dwFireDamage=129;p.dwLtngDamage=37;p.dwMagDamage=53;p.dwColdDamage=71;p.dwPoisDamage=769;p.dwColdLen=7;p.dwPoisLen=11;p.dwStunLen=13;p.dwLifeLeech=5;p.dwManaLeech=7;p.dwStamLeech=9;p.dwEnDmgPct=17;}
if(variant==5){p.dwColdDamage=0;p.dwColdLen=0;}
if(variant==6){p.dwPhysDamage=p.dwFireDamage=p.dwColdDamage=p.dwMagDamage=p.dwLtngDamage=p.dwPoisDamage=-257;}
if(variant==7)p.dwHitFlags=DAMAGEHITFLAG_1|DAMAGEHITFLAG_LIFEDRAIN|DAMAGEHITFLAG_MANADRAIN|DAMAGEHITFLAG_STAMINADRAIN;
if(variant>=8&&variant<=12){p.nConvType=variant==8?ELEMTYPE_FIRE:variant==9?ELEMTYPE_POIS:variant==10?ELEMTYPE_BURN:variant==11?ELEMTYPE_FREEZE:ELEMTYPE_RAND;p.dwConvPct=variant==10?125:37;}
if(variant==15)p.wResultFlags=DAMAGERESULTFLAG_32;p.dwHitClass=variant%2?0x63:3;
printf("%s{\"args\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"stats\":[",first?"":",",kind,hired,good,range,share,variant,roll,armed,two,blunt,mon.nCrit,targetMon.wMonType,demon,undead,mutate,eventChange);first=false;
bool comma=false;for(int stat=0;stat<368;stat++)if(actor.stats[stat]){printf("%s[%d,%d]",comma?",":"",stat,actor.stats[stat]);comma=true;}printf("],\"initial\":[");
auto Packet=[&](){printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%u,%d",p.dwPhysDamage,p.dwFireDamage,p.dwLtngDamage,p.dwColdDamage,p.dwMagDamage,p.dwPoisDamage,p.dwColdLen,p.dwPoisLen,p.dwStunLen,p.dwLifeLeech,p.dwManaLeech,p.dwStamLeech,p.dwEnDmgPct,p.dwHitFlags,p.wResultFlags,p.nConvType,p.dwConvPct,p.dwFrzLen,p.dwBurnLen,p.dwHitClass,overlay);};Packet();
SUNITDMG_FillDamageValues(nullptr,&actor,&target,&p,range,share);printf("],\"output\":[");Packet();printf("],\"armor\":%d,\"trace\":[",target.stats[STAT_ARMORCLASS]);for(size_t i=0;i<trace.size();i++)printf("%s[%d,%d,%d]",i?",":"",trace[i][0],trace[i][1],trace[i][2]);printf("]}");
}printf("]\n");}
'''
 cpp+=main
 for n,b in bodies.items():assert function(cpp,n)==b,n
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
 cases=json.loads(run.stdout);assert len(cases)==5120,len(cases);out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 for name in ['src/Sanctuary.Content/NpcDamageFill.cs','src/Sanctuary.Content/NpcEquipmentElements.cs','src/Sanctuary.Content/WeaponPoisonPackets.cs','src/Sanctuary.Content/NpcEquipmentDamage.cs','src/Sanctuary.Content/NamedCombat.cs',str(Path(__file__).resolve().relative_to(ROOT))]:inputs[name]=sha(ROOT/name)
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,cases=5120,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},statBindings=typed,serviceBindings=dict(goodAlignment=2,attackedInMelee=3,blunt=57,criticalResult=8192,eventSkipResult=32),inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in(path,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log')},scope='Six full unchanged older routines:FillDamageValues,ApplyDamageBonuses,element range,percentage,intrinsic critical,hit class. Stat arrays,hand metadata/mastery,alignment,target species/type,armor write,copy128,event and mutable RNG services are declared. Complete source caller and arithmetic,not complete stat engine,live source graph,recipient HP/events,current executable or learned actors.')
 target=ROOT/'tools/reference/npc-damage-fill-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Full fill proof stale'
 else:target.write_text(encoded)
 print('NPC full damage fill:5120complete caller contexts/6routines;header-verified stats/flags/elements;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
