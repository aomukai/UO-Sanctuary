#!/usr/bin/env python3
"""Reuse complete Blizzard damage creator/getters/writer for Frozen Orb's three rows."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/frozen-orb-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=ROOT/'tools/reference/blizzard-damage-source-evidence.json';prior=json.loads(parent.read_text());original=ROOT/'.local/class-skills/blizzard-damage-source/probe.cpp';retained=STAGE/'reused-blizzard.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('void setup(',1)[0];bodies={};sources={};tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']
 routes={'source/D2Common/src/D2Skills.cpp':['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage'],
 'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_SetDamageStats','MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage','MISSILE_GetMinDamage','MISSILE_GetMaxDamage','MISSILE_CalculateMasteryBonus']}
 for rel,names in routes.items():
  path=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(path)
  for name in names:bodies[name]=function(raw.decode(),name);assert function(cpp,name)==bodies[name],name
 a='int icebolt=0,iceblast=0,glacial=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return(icebolt+iceblast+glacial)*5;}'
 b='int icebolt=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula==1);return icebolt*2;}'
 assert cpp.count(a)==1;cpp=cpp.replace(a,b);tables={};pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('skills','missiles'):
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(path.relative_to(ROOT))]=sha(path);tables[name]=json.loads(path.read_text())
 skill=next(r for r in tables['skills'].values() if r.get('skill')=='Frozen Orb');rows=[tables['missiles'][n] for n in ('frozenorb','frozenorbbolt','frozenorbnova')];assert skill['HitShift']==7 and skill['Param8']==2 and all(r.get('Skill')=='Frozen Orb' for r in rows[1:]) and not rows[0].get('Skill') and not any(r.get('MissileSkill') or r.get('SrcDamage') or r.get('SrcMissDmg') for r in rows)
 cpp+='\nvoid setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_COLD;skill.dwDmgSymPerCalc=-1;skill.dwEDmgSymPerCalc=1;skill.dwELenSymPerCalc=-1;skill.nToHitShift=7;'
 cpp+=f'skill.dwEMin={skill["EMin"]};skill.dwEMax={skill["EMax"]};skill.dwELen={skill["ELen"]};'
 for i in range(5):cpp+=f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
 for i in range(3):cpp+=f'skill.dwELevLen[{i}]={skill.get("ELevLen"+str(i+1),0)};'
 cpp+='row.nSkill=kind==0?0:64;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.nHitShift=kind==0?8:0;row.dwDmgSymPerCalc=row.dwElemDmgSymPerCalc=-1;row.dwMissileFlags=0;}\n'
 fields=prior['packetFields'];stats=prior['storedStats']
 cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int rank=1;rank<=255;rank++)for(int ice:{0,1,20})for(int mastery:{0,50,200})for(int noise:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=260+kind;missileData.nSkill=999;missileData.nLevel=rank;icebolt=ice;critical=true;critCalls=0;
 for(int&stat:owner.stats)stat=noise;owner.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;D2UnitStrc origin;for(int&stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;
 MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[[%d,%d,%d,%d,%d],[",first?"":",",kind,rank,ice,mastery,noise);first=false;
'''
 for j,name in enumerate(fields):cpp+=f'printf("{"," if j else ""}%d",packet.{name});\n'
 cpp+='printf("],[");\n'
 for j,name in enumerate(stats):cpp+=f'printf("{"," if j else ""}%d",missile.stats[{name}]);\n'
 cpp+='printf("],%d,%d]",critCalls,missile.stats[STAT_LEVEL]);}printf("]\\n");}\n'
 for name,body in bodies.items():assert function(cpp,name)==body,name
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==13770
 def tier(base,growth,rank):
  left=rank-1;value=skill[base]
  for i,length in enumerate((7,8,6,6,255)):n=min(left,length);value+=n*skill.get(growth+str(i+1),0);left-=n
  return value
 for inputs,packet,stored,crit,level in cases:
  kind,rank,ice,mastery,noise=inputs;ends=[]
  for maximum in (False,True):
   v=tier('EMax' if maximum else 'EMin','EMaxLev' if maximum else 'EMinLev',rank)*128;v+=v*ice*2//100;v+=v*mastery//100;ends.append(v if kind else 0)
  lo,hi=ends;length=200+(rank-1)*25 if kind else 0;expected=[lo if n=='nColdMinDamage' else hi if n=='nColdMaxDamage' else length if n=='nColdLength' else 0 for n in fields]
  storedExpected=[lo if n=='STAT_COLDMINDAM' else hi if n=='STAT_COLDMAXDAM' else length if n=='STAT_COLDLENGTH' else 0 for n in stats]
  assert packet==expected and stored==storedExpected and crit==0 and level==rank,(inputs,packet,stored,crit,level,expected,storedExpected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},currentTable=pins,currentRows={'skill':skill,'root':rows[0],'trail':rows[1],'terminal':rows[2]},packetFields=fields,storedStats=stats,sanitizers='ASan/UBSan clean',
 established=['Complete retained Blizzard creator/getters/writer compose across255ranks. Root260has a zero packet,while trail261and terminal262select Skill64despite stored actor skill999. Hard Ice Bolt2percent follows skill HitShift7;positive cold damage mastery follows once. Both children store200+25*(rank-1)cold frames independently of mastery.',
 'Full packet and saved stat controls exclude unrelated physical/elemental/leech/weapon/owner/origin/critical inheritance for source0. Positive damage mastery is separate from Sorceress cold resistance piercing. Root zero damage does not establish inert collision/event behavior.'],
 declaredServices=['Older engine,not modern executable proof. The exact Blizzard ABI and sixteen canonical bodies are reused;table/hard-formula/owner stat/ratio/empty monster-type stat-copy services are explicit. Root missile length is supplied0;child length executes the complete skill getter.',
 'Factory/initial steps/global seeds,do15/do16/hit29/motion/collision/LastCollide,damage fill/return/events/cold/mitigation,modern payment/charged/oskill/client/save and assets remain separate. Runtime C#,data,installation and saves are unchanged.'],
 artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/frozen-orb-damage-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Frozen Orb damage:13770complete root/trail/terminal capture cases across255ranks;hard Ice Bolt2percent after HitShift7/mastery/200+25per-rank cold length;ASan/UBSan clean;runtime unchanged.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
