#!/usr/bin/env python3
"""Reuse complete Meteor damage getters/creator/writer for Blizzard center and shard."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blizzard-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/meteor-damage-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/meteor-damage-source/probe.cpp';retained=STAGE/'reused-meteor.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void setup(',1)[0];bodies={};sources={};tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']
 routes={'source/D2Common/src/D2Skills.cpp':['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage'],
 'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_SetDamageStats','MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage','MISSILE_GetMinDamage','MISSILE_GetMaxDamage','MISSILE_CalculateMasteryBonus']}
 for rel,names in routes.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(path)
  for name in names:bodies[name]=function(raw.decode(),name);assert function(cpp,name)==bodies[name],name
 for a,b in [('int firebolt=0,fireball=0,inferno=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return(firebolt+fireball)*5;}',
 'int icebolt=0,iceblast=0,glacial=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return(icebolt+iceblast+glacial)*5;}'),
 ('int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return inferno*3;}','int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){std::abort();}')]:assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
 tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Blizzard');center=tables['missiles']['blizzardcenter'];shard=tables['missiles']['blizzard1'];assert shard['Skill']=='Blizzard' and not center.get('Skill') and skill['HitShift']==8 and skill['ELen']==100 and skill['Param8']==5
 cpp+='\nvoid setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_COLD;skill.dwDmgSymPerCalc=-1;skill.dwEDmgSymPerCalc=1;skill.dwELen=100;skill.dwELenSymPerCalc=-1;for(int&v:skill.dwELevLen)v=0;skill.nToHitShift=8;'
 cpp+=f'skill.dwEMin={skill["EMin"]};skill.dwEMax={skill["EMax"]};'
 for i in range(5):cpp+=f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
 cpp+='row.nSkill=kind==0?0:59;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.nHitShift=8;row.dwDmgSymPerCalc=row.dwElemDmgSymPerCalc=-1;row.dwMissileFlags=0;}\n'
 fields=prior['packetFields'];stats=prior['storedStats']+['STAT_COLDLENGTH']
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");int hardsets[8][3]={{0,0,0},{1,0,0},{0,1,0},{0,0,1},{20,0,0},{0,20,0},{0,0,20},{20,20,20}};
for(int kind:{0,1})for(int rank=1;rank<=255;rank++)for(int set=0;set<8;set++)for(int mastery:{0,50,200})for(int noise:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=kind==0?158:159;missileData.nSkill=999;missileData.nLevel=rank;icebolt=hardsets[set][0];iceblast=hardsets[set][1];glacial=hardsets[set][2];critical=true;critCalls=0;
 for(int&stat:owner.stats)stat=noise;owner.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;D2UnitStrc origin;for(int&stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;
 MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d],[",first?"":",",kind,rank,icebolt,iceblast,glacial,mastery,noise);first=false;
'''
 for j,name in enumerate(fields):cpp+=f'printf("{"," if j else ""}%d",packet.{name});\n'
 cpp+='printf("],[");\n'
 for j,name in enumerate(stats):cpp+=f'printf("{"," if j else ""}%d",missile.stats[{name}]);\n'
 cpp+='printf("],%d,%d]",critCalls,missile.stats[STAT_LEVEL]);}printf("]\\n");}\n'
 for name,body in bodies.items():assert function(cpp,name)==body,name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==24480
 def tier(base,growth,rank):
  left=rank-1;value=skill[base]
  for i,length in enumerate((7,8,6,6,255)):n=min(left,length);value+=n*skill.get(growth+str(i+1),0);left-=n
  return value
 for inputs,packet,stored,crit,level in cases:
  kind,rank,ice,blast,glacial,mastery,noise=inputs;ends=[]
  for maximum in (False,True):
   v=tier('EMax' if maximum else 'EMin','EMaxLev' if maximum else 'EMinLev',rank)*256;v+=v*(ice+blast+glacial)*5//100;v+=v*mastery//100;ends.append(v if kind else 0)
  lo,hi=ends;length=100 if kind else 0;expected=[lo if n=='nColdMinDamage' else hi if n=='nColdMaxDamage' else length if n=='nColdLength' else 0 for n in fields]
  statsExpected=[lo if n=='STAT_COLDMINDAM' else hi if n=='STAT_COLDMAXDAM' else length if n=='STAT_COLDLENGTH' else 0 for n in stats]
  assert packet==expected and stored==statsExpected and crit==0 and level==rank,(inputs,packet,stored,crit,level,expected,statsExpected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,currentRows={'skill':skill,'center':center,'shard':shard},packetFields=fields,storedStats=stats,sanitizers='ASan/UBSan clean',
 established=['Complete retained source damage creator/add/scale/writer and skill/missile elemental/physical/tier/mastery/length getters compose across255ranks. Center158has no inherited skill/weapon/item/critical damage. Shard159selects Skill59despite stored actor skill999,applies hard Ice Bolt/Ice Blast/Glacial Spike5percent each after HitShift8,then passive cold damage mastery once,and stores100frame cold length independently of mastery.',
 'Full packet and saved stat comparisons exclude unrelated elemental/physical/leech/critical/owner/origin noise for source0. Positive damage mastery here is the separate stat service;Sorceress Cold Mastery resistance piercing is not converted into a damage bonus.'],
 declaredServices=['Older engine,not modern executable proof. Reused exact Meteor ABI;declared table/hard-formula/owner-stat/ratio/empty monster-type stat-copy services remain. Missile length service is zero for center row;shard cold length executes the complete skill getter. No separate missile formula is expected for these current rows.',
 'Creation/scatter/lifetime,actual native factory/stat capture/seed,rolling/mitigation/cold/LastCollide/return/global events,outer payment/client/save and assets remain separate. This changes no gameplay C#,runtime data,installation or saves.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/blizzard-damage-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Blizzard damage:24480complete center/shard capture cases across255ranks;hard cold synergies/shift/mastery/100frame length;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
