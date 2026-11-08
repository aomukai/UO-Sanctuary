#!/usr/bin/env python3
"""Bind own Flight packet checks and retained Talon/Tail consumers."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_shadow_tail import sources
from review_shadow_flight import main as review
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-flight-packets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 review(True);auth=sources();log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==18 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-kick-packets','shadow-tail','shadow-talon']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-1.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-1.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'shadow-kick-packets-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','native-run-2.log','content-build-1.log','integration-build-1.log','shadow-kick-packets-fixtures.json','shadow-tail-fixtures.json','shadow-talon-fixtures.json']]
 files += [ROOT/p for p in ['tools/record_shadow_flight_packets.py','tools/review_shadow_flight.py','tools/reference/shadow-flight-source-review.json','tests/check_shadow_flight_packets.py','docs/shadow-flight-action-proposal.md']]
 receipt=dict(parent='e4d06ccd0c32f494e7e8718d03061f4b71300cb6',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicFlightConnected=False,flightPacketQualified=True,adaptationApproved=True,approval='Approve this Shadow Flight rule',
  nativeMarkers=18,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,authenticatedSource=auth,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
  diagnostic='Initial harness invocation stopped before server boot because the new stage lacked its shared native-kernel symlink. Linked the existing verified kernel and ran the cohort;no production or test-code correction was required.',
  scope='Both original Shadows:Flight rank12 damage485percent through the existing body/boot/typed source128 builder,own used-skill/weapsel4,unmodified filler hit class,actor-only RNG,primary typed hand/weapon-ED exclusion,ordinary critical bypass,all-six cached inline postfill without emissions or charge consumption,miss/no damage RNG,ordered HP/no replay and unchanged player mana. Same test helper retains Talon. Real Talon/Tail actions,original actor/items/resources/wounds/RNG and synthetic prepared kick survive two reloads. Flight phase approval and source/data conflict are recorded;no old source compiler matrices rerun.',
  limits=['Own Flight startup,sequence clock,two-stage teleport/kick,failed landing,self-transfer/ground-state synchronization,AI and pending-action cancellation remain to implement/verify under the approved rule.',
   'Saved synthetic prepared kick remains the existing Talon fixture;this does not prove Flight action persistence or authorize replaying a saved windup. No learned Shadow route is enabled.',
   'Exact current NPC callback/phase/stat mutation/geometry/PvP/removal parity and unrestricted Master loadouts remain unverified. Flight final effects/audio and talent/hotkey icons are separate placeholders.'])
 out=ROOT/'tools/reference/shadow-flight-packets-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC Flight packet receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own NPC Flight packet:18 native markers/two reloads;Talon/Tail retained;Flight action remains pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
