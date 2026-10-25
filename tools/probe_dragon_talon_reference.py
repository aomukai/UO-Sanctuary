#!/usr/bin/env python3
"""Bounded complete legacy Dragon Talon start/sequence control flow, not kick damage."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 text=raw.decode();blocks={}
 for name in ['SKILLS_SrvSt24_DragonTalon','SKILLS_SrvDo042_DragonTalon']:
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end]
 modern=(ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_bytes();row=json.loads(modern)['255'];assert(row['srvstfunc'],row['srvdofunc'],row['calc1'],row['prgchargesconsumed'])==(24,42,'lvl/6+1',1)
 base=r'''
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <algorithm>
#define __fastcall
constexpr int UNIT_PLAYER=0,STAT_LEVEL=12,MONTYPEFLAG_UNIQUE=1,UNITFLAG_SKSRVDOFUNC=0x40;
struct D2SeedStrc{unsigned value=0;};
struct D2SkillStrc{int remaining=0;};
struct D2UnitStrc{int dwUnitType=1;unsigned dwFlags=0;D2SeedStrc pSeed;int level=1;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3};};
struct D2GameStrc{};struct D2DamageStrc{};
D2UnitStrc owner,target;D2SkillStrc skill;D2SkillsTxt row;D2GameStrc game;D2DamageStrc damage;
bool hasRow=true,hasSkill=true,hasTarget=true,inRange=true,boss=false,unique=false,hireling=false;
int builders=0,knockback=-1,chargeCalls=0,durability=0,rewinds=0,chanceEval=-1;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&skill:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return inRange;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){if(calc==0)return rank/6+1;chanceEval=calc;return calc==1?50+(50*(std::min(100,110*rank/(rank+6))))/100:25+(74*(std::min(100,110*rank/(rank+6))))/100;}
void SKILLS_SetParam1(D2SkillStrc* p,int v){if(!p)std::abort();p->remaining=v;}
int SKILLS_GetParam1(D2SkillStrc* p){if(!p)std::abort();return p->remaining;}
void sub_6FCF7BC0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2SkillsTxt*,int,int,int kb){++builders;knockback=kb;}
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return &damage;}
void sub_6FCF77E0(D2GameStrc*,D2UnitStrc*,D2DamageStrc*){++chargeCalls;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){++durability;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p->level;}
bool MONSTERS_GetHirelingTypeId(D2UnitStrc*){return hireling;}
bool MONSTERS_IsBoss(void*,D2UnitStrc*){return boss;}
bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,int){return unique;}
unsigned ITEMS_RollRandomNumber(D2SeedStrc* p){return p->value;}
void sub_6FD15080(D2UnitStrc*,int percent){if(percent!=100)std::abort();++rewinds;}
void reset(){builders=chargeCalls=durability=rewinds=0;knockback=chanceEval=-1;owner.dwFlags=UNITFLAG_SKSRVDOFUNC;owner.pSeed.value=0;hasSkill=true;hasRow=true;hasTarget=true;inRange=true;boss=unique=hireling=false;target.dwUnitType=1;target.level=99;}
'''
 base+='\n'.join(blocks.values())+r'''
int main(){bool first=true;printf("{\"start\":[");
for(int rank=1;rank<=255;++rank)for(int valid:{0,1})for(int used:{0,1})for(int selected:{0,1})for(int range:{0,1}){
 reset();hasRow=valid;hasSkill=used;hasTarget=selected;inRange=range;skill.remaining=77;
 int result=SKILLS_SrvSt24_DragonTalon(&game,&owner,255,rank);
 if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,valid,used,selected,range,result,skill.remaining,builders,knockback);
}printf("],\"sequence\":[");first=true;
for(int rank:{1,3,6,12,20,255})for(int remaining:{0,1,2,3,42})for(int valid:{0,1})for(int selected:{0,1})for(int kind:{0,1,2,3,4})for(int roll:{0,49,50,74,99}){
 reset();hasRow=valid;hasTarget=selected;skill.remaining=remaining;owner.pSeed.value=roll;unique=kind==1;boss=kind==2;hireling=kind==3;if(kind==4)target.dwUnitType=UNIT_PLAYER;
 int result=SKILLS_SrvDo042_DragonTalon(&game,&owner,255,rank);
 if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,remaining,valid,selected,kind,roll,result,skill.remaining,builders,knockback,chargeCalls,durability,rewinds,chanceEval);
}printf("]}\n");}
'''
 cpp=STAGE/'dragon-talon-sequence-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data['start'])==4080 and len(data['sequence'])==3000
 for rank,valid,used,selected,near,result,left,built,kb in data['start']:
  accepted=bool(valid and used and selected and near)
  expected_left=rank//6 if accepted else 0 if valid and used and not selected else 77
  assert(result,left,built,kb)==(int(accepted),expected_left,int(accepted),int(rank//6==0) if accepted else -1)
 for rank,remaining,valid,selected,kind,roll,result,left,built,kb,charges,drains,rewinds,calc in data['sequence']:
  active=valid and selected;nextkick=active and remaining>0
  expected_calc=(1 if kind==1 else 2 if kind==2 else 3) if nextkick and remaining==1 and kind else -1
  chance=100 if expected_calc<0 else (50+50*(min(100,110*rank//(rank+6)))//100 if expected_calc==1 else 25+74*(min(100,110*rank//(rank+6)))//100)
  expected_kb=int(remaining==1 and roll<chance) if nextkick else -1
  assert(result,left,built,kb,charges,drains,rewinds,calc)==(int(nextkick),remaining-1 if nextkick else remaining,int(nextkick),expected_kb,int(bool(active)),int(bool(active))+int(bool(nextkick)),int(bool(nextkick and remaining>1)),expected_calc)
 fixture=STAGE/'dragon-talon-sequence-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.dragontalon',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},currentSkillsSha256=hashlib.sha256(modern).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),startCases=len(data['start']),sequenceCases=len(data['sequence']),sanitizers='ASan+UBSan'),declaredServices=['Start packet builder is an observed call boundary, not compiled damage. Used-skill/record/selected-target/range services expose explicit presence controls.','Sequence charge dispatcher and durability functions are counters; they do not prove damage commitment or modern charge consumption. Formula service evaluates current floor(rank/6)+1 kick count and declared diminishing knockback formula; it is not compiled formula interpreter proof.','Category services select ordinary/unique/boss/hireling/player contexts; RNG returns declared integer controls. Source level reads execute literally. Null used skill in do42 is excluded because the routine dereferences it unconditionally.'],established=['Start prepares one packet and stores total-kicks minus1 only with valid row/usedskill/selected target in melee range; selected missing target clears remaining count.','Each valid selected-target do42 calls charge dispatcher and durability before testing remaining kicks. Positive remaining count decrements and prepares another packet, calls durability again, and rewinds100percent only while further kicks remain.','Last follow-up kick selects category-specific diminishing knockback; initial single kick is marked knockback unconditionally by start24.'],limits=['No damage/boot/property/charge-consumption/hit-guarantee/block/input/payment/timing or native handler certificate. This is complete older start/do42 control flow with declared services.','Current consume-one/guaranteed-hit policy postdates source and needs a separate identified adapter. Exact per-kick versus per-sequence modern behavior is not settled by this proof.','No copied arithmetic fragment is presented as full kick damage proof. Final visual/audio/icons are independently pending.'])
 out=ROOT/'tools/reference/dragon-talon-sequence-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon Talon sequence evidence stale'
 else:out.write_text(encoded)
 print('Dragon Talon:4080 complete start and3000 complete sequence cases pass within declared services; no native handler.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
