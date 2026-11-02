#!/usr/bin/env python3
"""Compare Meteor impact and field capture with complete source getters/creator/writer."""
import argparse,hashlib,json,re,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/meteor-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/fire-wall-damage-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/fire-wall-damage-source/probe.cpp';retained=STAGE/'reused-fire-wall.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void setup(',1)[0]
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 blocks={};sources={}
 for rel,names in {'source/D2Common/src/D2Skills.cpp':['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage'],
 'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_SetDamageStats','MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage','MISSILE_GetMinDamage','MISSILE_GetMaxDamage','MISSILE_CalculateMasteryBonus']}.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(path)
  for n in names:
   b=function(raw.decode(),n);blocks[n]=b
   if n=='MISSILE_CalculateMasteryBonus':
    old='int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int){return 0;}';assert cpp.count(old)==1;cpp=cpp.replace(old,'int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int);');cpp+='\n'+b+'\n'
   else:assert function(cpp,n)==b,n
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
 replace('int warmth=0,inferno=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return warmth*4+inferno;}','int firebolt=0,fireball=0,inferno=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return(firebolt+fireball)*5;}')
 replace('int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return hard*8;}','int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return inferno*3;}')
 if 'ELEMTYPE_FREEZE' not in cpp.split('int __fastcall MISSILE_CalculateMasteryBonus',1)[0]:cpp='#define ELEMTYPE_FREEZE 99\n'+cpp
 tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Meteor');center=tables['missiles']['meteorcenter'];field=tables['missiles']['meteorfire']
 assert center['Skill']=='Meteor' and not center.get('MissileSkill',0) and field.get('Skill') is None and field['ApplyMastery']==1 and field['HitShift']==3
 assert skill['HitShift']==8 and skill['Param8']==5 and not skill.get('SrcDam',0) and field['EDmgSymPerCalc']=="skill('Inferno'.blvl)*3"
 cpp+='\nvoid setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwDmgSymPerCalc=-1;skill.dwEDmgSymPerCalc=1;skill.dwELen=0;for(int&v:skill.dwELevLen)v=0;skill.nToHitShift=8;'
 cpp+=f'skill.dwEMin={skill["EMin"]};skill.dwEMax={skill["EMax"]};'
 for i in range(5):cpp+=f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
 cpp+='row.nSkill=kind==0?56:0;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=kind==0?ELEMTYPE_NONE:ELEMTYPE_FIRE;row.nHitShift=kind==0?8:3;row.dwElemDmgSymPerCalc=kind==0?-1:1;row.dwMissileFlags=kind==0?0:gdwBitMasks[MISSILESFLAGINDEX_APPLYMASTERY];'
 cpp+=f'row.dwElemMin={field["EMin"]};row.dwElemMax={field["EMax"]};'
 for i in range(5):cpp+=f'row.dwMinElemLev[{i}]={field.get("MinELev"+str(i+1),0)};row.dwMaxElemLev[{i}]={field.get("MaxELev"+str(i+1),0)};'
 cpp+='}\n'
 fields=re.findall(r'int (\w+)=0;',re.search(r'struct D2MissileDamageDataStrc\{([^}]+)\}',cpp)[1]);stats=prior['storedStats'];assert len(fields)>20
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");int hardsets[6][2]={{0,0},{1,0},{0,1},{20,0},{0,20},{20,20}};
for(int kind:{0,1})for(int rank=1;rank<=255;rank++)for(int set=0;set<6;set++)for(int inf:{0,1,20})for(int mastery:{0,50,200})for(int item:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=kind==0?101:240;missileData.nSkill=999;missileData.nLevel=rank;firebolt=hardsets[set][0];fireball=hardsets[set][1];inferno=inf;critical=true;critCalls=0;
 for(int&stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 D2UnitStrc origin;for(int&stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d],[",first?"":",",kind,rank,firebolt,fireball,inf,mastery,item);first=false;
'''
 for j,n in enumerate(fields):cpp+=f'printf("{"," if j else ""}%d",packet.{n});\n'
 cpp+='printf("],[");\n'
 for j,n in enumerate(stats):cpp+=f'printf("{"," if j else ""}%d",missile.stats[{n}]);\n'
 cpp+='printf("],%d,%d]",critCalls,missile.stats[STAT_LEVEL]);}printf("]\\n");}\n'
 for n,b in blocks.items():assert function(cpp,n)==b,n
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==55080
 def tier(row,basis,growth,rank):
  left=rank-1;value=row[basis]
  for i,length in enumerate((7,8,6,6,255)):
   n=min(left,length);value+=n*row.get(growth+str(i+1),0);left-=n
  return value
 for inputs,packet,stored,crit,level in cases:
  kind,rank,fb,ball,inf,mastery,item=inputs;ends=[]
  for maximum in (False,True):
   if kind:
    value=tier(field,'EMax' if maximum else 'EMin','MaxELev' if maximum else 'MinELev',rank);value+=value*inf*3//100;value*=8
   else:
    value=tier(skill,'EMax' if maximum else 'EMin','EMaxLev' if maximum else 'EMinLev',rank)*256;value+=value*(fb+ball)*5//100
   value+=value*mastery//100;ends.append(value)
  lo,hi=ends;expected=[lo if n=='nFireMinDamage' else hi if n=='nFireMaxDamage' else 0 for n in fields]
  assert packet==expected and stored==[0,0,lo,hi]+[0]*(len(stats)-4) and crit==0 and level==rank,(inputs,packet,stored,crit,level,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTable=pins,currentRows={'skill':skill,'center':center,'field':field},packetFields=fields,storedStats=stats,sanitizers='ASan/UBSan clean',
 established=['Across255ranks,Meteor center101selects Skill56despite an unrelated stored actor skill. Fire Bolt and Fire Ball hard points each add5percent after skill HitShift8;fire mastery follows once. Inferno is not an impact synergy.',
 'Ground240uses its own missile endpoints,hard Inferno3percent before HitShift3 and the complete MISSILE_CalculateMasteryBonus once via ApplyMastery1. Fire Bolt/Fire Ball do not enhance this child. Full packets and saved endpoints/level/mastery/critical stats exclude owner/origin weapon/item/critical inheritance for source0.'],
 declaredServices=['Older engine,not modern binary proof. Complete retained damage creator/add/scale/stat writer,physical/elemental skill and missile getters,tier/mastery and additional missile mastery helper are unchanged. Hard-point formula/table/owner stats/ratio/empty monster-type stat-copy services are explicit;zero missile length is supplied for the current zero-length rows.',
 'Factory,warning expiry/area/field placement,rolling/mitigation/return/global seed/Slow Missiles/potion/lifecycle/client and native saves remain separate. Reuse existing Mage/Phoenix warning/impact producer and shared typed services;do not equate differently ordered impact and field synergies.'],artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/meteor-damage-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Meteor damage:55080complete older impact/field creator/getter/writer cases across255ranks;distinct hard synergies/shifts/mastery;ASan/UBSan clean.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
