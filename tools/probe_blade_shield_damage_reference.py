#!/usr/bin/env python3
"""Run complete pinned melee weapon fill with Blade Shield's fraction/flags.

Inventory/stat collection, skill endpoints, target classification, RNG and item
registration are explicit fixtures. Full damage mitigation/events are separate.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
R=Path(__file__).resolve().parents[1];S=R/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((S/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def source(path):
 b=(S/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(b).hexdigest();return b.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');header=source('source/D2Game/include/UNIT/SUnitDmg.h');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
bodies=[routine(unique,'MONSTERUNIQUE_CalculatePercentage'),routine(damage,'SUNITDMG_ApplyDamageBonuses'),routine(damage,'SUNITDMG_RollDamageValueInRange'),routine(damage,'SUNITDMG_FillDamageValues')]
joined='\n'.join(bodies);stats=sorted(set(re.findall(r'\bSTAT_[A-Z0-9_]+',joined)));fields=sorted(set(re.findall(r'pDamage->(\w+)',joined)))
flags={n:int(v,16) for n,v in re.findall(r'^\s*((?:DAMAGERESULTFLAG|DAMAGEHITFLAG)_\w+)\s*=\s*(0x[0-9a-fA-F]+)',header,re.M)}
cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <iterator>
#include <map>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ALIGNMENT_GOOD=1,ITEMTYPE_BLUNT=2,UNITEVENT_ATTACKEDINMELEE=3;
enum{ELEMTYPE_FIRE=1,ELEMTYPE_LTNG,ELEMTYPE_MAGIC,ELEMTYPE_COLD,ELEMTYPE_POIS,ELEMTYPE_BURN,ELEMTYPE_FREEZE,ELEMTYPE_RAND};
'''
cpp+='enum{'+','.join(stats)+',MASTERY_DAMAGE,MASTERY_CRITICAL};\n'
cpp+='constexpr uint32_t '+','.join(n+'='+str(v) for n,v in flags.items())+';\n'
cpp+='struct D2DamageStrc{'+''.join('int '+n+'=0;' for n in fields)+'};\n'
cpp+=r'''
struct D2GameStrc{};struct D2UnitStrc{int dwUnitType=0,dwClassId=0,pSeed=0;void* pInventory=this;std::map<int,int> stats;bool armed=false,demon=false,undead=false,blunt=false;};
struct D2StatStrc{int nLayer=0,nValue=0;};struct D2MonStatsTxt{int wMonType=0;}mon;
int high=0,criticalRoll=99,draws=0,events=0;
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc* u){return u->armed?u:nullptr;}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int){return u?u->stats[stat]:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* u,int stat,int layer){return STATLIST_UnitGetStatValue(u,stat,layer);}
int ITEMS_GetStrengthBonus(D2UnitStrc*){return 75;}int ITEMS_GetDexBonus(D2UnitStrc*){return 75;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc* u,D2UnitStrc* weapon,int,int mode){return weapon?u->stats[mode==2?MASTERY_CRITICAL:MASTERY_DAMAGE]:0;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){draws++;return high&&bound>0?bound-1:0;}
uint32_t ITEMS_RollRandomNumber(int*){draws++;return criticalRoll;}
void sub_6FC7C900(D2UnitStrc*,int){}void sub_6FC7CA10(D2UnitStrc*,int){}
int STATLIST_GetUnitAlignment(D2UnitStrc*){return 0;}bool MONSTERS_IsDemon(D2UnitStrc* u){return u->demon;}bool MONSTERS_IsUndead(D2UnitStrc* u){return u->undead;}
int ITEMS_CheckItemTypeId(D2UnitStrc* u,int){return u->blunt;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,size_t){return 0;}bool SUNITDMG_CheckMonType(int,int){return false;}
void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){u->stats[stat]=value;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){events++;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc* a,D2UnitStrc*,D2DamageStrc*){assert(a->dwUnitType==UNIT_PLAYER);}
'''
cpp+='\n'.join(bodies)
cpp+=r'''
int main(){D2GameStrc game;bool first=true;printf("[");
for(int armed:{0,1})for(int minimum:{1,10})for(int percent:{-90,0,100})for(int skill:{256,8192})for(int elemental:{0,1})for(int mastery:{0,50})for(int critical:{0,1,2,3})for(int critRoll:{0,99})for(int roll:{0,1})for(int species:{0,1,2}){
 D2UnitStrc a,d;d.dwUnitType=UNIT_MONSTER;a.armed=armed;a.blunt=armed;d.demon=species==1;d.undead=species==2;
 a.stats[STAT_MINDAMAGE]=a.stats[STAT_SECONDARY_MINDAMAGE]=minimum;a.stats[STAT_MAXDAMAGE]=a.stats[STAT_SECONDARY_MAXDAMAGE]=minimum+7;
 a.stats[STAT_STRENGTH]=20;a.stats[STAT_DEXTERITY]=30;a.stats[STAT_DAMAGEPERCENT]=percent;a.stats[MASTERY_DAMAGE]=20;
 a.stats[MASTERY_CRITICAL]=critical==1?35:0;a.stats[STAT_PASSIVE_CRITICAL_STRIKE]=critical==2?35:0;a.stats[STAT_ITEM_DEADLYSTRIKE]=critical==3?35:0;
 a.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=10;a.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=20;a.stats[STAT_LIFEDRAINMINDAM]=6;a.stats[STAT_MANADRAINMINDAM]=7;
 for(auto stat:{STAT_FIREMINDAM,STAT_LIGHTMINDAM,STAT_COLDMINDAM,STAT_MAGICMINDAM})a.stats[stat]=elemental?1:0;
 for(auto stat:{STAT_FIREMAXDAM,STAT_LIGHTMAXDAM,STAT_COLDMAXDAM,STAT_MAGICMAXDAM})a.stats[stat]=elemental?5:0;
 for(auto stat:{STAT_PASSIVE_FIRE_MASTERY,STAT_PASSIVE_LTNG_MASTERY,STAT_PASSIVE_COLD_MASTERY,STAT_PASSIVE_MAG_MASTERY,STAT_PASSIVE_POIS_MASTERY})a.stats[stat]=mastery;
 a.stats[STAT_POISONMINDAM]=elemental?20:0;a.stats[STAT_POISONMAXDAM]=elemental?40:0;a.stats[STAT_POISONLENGTH]=101;a.stats[STAT_POISON_COUNT]=2;a.stats[STAT_COLDLENGTH]=100;
 high=roll;criticalRoll=critRoll;draws=events=0;D2DamageStrc damage;damage.dwPhysDamage=skill;damage.wResultFlags=32;damage.dwHitFlags=2;
 SUNITDMG_FillDamageValues(&game,&a,&d,&damage,0,96);assert(events==0&&damage.dwLifeLeech==6&&damage.dwManaLeech==7);
 if(!first)printf(",");first=false;
 printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",armed,minimum,percent,skill,elemental,mastery,critical,critRoll,roll,species,damage.dwPhysDamage,damage.dwFireDamage,damage.dwLtngDamage,damage.dwColdDamage,damage.dwMagDamage,damage.dwPoisDamage,damage.dwPoisLen,damage.dwColdLen,(damage.wResultFlags&DAMAGERESULTFLAG_CRITICALSTRIKE)!=0,draws);
}printf("]\n");}
'''
path=S/'blade-shield-damage-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==4608,len(data)
fixture=S/'blade-shield-damage-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(binary)+'-san'],check=True)
with (S/'blade-shield-damage-sanitizer.log').open('wb') as log:checked=json.loads(subprocess.check_output([str(binary)+'-san'],stderr=log))
assert checked==data and not (S/'blade-shield-damage-sanitizer.log').read_bytes()
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes=blocks,completePlayerFillCases=len(data),fixture=dict(path=str(fixture.relative_to(R)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete ApplyDamageBonuses adds the existing skill roll to the independently enhanced/rolled current weapon, then scales their sum96/128. Complete FillDamageValues doubles that physical result once in mastery/generic/deadly order.','Each current weapon typed endpoint is mastery-enhanced and rolled before96/128 scaling. Poison rate scales while its source count/override length remains independent; ordinary player leech percentages remain unscaled.','Current result flag32 suppresses the attacked-in-melee callback in complete FillDamageValues. Complete ExecuteEvents is separately reviewed, not compiled here: its successful weapon and kill callback blocks also require flag32 absent.'],limits=['Stat composition, selected weapon, physical skill roll, class/species and RNG are explicit fixtures. No claim of whole current executable equivalence.','Full mitigation, block/hit result, actual native delivery, item registration, durability and complete ExecuteEvents are separate boundaries.','Conversion, monster/hireling attacker, nonzero normal-damage stat, independent min/max percentages, stat damage-vs-montype and stun source lanes are compiled but not covered by this player matrix.'],handlerEnabled=False)
p=R/'tools/reference/blade-shield-damage-source-evidence.json'
if args.check:assert json.loads(p.read_text())==e
else:p.write_text(json.dumps(e,indent=2)+'\n')
print('Blade Shield:4608 complete player melee fill cases pass ASan/UBSan; explicit service boundaries retained.')
