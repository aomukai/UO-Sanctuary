#!/usr/bin/env python3
"""Authenticate current Sigil tracking core and native ordered comparisons."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sigil-tracking';BASE=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-157-20261108'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 sp=ROOT/'tools/reference/sigil-tracking-current-evidence.json';source=json.loads(sp.read_text());assert source['cases']==111 and source['sourceBytes']==2217 and source['currentMachineCodeExecutedOffline']and not source['processMemoryRead']
 for rel,h in source['artifacts'].items():assert sha(ROOT/rel)==h,rel
 statPath=ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json';statRows=json.loads(statPath.read_text());assert statRows['source_unit_id']['*ID']==354 and statRows['shortparam1']['*ID']==355
 captures={};artifacts={str(statPath.relative_to(ROOT)):sha(statPath)}
 for name in ['sigil-tracking-callback-20261108.json','services-sigil-removal-services-20261108.json']:
  p=BASE/name;r=json.loads(p.read_text());assert r['referenceVersion']=='3.3.93847'and r['sourceReferencesVerified']and r['bytesRead']<=8192 and r['bytesRead']==sum(x['size']for x in r['reads'])*2
  assert not any(r[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  assert all(x['allUnwindFragmentsCaptured']for x in r['records'])
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel;artifacts[rel]=h
  artifacts[str(p.relative_to(ROOT))]=sha(p);captures[name]=r
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for name in ['sigil-tracking','sigil-launch']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert int((STAGE/'sigil-tracking-fixtures.json').read_text())==2
 files=['src/Sanctuary.Content/SigilTracking.cs','tests/Sanctuary.Integration/SigilTrackingChecks.cs','tests/check_sigil_tracking.py','tools/capture_sigil_tracking.py','tools/probe_sigil_tracking.py','tools/record_sigil_tracking.py']
 result=dict(referenceVersion='3.3.93847',handlers=210,completedClasses=[],learnedSigilsEnabled=False,sourceCases=111,sourceBytes=2217,sourceEvidenceSha256=sha(sp),nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,nativeRun=1,newCompilerMatrices=0,sourceCaptures=artifacts,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in files},preservation=preservation(),scope='Prepared C# tracking matches111offline executions of complete1512byte current core28fa10and705byte actual sort290910 over declared state-list/stat/missile services. Ordered lookups/writes,source_unit_id354andshortparam1355layers,max slot,missing/invalid IDs,descending stable time sort,equal-time ties,oldest lifetime-zero replacement,free-slot reuse,nullable new missile,limit0and conditional state timer updates match. All126shared Sigil launch comparisons retained. No actual world actor/status/player route or new class certificate.',remaining=['Actual owner-state acquisition/posting,tracking wrapper290000integration and saved world missile adapters remain separate;larger foreign/corrupt lists over8slots are explicitly refused','Captured owner-state removal callback3a7f30and event-unregister helper3c2e20are complete but not yet translated or integrated. They must not be mislabelled as immediate missile deletion','Connect do14periodic recipients through curse30for Lethargy/Death and Confuse61for Rancor;Death reactive/execution/explosion remains separate','Actual player SC/payment/cancellation/input,world acceptance and final effects/audio/icons remain open'],correction='The launch tracking result is timer-reschedule-needed,not a general tracking success/refusal result. Current core modifies entries even when returning false;new checkpoint supersedes earlier refusal wording. Source expires evicted missiles through raw lifetime float14=0,rather than deleting their actors synchronously.')
 p=ROOT/'tools/reference/sigil-tracking-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Sigil tracking:111current core/sort comparisons,15native markers/two reloads;ordered replacement/slot reuse/timer flag;launch retained,installed saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
