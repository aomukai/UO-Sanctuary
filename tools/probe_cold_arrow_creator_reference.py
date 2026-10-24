#!/usr/bin/env python3
"""Reuse complete immutable creator routines with declared cold endpoints/length."""
from pathlib import Path
import argparse,contextlib,hashlib,importlib.util,io,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
p=ROOT/'tools/probe_bow_launch_reference.py';spec=importlib.util.spec_from_file_location('bow_launch',p);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
with contextlib.redirect_stdout(io.StringIO()):module.build(True)
base=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0]
assert 'int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}' in base
base=base.replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,elementMin=0,elementMax=0,elementLength=0;')
base=base.replace('int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int,int){return elementMin;}').replace('int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int,int){return elementMax;}').replace('int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}','int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return elementLength;}')
fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields']
cpp=base+r'''int main(){bool first=true;for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;std::printf("[");
for(int lo:{1,9})for(int added:{0,257,6784})for(int length:{0,1,100,101,670})for(int ownerLength:{0,1,25})for(int pct:{0,150})for(int crit:{0,1}){
 owner={};weapon={};missile={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;skill.nSrcDam=128;skill.nEType=ELEMTYPE_COLD;skillMin=skillMax=0;elementMin=added;elementMax=added+512;elementLength=length;critical=crit;
 owner.stats[STAT_MINDAMAGE]=lo;owner.stats[STAT_MAXDAMAGE]=lo+5;owner.stats[STAT_DAMAGEPERCENT]=pct;owner.stats[STAT_DEXTERITY]=42;owner.stats[STAT_ITEM_MINDAMAGE_PERCENT]=5;owner.stats[STAT_ITEM_MAXDAMAGE_PERCENT]=15;
 owner.stats[STAT_COLDMINDAM]=7;owner.stats[STAT_COLDMAXDAM]=13;owner.stats[STAT_COLDLENGTH]=ownerLength;D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,[",lo,added,length,ownerLength,pct,crit);
'''
for i,field in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",d.{field});\n'
cpp+='std::printf("]]");}std::printf("]\\n");}\n';path=STAGE/'cold-arrow-creator-source-probe.cpp';path.write_text(cpp);binary=STAGE/'cold-arrow-creator-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==360
data=dict(fields=fields,cases=cases);fixture=STAGE/'cold-arrow-creator-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',legacyCommit=parent['legacyCommit'],sourceHash=parent['sourceHash'],compiledRoutines=parent['compiledRoutines'],blockHashes=parent['blockHashes'],sharedGeneratorSha256=hashlib.sha256(p.read_bytes()).hexdigest(),probe=dict(cases=360,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete unchanged creator adds declared cold skill endpoints and length to owner cold endpoints and length at source128, preserving fractional damage and additive raw frames. Physical percentage/critical remain separate.'],limits=['Declared skill damage/length providers and stat/owner/RNG services retain the original harness boundaries. Current scalar, gear mastery, cold state/mitigation, input and persistence need native checks.'])
out=ROOT/'tools/reference/cold-arrow-creator-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Cold creator evidence stale'
else:out.write_text(encoded)
print('Cold Arrow:360 complete unchanged creator cases, including additive raw cold length.')
