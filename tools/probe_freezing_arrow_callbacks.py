#!/usr/bin/env python3
"""Execute the immutable shared explosion callbacks with current Freezing Arrow IDs/packet."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_exploding_arrow_callbacks.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
code=(STAGE/'exploding-arrow-callback-source-probe.cpp').read_text()
for old,new in [('dwClassId=41','dwClassId=87'),('skill=16','skill=31'),('id==41','id==87'),('dwClassId=656','dwClassId=657'),('=656+i','=657+i'),('int dwPhysDamage=512,dwFireDamage=769;','int dwPhysDamage=512,dwColdDamage=769,dwFrzLen=52;'),('p->dwPhysDamage=512;p->dwFireDamage=769','p->dwPhysDamage=0;p->dwColdDamage=769;p->dwFrzLen=52'),('packets.push_back(p->dwFireDamage);','packets.push_back(p->dwColdDamage);packets.push_back(p->dwFrzLen);'),('p->dwPhysDamage=-1234;','p->dwPhysDamage=-1234;p->dwColdDamage=-999;p->dwFrzLen=-1;')]:
 assert old in code,old;code=code.replace(old,new)
path=STAGE/'freezing-arrow-callback-source-probe.cpp';path.write_text(code);binary=STAGE/'freezing-arrow-callback-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['parent'])==256 and len(data['child'])==4320
for row in data['parent']:
 exists,record,own,param,fail,slots,rank,result,attempts,created,immediate,skill,level,owner,origin=row;expected=(4 if slots==3 else slots) if exists and record else 0
 assert result==(3 if exists and record else 1) and attempts==expected and created==expected*(not fail) and immediate==created*param
 assert(skill,level,owner,origin)==((31,rank,own,1) if expected else (0,0,0,0))
for row in data['child']:
 exists,record,own,radius,skillExists,formula,count,block,flags,result,damage,area,formulas,r,filter,x,y,packets,events=row
 accepted=bool(exists and record and own and(radius>0 or skillExists));assert result==1 and damage==area==int(accepted)
 if accepted:
  assert formulas==int(radius<=0) and r==(radius if radius>0 else max(1,formula)) and filter==0x8583 and(x,y)==(40,70)
  assert len(packets)==(count*5 if flags else 0) and all(packets[i+2:i+5]==[0,769,52] for i in range(0,len(packets),5))
  assert events==(count if flags&1 and not block else 0)
fixture=STAGE/'freezing-arrow-callback-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/exploding-arrow-callback-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',talentId='amazon.freezingarrow',handlerEnabled=False,legacyCommit=parent['legacyCommit'],sourceHashes=parent['sourceHashes'],compiledRoutines=parent['compiledRoutines'],blockHashes=parent['blockHashes'],sharedEvidence='tools/reference/exploding-arrow-callback-engine-evidence.json',probe=dict(parentCases=256,childAreaCases=4320,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete shared hit04 preserves owner/origin/Skill31/rank for current parent87/child657 IDs; current parent sHitPar1=0 does not dispatch an immediate child hit.','Complete hit01 rolls one declared cold/freeze packet and delivers per-recipient copies using area filter0x8583. Cold769/freeze52 with physical0 remain identical for all recipients despite deliberately destructive downstream service.','Shared callbacks preserve ResultFlags5/HitFlags2 and individual event/avoidance dispatch. Radius5 is supplied by the child row; no target LOS flag or independent damage reroll is added.'],limits=['Damage getter/factory/world enumeration/stat/critical/final damage/events remain explicit adapters. The actual freeze-only getter is executed separately in freezing-arrow-damage-engine-evidence.json.','This does not execute complete modern scheduling/collision/geometry/seed/owner/client behavior. Native parent/child/state/input/reload verification is separate; handler remains disabled at this preparation stage.'])
out=ROOT/'tools/reference/freezing-arrow-callback-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Freezing Arrow callback evidence stale'
else:out.write_text(encoded)
print('Freezing Arrow:256 complete parent and4320 cold/freeze child-area callback cases compiled.')
