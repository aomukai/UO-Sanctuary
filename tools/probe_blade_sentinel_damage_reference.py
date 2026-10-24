#!/usr/bin/env python3
"""Run full Blade Sentinel missile creator and physical skill getters unchanged."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
import probe_bow_launch_reference as basis
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 basis.build(check=True);old=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text());tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']==old['legacyCommit'];hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b+'\n'
 text=source('source/D2Common/src/D2Skills.cpp');getters=''.join(routine(text,n) for n in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage'])
 source('source/D2Common/src/Units/Missile.cpp')
 ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());pins={};tables={}
 for name in ['skills','missiles']:
  p=ref/f'json/{name}.json';pins[name]=hashlib.sha256(p.read_bytes()).hexdigest();assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(p.read_text())
 skill=tables['skills']['257'];miss=next(v for v in tables['missiles'].values() if v.get('Missile')=='blade creeper');assert skill['SrcDam']==96 and miss['Half2HSrc']==1 and miss['Skill']=='Blade Sentinel';assert skill['DmgSymPerCalc']=="(skill('Blade Fury'.blvl)+skill('Blade Shield'.blvl))*par8" and skill['Param8']==10
 cpp=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0].replace('#define __stdcall','#define __stdcall\n#include <cassert>\nusing BOOL=int;')
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 replace('struct D2SkillsTxt{int nSrcDam=128,nEType=0;}skill;','struct D2SkillsTxt{int nSrcDam=96,nEType=0,nToHitShift=8,dwMinDam=6,dwMaxDam=10,dwMinLvlDam[5]={3,4,5,5,5},dwMaxLvlDam[5]={3,4,5,5,5},dwDmgSymPerCalc=1;unsigned dwFlags[2]={};}skill;')
 replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,gear=0,hard=0;constexpr int SKILLSFLAGINDEX_KICK=31;')
 replace('D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &weapon;}','D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return gear==2?nullptr:&weapon;}')
 replace('bool ITEMS_CheckWeaponIfTwoHanded(D2UnitStrc*){return true;}','bool ITEMS_CheckWeaponIfTwoHanded(D2UnitStrc*){return gear==1;}')
 replace('int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,void*,int){return 0;}','int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,void*,int){return 17;}')
 replace('short ITEMS_GetStrengthBonus(D2UnitStrc*){return 0;}','short ITEMS_GetStrengthBonus(D2UnitStrc*){return 100;}')
 replace('short ITEMS_GetDexBonus(D2UnitStrc*){return 100;}','short ITEMS_GetDexBonus(D2UnitStrc*){return 50;}')
 replace('bool ITEMS_CheckIfThrowable(D2UnitStrc*){return false;}','bool ITEMS_CheckIfThrowable(D2UnitStrc*){return gear==3;}')
 replace('int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 1;}','int INVENTORY_GetWieldType(D2UnitStrc*,void*){return gear==4?2:1;}')
 for name in ['Min','Max']:replace(f'int SKILLS_Get{name}PhysDamage(D2UnitStrc*,int,int,int){{return skill{name};}}',f'int SKILLS_Get{name}PhysDamage(D2UnitStrc*,int,int,int);')
 # Extra constants for compiled but deliberately unexercised kick branch.
 if 'STAT_LEVEL' not in re.search(r'enum Stats\{([^}]+)',cpp)[1]:replace('enum Stats{','enum Stats{STAT_LEVEL,')
 idx=cpp.index('void __fastcall MISSILE_AddStatsToDamage')
 cpp=cpp[:idx]+'''int STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){assert(false);return 0;}
int STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){assert(false);return 0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int expression,int id,int rank){assert(p==&owner && expression==1 && id==257 && rank>0);return hard*10;}
'''+getters+cpp[idx:]
 fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields']
 supplied=dict(old['sourceInputs'])|dict(STAT_STRENGTH=80,STAT_SECONDARY_MINDAMAGE=25,STAT_SECONDARY_MAXDAMAGE=35,STAT_ITEM_THROW_MINDAMAGE=40,STAT_ITEM_THROW_MAXDAMAGE=60)
 cpp+='''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank:{1,3,8,9,16,17,20,21,22,23,28,255})for(int h:{0,1,20,40})for(int g:{0,1,2,3,4})for(int pct:{-200,37})for(int crit:{0,1}){
owner={};weapon={};missile={};row={};skill={};owner.dwUnitType=UNIT_PLAYER;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=392;row.nSkill=257;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_HALF2HSRC];hard=h;gear=g;critical=crit;
owner.stats[STAT_MINDAMAGE]=9;owner.stats[STAT_MAXDAMAGE]=14;owner.stats[STAT_DAMAGEPERCENT]=pct;
'''
 for name,value in supplied.items():cpp+=f'owner.stats[{name}]={value};\n'
 cpp+='D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);printf("%s[%d,%d,%d,%d,%d,[",first?"":",",rank,h,g,pct,crit);first=false;\n'
 for i,field in enumerate(fields):cpp+=f'printf("{"," if i else ""}%d",d.{field});\n'
 cpp+='printf("]]");}printf("]\\n");}\n'
 p=STAGE/'blade-sentinel-damage-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==960
 def tier(base,rank):return base+sum([3 if n<=8 else 4 if n<=16 else 5 for n in range(2,rank+1)])
 trunc=lambda n,d:abs(n)//d*(1 if n>=0 else -1)
 for rank,h,g,pct,critical,values in rows:
  d=dict(zip(fields,values));factor=48 if g==1 else 96;lo,hi=(40,60) if g==3 else (25,35) if g==4 else (9,14)
  expected=dict(nMinDamage=(tier(6,rank)+tier(6,rank)*h*10//100)*256+lo*256*factor//128,nMaxDamage=(tier(10,rank)+tier(10,rank)*h*10//100)*256+hi*256*factor//128,nDamagePercent=max(-90,pct+15+(0 if g==2 else 118)),nDemonDamagePercent=11,nUndeadDamagePercent=13,nFlags=1+critical*2,nColdLength=15*factor//128,nPoisonLength=101,nPoisonCount=2*factor//128,nLifeDrainMinDamage=7*factor//128,nManaDrainMinDamage=5*factor//128)
  for label,stat in [('Fire','FIRE'),('Light','LIGHT'),('Magic','MAGIC'),('Cold','COLD')]:
   for end in ['Min','Max']:expected['n'+label+end+'Damage']=supplied[f'STAT_{stat}{end.upper()}DAM']*256*factor//128
  for end in ['Min','Max']:expected['nPoison'+end+'Damage']=supplied[f'STAT_POISON{end.upper()}DAM']*factor//128
  assert all(d[k]==v for k,v in expected.items()),(rank,h,g,pct,critical,d,expected)
  assert all(v==0 for k,v in d.items() if k not in expected),(rank,h,g,d)
 out=STAGE/'blade-sentinel-damage-source-probe.json';out.write_text(json.dumps(dict(fields=fields,cases=rows),separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,pinnedHashes=pins,compiledUnchangedRoutines=blocks|old['blockHashes'],fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=960),established=[
 'Complete physical getters apply tier base6/10 and hard Blade Fury+Blade Shield total*10percent in whole points before HitShift8. Creator calls them with includeWeapon0, preventing the weapon from entering both skill and weapon lanes. All twelve rank/tier boundaries through255 and hard totals0/1/20/40 agree with independent Python arithmetic.',
 'Complete missile creator uses SrcDam96, halved to48 for declared two-handed weapon via current Half2HSrc flag. One-hand/unarmed/throwing/secondary-wield controls select the correct source endpoint lane. Skill physical endpoints are added after the separately scaled weapon endpoints and are not themselves reduced by96/48.',
 'Damage percent retains max(owner item min/max percent)+owner generic+weapon mastery+weapon strength/dexterity contributions with minimum-90. Under unarmed control weapon stat bonuses are absent. Demon/undead percentages and one per-created-missile critical flag remain separate.',
 'Full typed addition/scaling preserves fractional direct endpoints and scales cold duration, poison rate/count and life/mana drain by96 or48. Poison aggregate length remains101 before later impact length/count processing. This can reduce poison count2 to0 for48; later getter behavior must be retained rather than inventing a new divisor.',
 'Owner is the player at creation. These snapshots precede AI reassignment to the blade pet. Later AR, owner events, mastery/pierce and mitigation/stat suppliers require the inherited pet context; this proof does not substitute a live player owner at impact.'
 ],services=[
 'Five declared weapon contexts supply raw owner stats/endpoints and metadata. Formula service supplies asserted hard total*10. Complete creator/add/scale/tier/physical getters execute unchanged. Critical selection is a declared boolean, with separate existing complete selection evidence. Full real gear/item stat acquisition, alternate monster/hireling/kick branches and game RNG are outside this matrix.',
 'Elemental skill getters are zero services because this pinned skill has no elemental lane; owner weapon elemental and poison fields are explicit nonzero sentinels. No Venom override is supplied in this matrix. Later mastery/poison/hit route and exact modern executable acceptance remain separate.'
 ])
 p=ROOT/'tools/reference/blade-sentinel-damage-engine-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade Sentinel:960 complete creator/tier/physical-getter cases pass, including Half2HSrc and throwing/secondary/unarmed lanes.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
