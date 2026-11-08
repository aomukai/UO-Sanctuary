#!/usr/bin/env python3
"""Record own Thunder shared-pool composition and affected retained consumers."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-thunder'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-thunder','shadow-fists','shadow-finisher','shadow-phoenix','thunder-release']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind,run in [('content',2),('integration',1)]:assert all(x in(STAGE/f'{kind}-build-{run}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-2.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 source=json.loads((ROOT/'tools/reference/shadow-thunder-source-review.json').read_text());assert len(source['routineHashes'])==17 and source['newCompiledControls']==0
 for p,h in source['inputs'].items():assert sha(ROOT/p)==h,p
 saved=json.loads((STAGE/'shadow-thunder-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 for b in saved['Bodies']:
  flights=b['Projectiles']['Flights'];assert len(flights)==80 and all(f['Frame']==4 and f['Npc']['Actor']==b['Actor']and not f['Npc']['Dormant']for f in flights)
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-2.log','integration-build-1.log','shadow-thunder-fixtures.json']]
 files+=[ROOT/p for p in ['tools/record_shadow_thunder.py','tools/review_shadow_thunder.py','tools/reference/shadow-thunder-source-review.json','tests/check_shadow_thunder.py']]
 receipt=dict(parent='c67718b6e6682daa4a5d9766609b7ec15bcbbfcb',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicThunderConnected=True,newCompiledSourceControls=0,reusedAuthenticatedRoutines=17,nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=1,sourceSnapshot=2,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),scope='Both original Shadows:post-fill inline lightning/unchanged physical,pre-parent64nova/16charged emission with exact caster/world/private initializer/path streams,typed moving lightning/HitClass64/per-cast four-frame lease,immunity/miss/tight-range/offline/expiry/atomic restore,world/basic/natural Warrior AI and exact original partial-motion saves. Shared player pool reused and retained own Fists/Tiger/Cobra/Phoenix/player Thunder pass. Modern parity/removal/stat/factory/event/geometry/PvP and other intrinsic/death/learned gates remain;no class completion.')
 out=ROOT/'tools/reference/shadow-thunder-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own Shadow Thunder receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own Thunder:24 native markers,two reloads;installed build and saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
