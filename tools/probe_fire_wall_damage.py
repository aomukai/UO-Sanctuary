#!/usr/bin/env python3
"""Check Fire Wall maker/field through complete damage getters, creator and writer."""
import argparse,hashlib,json,re,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/fire-wall-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/molten-boulder-damage-source-evidence.json';prior=json.loads(parent.read_text())
 original=ROOT/'.local/class-skills/molten-boulder-damage-source-probe.cpp';retained=STAGE/'reused-boulder.cpp'
 if not retained.exists():shutil.copy2(original,retained)
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 cpp='#include <cassert>\n'+retained.read_text().split('void setup(',1)[0];blocks={};sources={}
 names={'source/D2Common/src/D2Skills.cpp':['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage'],
 'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_SetDamageStats','MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage','MISSILE_GetMinDamage','MISSILE_GetMaxDamage']}
 for rel,functions in names.items():
  raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(ROOT/'.local/class-skills/d2moo-pinned-source'/rel)
  for n in functions:
   b=function(raw.decode(),n);blocks[n]=b
   if n in ('MISSILE_GetMinDamage','MISSILE_GetMaxDamage'):
    old='int '+n+'(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}';assert cpp.count(old)==1;cpp=cpp.replace(old,'int '+n+'(D2UnitStrc*,D2UnitStrc*,int,int);');cpp+='\n'+b+'\n'
   else:assert function(cpp,n)==b,n
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
 replace('struct D2MissilesTxt{','struct D2MissilesTxt{int dwMinDamage=0,dwMaxDamage=0,dwMinLevDam[5]={},dwMaxLevDam[5]={},dwDmgSymPerCalc=-1;')
 replace('int volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}','int warmth=0,inferno=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return warmth*4+inferno;}')
 tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  p=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Fire Wall');maker=tables['missiles']['firewallmaker'];field=tables['missiles']['firewall']
 assert skill['HitShift']==4 and not skill.get('SrcDam',0)
 assert skill['Param7']==1 and skill['Param8']==4 and not maker.get('Skill') and field['Skill']=='Fire Wall'
 assert all(not maker.get(k) for k in ('MinDamage','MaxDamage','EMin','EMax','EType','SrcDamage','SrcMissDmg','MissileSkill','ApplyMastery'))
 assert not any(field.get(k) for k in ('SrcDamage','SrcMissDmg','MissileSkill'))
 cpp+='\nvoid setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwDmgSymPerCalc=-1;skill.dwEDmgSymPerCalc=1;skill.dwELen=0;for(int&v:skill.dwELevLen)v=0;skill.nToHitShift=4;'
 cpp+=f'skill.dwEMin={skill["EMin"]};skill.dwEMax={skill["EMax"]};'
 for i in range(5):cpp+=f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
 cpp+='row.nSkill=kind==0?0:51;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.nHitShift=kind==0?8:0;row.dwElemDmgSymPerCalc=-1;}\n'
 fields=re.findall(r'int (\w+)=0;',re.search(r'struct D2MissileDamageDataStrc\{([^}]+)\}',cpp)[1]);assert len(fields)>20
 stats=['STAT_MINDAMAGE','STAT_MAXDAMAGE','STAT_FIREMINDAM','STAT_FIREMAXDAM','STAT_COLDMINDAM','STAT_COLDMAXDAM','STAT_LIGHTMINDAM','STAT_LIGHTMAXDAM','STAT_POISONMINDAM','STAT_POISONMAXDAM','STAT_MAGICMINDAM','STAT_MAGICMAXDAM','STAT_PASSIVE_FIRE_MASTERY','STAT_PASSIVE_COLD_MASTERY','STAT_PASSIVE_LTNG_MASTERY','STAT_PASSIVE_POIS_MASTERY','STAT_ITEM_DEADLYSTRIKE']
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1})for(int rank=1;rank<=255;rank++)for(int w:{0,1,20})for(int i:{0,1,20})for(int mastery:{0,50,200})for(int item:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=kind==0?68:69;missileData.nSkill=999;missileData.nLevel=rank;warmth=w;inferno=i;critical=true;critCalls=0;
 for(int&stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 D2UnitStrc origin;for(int&stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[[%d,%d,%d,%d,%d,%d],[",first?"":",",kind,rank,w,i,mastery,item);first=false;
'''
 for j,name in enumerate(fields):cpp+=f'printf("{"," if j else ""}%d",packet.{name});\n'
 cpp+='printf("],[");\n'
 for j,name in enumerate(stats):cpp+=f'printf("{"," if j else ""}%d",missile.stats[{name}]);\n'
 cpp+='printf("],%d,%d]",critCalls,missile.stats[STAT_LEVEL]);}printf("]\\n");}\n'
 for n,b in blocks.items():assert function(cpp,n)==b,n
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==27540
 def tier(basis,growth,rank):
  left=rank-1;value=basis
  for length,step in zip((7,8,6,6,255),growth):
   n=min(left,length);value+=n*step;left-=n
  return value
 for inputs,packet,stored,crit,level in cases:
  kind,rank,w,i,mastery,item=inputs;lo=hi=0
  if kind:
   lo=tier(15,[9,14,21,21,21],rank)*16;hi=tier(20,[9,14,21,21,21],rank)*16
   lo+=lo*(w*4+i)//100;hi+=hi*(w*4+i)//100
   lo+=lo*mastery//100;hi+=hi*mastery//100
  expected=[lo if n=='nFireMinDamage' else hi if n=='nFireMaxDamage' else 0 for n in fields]
  assert packet==expected and stored==[0,0,lo,hi]+[0]*(len(stats)-4) and crit==0 and level==rank,(inputs,packet,stored,crit,level,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTable=pins,packetFields=fields,storedStats=stats,sanitizers='ASan/UBSan clean',
  established=['Across all255ranks,current maker68has no damage packet despite a retained stored skill. It does not copy owner/origin weapon,element,leech or critical controls. Full physical and elemental getters execute; zero maker output is not a physical-getter stub.',
  'Current field69uses its Skill51row instead of the deliberately different stored actor skill. Hard Warmth4plus Inferno1percent applies after skill HitShift4;fire mastery follows once. Source0 excludes owner weapon/item/origin/critical inheritance,and no passive mastery stat is copied into the saved missile.',
  'Complete creator/add/scale,physical and elemental skill/missile getters,tier/mastery and stat writer bodies compose. Both entire packet and selected saved endpoints/mastery/critical/level stats are compared.'],
  declaredServices=['Older engine,not modern executable proof. The exact retained Molten Boulder scaffold supplies explicit ABI,table lookup,hard-point formula,owner/item stats,ratio and empty monster-type copying services. Current rows are pinned;the formula service returns4*Warmth+Inferno. Unused physical missile fields are supplied zero from the pinned empty maker row;all physical getters execute.',
  'Factory,movement,mask/recipient/hit routing,damage roll/mitigation/event timing,Slow Missiles,Magical Penetration,native saves/inputs/client and class acceptance remain separate. This qualifies maker/field numeric capture before the native Fire Wall replacement.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/fire-wall-damage-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Fire Wall damage:27540complete older creator/getter/stat-writer cases across255ranks;maker zero packet and field skill/synergy/mastery isolation;ASan/UBSan clean.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
