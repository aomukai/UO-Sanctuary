#!/usr/bin/env python3
"""Authenticate the prepared native Death Mark world-mapping proposal."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/death-mark-native'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 pet=ROOT/'tools/reference/d2r-named-equipment/json/pettype.json';states=ROOT/'tools/reference/d2r-named-equipment/json/states.json'
 pets=json.loads(pet.read_text());ss=json.loads(states.read_text())
 assert len(pets)==21 and pets['demon']['lineNumber']==20 and len(ss)==232 and ss['death_mark']['*ID']==212
 capture=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-157-20261108/services-sigil-cast-services-20261108.json'
 c=json.loads(capture.read_text());r=next(r for r in c['records']if r['rva']=='0x3ad8d0');assert r['bytes']==290 and r['allUnwindFragmentsCaptured']
 helper={p:h for p,h in c['artifacts'].items()if '3ad8d0' in p}
 for p,h in helper.items():assert sha(ROOT/p)==h,p
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==18 and ': failed'not in log and ' ERR]'not in log and counts['iron-golem-birth-check']==3
 for name in ['iron-golem-items','npc-resource-callbacks','death-mark-debuff','death-mark-native']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert counts['death-mark-native-check-result']==1 and counts['death-mark-native-reload-check-result']==2
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind in ['content','integration']:assert all(s in(STAGE/f'{kind}-build-3.log').read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text())
 assert native['copiedData']==snapshot['inputs']['copiedData']==json.loads((ROOT/'.local/class-skills/death-mark-world-release/birth-inputs.json').read_text())['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 f=json.loads((STAGE/'death-mark-native-fixtures.json').read_text());assert f['Generation']==2 and f['Mark']['CastId']==98765 and f['Mark']['Rank']==255
 assert len(f['Pets'])==3 and len({p['Serial']for p in f['Pets']})==3 and len({p['Order']for p in f['Pets']})==3
 assert {json.loads(p['Birth'])['Profile']for p in f['Pets']}=={'prepared-goatman-life-owner-v1','prepared-tainted-life-owner-v1','prepared-defiler-life-owner-v1'}
 assert json.loads((STAGE/'death-mark-debuff-fixtures.json').read_text())['Generation']==2
 paths=['src/Sanctuary.Content/DeathMarkNativeRelease.cs','tests/Sanctuary.Integration/DeathMarkNativeChecks.cs','tests/check_death_mark_native.py','tools/record_death_mark_native.py']
 approval=ROOT/'tools/reference/death-mark-native-world-approval.json';approved=approval.exists() and json.loads(approval.read_text()).get('approvedByUser')is True
 result=dict(referenceVersion='3.3.93847',status='tested-approved-adaptation'if approved else'tested-proposal-awaiting-world-mapping-approval',handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,automaticGenericPetAiEnabled=False,currentFullWorldParity=False,
  approval=dict(path=str(approval.relative_to(ROOT)),sha256=sha(approval))if approved else None,nativeMarkers=18,markers=dict(counts),boots=3,reloads=2,saves=3,
  contentBuild=3,integrationBuild=3,sourceSnapshot=3,nativeRun=1,priorCompilerMatricesRerun=0,pinnedRecipientRanks=255,
  primaryTables={str(p.relative_to(ROOT)):sha(p)for p in [pet,states]},petTypeCount=21,demonType=20,stateCount=232,deathMarkState=212,
  reusedCoordinateHelper=dict(rva='0x3ad8d0',bytes=290,artifacts=helper,analysis='Existing current capture reviewed manually;no additional process read or emulator-equivalence claim'),
  files={p:sha(ROOT/p)for p in paths},artifacts=native['artifacts'],preservation=preservation(),
  scope='Native moved/stale/self/ground mobile destination resolution,zero guard,actual follower ownership/creation order,required-demon absence and explicit unregistered/uninitialized controller refusal. Actual three original species relocate/command and bind approved mark;ground casts preserve existing mark and source38unit range remains enforced. Original bodies/birth/wounds/order,recipient natural deadline/castid and native roster movement survive two actual reloads. Prepared bridge pays no mana and enables no learned route.',
  limits=['Native safe-area/floor/static/LOS,per-pet placement and creation-ledger order are explicit host adaptation choices;no D2 room flag/list-order parity claim.','Source context/selector/counts/castid still explicit controller inputs. The test count bounds match pinned primary metadata.','Automatic GenericPet command/neutral/action interruption,startup/payment/localdelay/cancellation and left/right/quick admission remain unfinished. Item/PvP/unclassified recipients outside scope.','Existing identified effect placeholders and separate final effects/audio/icons remain pending.'])
 path=ROOT/'tools/reference/death-mark-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Death Mark native bridge:18 markers/2 actual reloads;real destination/roster/original bodies and saved mark verified. World mapping '+('approved'if approved else'pending')+';211handlers unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
