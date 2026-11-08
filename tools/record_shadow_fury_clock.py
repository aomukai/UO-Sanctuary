#!/usr/bin/env python3
"""Record original Fury sequence qualification;automatic policy stays gated."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-action'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 proofs={}
 for name,contexts in [('shadow-fury-action',46),('shadow-fury-clock',12)]:
  p=ROOT/'tools/reference'/f'{name}-source-evidence.json';d=json.loads(p.read_text());assert d['contexts']==contexts
  for f,h in (d['inputs']|d['artifacts']).items():assert sha(ROOT/f)==h,f
  stage=BASE/(name+'-source');assert not(stage/'sanitizer.log').read_text();proofs[name]=sha(p)
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 for name in ['shadow-fury-clock','shadow-fury-missiles','blade-fury-missile','shadow-flight']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-2.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-3.json').read_text());c2=json.loads((STAGE/'source-snapshot-2.json').read_text());assert native['copiedData']==snapshot['inputs']['copiedData']==c2['inputs']['copiedData']
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 for p,h in c2['inputs']['sourceInputs'].items():
  if p.startswith(('src/','shared/')):assert native['sourceInputs'][p]==h,p
 saved=json.loads((STAGE/'shadow-fury-clock-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[p for pattern in ['source-snapshot-*.json','*-build-*.log','native-run-*.log','birth-*.json','birth-*.log','*-fixtures.json','*-fixture.json','rejected-sc-prototype/*.cs']for p in STAGE.glob(pattern)]
 files += [ROOT/p for p in ['tools/probe_shadow_fury_action.py','tools/probe_shadow_fury_clock.py','tools/record_shadow_fury_clock.py','tests/check_shadow_fury_clock.py','tools/reference/shadow-fury-action-source-evidence.json','tools/reference/shadow-fury-clock-source-evidence.json','docs/shadow-missile-critical-proposal.md']]
 receipt=dict(parent='c905558b4647f383cdbedce65212119f848f6c39',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,intrinsicFuryConnected=False,originalFuryClock=True,automaticCriticalPolicy='unapproved;proposed unsigned helper has no gameplay caller',originalMode=14,genericMetadataMode=7,sequence='seq_swbladefury',points=17,sequenceSpeed=256,releaseMarker=10,unrewoundEnd=18,sourceProof=proofs,newSourceContexts={'actualOriginalSequence':12,'genericSCComparisonOnly':30,'legacyAndProposedCriticalComparison':16},nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=3,artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},preservation=preservation(),
 diagnostic='Initial source probe expected an absent old generated timing template;reconstructed only its harness preamble from the retained tool and exact pinned routine bodies. Native run1 refused a nonexistent unnecessary Flight fixture path before startup;runner corrected without rebuilding. Run2 rejected a generic-SC prototype on real initialized originals:their slot2entry retains mode14 despite metadata7. Prototype removed,existing action/executor/animation files restored byte-for-byte to parent,source copies retained. New own17point sequence/complete-rate/stop controls qualify only original clock. Accepted C2/I2/native3;tests renamed before I2,unchanged production sources match C2 snapshot.',
 scope='Both original seed1 Shadow species,actual mode14/17point clock,consume-before-advance marker10/unrewoundend18,Other rate sequence256 and own IAS/FCR/attack changes isolated;no RNG/mana/emission/admission. Original actors/items/resources/wounds/seed survive2reloads. Retained actual own Fury travelling receiver,player Fury and full own Flight pass.30ordinarySC comparison contexts are explicitly not original Fury;16critical cases distinguish full signed legacy routine from separately modified unsigned proposal. No old compiler matrices rerun.',
 remaining=['Actual NPC do48/rewind event dispatch and live sequence advancement must be composed rather than importing generic SC/player SQ23 clocks','Inferno timed list21/renew7/strict5-shot deadline must survive the appropriate action/reentry boundary;unrewound sequence end18 is earlier than initial state expiry21','Unsigned critical proposal awaits explicit answer;actual own selected-weapon chance collection and world/private seed allocation still need integration','Then full Fury AI,BladeSentinel/death modifier42 and learned Shadow admission','Shock Web NPC localdelay15 question remains nonblocking;no current executable/PvP/caster-removal certification'])
 target=ROOT/'tools/reference/shadow-fury-clock-native-evidence.json';s=json.dumps(receipt,indent=2)+'\n'
 if check:assert target.read_text()==s
 else:target.write_text(s)
 print('Original Fury clock qualified:12whole NPC controls+46scoped comparison controls,21native markers/2reloads;automatic policy/action stays gated;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
