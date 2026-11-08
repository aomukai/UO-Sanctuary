#!/usr/bin/env python3
"""Record current-instruction Ring launch translation and native comparisons."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'ring-of-fire-launch'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 sourcepath=ROOT/'tools/reference/ring-of-fire-launch-current-evidence.json';source=json.loads(sourcepath.read_text());assert source['cases']==148 and source['currentMachineCodeExecutedOffline']and not source['processMemoryRead']
 for rel,h in source['artifacts'].items():assert sha(ROOT/rel)==h,rel
 # Current captured cookie normal path:comparison/rotate/test/return,no RAX
 # write. This authenticates the corrected declared ABI service,not the
 # encrypted executable-file view or a changed game security setting.
 cp=BASE/'warlock-runtime-reference/warlock-new-do-20261108/services-ring-cookie-20261108.json';cookie=json.loads(cp.read_text());assert cookie['sourceReferencesVerified']and cookie['records'][0]['allUnwindFragmentsCaptured']
 for rel,h in cookie['artifacts'].items():assert sha(ROOT/rel)==h,rel
 fragment=cookie['records'][0]['fragments'][0];p=next(ROOT/rel for rel,h in cookie['artifacts'].items()if rel.endswith('.bin')and h==fragment['sha256']);raw=p.read_bytes();assert len(raw)==33 and raw[:3]==bytes.fromhex('483b0d')and raw[7:24]==bytes.fromhex('f2751248c1c11066f7c1fffff27502f2c3')
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for name in ['ring-of-fire-launch','warlock-cast-clock']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert int((STAGE/'ring-of-fire-launch-fixtures.json').read_text())==2
 paths=['src/Sanctuary.Content/RingOfFireLaunch.cs','tests/Sanctuary.Integration/RingOfFireLaunchChecks.cs','tests/check_ring_of_fire_launch.py','tools/probe_ring_of_fire_launch.py','tools/record_ring_of_fire_launch.py']
 result=dict(referenceVersion='3.3.93847',handlers=210,completedClasses=[],learnedRingEnabled=False,currentSourceContexts=148,currentSourceEvidenceSha256=sha(sourcepath),cookieCurrentEvidenceSha256=sha(cp),cookiePreservesPendingReturn=True,nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=2,newCompilerMatrices=0,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 scope='C# do154launch composition matches148offline executions of complete captured current instructions over explicit getter/formula/factory boundaries. Reuses all64FrozenOrbs.Direction vectors,matched to pinned current PE data. Server-do mark/guard/service ordering,fields198then194,32bit count rounding/clamp1..64/step,typed source-coordinate reads,flags3/7,origin/skill/rank/context12c and all independent allocation requests match;individual allocation failures do not stop later attempts or successful do return. Current Ring normal count supplier remains16/32at10,velocity adder0. World actor/RNG/resource/learned route is not enabled. Existing ordinary Warlock timing checks retained;installed files/saves unchanged.',
 diagnostic='Native1 exposed the emulator cookie stub overwriting RAX after the real function had set its return result. Corrected declared service preserves pending RAX;only143return values changed,all148case flags/service traces/requests unchanged. Actual33byte current cookie function was subsequently captured through its verified direct edge and confirms the nonfatal path preserves RAX. Its encrypted executable-file view is not accepted as code. Prototype source/fixtures/evidence and failed native log/input manifest remain archived. C# launch implementation/binaries were unchanged;native2passes15markers/two reloads.',
 remaining=['Progressive missile/velocity helper internals and exact compiled formula-field binding remain separate;the source emulator supplies their declared values','Real current missile factory mutation/RNG/world emission and damage17,travel/collision/explosion/NextHit/immune knockback/owner lifecycle remain unqualified before learned admission','Outer player SC startup/payment/cancellation/input and public skill availability remain unfinished;handler count remains210','Current placeholder cast/ring/contact/explosion/expiry/audio and talent/hotkey icons remain separately tracked'])
 p=ROOT/'tools/reference/ring-of-fire-launch-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Current Ring launch:148x64/native comparisons,15markers/two reloads;shared vectors;actual cookie ABI checked;no new handler or installed/save change.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
