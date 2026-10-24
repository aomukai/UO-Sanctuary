#!/usr/bin/env python3
"""Reuse the complete cold creator harness with Ice Arrow's explicit Skill selector."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_cold_arrow_creator_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
code=(STAGE/'cold-arrow-creator-source-probe.cpp').read_text();anchor='skill.nSrcDam=128;';assert anchor in code;code=code.replace(anchor,'row.nSkill=21;row.dwMissileFlags=0;'+anchor)
path=STAGE/'ice-arrow-creator-source-probe.cpp';path.write_text(code);binary=STAGE/'ice-arrow-creator-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
parent=json.loads((ROOT/'tools/reference/cold-arrow-creator-engine-evidence.json').read_text());fields=json.loads((STAGE/'cold-arrow-creator-source-probe.json').read_text())['fields'];cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==360
for row in cases:
 d=dict(zip(fields,row[6]));assert d['nColdMinDamage']==row[1]+7*256 and d['nColdMaxDamage']==row[1]+512+13*256 and d['nColdLength']==row[2]+row[3]
fixture=STAGE/'ice-arrow-creator-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,cases=cases),separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.icearrow',legacyCommit=parent['legacyCommit'],sourceHash=parent['sourceHash'],compiledRoutines=parent['compiledRoutines'],blockHashes=parent['blockHashes'],sharedEvidence='tools/reference/cold-arrow-creator-engine-evidence.json',selector=dict(Skill=21,MissileSkill=False,SrcDam=128),probe=dict(cases=360,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['The complete unchanged creator selects Ice Arrow skill endpoints and length via explicit Skill21 without requiring MissileSkill. It adds raw weapon/item cold length at source128, with physical damage remaining separate.'],limits=['The existing cold creator harness supplies declared skill damage/length and owner/stat/RNG services. These are bounded creator cases; current hard/soft synergy, real gear, mastery, freeze state, input and persistence are verified separately.'])
out=ROOT/'tools/reference/ice-arrow-creator-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Ice creator evidence stale'
else:out.write_text(encoded)
print('Ice Arrow:360 complete unchanged creator cases; explicit Skill21 and additive raw item cold length.')
