#!/usr/bin/env python3
"""Record the bounded own-Shadow elemental charge sequence checkpoint."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-claw-sequence'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-claw','shadow-charge','shadow-finisher','shadow-phoenix']:
  assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:
  text=(STAGE/f'{kind}-build-2.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 src=json.loads((ROOT/'tools/reference/shadow-claw-sequence-source-evidence.json').read_text());assert src['contexts']==144 and src['rateContexts']==12
 for p,h in src['artifacts'].items():assert sha(ROOT/p)==h,p
 for name in ['sanitizer.log','rate-sanitizer.log']:assert not(ROOT/'.local/class-skills/shadow-claw-sequence-source'/name).read_text()
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 fixtures=json.loads((STAGE/'shadow-claw-fixtures.json').read_text());assert fixtures['Generation']==2 and len(fixtures['Bodies'])==2
 for b in fixtures['Bodies']:assert len(b['Charges'])==3 and all(c['Count']==3 for c in b['Charges'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present'for s in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-2.json','source-snapshot-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-2.log','integration-build-2.log','shadow-claw-fixtures.json','birth-native-checks-failed-1.log','birth-inputs-failed-1.json']]
 files+=[ROOT/p for p in ['tools/probe_shadow_claw_sequence.py','tools/record_shadow_claws.py','tools/reference/shadow-claw-sequence-source-evidence.json','tests/check_shadow_claws.py']]
 receipt=dict(parent='b20c059da42b1d86f780a318f2c1387f6fcbb10f',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,elementalBuildersInternal=True,elementalFinishersEnabled=False,automaticElementalBuilderAi=False,compiledNpcSequenceControls=144,compiledRateControls=12,nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=3,snapshotChronology='Snapshot2 precedes Content2 and initial Integration1. Only the test corridor anchor changed before Integration2. Snapshot3 captured afterward and matches the accepted native inputs; no duplicate build/test was run solely to refresh a receipt.',artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),scope='Normal older NPC mode14/skillflags0,owned16row sequence,original dual-claw source rates/selection and own start23/do35/do34 contacts/charge lifecycle. Current D2R raw-point parity unverified; no player ordinal adaptation inferred. Both original species/retained charge+finishers+Phoenix and exact saved bodies/items/resources/wounds/RNG/charges through two reloads. Elemental emission/AI/learned routes gated; other reference gaps retained.',diagnostic='Initial native attempt stopped at fixture corridor y3800, which is beyond the helper loop. Test-only anchor3520 fixed it; failed inputs/log retained. Production mechanics unchanged.')
 out=ROOT/'tools/reference/shadow-claw-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Shadow claw checkpoint receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded Shadow claw checkpoint:156 new source controls/21 native markers;installed build and saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
