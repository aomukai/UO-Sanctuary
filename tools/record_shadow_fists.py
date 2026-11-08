#!/usr/bin/env python3
"""Record own Fists composition and affected shared field/contact checks."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-fists'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==27 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-fists','shadow-claw','shadow-charge','shadow-finisher','shadow-phoenix','fists-fire']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind,run in [('content',3),('integration',2)]:assert all(x in(STAGE/f'{kind}-build-{run}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 source=json.loads((ROOT/'tools/reference/shadow-fists-source-review.json').read_text());assert len(source['routineHashes'])==12 and source['newCompiledControls']==0
 for p,h in source['inputs'].items():assert sha(ROOT/p)==h,p
 saved=json.loads((STAGE/'shadow-fists-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 for b in saved['Bodies']:assert len(b['Fields'])==1 and b['Fields'][0]['Ability']==185 and b['Fields'][0]['PhoenixNpc']['Actor']==b['Actor']
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-3.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-3.log','integration-build-2.log','shadow-fists-fixtures.json']]
 files+=[ROOT/p for p in ['tools/record_shadow_fists.py','tools/review_shadow_fists.py','tools/reference/shadow-fists-source-review.json','tests/check_shadow_fists.py']]
 receipt=dict(parent='e5f3f06f97d3da93080d8fbc21f637b926ad9543',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicFistsConnected=True,newCompiledSourceControls=0,reusedAuthenticatedRoutines=12,nativeMarkers=27,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=2,sourceSnapshot=3,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),scope='Both original Shadows:own post-fill inline fire/conversion/SOFTHIT,actual parent before copied burst,source scatter and private field RNG,immunity/death/miss/range/cancel/duplicate guards,63damaging field updates/64frame expiry,world mode14/basic action/natural Warrior AI and original actor/field persistence. Shared player extra Immolation srvdmg3 draw corrected and retained player/own consumers pass. Modern NPC parity/removal/event/geometry/PvP and other intrinsic/death/learned gates remain;no class completion.')
 out=ROOT/'tools/reference/shadow-fists-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own Shadow Fists receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own Fists:27 native markers,two reloads;installed build and saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
