#!/usr/bin/env python3
"""Record bounded current setter edges and the genuinely unreadable recalculator."""
import argparse,json
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills/warlock-runtime-reference'
NAMES=['services-shadow-unit-set-20261107.json','services-shadow-list-set-20261107.json','services-shadow-stat-propagate-20261107.json','services-shadow-stat-full-20261107.json']
def record(check=False):
 inputs={};captures=[]
 def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
 for name in NAMES:
  c=json.loads(pin(BASE/name));captures.append(c)
  assert c['referenceVersion']=='3.3.93847'and c['bytesRead']<=8192 and c['bytesRead']==2*sum(r['size']for r in c['reads'])
  assert all(r['stable']and r['size']<=4096 for r in c['reads'])and not any(c[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  for n,h in c['artifacts'].items():assert e.sha(ROOT/n)==h,n;pin(ROOT/n)
 complete={r['rva']:r for c in captures for r in c['records']if r['allUnwindFragmentsCaptured']}
 assert all(f['captured']and f['invalidInstructionMarkers']==0 for r in complete.values()for f in r['fragments'])
 assert {k:v['bytes']for k,v in complete.items()}=={'0x24dec0':224,'0x24dcd0':247,'0x24c4b0':288,'0x24ad50':427}
 missing=captures[-1]['records'][1];assert missing['rva']=='0x24c150'and missing['bytes']==852 and not missing['allUnwindFragmentsCaptured']
 assert len(missing['fragments'])==4 and all(not f['captured']and f['permissions']=='---s'for f in missing['fragments'])
 costs=json.loads(pin(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json'));maximum=next(r for r in costs if r['Id']==7)
 assert maximum['Name']=='maxhp'and maximum['Flags']&2048 and maximum['HasOperations']
 pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',executableSha256=captures[0]['executableSha256'],currentSetterRva='0x24dec0',completeFunctionBytes={k:v['bytes']for k,v in complete.items()},missingRecalculationRva='0x24c150',missingRecalculationBytes=852,missingFragments=4,allMissingFragmentsUnreadable=True,protectedReadAttempted=False,executedInstructionContexts=0,currentSetterAndCallbackParityVerified=False,handlerEnabled=False,inputs=inputs,scope=['Bounded current executable capture follows actual direct calls from the already captured Shadow helper to unit setter,list setter,propagation and full-value addition. All four selected functions are complete,zero invalid-instruction markers.','Current original maxhp metadata has value callbacks and operations. Its recalculation path remains unreadable in this observed process. No protected fragment was forced,read or replaced.'],limits=['Captured call edges and inspected instructions do not certify the complete current stat setter/callback numeric behavior. No instruction-execution contexts or current callback ABI claim is made. The parked resource body uses separately qualified older numeric branches/current tables and native integration evidence.','This is an individual reference gap,not a stalled class-roadmap goal. Continue actual NPC skill ownership/execution and other unaffected work;revisit the complete current recalculation/callback path or a concrete approved adaptation before claiming parity.'])
 encoded=json.dumps(result,indent=2)+'\n';out=ROOT/'tools/reference/shadow-stat-setter-edges-source-evidence.json'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current setter edges:224/247/288/427bytes complete;required852byte recalculator/four protected fragments unread;no forced access or current parity claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
