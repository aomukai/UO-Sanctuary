#!/usr/bin/env python3
"""Authenticate the tested,user-approved Death Mark recipient adaptation."""
import argparse,hashlib,json,re,struct
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/death-mark-debuff'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 approval_path=ROOT/'tools/reference/death-mark-recipient-approval.json'
 approval=json.loads(approval_path.read_text());assert approval['approvedByUser'] and approval['answer']=='Approve this Death Mark recipient rule'
 capture_path=ROOT/'.local/class-skills/warlock-runtime-reference/callees-death-mark-recipient-services-20261109.json'
 capture=json.loads(capture_path.read_text());assert capture['referenceVersion']=='3.3.93847'
 for p,h in capture['artifacts'].items():assert sha(ROOT/p)==h,p
 records={r['rva']:r for r in capture['records']}
 assert records['0x3afe80']['allUnwindFragmentsCaptured'] and records['0x3afe80']['bytes']==233
 assert records['0x3a80c0']['allUnwindFragmentsCaptured'] and records['0x3a80c0']['bytes']==1221
 assert not records['0x45f8d0']['allUnwindFragmentsCaptured'] and all(not f['captured']for f in records['0x45f8d0']['fragments'])
 table_path=ROOT/'.local/class-skills/defiler-compiled-formula/skills.bin';table=table_path.read_bytes()
 assert struct.unpack_from('<I',table)[0]==429 and len(table)==4+429*748
 row=table[4+375*748:4+376*748]
 assert struct.unpack_from('<6h',row,0x74)==(31,34,35,-1,-1,-1)
 assert struct.unpack_from('<2h',row,0xa0)==(-1,212)
 assert struct.unpack_from('<4h',row,0xa4)==(-1,-1,-1,-1) and struct.unpack_from('<4h',row,0xac)==(0,0,0,0)
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==12 and ': failed'not in log and ' ERR]'not in log
 assert counts['death-mark-debuff-check']==1 and counts['death-mark-debuff-reload-check']==2
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
 for kind,n in [('content',2),('integration',1)]:assert all(s in(STAGE/f'{kind}-build-{n}.log').read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-2.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 fixture=json.loads((STAGE/'death-mark-debuff-fixtures.json').read_text());assert fixture['Generation']==2 and fixture['State']['Rank']==255
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['DeathMarkDebuffs','GemCombat','NamedElements','AmazonInnerSight']]+['tests/Sanctuary.Integration/DeathMarkDebuffChecks.cs','tests/check_death_mark_debuff.py','tools/record_death_mark_debuff.py']
 result=dict(referenceVersion='3.3.93847',status='tested-approved-adaptation',approval=dict(path=str(approval_path.relative_to(ROOT)),sha256=sha(approval_path)),currentNumericBinderVerified=False,currentExecutableParity=False,
  handlers=211,warlockHandlers=2,completedClasses=[],learnedDeathMarkEnabled=False,nativeMarkers=12,markers=dict(counts),boots=3,reloads=2,saves=3,
  contentBuild=2,integrationBuild=1,sourceSnapshot=2,nativeRun=1,priorCompilerMatricesRerun=0,pinnedFormulaRanks=255,
  capture=dict(path=str(capture_path.relative_to(ROOT)),sha256=sha(capture_path),bytesRead=capture['bytesRead'],process=capture['process'],artifacts=capture['artifacts']),
  compiledRow=dict(path=str(table_path.relative_to(ROOT)),sha256=sha(table_path),stride=748,id=375,auraStats=[31,34,35,-1,-1,-1],targetState=212,auraEvents=[-1]*4,eventFunctions=[0]*4),
  artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
  scope='Prepared hostile ordinary NPC recipient:actual armor/native damage hooks,signed flat penalties before resistance,no absent components or poison amplification,positive scaling controls,safe saturation,rank/creator/deadline replacement,curse/InnerSight coexistence,atomic malformed restore,expiry prune. Natural rank255 mark and ordinary curse survive two actual reloads without retention holds. No learned cast or automatic pet AI enabled.',
  limitations=['Current numeric binder45f8d0 remains protected. Numeric and replacement rules are user-approved Sanctuary adaptations,not current-build equivalence.','Player/PvP,enemy-owned pets and unclassified recipients remain outside the prepared predicate.','Automatic GenericPet controller consumption/action cancellation,startup/payment/input and final assets still required.'])
 path=ROOT/'tools/reference/death-mark-debuff-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print('Death Mark recipient adaptation:12 native markers,3 boots/2 reloads/3 saves;255 pinned rank formulas. User approved;211 handlers unchanged.')
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
