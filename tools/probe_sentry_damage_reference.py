#!/usr/bin/env python3
"""Execute complete missile damage creator and elemental getters for Sentry495.

The missile's Skill261 overrides stored actor skill306 unless MissileSkill is
explicitly set. Owner stats, formula lookup and ratio services are declared.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_bow_launch_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;e=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[e]=='{')-(s[e]=='}');e+=1
 body=s[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
common='source/D2Common/src/D2Skills.cpp';miss='source/D2Common/src/Units/Missile.cpp'
names=['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength'];bodies={n:routine(common,n) for n in names};routine(miss,'MISSILE_CalculateDamageData')
base=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('#define __fastcall','#include <cassert>\n#define D2_MAYBE_UNUSED(x) (void)(x)\n#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))\nusing BOOL=int;\n#define __fastcall')
base=base.replace('int nSrcDam=128,nEType=0;','int nSrcDam=0,nEType=0,dwEMin=2,dwEMax=14,dwEMinLev[5]={0,0,0,0,0},dwEMaxLev[5]={6,8,12,14,16},nToHitShift=7,dwEDmgSymPerCalc=1,dwELen=0,dwELevLen[3]={0,0,0},dwELenSymPerCalc=-1;')
base=base.replace('}skill;', '}skill,actorSkill;')
base=base.replace('D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &skill;}','D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==261||id==306);return id==261?&skill:&actorSkill;}')
base=base.replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,fireHard=0,lightHard=0,critCalls=0;')
base=base.replace('bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){return critical;}','bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){critCalls++;return critical;}')
for n in ['Min','Max']:base=base.replace(f'int SKILLS_Get{n}ElemDamage(D2UnitStrc*,int,int,int){{return 0;}}',f'int SKILLS_Get{n}ElemDamage(D2UnitStrc*,int,int,int);')
base=base.replace('int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int);')
extra=set(re.findall(r'\bSTAT_\w+','\n'.join(bodies.values())));m=re.search(r'enum Stats\{([^}]+)\}',base);old=m[1].split(',');base=base[:m.start(1)]+','.join(old+sorted(extra-set(old)))+base[m.end(1):]
base+='\nint SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int expression,int id,int rank){assert(p==&owner&&expression==1&&(id==261||id==306)&&rank>0);return id==261?(fireHard+lightHard)*9:0;}\nint DATATBLS_ApplyRatio(int value,int percent,int divisor){return (int)((int64_t)value*percent/divisor);}\n'
base+='\n'.join(bodies.values())+'\n'
fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields']
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank:{1,3,8,9,16,17,20,22,23,28,40,255})for(int fire:{0,1,20})for(int lightning:{0,4,20})for(int mastery:{-50,0,25,100})for(int noise:{0,1})for(int overrideSkill:{0,1}){
 owner={};weapon={};missile={};skill={};actorSkill={};row={};missileData={};owner.dwUnitType=UNIT_MONSTER;owner.pInventory=nullptr;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=495;missileData.nSkill=306;
 row.nSkill=261;row.dwMissileFlags=overrideSkill?gdwBitMasks[MISSILESFLAGINDEX_MISSILESKILL]:0;skill.nEType=ELEMTYPE_LTNG;actorSkill.nEType=ELEMTYPE_NONE;actorSkill.dwEMin=actorSkill.dwEMax=0;
 fireHard=fire;lightHard=lightning;critCalls=0;critical=true;for(int& stat:owner.stats)stat=noise?37:0;owner.stats[STAT_PASSIVE_LTNG_MASTERY]=mastery;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,[",first?"":",",rank,fire,lightning,mastery,noise,overrideSkill);first=false;
'''
for i,field in enumerate(fields):cpp+=f'printf("{"," if i else ""}%d",d.{field});\n'
cpp+='printf("],%d]",critCalls);}\nprintf("]\\n");}\n'
source=STAGE/'sentry-damage-source-probe.cpp';source.write_text(cpp);binary=source.with_suffix('')
subprocess.run(['g++','-std=c++20','-O2',str(source),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==1728
# Pin the actual rows used to configure the complete getters and creator.
pins={}
for table in ['skills','missiles']:
 p=ROOT/f'tools/reference/d2r-named-equipment/json/{table}.json';pins[table]=hashlib.sha256(p.read_bytes()).hexdigest()
 manifest=json.loads((p.parent.parent/'sources.json').read_text());assert pins[table]==manifest['files'][f'json/{table}.json']['sha256']
rawSkill=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text())['261']
rawMissile=next(r for r in json.loads((ROOT/'tools/reference/d2r-named-equipment/json/missiles.json').read_text()).values() if r.get('Missile')=='sentrychargedbolt')
assert rawMissile['Skill']=='Charged Bolt Sentry' and not rawMissile.get('MissileSkill') and not rawSkill.get('SrcDam') and rawSkill['HitShift']==7
assert [rawSkill['EMaxLev'+str(i)] for i in range(1,6)]==[6,8,12,14,16] and rawSkill['EDmgSymPerCalc']=="(skill('Fire Trauma'.blvl) + skill('Lightning Sentry'.blvl)) * par8" and rawSkill['Param8']==9
trunc=lambda a,b:abs(a)//b*(1 if a>=0 else -1)
for rank,fire,lightning,mastery,noise,override,values,criticalCalls in data:
 d=dict(zip(fields,values));maximum=14;left=rank-1
 for n,amount in zip([7,8,6,6,2**30],[6,8,12,14,16]):count=min(n,left);maximum+=count*amount;left-=count
 maximum<<=7;maximum+=maximum*(fire+lightning)*9//100;minimum=256
 expected=(minimum+trunc(minimum*mastery,100),maximum+trunc(maximum*mastery,100)) if not override else (0,0)
 assert (d['nLightMinDamage'],d['nLightMaxDamage'])==expected and criticalCalls==0
 assert all(v==0 for k,v in d.items() if k not in ['nLightMinDamage','nLightMaxDamage'])
san=STAGE/'sentry-damage-source-probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(san)],check=True)
with (STAGE/'sentry-damage-sanitizer.log').open('wb') as log:sanitized=subprocess.check_output([str(san)],stderr=log)
assert json.loads(sanitized)==data and not (STAGE/'sentry-damage-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-damage-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,cases=data),separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text())
e=dict(referenceVersion='3.3.93847',talentId='assassin.chargedboltsentry',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,pinnedInputHashes=pins,compiledRoutines=list(blocks)+parent['compiledRoutines'],blockHashes=blocks,creatorBlockHashes=parent['blockHashes'],verification=dict(completeDamageCases=len(data)),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Sentry495 uses table Skill261 damage despite stored actor skill306. Explicit MissileSkill override redirects to306 and produces no lightning from that actor row.','Complete minimum remains256FP before mastery despite hard Fire Trauma/Lightning Sentry synergy. Maximum receives9percent per hard point, then inherited lightning mastery applies separately to each endpoint.','Current SrcDam0 excludes weapon, item typed/poison/drain, species/critical and noise owner damage stats. No critical RNG callback occurs.'],limits=['Owner stats, hard skill lookup, ratio helper, physical-zero getters and table/skill selection are explicit services. Full unchanged elemental getters/tier/mastery and creator/add/scale execute.','No full current executable parity, live spawn/passive capture, target mitigation/pierce/sunder, projectile collision/timing or NPC mode integration is established.'])
path=ROOT/'tools/reference/sentry-damage-source-evidence.json'
if args.check:assert json.loads(path.read_text())==e
else:path.write_text(json.dumps(e,indent=2)+'\n')
print(len(data),'complete sentry damage creator/getter cases; sanitizers clean')
