#!/usr/bin/env python3
"""Authenticate prepared current damage scaling; do not claim Ring admission."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/missile-damage-scale';BASE=ROOT/'.local/class-skills/warlock-runtime-reference'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 sp=ROOT/'tools/reference/missile-damage-scale-current-evidence.json';source=json.loads(sp.read_text());assert source['cases']==112 and source['sourceBytes']==421 and source['currentMachineCodeExecutedOffline']and not source['dispatcherIndexLinked']and not source['processMemoryRead']
 captures={};artifacts={}
 for name in ['ring-damage-body-20261108/capture.json','ring-damage-dispatch-candidates-20261108/capture.json','ring-damage-body-20261108/services-ring-damage-formula-20261108.json']:
  p=BASE/name;r=json.loads(p.read_text());assert r['referenceVersion']=='3.3.93847'and r['bytesRead']<=8192 and r['bytesRead']==sum(x['size']for x in r['reads'])*2
  assert not any(r[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel;artifacts[rel]=h
  artifacts[str(p.relative_to(ROOT))]=sha(p);captures[name]=r
 body=captures['ring-damage-body-20261108/capture.json'];assert body['immutableCandidateTableMatches']and not body['dispatcherIndexLinked']and body['tablePointers'][17]=='0x3fa2f0'and body['records'][0]['allUnwindFragmentsCaptured']
 assert all(not r['allUnwindFragmentsCaptured']for r in captures['ring-damage-dispatch-candidates-20261108/capture.json']['records'])
 assert all(r['allUnwindFragmentsCaptured']for r in captures['ring-damage-body-20261108/services-ring-damage-formula-20261108.json']['records'])
 for rel,h in source['artifacts'].items():assert sha(ROOT/rel)==h,rel
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for name in ['missile-damage-scale','ring-of-fire-launch']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert int((STAGE/'missile-damage-scale-fixtures.json').read_text())==2
 files=['src/Sanctuary.Content/MissileDamageScale.cs','tests/Sanctuary.Integration/MissileDamageScaleChecks.cs','tests/check_missile_damage_scale.py','tools/capture_ring_damage_reference.py','tools/probe_missile_damage_scale.py','tools/record_missile_damage_scale.py']
 result=dict(referenceVersion='3.3.93847',handlers=210,completedClasses=[],learnedRingEnabled=False,dispatcherIndexLinked=False,sourceCases=112,sourceEvidenceSha256=sha(sp),nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,newCompilerMatrices=0,sourceCaptures=artifacts,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in files},preservation=preservation(),scope='Prepared C# component matches complete421byte current candidate instructions over explicit row/owner/formula services. Guards,owner resolution order,signed raw word0e,formula token/ID/context,nonnegative percentage clamp,double division then float32 factor/multiply,CVTTSS2SI truncation/overflow and seven raw offsets match112contexts. All17other packet words remain untouched. Existing148case Ring launch proof retained. No world actor,player payment,learned admission or class certification.',remaining=['Candidate31entry immutable table/slot17matches owned image,but current dispatcher linkage remains unverified;three neighboring candidate dispatchers remain inaccessible and were not forced or repeatedly polled','Raw current packet fields are preserved by offset;older D2DamageStrc names are not silently reused because its layout differs','Current row getter and missile formula wrapper were captured through verified direct calls;formula evaluator/fnum token meaning and outer damage/mitigation/hit ordering remain unqualified','Ring world factory,travel/collision/explosion/NextHit/immune knockback/owner lifecycle and player cast/payment still required','Existing Ring effects/audio and talent/hotkey icons remain separate placeholders'],diagnostic='First native invocation failed before boot because the stage lacked its retained native/Distribution link. Added the link to the existing tested host;no compilation or production change. Native2is the first booted run.')
 p=ROOT/'tools/reference/missile-damage-scale-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Prepared missile scale:112current instruction cases,15native markers/two reloads;Ring launch retained;identity gate and installed saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
