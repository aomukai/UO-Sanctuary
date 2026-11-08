#!/usr/bin/env python3
"""Bind approved NPC-owned Hydra world/proc integration to native evidence."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-hydra-world'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 reused={}
 for name in ['hydra-creation-source-evidence','hydra-ai-source-evidence','hydra-clock-source-evidence','hydra-damage-source-evidence','hydra-bolt-creation-source-evidence','hydra-retained-owner-damage-source-evidence']:
  p=ROOT/'tools/reference'/f'{name}.json';proof=json.loads(p.read_text());reused[name]=sha(p)
  for rel,h in proof.get('sourceHashes',{}).items():assert sha(BASE/'d2moo-pinned-source'/rel)==h,rel
  if name=='hydra-creation-source-evidence':
   assert proof['cases']==12800
   for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h,rel
 approval=json.loads((STAGE/'approval-state.json').read_text());assert approval['approved']=='Approve independent Hydra lifetime'
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==20 and ': failed'not in log and ' ERR]'not in log
 for n in ['npc-hydra-world','npc-hydra-birth','hydra-world']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2
 assert counts['hydra-world-reload-result']==2 and 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,n in [('content',4),('integration',6)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-9.json').read_text())
 assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 saved=json.loads((STAGE/'npc-hydra-world-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Heads'])==6
 assert {h['Source']['NpcOwner']['Caster']for h in saved['Heads']}=={saved['Caster'],saved['MissingCaster']}
 paths=['src/Sanctuary.Content/'+n+'.cs'for n in ['HydraBody','Hydras','PlayerHydras','NpcHydraWorld','NpcHydraCreation','NpcOriginalItemProcs','NpcItemSpellCasts','NpcItemSkillExecution','NpcItemSpellDamage']]+['tests/Sanctuary.Integration/NpcHydraWorldChecks.cs','tests/Sanctuary.Integration/NpcHydraBirthChecks.cs','tests/check_npc_hydra_world.py','tools/record_npc_hydra_world.py','docs/npc-hydra-parent-removal-proposal.md']
 result=dict(parent='5073a40865a3c5b46a92605e29ffee99cd8c970e',referenceVersion='3.3.93847',handlers=208,completedClasses=[],learnedShadowsEnabled=False,automaticNpcHydraEnabled=True,automaticDomain='qualified original NPCs with zero inherited immunity-pierce;nonzero explicitly refused before admission/allocation',approvedRule=approval['approved'],reusedSource=reused,newCompilerMatrices=0,nativeMarkers=20,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=4,integrationBuild=6,sourceSnapshot=9,artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
 diagnostic='Prototype native1 exposed deleting an unadmitted Master9 numeric actor from a world map;fixture cleanup/removal now parks it first. Native2 passed damage/credit before its fixture incorrectly expected immediate actor cleanup on the first overdue observation;existing400ms retirement lease requires a second observation. Prototype native3 passed all20markers. User approved independent lifetime;removed temporary per-head proposal flag,connected real item metadata/start14/common do144 and registry callback,activated genuine Master9 numeric/item world profile. Final C4/I6/native4 passes all20markers including real parent death/removal and registered automatic item callback. Earlier logs/manifests retained;no old compiler matrices rerun.',
 scope='Actual NPC summon owner is distinct from root responsible player. Existing Hydra controller/bolt/typed damage reused,with own birth capture and original ID/binding provenance.21NPCheads/no player18-head FIFO,three independent allocations,private world/head/bolt seeds,unchanged parent attack RNG for head creation/control/bolts and no player mana cost;normal automatic proc chance retains its parent RNG,source AI/action/travel/HP/actual-head kill/root credit,expiry/retired emitter cleanup,atomic malformed graph rejection and real/missing-caster saves pass. Approved250frame parent-independent lifetime is standard,with existing already-fired bolt lifetime and root lifecycle. Missing parent never becomes a replacement mobile or player summon owner;native owner reference may be null after reload while saved NPC provenance remains. Real Master9 item registry callback creates three actual heads through start14/do144;failed automatic start returns0without allocation. Original player Hydra admission/payment/AI/bolts/damage/other-facet return/retirement and natural reload-resume checks retained. No class completion/deployment claim.',
 remaining=['Nonzero inherited item_pierce_fire_immunity remains explicitly gated;current table value300does not itself establish its current damage-side interpretation','Player/PvP recipient adapters,full modern room/target-acquisition and arbitrary nested mutation parity remain unqualified','Fury current callback/rewind and pending weapon-missile critical proposal,Master range/AI and learned Shadow admission remain unfinished','Final NPC Hydra head/cast/bolt/impact/retirement effects/audio remain shared placeholders;no new public proc icon'])
 p=ROOT/'tools/reference/npc-hydra-world-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Approved NPC Hydra world/procs:20native markers/2reloads;real parent death and missing-caster persistence;player Hydra retained;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
