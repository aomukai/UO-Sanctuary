#!/usr/bin/env python3
"""Record scoped Apocalypse endpoints/shared-roll/native evidence,without rerunning old matrices."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/apocalypse-packets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check):
 for name,count in [('spell-packet-roll',132),('apocalypse-maximum',108)]:
  evidence=json.loads((ROOT/f'tools/reference/{name}-current-evidence.json').read_text());assert evidence['cases']==count
  for p,h in evidence['artifacts'].items():assert sha(ROOT/p)==h,p
 approval=ROOT/'tools/reference/apocalypse-endpoints-approval.json';assert json.loads(approval.read_text())['approvedByUser']
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 assert counts['iron-golem-birth-check']==3
 for name in ['iron-golem-items','npc-resource-callbacks','apocalypse-impact','apocalypse-packets']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3 and 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind,n in [('content',2),('integration',3)]:assert all(s in(STAGE/f'{kind}-build-{n}.log').read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 inputs=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-4.json').read_text())
 assert inputs['copiedData']==snapshot['inputs']['copiedData']
 for p,h in inputs['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert inputs['sourceInputs'][p]==h,p
 fixture=json.loads((STAGE/'apocalypse-packets-fixtures.json').read_text());assert fixture['Generation']==2 and fixture['Packet']['Rank']==1
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present'for s in inventory['skills'])==211 and inventory['completedClasses']==[]
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['ApocalypsePackets','SourceSpellPacketRoll','SkillMechanics']]+['tests/Sanctuary.Integration/ApocalypsePacketChecks.cs','tests/check_apocalypse_packets.py','tools/probe_spell_packet_roll.py','tools/probe_apocalypse_maximum.py','tools/record_apocalypse_packets.py','tools/reference/apocalypse-endpoints-approval.json']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedApocalypseEnabled=False,minimumGetterFallbackApproved=True,fullCurrentMinimumGetterVerified=False,currentRollCases=132,currentMaximumCases=108,pinnedEndpointPairs=36,nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=3,sourceSnapshot=4,nativeRun=3,priorCompilerMatricesRerun=0,
  files={p:sha(ROOT/p)for p in paths},artifacts=inputs['artifacts'],preservation=preservation(),
  limits=['Prepared fire explosion packet only;area/launch/payment/input and potion components remain unfinished.','Offline getters use explicit supplied table/tier/formula/mastery leaves. Protected current minimum is approved table fallback.','Saved DTO proves packet serialization/continued receiver use,not persistence of active explosions.','No new final effects/icons;recipient0x36D4 remains identified placeholder.'])
 path=ROOT/'tools/reference/apocalypse-packets-native-evidence.json';body=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==body
 else:path.write_text(body)
 print('Apocalypse packet checkpoint:15 native markers/3boots/2reloads;132 wrapper and108 maximum cases;approved minimum fallback;installed build/config/saves preserved.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');record(a.parse_args().check)
