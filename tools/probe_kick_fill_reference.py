#!/usr/bin/env python3
"""Execute the complete source melee fill/element-roll bodies for prepared kick packets."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/UNIT/SUnitDmg.cpp';source_bytes=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(source_bytes)).encode()+b'\0'+source_bytes).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=source_bytes.decode();blocks={}
 for name in ['SUNITDMG_RollDamageValueInRange','SUNITDMG_FillDamageValues']:
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end]
 bodies='\n'.join(blocks.values());stats=sorted(set(re.findall(r'\bSTAT_\w+',bodies)));fields=sorted(set(re.findall(r'pDamage->(\w+)',bodies)))
 cpp='#include <algorithm>\n#include <cstdint>\n#include <cstdio>\n#include <cstdlib>\n#include <iterator>\n#include <initializer_list>\n#define __fastcall\n'
 cpp+='enum Stat{'+','.join(stats)+',STAT_COUNT};\n'
 cpp+='struct D2DamageStrc{'+''.join('int '+n+'=0;' for n in fields)+'};\n'
 cpp+=r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ALIGNMENT_GOOD=1,ITEMTYPE_BLUNT=1,DAMAGEHITFLAG_1=1,DAMAGEHITFLAG_32=32,DAMAGEHITFLAG_LIFEDRAIN=256,DAMAGEHITFLAG_MANADRAIN=512,DAMAGEHITFLAG_STAMINADRAIN=1024,DAMAGEHITFLAG_BYPASSUNDEAD=2048,DAMAGEHITFLAG_BYPASSDEMON=4096,DAMAGEHITFLAG_BYPASSBEASTS=8192,DAMAGERESULTFLAG_CRITICALSTRIKE=2,DAMAGERESULTFLAG_32=32,UNITEVENT_ATTACKEDINMELEE=1;
enum Element{ELEMTYPE_RAND,ELEMTYPE_FIRE,ELEMTYPE_LTNG,ELEMTYPE_MAGIC,ELEMTYPE_COLD,ELEMTYPE_POIS,ELEMTYPE_BURN,ELEMTYPE_FREEZE};
struct D2UnitStrc{int dwUnitType=UNIT_PLAYER,dwClassId=0,pSeed=0,stats[STAT_COUNT]={};};struct D2GameStrc{};
struct D2MonStatsTxt{int wMonType=0;};struct D2StatStrc{int nLayer,nValue;};
D2GameStrc game;D2UnitStrc owner,target;int endpoint=0,randomCalls=0,criticalCalls=0,fillHands=0,restoreHands=0,events=0,monsterCalls=0;
void sub_6FC7C900(D2UnitStrc*,int){fillHands++;}void sub_6FC7CA10(D2UnitStrc*,int){restoreHands++;}
int STATLIST_GetUnitAlignment(D2UnitStrc*){return UNIT_ALIGNMENT_GOOD;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}
bool MONSTERS_IsDemon(D2UnitStrc*){return false;}bool MONSTERS_IsUndead(D2UnitStrc*){return false;}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return nullptr;}bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){return false;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return nullptr;}int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){return 0;}bool SUNITDMG_CheckMonType(int,int){return false;}
int SUNITDMG_ApplyDamageBonuses(D2UnitStrc*,int,D2UnitStrc*,int,int,int,int damage,uint8_t){return damage;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){return 100;}
unsigned ITEMS_RollRandomNumber(int*){criticalCalls++;return 0;}
int ITEMS_RollLimitedRandomNumber(int*,int span){randomCalls++;return endpoint?std::max(0,span-1):0;}
int MONSTERUNIQUE_CalculatePercentage(int v,int p,int d){return int(int64_t(v)*p/d);}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){events++;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){monsterCalls++;}
'''+bodies+r'''
int main(){bool comma=false;printf("[");target.dwUnitType=UNIT_MONSTER;
for(int flags:{2,3})for(int raw:{0,257,25600})for(int mastery:{0,25})for(int roll:{0,1})for(int elements:{0,1})for(int venom:{0,10})for(int source:{0,128})for(int guard:{0,1,2}){
 owner={};owner.stats[STAT_PASSIVE_CRITICAL_STRIKE]=owner.stats[STAT_ITEM_DEADLYSTRIKE]=100;endpoint=roll;randomCalls=criticalCalls=fillHands=restoreHands=events=monsterCalls=0;
 if(elements){owner.stats[STAT_FIREMINDAM]=3;owner.stats[STAT_FIREMAXDAM]=7;owner.stats[STAT_COLDMINDAM]=2;owner.stats[STAT_COLDMAXDAM]=5;owner.stats[STAT_LIGHTMINDAM]=1;owner.stats[STAT_LIGHTMAXDAM]=9;owner.stats[STAT_MAGICMINDAM]=owner.stats[STAT_MAGICMAXDAM]=4;owner.stats[STAT_POISONMINDAM]=96;owner.stats[STAT_POISONMAXDAM]=128;owner.stats[STAT_POISONLENGTH]=75;owner.stats[STAT_POISON_COUNT]=2;owner.stats[STAT_COLDLENGTH]=50;}
 owner.stats[STAT_PASSIVE_FIRE_MASTERY]=owner.stats[STAT_PASSIVE_COLD_MASTERY]=owner.stats[STAT_PASSIVE_LTNG_MASTERY]=owner.stats[STAT_PASSIVE_MAG_MASTERY]=owner.stats[STAT_PASSIVE_POIS_MASTERY]=mastery;owner.stats[STAT_SKILL_POISON_OVERRIDE_LENGTH]=venom;owner.stats[STAT_LIFEDRAINMINDAM]=10;owner.stats[STAT_MANADRAINMINDAM]=7;
 D2DamageStrc d;d.dwHitFlags=flags;d.dwPhysDamage=raw;d.wResultFlags=1;SUNITDMG_FillDamageValues(&game,guard==1?nullptr:&owner,guard==2?nullptr:&target,&d,1,source);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,raw,mastery,roll,elements,venom,source,guard,d.dwPhysDamage,d.dwFireDamage,d.dwColdDamage,d.dwLtngDamage,d.dwMagDamage,d.dwPoisDamage,d.dwPoisLen,d.dwColdLen,d.dwLifeLeech,d.dwManaLeech,d.dwHitFlags,d.wResultFlags,randomCalls,criticalCalls,fillHands,restoreHands,events,monsterCalls);
}printf("]\n");}
'''
 p=STAGE/'kick-fill-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-pie','-no-pie',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==576
 def typed(lo,hi,mastery,roll):
  lo=lo*256+lo*256*mastery//100;hi=hi*256+hi*256*mastery//100;return hi-1 if roll and hi>lo else lo
 for flags,raw,mastery,roll,elements,venom,source,guard,physical,fire,cold,lightning,magic,poison,plen,clen,life,mana,hit,result,random,critical,hands,restore,events,monster in rows:
  if guard:assert [physical,fire,cold,lightning,magic,poison,plen,clen,life,mana,hit,result,random,critical,hands,restore,events,monster]==[raw,0,0,0,0,0,0,0,0,0,flags,1,0,0,0,0,0,0]
  else:
   expected=[typed(3,7,mastery,roll),typed(2,5,mastery,roll),typed(1,9,mastery,roll),typed(4,4,mastery,roll)] if elements else [0]*4
   poisonLo=96+96*mastery//100;poisonHi=128+128*mastery//100;poisonExpected=(poisonHi-1 if roll else poisonLo) if elements else 0
   assert [physical,fire,cold,lightning,magic,poison,plen,clen,life,mana,hit,result,random,critical,hands,restore,events,monster]==[raw*(2 if flags==2 else 1),*expected,poisonExpected,(venom or 37) if elements else 0,50 if elements else 0,10,7,flags|32,3 if flags==2 else 1,4 if elements else 0,1 if flags==2 else 0,1,1,1,1]
 fixture=STAGE/'kick-fill-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',component='Prepared kick source fill and typed damage',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(source_bytes).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(p.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete kick flags3skip physical recomputation and mastery/passive Critical Strike/Deadly Strike;ordinary flags2positive controls double physical with a critical draw. Typed fire/lightning/cold/magic/poison,drains,lengths and attacked-in-melee dispatch remain active.','Source0defaults to128and retains full typed stats. Poison length averages contributors unless an explicit override is present;cold duration is collected only when positive cold damage exists.','Complete typed roll helper applies mastery before exclusive-end range draw. Missing attacker/defender returns without any damage/stat/hand/event work.'],services=['Player Assassin/ordinary non-demon/non-undead target and explicit stat endpoints/mastery/poison contributors/drains are supplied services. Hand-context calls are counters;full selected-hand stat posting is separate evidence.','Ordinary physical recomputation is an identity service for the positive control;monster critical/event dispatches are counters. No full target mitigation,proc,health,charge or learned finisher acceptance is claimed.','Conversion fields stay0;Fists progressive conversion/inline and Tiger/Cobra helpers remain separately compiled source evidence. Native material/gear/stat graph and current executable parity remain acceptance work.'],limits=['Source composition only;does not enable a learned Dragon skill or prove modern charged hit/block/consumption/payment.','Final effects and icons remain separate placeholders.'])
 out=ROOT/'tools/reference/kick-fill-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Kick fill evidence stale'
 else:out.write_text(encoded)
 print('Kick fill:576 complete fill/typed-roll contexts pass;prepared physical bypasses critical while typed stats/drain/duration/event collection remains active.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
