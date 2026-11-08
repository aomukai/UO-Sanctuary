#!/usr/bin/env python3
"""Record approved original Shadow Flight action,pose and contact checks."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_shadow_tail import sources
from review_shadow_flight import main as review
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-flight'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 review(True);auth=sources();proof=json.loads((ROOT/'tools/reference/shadow-flight-clock-source-evidence.json').read_text());assert proof['contexts']==12 and len(proof['completeClockRoutines'])==6
 for p,h in (proof['inputs']|proof['reusedProofs']|proof['artifacts']).items():assert sha(ROOT/p)==h,p
 assert not(BASE/'shadow-flight-clock-source/sanitizer.log').read_text()
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==33 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-flight','shadow-kick-packets','shadow-tail','shadow-talon','dragon-flight-landing','dragon-flight']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 for command in ['dragon-flight-mechanics-check','leap-geometry-check']:assert counts[command]==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind,n in [('content',3),('integration',4)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-4.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 assert native['copiedData']==snapshot['inputs']['copiedData']
 saved=json.loads((STAGE/'shadow-flight-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(b['Charges']for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-1.json','source-snapshot-2.json','source-snapshot-3.json','source-snapshot-4.json','birth-inputs.json','birth-native-checks.log','birth-inputs-failed-1.json','birth-native-checks-failed-1.log','birth-inputs-pre-arrival-pass.json','birth-native-checks-pre-arrival-pass.log','native-run-1.log','native-run-2.log','native-run-3.log','content-build-1.log','content-build-2.log','content-build-3.log','integration-build-1.log','integration-build-2.log','integration-build-3.log','integration-build-4.log','shadow-flight-fixtures.json']]
 files += [BASE/'shadow-flight-clock-source'/n for n in ['source-compile-failed-1.cpp','compiler-failed-1.log','source-failed-2.cpp','compiler-failed-2.log','sanitizer-failed-2.log']]
 files += [ROOT/p for p in ['tools/record_shadow_flight.py','tools/probe_shadow_flight_clock.py','tools/reference/shadow-flight-clock-source-evidence.json','tests/check_shadow_flight.py','docs/shadow-flight-action-proposal.md']]
 receipt=dict(parent='72ce9beaed4e358ce8885208bae644785ab6fed5',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicFlightConnected=True,adaptationApproved=True,approval='Approve this Shadow Flight rule',
  nativeMarkers=33,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=4,sourceSnapshot=4,newClockSourceContexts=12,completeClockRoutines=proof['completeClockRoutines'],authenticatedSource=auth,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
  diagnostic='Clock fixture first needed an aborting declaration for an unused nonsequence helper,then a leading sequence-id column on owned rows;both diagnostics retained. Initial content compile exposed stackalloc-in-loop warning,removed before acceptance. First native run passed initial boot but reload fixture started a fresh action before settling the existing loaded-controller skip;fixture now settles it before entry. Second run passed. Review then identified missing arrival-field dispatch;shared Mobile receiver and prior-cast pose merge were added with an actual field-callback interruption check. Final builds/cohort are clean.',
  scope='Both original Shadows use own mode14/slot5 23-point sequence,full source clock services with qualified conversion supplied and approved explicit teleport10/kick15/end24 phases. Actual original NPC traveller uses shared ground/free-cell/occupancy/town/travel/arrival-field services,no owner movement/payment. Real transfer survives stored and in-flight local controller updates,including action cancellation inside arrival. Own full Flight kick/AR/typed fill/actor RNG and all-six per-contact charge services,accuracy-only guarantee with shield retained,miss/block/no early damage/consume,no mana threshold,deletion/escape/recovery/freeze/movement/park/offline cancellation,world requests/full recovery,natural Warrior AI and exact original actor/items/resources/wounds/RNG/charge saves through two reloads pass. Talon/Tail/packet,player Flight action/landing/mechanics and Leap geometry consumers remain passing.',
  limits=['Explicit approved NPC phase/landing/charge adaptation replaces unresolved older raw-point/flag behavior;no exact current executable proof. Clock conversion is the retained complete state119/monster14 result supplied to this new12-context clock composition,not a new transform or full NPC callback matrix.',
   'Native tile/half-cell/height/room ordering and PvP/removal/stat mutation boundaries remain scoped. Arrival test proves shared field dispatch to the actual NPC and interruption/pose preservation;it does not independently certify every native field damage effect.',
   'Both species world requests and natural Warrior AI are verified;unrestricted natural Master loadouts/selection retain their existing gaps. Own Fury/Sentinel/modifier42death/learned Shadow dependencies and player mixed-finisher audit remain.',
   'No deployment or learned Shadow handler enabled. Final teleport/kick/charge effects/audio and talent/hotkey icons remain separately inventoried placeholders.'])
 out=ROOT/'tools/reference/shadow-flight-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC Flight action receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own NPC Flight action:33 native markers/two reloads,12new clock contexts;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
