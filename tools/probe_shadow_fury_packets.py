#!/usr/bin/env python3
"""Sixteen new monster-branch Fury creator controls;reuse complete bodies."""
import hashlib,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-packets-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);old=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text());fury=json.loads((ROOT/'tools/reference/blade-fury-damage-source-evidence.json').read_text());tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};routines={}
 original=BASE/'bow-launch-source-probe.cpp';assert sha(ROOT/old['probe']['fixture'])==old['probe']['sha256'];cpp=original.read_text().split('int main()',1)[0].replace('#define __stdcall','#define __stdcall\n#include <cassert>\n#include <cstdlib>\nusing BOOL=int;')
 def source(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  b=function(raw.decode(),name);h=hashlib.sha256(b.encode()).hexdigest();assert h==fury['compiledUnchangedRoutines'][name];routines[name]=h;return b
 getters='\n'.join(source('source/D2Common/src/D2Skills.cpp',n)for n in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage'])
 for n in ['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage']:assert function(cpp,n)==source('source/D2Common/src/Units/Missile.cpp',n)
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('struct D2SkillsTxt{int nSrcDam=128,nEType=0;}skill;','struct D2SkillsTxt{int nSrcDam=96,nEType=0,nToHitShift=8,dwMinDam=8,dwMaxDam=10,dwMinLvlDam[5]={3,5,8,8,8},dwMaxLvlDam[5]={3,5,8,8,8},dwDmgSymPerCalc=1;unsigned dwFlags[2]={};}skill;')
 replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,hard=0;constexpr int SKILLSFLAGINDEX_KICK=31;')
 replace('D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &weapon;}','D2UnitStrc* D2Common_10434(D2UnitStrc*,int){std::abort();}')
 replace('D2UnitStrc* UNITS_GetEquippedWeaponFromMonster(D2UnitStrc*){return &weapon;}','D2UnitStrc* UNITS_GetEquippedWeaponFromMonster(D2UnitStrc* p){assert(p==&owner&&p->dwUnitType==UNIT_MONSTER);return &weapon;}')
 replace('bool ITEMS_CheckWeaponIfTwoHanded(D2UnitStrc*){return true;}','bool ITEMS_CheckWeaponIfTwoHanded(D2UnitStrc*){return false;}')
 replace('int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,void*,int){return 0;}','int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,void*,int){return 17;}')
 replace('short ITEMS_GetStrengthBonus(D2UnitStrc*){return 0;}','short ITEMS_GetStrengthBonus(D2UnitStrc*){return 100;}');replace('short ITEMS_GetDexBonus(D2UnitStrc*){return 100;}','short ITEMS_GetDexBonus(D2UnitStrc*){return 50;}')
 for end in ['Min','Max']:replace(f'int SKILLS_Get{end}PhysDamage(D2UnitStrc*,int,int,int){{return skill{end};}}',f'int SKILLS_Get{end}PhysDamage(D2UnitStrc*,int,int,int);')
 replace('enum Stats{','enum Stats{STAT_LEVEL,')
 i=cpp.index('void __fastcall MISSILE_AddStatsToDamage');cpp=cpp[:i]+'int STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){std::abort();}int STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){std::abort();}\nint SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int e,int id,int rank){assert(p==&owner&&e==1&&id==266&&rank>0);return hard*10;}\n'+getters+'\n'+cpp[i:]
 supplied=old['sourceInputs']|dict(STAT_STRENGTH=80,STAT_SECONDARY_MINDAMAGE=25,STAT_SECONDARY_MAXDAMAGE=35,STAT_ITEM_THROW_MINDAMAGE=40,STAT_ITEM_THROW_MAXDAMAGE=60)
 fields=json.loads((BASE/'bow-launch-source-probe.json').read_text())['fields'];cpp+='int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");for(int rank:{1,12})for(int h:{6,7})for(int pct:{-200,37})for(int crit:{0,1}){owner={};weapon={};missile={};row={};skill={};owner.dwUnitType=UNIT_MONSTER;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=506;row.nSkill=266;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_HALF2HSRC];hard=h;critical=crit;owner.stats[STAT_MINDAMAGE]=9;owner.stats[STAT_MAXDAMAGE]=14;owner.stats[STAT_DAMAGEPERCENT]=pct;\n'
 for n,v in supplied.items():cpp+=f'owner.stats[{n}]={v};\n'
 cpp+='D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);printf("%s[%d,%d,%d,%d,[",first?"":",",rank,h,pct,crit);first=false;'
 for i,n in enumerate(fields):cpp+=f'printf("{","if i else""}%d",d.{n});'
 cpp+='printf("]]");}printf("]\\n");}\n';generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
 cases=json.loads(run.stdout);assert len(cases)==16
 path=STAGE/'cases.json';path.write_text(json.dumps(dict(fields=fields,stats=supplied,cases=cases),separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=16,completeRoutines=routines,inputs=inputs|{str(original.relative_to(ROOT)):sha(original)},retainedProof='tools/reference/blade-fury-damage-source-evidence.json',artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',path,Path(__file__).resolve()]},scope='Full unchanged monster-branch missile creator/add/scale and physical skill getters;ordinary equipped nonhireling owner,primary one-hand weapon,rank/hard synergy/percent/critical contexts. Weapon selection,mastery17/strength100/dex50,raw fields,critical boolean and primary restore are declared services. No source RNG selection,world impact,channel/timing or current executable equivalence. Existing960player matrix is not rerun.')
 (ROOT/'tools/reference/shadow-fury-packets-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Own Fury:16 new complete monster creator/getter contexts,ASan/UBSan clean;old matrices reused.')
if __name__=='__main__':main()
