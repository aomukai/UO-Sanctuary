#!/usr/bin/env python3
"""Record own Ice and six-family composition, retaining diagnostics/preservation."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-ice'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==27 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-ice','shadow-charge','shadow-finisher','shadow-fists','shadow-thunder','ice-release']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,run in [('content',2),('integration',4)]:assert all(x in(STAGE/f'{kind}-build-{run}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-4.json').read_text());old=json.loads((STAGE/'source-snapshot-2.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 for p,h in old['inputs']['sourceInputs'].items():
  if p.startswith(('src/','shared/')):assert snapshot['inputs']['sourceInputs'][p]==h,p
 assert old['inputs']['copiedData']==snapshot['inputs']['copiedData']
 source=json.loads((ROOT/'tools/reference/shadow-ice-source-review.json').read_text());assert len(source['reusedRoutineHashes'])==15 and len(source['additionalReviewedRoutineHashes'])==2 and source['newCompiledControls']==0
 for p,h in source['inputs'].items():assert sha(ROOT/p)==h,p
 saved=json.loads((STAGE/'shadow-ice-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 for b in saved['Bodies']:
  c=b['Cubes']['Cubes'];assert len(c)==1 and c[0]['Frame']==1 and c[0]['LastCollision']==b['Target']and c[0]['Npc']['Actor']==b['Actor']and b['Status']is not None
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 names=['source-snapshot-2.json','source-snapshot-4.json','birth-inputs.json','birth-native-checks.log','native-run-4.log','content-build-2.log','integration-build-4.log','shadow-ice-fixtures.json','birth-native-checks-failed-1.log','birth-inputs-failed-1.json','birth-native-checks-failed-2.log','birth-inputs-failed-2.json','birth-native-checks-run-3.log','birth-inputs-run-3.json']
 files=[STAGE/n for n in names]+[ROOT/p for p in ['tools/record_shadow_ice.py','tools/review_shadow_ice.py','tools/reference/shadow-ice-source-review.json','tests/check_shadow_ice.py']]
 receipt=dict(parent='f57a13fca01b1b87290ef8627703f6931b698cc1',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicIceConnected=True,allSixOwnProgressiveFamilies=True,newCompiledSourceControls=0,reusedAuthenticatedRoutines=15,additionalWholeBodyReviews=2,nativeMarkers=27,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=4,sourceSnapshot=4,snapshotChronology='Content2production inputs/data match snapshots2and4. Snapshot4precedes final Integration4 and matches accepted native inputs;only fixture changes followed Content2,no redundant content build.',artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),diagnostic='Two initial runs found a saved probe target overlapping the next Shadow test. Actual cube LastCollision correctly chose that first collider;separate saved target positions fix the fixture with no production collision change. Final run also tightens exact mixed cold/lightning allocation;earlier successful full run retained.',scope='Both original Shadows:replacement inline cold/count3freeze,parent-before-chill area,source scatter/private cube/damage10/LastCollision/30frame expiry,separate cold/freeze/immunity/miss/range/cancel/atomic restore,exact all-six packet/RNG/emission ordering,world/basic/natural Warrior AI and original cube/status persistence. Retained own charge/Tiger/Cobra/Fists/Thunder/player Ice pass. Player mixed callback chronology is separately unverified;modern NPC parity/removal/stat/factory/event/geometry/PvP and other intrinsic/death/learned gates remain,no class completion.')
 out=ROOT/'tools/reference/shadow-ice-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own Shadow Ice receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own Ice/six families:27 native markers,two reloads;installed build and saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
