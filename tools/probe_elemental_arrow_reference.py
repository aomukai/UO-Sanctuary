#!/usr/bin/env python3
"""Reuse the immutable bow creator and impact harnesses for elemental arrows."""
from pathlib import Path
import argparse,contextlib,hashlib,importlib.util,io,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');check=parser.parse_args().check
for name in ['magic_arrow','bow_launch']:
 path=ROOT/f'tools/probe_{name}_reference.py';spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
 with contextlib.redirect_stdout(io.StringIO()):module.build(True)
prefix=(STAGE/'magic-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
assert prefix.count('int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_MAGIC;}')==1
prefix=prefix.replace('int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_MAGIC;}','int outputElement=ELEMTYPE_FIRE;int MISSILE_GetElemTypeFromMissileId(int){return outputElement;}')
cpp=prefix+r'''int main(){bool first=true;std::printf("{\"conversion\":[");
for(int element:{ELEMTYPE_FIRE,ELEMTYPE_COLD,ELEMTYPE_MAGIC})for(int physical:{0,1,255,256,257,1048576,1048577,16640000})for(int original:{0,257,10000})for(int pct:{-1,0,1,5,7,50,99,100,103}){
 outputElement=element;conversion=pct;D2DamageStrc d={};d.dwPhysDamage=physical;d.dwFireDamage=d.dwColdDamage=d.dwMagDamage=original;
 MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow(&game,&missile,&target,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",element==ELEMTYPE_FIRE?0:element==ELEMTYPE_COLD?1:2,physical,original,pct,d.dwPhysDamage,d.dwFireDamage,d.dwColdDamage,d.dwMagDamage);
}std::printf("],\"impact\":[");first=true;
for(int element:{ELEMTYPE_FIRE,ELEMTYPE_COLD,ELEMTYPE_MAGIC})for(int raw:{257,1024,65536,1048577})for(int bonus:{-90,0,37,100})for(int critical:{0,1})for(int pct:{0,5,7,50,100,150})for(int mastery:{0,25})for(int kind:{0,1,2,3}){
 missile={};target={};target.dwUnitType=UNIT_MONSTER;target.kind=kind;outputElement=element;conversion=pct;
 missile.stats[STAT_MINDAMAGE]=missile.stats[STAT_MAXDAMAGE]=raw;
 missile.stats[STAT_FIREMINDAM]=257;missile.stats[STAT_FIREMAXDAM]=513;missile.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 missile.stats[STAT_COLDMINDAM]=769;missile.stats[STAT_COLDMAXDAM]=1025;missile.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;
 missile.stats[STAT_MAGICMINDAM]=1281;missile.stats[STAT_MAGICMAXDAM]=1537;missile.stats[STAT_PASSIVE_MAG_MASTERY]=mastery;
 missile.stats[STAT_DAMAGEPERCENT]=bonus;missile.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=31;missile.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=47;missile.stats[STAT_ITEM_DEADLYSTRIKE]=critical;
 D2DamageStrc d={};MISSMODE_FillDamageParams(&missile,kind?&target:nullptr,&d);MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow(&game,&missile,kind?&target:nullptr,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",element==ELEMTYPE_FIRE?0:element==ELEMTYPE_COLD?1:2,raw,bonus,critical,pct,mastery,kind,d.dwPhysDamage,d.dwFireDamage,d.dwColdDamage,d.dwMagDamage);
}std::printf("]}\n");}
'''
def run(name,cpp):
 p=STAGE/(name+'.cpp');p.write_text(cpp);binary=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);return json.loads(subprocess.check_output([str(binary)]))
data=run('elemental-arrow-impact',cpp);assert len(data['conversion'])==648 and len(data['impact'])==4608
prefix=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0]
prefix=prefix.replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,elementMin=0,elementMax=0;')
prefix=prefix.replace('int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return elementMin;}').replace('int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return elementMax;}')
fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields']
cpp=prefix+r'''int main(){bool first=true;for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;std::printf("[");
for(int lo:{1,9})for(int added:{0,257,3153})for(int pct:{0,150})for(int crit:{0,1}){
 owner={};weapon={};missile={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;skill.nSrcDam=128;skill.nEType=ELEMTYPE_FIRE;skillMin=skillMax=0;elementMin=added;elementMax=added+512;critical=crit;
 owner.stats[STAT_MINDAMAGE]=lo;owner.stats[STAT_MAXDAMAGE]=lo+5;owner.stats[STAT_DAMAGEPERCENT]=pct;owner.stats[STAT_DEXTERITY]=42;owner.stats[STAT_ITEM_MINDAMAGE_PERCENT]=5;owner.stats[STAT_ITEM_MAXDAMAGE_PERCENT]=15;
 owner.stats[STAT_FIREMINDAM]=10;owner.stats[STAT_FIREMAXDAM]=31;D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,[",lo,added,pct,crit);
'''
for i,field in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",d.{field});\n'
cpp+='std::printf("]]");}std::printf("]\\n");}\n'
data['creator']=run('elemental-arrow-creator',cpp);data['creatorFields']=fields;assert len(data['creator'])==24
fixture=STAGE/'elemental-arrow-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
base=json.loads((ROOT/'tools/reference/magic-arrow-engine-evidence.json').read_text());creator=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text())
evidence=dict(referenceVersion='3.3.93847',legacyCommit=base['legacyCommit'],sourceHashes=base['sourceHashes']|{'source/D2Common/src/Units/Missile.cpp':creator['sourceHash']},compiledRoutines=base['compiledRoutines']+creator['compiledRoutines'],blockHashes=base['blockHashes']|creator['blockHashes'],sharedGenerators={str((ROOT/f'tools/probe_{n}_reference.py').relative_to(ROOT)):hashlib.sha256((ROOT/f'tools/probe_{n}_reference.py').read_bytes()).hexdigest() for n in ['magic_arrow','bow_launch']},probe=dict(conversionRows=648,impactRows=4608,creatorRows=24,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Full unchanged percentage/conversion/add/impact bodies execute with Fire/Cold/Magic output channels, preexisting independent channels/mastery, critical/species/percentage ordering and large FP256 boundaries. Converted physical is added after independent mastery rolls.','Complete unchanged creator adds declared already-calculated Fire skill endpoints to raw owner fire endpoints at SrcDam128, preserving fractions and separate physical percentage/critical fields. No extra physical skill bonus is supplied for Fire Arrow.'],limits=['These reuse declared table/stat/owner/formula/RNG services from prior immutable harnesses. The skill endpoints in the creator are explicit inputs, not a modern executable or full gear/mastery inheritance certificate.','Only ordinary human single-bow player Fire creation at SrcDam128 is exercised. Cold conversion is component-only in this harness; separate Cold Arrow creator/state/native evidence governs its integration. Native active gear, hard synergies, mana/ammunition, collision/mitigation and persistence require independent checks.'])
out=ROOT/'tools/reference/elemental-arrow-engine-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
if check:assert out.read_text()==encoded,'Elemental arrow source evidence stale'
else:out.write_text(encoded)
print('Elemental arrows:648 conversion/4608 impact/24 Fire creator source rows compiled from unchanged routines.')
