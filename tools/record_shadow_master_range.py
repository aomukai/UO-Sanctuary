#!/usr/bin/env python3
"""Record the approved Master Sentinel range adapter without rerunning source matrices."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-master-range'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 proofpath=ROOT/'tools/reference/shadow-master-ai-source-evidence.json';proof=json.loads(proofpath.read_text());assert proof['contexts']==497
 for group in ['inputs','artifacts']:
  for rel,h in proof[group].items():assert sha(ROOT/rel)==h,rel
 for name in ['data','guide']:
  p=ROOT/f'tools/reference/d2r-local-missile-{name}-evidence.json';e=json.loads(p.read_text())
  if name=='guide':assert sha(ROOT/e['file']['path'])==e['file']['sha256']
 approval=json.loads((STAGE/'approval-state.json').read_text());assert approval['approved']=='Approve the Master Sentinel range rule'
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==18 and ': failed'not in log and ' ERR]'not in log
 for name in ['shadow-master-range','shadow-master-world']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert counts['shadow-master-ai-check']==3 and 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,n in [('content',2),('integration',4)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-6.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert int((STAGE/'shadow-master-range-fixtures.json').read_text())==2
 paths=['src/Sanctuary.Content/SkillMissileRanges.cs','src/Sanctuary.Content/ShadowMasterWorld.cs','tests/Sanctuary.Integration/ShadowMasterAiChecks.cs','tests/Sanctuary.Integration/ShadowMasterRangeChecks.cs','tests/Sanctuary.Integration/ShadowMasterWorldChecks.cs','tests/check_shadow_master_range.py','tools/record_shadow_master_range.py','docs/shadow-master-sentinel-range-proposal.md']
 result=dict(referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,approvedRule=approval['approved'],newCompilerMatrices=0,reusedSourceContexts=497,nativeMarkers=18,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=4,sourceSnapshot=6,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 scope='Only Master Sentinel257/blade creeper supplies its own effective rank to current10+5*lvl Range and retains the complete older AI strict squared-distance guard.694literal rows unchanged,48default expression gates retained,seven rank goldens and21whole-policy boundaries pass. Other skill/missile pairings refuse expression context. Actual initialized original Master natural selection/release/recovery creates exactly one own Sentinel with own rank and no player mana;existing Master world movement/target queries/damage/unsupported actions/nontrivial repeat/original-equipment and wound saves retained through two reloads. Root stat/weapon factory and real blade lifetime/path unchanged. Exact modern range caller context remains an approved adaptation,not a current-executable claim.',
 remaining=['Fury current NPC callback/rewind and pending unsigned missile critical proposal remain separate','Other missile expression contexts and full modern Master AI/geometry/callback parity remain unqualified','Learned Shadow admission and class certification remain gated','Existing Sentinel/Shadow effects/audio and talent/hotkey placeholders remain in separate inventories'])
 p=ROOT/'tools/reference/shadow-master-range-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Approved Master Sentinel range:18native markers/2reloads;497existing source controls reused;installed artifacts/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
