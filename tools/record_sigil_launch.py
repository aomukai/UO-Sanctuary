#!/usr/bin/env python3
"""Authenticate shared current Sigil launch and explicit remaining services."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sigil-launch';BASE=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-157-20261108'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 sp=ROOT/'tools/reference/sigil-launch-current-evidence.json';source=json.loads(sp.read_text());assert source['cases']==126 and source['sourceBytes']==858 and source['currentMachineCodeExecutedOffline']and source['dispatcherIndexLinked']and not source['processMemoryRead']
 for rel,h in source['artifacts'].items():assert sha(ROOT/rel)==h,rel
 captures={};artifacts={}
 for name in ['services-sigil-cast-services-20261108.json','services-sigil-limit-services-20261108.json','services-sigil-rounding-20261108.json']:
  p=BASE/name;r=json.loads(p.read_text());assert r['referenceVersion']=='3.3.93847'and r['sourceReferencesVerified']and r['bytesRead']<=8192 and r['bytesRead']==sum(x['size']for x in r['reads'])*2
  assert not any(r[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel;artifacts[rel]=h
  artifacts[str(p.relative_to(ROOT))]=sha(p);captures[name]=r
 assert all(r['allUnwindFragmentsCaptured']for name,c in captures.items()if'rounding'not in name for r in c['records'])
 rounding=captures['services-sigil-rounding-20261108.json']['records'][0];assert rounding['boundedLeafReference']and not rounding['allUnwindFragmentsCaptured']and rounding['fragments'][0]['captured']
 assert [(r['skill'],r['missiles'],r['dispatch'])for r in source['pinnedBindings']]==[(393,[699,686,702],30),(396,[700,687,703],61),(400,[701,688,704],30)]
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for name in ['sigil-launch','ring-of-fire-launch']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert int((STAGE/'sigil-launch-fixtures.json').read_text())==2
 files=['src/Sanctuary.Content/SigilLaunch.cs','tests/Sanctuary.Integration/SigilLaunchChecks.cs','tests/check_sigil_launch.py','tools/probe_sigil_launch.py','tools/record_sigil_launch.py']
 result=dict(referenceVersion='3.3.93847',handlers=210,completedClasses=[],learnedSigilsEnabled=False,sourceCases=126,sourceBytes=858,sourceEvidenceSha256=sha(sp),nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,nativeRun=1,newCompilerMatrices=0,sourceCaptures=artifacts,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in files},preservation=preservation(),pinnedBindings=source['pinnedBindings'],
 scope='Complete723byte current do157composition plus135byte finite-input rounding paths execute offline. C# matches126contexts:record/index guards,flag ordering,placement/point/distance,factory flags1and152zero-initialized bytes,skill/rank/opaque12c,allocation failure,post-allocation field198limit evaluation,raw signed state,ceil(float14/40),32bit expiry overflow,tracking refusal and conditional event12. Three skills share one component and existing GroundMissileRequest. Owned nine missile rows bind do14/period6and dispatch30for Lethargy/Death,61for Rancor. Retained148case Ring launch passes. No learned admission or live world/status side effect.',
 remaining=['Progressive selection,placement,coordinates,distance and factory are explicit services;their captured current helpers can now be qualified/reused with existing world geometry','Current tracking wrapper290000and core28fa10are captured completely,but list replacement,callback3a7f30cleanup,event12and save/lifecycle consumers are not yet translated','Lethargy needs three numeric status effects through curse do30;Rancor must reuse Confuse do61rather than a generic curse;Death needs its separate reactive/execution/explosion callbacks','Current bounded rounding leaf includes an adjacent function after offset0x88;only135byte prefix is used. All finite paths stay in that prefix;NaN tail/error handling is unqualified','Player cast/payment/cancellation/input,actual native world acceptance and final effects/audio/icons remain separate'],diagnostic='Initial offline hook checked unused event stack bytes as if all were64bit arguments. Corrected it to the actual pointer plus three32bit zero arguments. No C# or world behavior was changed by that fixture correction.')
 p=ROOT/'tools/reference/sigil-launch-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Shared Sigil launch:126current contexts,15native markers/two reloads;owned9row bindings and current finite rounding;no invented status or learned admission.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
