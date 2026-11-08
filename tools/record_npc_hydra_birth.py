#!/usr/bin/env python3
"""Bind prepared original-NPC Hydra birth/start/item frame to retained source."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-hydra-birth'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 source=ROOT/'tools/reference/hydra-creation-source-evidence.json';proof=json.loads(source.read_text());assert proof['cases']==12800
 for rel,h in proof['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/rel)==h,rel
 for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h,rel
 assert proof['sanitizers']=='ASan/UBSan clean'
 cases=json.loads((BASE/'hydra-creation-source/cases.json').read_text());selected=[c for c in cases if c[0][0]==3 and c[0][1]==0 and c[0][4]in(1,20)];assert len(selected)==128
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==15 and ': failed'not in log and ' ERR]'not in log
 for n in ['npc-hydra-birth','npc-item-spell-flights']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind,n in [('content',1),('integration',2)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-3.json').read_text());content=json.loads((STAGE/'source-snapshot-1.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']==content['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 for rel,h in content['inputs']['sourceInputs'].items():
  if rel.startswith(('src/','shared/')):assert native['sourceInputs'][rel]==h,rel
 saved=json.loads((STAGE/'npc-hydra-birth-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2
 for body in saved['Bodies']:assert body['Capture']['Caster']==body['Actor'] and body['Capture']['ResponsibleOwner']==saved['Owner'] and body['Capture']['Birth']['Rank']==3
 paths=['src/Sanctuary.Content/NpcHydraCreation.cs','src/Sanctuary.Content/NpcItemSpellCasts.cs','tests/Sanctuary.Integration/NpcHydraBirthChecks.cs','tests/check_npc_hydra_birth.py','tools/record_npc_hydra_birth.py']
 result=dict(parent='138b50268b9911a19b2d9409d2052146047542c2',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,automaticNpcHydraEnabled=False,sourceProofSha256=sha(source),reusedSourceContexts=12800,selectedNonhirelingNativeComparisons=128,newCompilerMatrices=0,nativeMarkers=15,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=3,contentSourceSnapshot=1,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 diagnostic='First native run passed source/start/frame controls for both original casters,then its root-gear fixture requested unavailable Ormus Robes. Replaced that unrelated fixture with a supported ordinary root claw/stat swap;unchanged content assembly reused. Final run passes all15markers/2reloads. First log/input manifest retained. No old compiler matrices rerun.',
 scope='Prepared birth reads actual original NPC accrued mastery/pierce/sunder and own hard Fire Bolt/Fire Ball;explicit item rank stays six-bit. Caster is actual NPC;root player is responsible-credit provenance only. Existing unchanged source explicitly keeps ordinary monster ownership,alignment and no player FIFO registration;hirelings are a distinct branch. Source start14 room/town semantics and common do144 item executor feed three independently accepted/rejected half-tile requests. All partial masks,target/ground,outgoing flags and exception restoration pass with no mana/actor RNG/world allocation/head materialization. Real Warrior allocation1/Master allocation9 original equipment and source snapshots persist;root equipment/stat swaps do not rewrite capture. Existing original item-spell flight consumers retained. This is a required prepared factory component,not working automatic NPC Hydra or learned Shadow admission.',
 remaining=['Actual NPC-owned Hydra head allocation/ownership/alignment,own roster exclusion from player18-head FIFO,private seeds,AI/bolts/credit/persistence remain to connect through existing backend','NPC parent-removal/lifetime and current executable behavior still need source qualification or explicit adaptation;do not silently substitute player ownership','Nonzero inherited fire sunder remains blocked by existing Hydra damage host','Automatic proc metadata still explicitly refuses skill62;do not remove original Master equipment to bypass the gap','Fury/other Master range/AI gaps and learned Shadow admission remain unfinished;effects/icons retain separate placeholders'])
 p=ROOT/'tools/reference/npc-hydra-birth-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('NPC Hydra prepared capture/start/item frame:128source comparisons/15native markers/2reloads;automatic world proc remains gated;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
