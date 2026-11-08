#!/usr/bin/env python3
"""Record NPC Dragon Claw source/contact/world/persistence checkpoint."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-dragon-claw'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-dragon-claw','shadow-claw','shadow-finisher','shadow-ice','shadow-thunder']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind,run in [('content',3),('integration',2)]:assert all(x in(STAGE/f'{kind}-build-{run}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 source=json.loads((ROOT/'tools/reference/shadow-dragon-claw-source-evidence.json').read_text());assert source['contexts']==24 and len(source['reusedWholePacketRoutineHashes'])==13 and len(source['genericExecutionRoutineHashes'])==1
 for p,h in source['artifacts'].items():assert sha(ROOT/p)==h,p
 assert not(ROOT/'.local/class-skills/shadow-dragon-claw-source/sanitizer.log').read_text()
 saved=json.loads((STAGE/'shadow-dragon-claw-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(b['Charges']for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-3.log','integration-build-2.log','shadow-dragon-claw-fixtures.json','birth-native-checks-failed-1.log','birth-inputs-failed-1.json']]
 files+=[ROOT/p for p in ['tools/record_shadow_dragon_claw.py','tools/probe_shadow_dragon_claw.py','tools/reference/shadow-dragon-claw-source-evidence.json','tests/check_shadow_dragon_claw.py','.local/class-skills/shadow-dragon-claw-source/source-before-handler-review.cpp','.local/class-skills/shadow-dragon-claw-source/cases-before-handler-review.json']]
 receipt=dict(parent='830f9d380b413800ca2b7dd26419ee332c3584e3',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicDragonClawConnected=True,newCompiledSourceControls=24,reusedAuthenticatedPacketRoutines=13,reusedGenericExecutorRoutines=1,nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=2,sourceSnapshot=3,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),diagnostic='Initial native run exposed a missing progressive-phase flag. Generic non-missile executor does not pre-set it;full progressive helper sets before contact,do46writes raw parity afterward. Runtime fixed and source bridge assumption corrected;failed inputs/log and pre-review source/cases preserved.',scope='Both original Shadows:own NPC mode14/start25/do46/raw-point hand/rates,no NPC mana,full original source128skill ED/hard mastery,approved charged accuracy-only guarantee with block retained,per-contact all-six release/consume-one,world/natural Warrior AI,target-loss/interrupt/no replay and exact original saves. Source packet/progressive trace services declared;existing whole packet proof reused. Current NPC parity/charged boundaries and other intrinsic/death/learned/player mixed-order/geometry/PvP/event/removal scopes remain,no class completion.')
 out=ROOT/'tools/reference/shadow-dragon-claw-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC Dragon Claw receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded NPC Dragon Claw:24 new source controls/24 native markers,two reloads;installed build and saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
