#!/usr/bin/env python3
"""Execute complete hit-success routine with a monster/trap owner and explicit stats."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';path='source/D2Game/src/UNIT/SUnitDmg.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);text=raw.decode();name='SUNITDMG_IsHitSuccessful';m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);i=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
 body=text[m.start():i];stats=sorted(set(re.findall(r'\bSTAT_\w+',body)))
 cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <iterator>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONTYPEFLAG_UNIQUE=1,MONTYPEFLAG_SUPERUNIQUE=2;
struct D2UnitStrc{int dwUnitType=UNIT_MONSTER,dwClassId=413,pSeed=0,defense=0,stats[64]={};}actor,target;
struct D2MonStatsTxt{int wMonType=0;}row;
struct D2StatStrc{int nLayer=0,nValue=0;};
int draw=0,healCalls=0;
'''+ 'enum{'+','.join(stats)+'};\n'+r'''
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
int UNITS_GetDefense(D2UnitStrc* p){assert(p==&target);return p->defense;}
int UNITS_GetAttackRate(D2UnitStrc*){assert(false);return 0;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,int){assert(false);return 0;}
int MONSTERS_IsBoss(void*,D2UnitStrc*){assert(false);return 0;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){assert(false);return 0;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return (int)((int64_t)value*percent/divisor);}
int D2Clamp(int value,int minimum,int maximum){return std::clamp(value,minimum,maximum);}
int MONSTERS_IsDemon(D2UnitStrc*){assert(false);return 0;}int MONSTERS_IsUndead(D2UnitStrc*){assert(false);return 0;}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){assert(false);return nullptr;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){assert(false);return 0;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){assert(false);return &row;}
int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){assert(false);return 0;}
int SUNITDMG_CheckMonType(int,int){assert(false);return 0;}
int ITEMS_RollRandomNumber(int*){return draw;}
void SUNITDMG_PreventMonsterHeal(D2UnitStrc* a,D2UnitStrc* d){assert(a==&actor && d==&target);healCalls++;}
'''+body+r'''
int main(){bool first=true;printf("[");for(int level:{1,30,99})for(int targetLevel:{1,30,99})for(int rating:{0,250,5000})for(int pct:{-150,0,28})for(int defense:{-30,0,1000})for(int missileRating:{0,37})for(int dex:{0,20})for(int roll:{0,4,5,49,94,95,99}){
actor={};target={};actor.stats[STAT_LEVEL]=level;actor.stats[STAT_TOHIT]=rating;actor.stats[STAT_ITEM_TOHIT_PERCENT]=pct;actor.stats[STAT_DEXTERITY]=dex;
// These player-only modifiers must not leak into the monster branch.
actor.stats[STAT_ITEM_IGNORETARGETAC]=1;actor.stats[STAT_ITEM_FRACTIONALTARGETAC]=99;actor.stats[STAT_ITEM_DEMON_TOHIT]=500;actor.stats[STAT_ITEM_UNDEAD_TOHIT]=700;
target.stats[STAT_LEVEL]=targetLevel;target.defense=defense-17;target.stats[STAT_ARMORCLASS_VS_MISSILE]=17;draw=roll;healCalls=0;
int result=SUNITDMG_IsHitSuccessful(&actor,&target,missileRating,1);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,targetLevel,rating,pct,defense,missileRating,dex,roll,result,healCalls);first=false;
}printf("]\n");}
'''
 p=STAGE/'assassin-trap-attack-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==6804
 trunc=lambda n,d:abs(n)//d*(1 if n>=0 else -1)
 for level,targetLevel,rating,pct,defense,missileRating,dex,roll,result,heal in data:
  attack=missileRating+5*dex+rating;attack+=trunc(attack*pct,100);d=defense
  if d<0:attack-=d;d=0
  if attack<0:d-=attack;attack=0
  d=max(0,d);factor=100*attack//(attack+d) if attack+d else 100;chance=max(5,min(95,2*level*factor//(level+targetLevel)));expected=int(roll<chance)
  assert (result,heal)==(expected,expected)
 out=STAGE/'assassin-trap-attack-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],source=dict(path=path,sha256=hashlib.sha256(raw).hexdigest()),compiledUnchangedRoutine=dict(name=name,sha256=hashlib.sha256(body.encode()).hexdigest()),fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=6804),established=[
 'Full hit-success routine runs the nonplayer branch for actual blade owner: explicit missile tohit is flat, then5*pet Dexterity plus pet tohit, with pet item_tohit_percent. Player-only ignore defense, fractional defense and demon/undead flat AR inputs are deliberately nonzero but never read.',
 'It handles negative total defense and negative resulting AR before integer factor100*AR/(AR+defense), then integer level weighting and clamp5..95. RNG equality fails (roll>=chance). Successful rolls call the declared prevent-heal service exactly once.',
 'Independent arithmetic checks cover levels1/30/99 on both sides, base AR0/250/5000, percent-150/0/28, total defense-30/0/1000 including missile-defense17, explicit missile tohit0/37, Dexterity0/20 and seven threshold rolls.'
 ],services=['Source stat/defense getters, percentage helper and explicit RNG integer are declared services. PreventMonsterHeal is a recorded call only; actual item prevent-heal eligibility and state effects are not executed. Full hit-success control flow executes unchanged.','No live native trap context or new global player hit-chance behavior is enabled. Exact current executable stats/rounding/RNG and the whole defense/event/state dependency graph remain separate.'])
 p=ROOT/'tools/reference/assassin-trap-attack-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Assassin trap attack:6804 full monster-owner hit-success cases pass, including integer rounding, negative stats and player-only modifier exclusion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
