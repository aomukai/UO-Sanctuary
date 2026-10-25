#!/usr/bin/env python3
"""Current Ice skill getters, cube creator and full elemental length writer."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_poison_javelin_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def routine(rel,name):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  blocks[name]=s[m.start():i];return blocks[name]
 base=(STAGE/'poison-javelin-damage-source-probe.cpp').read_text().split('int main()',1)[0]
 base='#include <cstdlib>\n'+base
 def replace(a,b):
  nonlocal base
  assert base.count(a)==1,a;base=base.replace(a,b)
 replace('return hard*12;','return hard*8;')
 replace('unsigned dwMissileFlags=0;', 'int dwElemMin=1,dwElemMax=1,dwMinElemLev[5]={},dwMaxElemLev[5]={},dwElemDmgSymPerCalc=-1,nHitShift=8,dwElemLen=100,dwElemLevLen[3]={};unsigned dwMissileFlags=0;')
 rel='source/D2Common/src/Units/Missile.cpp'
 for name in ['MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage']:
  replace(f'int {name}(D2UnitStrc*,D2UnitStrc*,int,int){{return 0;}}',f'int {name}(D2UnitStrc*,D2UnitStrc*,int,int);')
 replace('int MISSILE_GetElementalLength(int,D2UnitStrc*,int,int){return 0;}','int MISSILE_GetElementalLength(int,D2UnitStrc*,int,int);')
 replace('int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int){return 0;}','int MISSILE_CalculateMasteryBonus(D2UnitStrc*,int,int);')
 base+='\nint MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return 0;}\n'
 for name in ['MISSILE_GetMinElemDamage','MISSILE_GetMaxElemDamage','MISSILE_GetElementalLength','MISSILE_CalculateMasteryBonus']:base+='\n'+routine(rel,name)
 writer=routine('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD11E40')
 fields=sorted(set(re.findall(r'pDamage->(\w+)',writer)));stats=sorted(set(re.findall(r'\bSTAT_\w+',writer)));old=re.search(r'enum Stats\{([^}]+)',base).group(1).split(',');extra=[s for s in stats if s not in old];base=base.replace('enum Stats{','enum Stats{'+','.join(extra)+',')
 base+='\nconstexpr int ELEMTYPE_RAND=99;\n'
 # RAND is unselected; keep its complete body and abort the explicit service.
 replace('int dwUnitType=0,dwClassId=0;', 'int pSeed=0,dwUnitType=0,dwClassId=0;')
 base+='unsigned ITEMS_RollRandomNumber(int*){std::abort();}\nstruct D2DamageStrc{'+''.join('int '+f+'=0;' for f in fields)+'};\n'+writer+'\n'
 # Real current tables configure complete getters; no hand-written tier formula is compiled into the oracle.
 src=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((src/'sources.json').read_text());pins={}
 for file in ['skills','missiles']:
  raw=(src/f'json/{file}.json').read_bytes();pins[file]=hashlib.sha256(raw).hexdigest();assert pins[file]==manifest['files'][f'json/{file}.json']['sha256']
 skills=json.loads((src/'json/skills.json').read_text());skill=skills['274'];cube=json.loads((src/'json/missiles.json').read_text())['bladesoficecubes'];assert cube['pSrvDmgFunc']==10 and cube['ApplyMastery']==1
 setup='void setup(){skill={};row={};skill.nEType=ELEMTYPE_COLD;skill.nSrcDam=0;'
 for field,key in [('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift'),('dwELen','ELen')]:setup+=f'skill.{field}={skill[key]};'
 for i in range(5):setup+=f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
 for i in range(3):setup+=f'skill.dwELevLen[{i}]={skill.get("ELevLen"+str(i+1),0)};'
 setup+='row.nSkill=0;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_FREEZE;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_APPLYMASTERY];}\n'
 base+=setup+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool comma=false;
for(int rank:{1,3,8,16,20,21,22,23,28,29,40,255})for(int points:{0,1,20})for(int boost:{0,25,125})for(int item:{0,99999}){
owner={};weapon={};missile={};missileData={};setup();missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=561;missileData.nSkill=274;missileData.nLevel=rank;hard=points;critical=true;critCalls=0;for(int& x:owner.stats)x=item;owner.stats[STAT_PASSIVE_COLD_MASTERY]=boost;
int minimum=SKILLS_GetMinElemDamage(&owner,274,rank,1),maximum=SKILLS_GetMaxElemDamage(&owner,274,rank,1),length=SKILLS_GetElementalLength(&owner,274,rank,1);
D2DamageStrc inlineDamage;inlineDamage.dwColdDamage=17;inlineDamage.dwColdLen=777;sub_6FD11E40(&owner,&inlineDamage,ELEMTYPE_COLD,minimum,length,nullptr,nullptr);
D2MissileDamageDataStrc data;MISSILE_CalculateDamageData(&data,&owner,nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&data,rank);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,points,boost,item,minimum,maximum,length,inlineDamage.dwColdDamage,inlineDamage.dwColdLen,data.nColdMinDamage,data.nColdMaxDamage,data.nColdLength,data.nMinDamage,data.nLifeDrainMinDamage,missile.stats[STAT_PASSIVE_COLD_MASTERY],critCalls);
}printf("]\n");}
'''
 path=STAGE/'ice-damage-source-probe.cpp';path.write_text(base);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==216
 def endpoint(maximum,rank,hard,boost):
  value=skill['EMax' if maximum else 'EMin'];left=rank-1
  for i,n in enumerate([7,8,6,6,2**30],1):step=min(left,n);value+=step*skill.get(('EMaxLev' if maximum else 'EMinLev')+str(i),0);left-=step
  value<<=skill['HitShift'];value+=value*hard*8//100;return value+value*boost//100
 for rank,hard,boost,item,lo,hi,length,inline,inlineLen,cmin,cmax,clen,physical,leech,storedMastery,crits in rows:
  assert(lo,hi,length,inline,inlineLen,cmin,cmax,clen,physical,leech,storedMastery,crits)==(endpoint(False,rank,hard,boost),endpoint(True,rank,hard,boost),100+10*(rank-1),17+lo,length,256+256*boost//100,256+256*boost//100,100,0,0,0,0)
 fixture=STAGE/'ice-damage-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');parent=json.loads((ROOT/'tools/reference/poison-javelin-damage-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesofice',sourceComposition='tools/reference/poison-javelin-damage-engine-evidence.json',sourceHashes=hashes,blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},reusedCompiledRoutines=parent['compiledRoutines'],pinnedInputHashes=pins,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(path.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete current inline skill endpoint/length getters establish hard Phoenix8percent after HitShift8,mastery once afterward,and100+10*(rank-1)raw frames with no length synergy.','The complete elemental writer adds cold damage but replaces an existing777frame cold length with the skill length. Inline count3freeze therefore uses the final skill length,not added item duration.','Complete current cube creator/getters/mastery/stat writer establish source0,constant1..1cold damage before mastery and100raw cold frames at every retained rank. Item damage/leech/critical and hard Phoenix damage synergy do not enter that cube packet.'],services=['Prepared owner/weapon stats,hard-point formula,ratio helper and empty monster-type stat copying reuse the bounded parent composition. RAND service aborts because only cold writer is selected.','This composes numeric packet creation/writing; complete fill/damage10router and native freeze/chill/target/geometry/seed/health/client remain separate.'],limits=['Scoped older-source/current-table proof; no current executable certificate or native Ice handler.','Nonnegative supplied mastery and selected ranks through255 are tested. Other item/state/proc/PvP/NPC contexts remain explicit acceptance work.']);out=ROOT/'tools/reference/ice-damage-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Ice damage evidence stale'
 else:out.write_text(encoded)
 print('Ice damage:216 complete current skill/cube getter/creator/writer cases pass; cold duration replacement established.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
