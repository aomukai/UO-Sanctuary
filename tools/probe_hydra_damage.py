#!/usr/bin/env python3
"""Reuse the complete Frozen Orb damage composition for Hydra's monster owner."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hydra-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/frozen-orb-damage-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/frozen-orb-damage-source/probe.cpp';retained=STAGE/'reused-frozen-orb.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void setup(',1)[0];tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit'];bodies={}
 for rel,digest in prior['sourceHashes'].items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert sha(p)==digest and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
  for name,h in prior['routineHashes'].items():
   try:b=function(raw.decode(),name)
   except AssertionError:continue
   assert hashlib.sha256(b.encode()).hexdigest()==h and function(cpp,name)==b;bodies[name]=b
 assert len(bodies)==16
 before='int icebolt=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return icebolt*2;}'
 after='int firebolt=0,fireball=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int){assert(formula==1&&id==62);return(firebolt+fireball)*3;}'
 assert cpp.count(before)==1;cpp=cpp.replace(before,after)
 before='D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &skill;}'
 assert cpp.count(before)==1;cpp=cpp.replace(before,'D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==62);return &skill;}')
 tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles','monstats'):
  p=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Hydra');missile=tables['missiles']['hydra']
 assert skill['*Id']==62 and skill['HitShift']==7 and skill['EType']=='fire' and skill['Param8']==3 and skill['EDmgSymPerCalc']=="(skill('Fire Bolt'.blvl) + skill('Fire Ball'.blvl))*par8"
 assert missile['*ID']==247 and missile['Skill']=='Hydra' and not any(missile.get(k) for k in ('SrcDamage','SrcMissDmg','MissileSkill','ApplyMastery'))
 cpp+='\nvoid setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwDmgSymPerCalc=-1;skill.dwEDmgSymPerCalc=1;skill.dwELenSymPerCalc=-1;skill.nToHitShift=7;'
 for field,member in (('EMin','dwEMin'),('EMax','dwEMax'),('ELen','dwELen')):cpp+=f'skill.{member}={skill.get(field,0)};'
 for i in range(5):cpp+=f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
 for i in range(3):cpp+=f'skill.dwELevLen[{i}]={skill.get("ELevLen"+str(i+1),0)};'
 cpp+='row.nSkill=62;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.nHitShift=8;row.dwDmgSymPerCalc=row.dwElemDmgSymPerCalc=-1;row.dwMissileFlags=0;}\n'
 fields,stats=prior['packetFields'],prior['storedStats']
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int species:{351,352,353})for(int rank=1;rank<=255;rank++)for(int bolt:{0,1,20})for(int ball:{0,1,20})for(int mastery:{0,50,200})for(int noise:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup();owner.dwUnitType=UNIT_MONSTER;owner.dwClassId=species;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=247;missileData.nSkill=337;missileData.nLevel=rank;firebolt=bolt;fireball=ball;critical=true;critCalls=0;
 for(int& stat:owner.stats)stat=noise;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;D2UnitStrc origin;origin.dwUnitType=UNIT_MONSTER;for(int& stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;
 MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[[%d,%d,%d,%d,%d,%d],[",first?"":",",species,rank,bolt,ball,mastery,noise);first=false;
'''
 for i,name in enumerate(fields):cpp+=f'printf("{"," if i else ""}%d",packet.{name});\n'
 cpp+='printf("],[");\n'
 for i,name in enumerate(stats):cpp+=f'printf("{"," if i else ""}%d",missile.stats[{name}]);\n'
 cpp+='printf("],%d,%d]",critCalls,missile.stats[STAT_LEVEL]);}printf("]\\n");}\n'
 for name,b in bodies.items():assert function(cpp,name)==b
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==41310
 def tier(base,growth,rank):
  left=rank-1;value=skill[base]
  for i,length in enumerate((7,8,6,6,255)):n=min(left,length);value+=n*skill.get(growth+str(i+1),0);left-=n
  return value
 for inputs,packet,stored,crit,level in cases:
  species,rank,bolt,ball,mastery,noise=inputs;ends=[]
  for maximum in (False,True):
   n=tier('EMax' if maximum else 'EMin','EMaxLev' if maximum else 'EMinLev',rank)*128;n+=n*(bolt+ball)*3//100;n+=n*mastery//100;ends.append(n)
  lo,hi=ends;expected=[lo if name=='nFireMinDamage' else hi if name=='nFireMaxDamage' else 0 for name in fields];saved=[lo if name=='STAT_FIREMINDAM' else hi if name=='STAT_FIREMAXDAM' else 0 for name in stats]
  assert packet==expected and stored==saved and crit==0 and level==rank,(inputs,packet,stored,expected,saved)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=prior['sourceHashes'],routineHashes=prior['routineHashes'],currentTable=pins,currentRows={'skill':skill,'missile':missile},packetFields=fields,storedStats=stats,sanitizers='ASan/UBSan clean',
  established=['Sixteen complete retained skill/missile numeric creator/getter/writer bodies compose for all255ranks and three monster head classes. Missile247selects Skill62rather than stored emitting skill337. Source0excludes owner/origin/weapon/item/critical noise;there is no invented parent weapon payload.',
  'Hydra damage applies HitShift7then combined hard Fire Bolt/Fire Ball3percent each,then captured positive fire mastery once. Full packet/stat checks preserve only fire endpoints and level;no cold/poison/length/leech/physical/critical channel appears. Synergy and mastery supplied by the head are distinct from later caster gear changes;their birth capture has separate full passive-helper evidence.'],
  declaredServices=['Older source,current tables,not a modern executable proof. Exact Frozen Orb ABI and sixteen bodies are reused;only declared row/formula and actor fixture services change. Formula VM,stat/ratio lookup,empty monster-type stat copy and weapon lookup remain explicit services. No current caster is queried by these numeric calls.',
  'Full ordinary factory/InitSteps,AI/animation,per-shot roll/contact/HP/return/block/events,Slow Missiles/interception,mastery/pierce/sunder target consumers,head/caster removal and native save/input/client remain separate. No C#/runtime/install/save change.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 target=ROOT/'tools/reference/hydra-damage-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra damage:41310complete creator/getter/writer cases,255ranks/three monster heads;Skill62from emitting337,hard synergies after shift/mastery once;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
