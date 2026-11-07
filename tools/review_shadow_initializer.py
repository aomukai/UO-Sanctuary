#!/usr/bin/env python3
"""Bind Shadow initialization/numeric composition without admitting a live actor."""
import argparse,json
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT

def review(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
 data=json.loads(pin(ROOT/'tools/reference/shadow-initializer-data-evidence.json'))
 source=json.loads(pin(ROOT/'tools/reference/shadow-initializer-source-evidence.json'))
 assert data['referenceVersion']==source['referenceVersion']=='3.3.93847'
 assert source['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and not source['currentExecutableParity'] and source['contexts']==1344
 assert data['compiledTextMatches']['skillSlots']==6008 and data['compiledTextMatches']['primaryRows']==751 and data['compiledTextMatches']['secondaryRows']==623
 assert source['currentDataEvidenceSha256']==e.sha(ROOT/'tools/reference/shadow-initializer-data-evidence.json')
 for proof in [data,source]:
  for group in ['inputs','artifacts']:
   for name,h in proof.get(group,{}).items():assert e.sha(ROOT/name)==h,name;pin(ROOT/name)
 assert len(source['completeRoutineHashes'])>=8
 for r in data['shadows']:
  assert r['minimumLife']==r['maximumLife']==[376]*3 and r['resistances']==[[40]*3]+[[0]*3]*5 and r['difficultySkillBonus']==[0,3,7]
 birth=json.loads(pin(ROOT/'tools/reference/shadow-current-birth-source-evidence.json'));assert birth['sourceContexts']==3456 and birth['helperBytes']==872
 binding=json.loads(pin(ROOT/'tools/reference/current-skill-dispatch-source-evidence.json'));assert binding['skillDoTableIdentityVerified'] and binding['shadowEntryLinked'] and not binding['fullExecutorVerified']
 for name in ['ShadowInitializer.cs','ShadowBirthGraph.cs','ShadowCreation.cs','GeneratedNpcStatGraph.cs','ShadowEquipment.cs','GeneratedNpcEquipment.cs','EquipmentStatGraph.cs','EquipmentStatValues.cs','NpcWorldAllocation.cs','SourceRandom.cs']:
  pin(ROOT/'src/Sanctuary.Content'/name)
 for name in ['tests/Sanctuary.Integration/ShadowInitializerChecks.cs','tests/check_shadow_initializer.py',str(Path(__file__).resolve().relative_to(ROOT))]:pin(ROOT/name)
 result=dict(referenceVersion='3.3.93847',parent='2edd0037de37399f05521fef65cd7c22f4e2a7ce',sourceInitializerContexts=1344,currentBirthHelperContexts=3456,compiledSkillSlots=6008,preparedOnly=True,currentInitializerExecutableParity=False,handlerEnabled=False,inputs=inputs,scope=['Current owned primary/secondary/component/skill/difficulty fields supply the separately qualified complete older initializer. Fixed component and HP draws precede original equipment. Skill ranks include difficulty0/3/7;no Valkyrie-specific AC/AR additions.','ShadowInitialization records actual shared-world unit allocation and independent unit continuation. Basis supplies initialized numeric fields and do49 owner-level replacement. ShadowBirthGraph uses the existing current-helper plan and stat writers/list propagation before original item replay.','GeneratedNpcStatGraph tracks explicitly registered posted birth lists separately from its original item attachment order. Arbitrary unknown list owners still refuse. Existing item/stat/percentage/formula/random/allocation services are reused.'],limits=['No live Shadow actor/resource admission,AI105/106,actual NPC skill assignment/permission/modes/events,UMod42 death execution,learned casting/payment/input or full pet persistence. Native storage fixtures are existing UO Rat holders,not a substitute Shadow species. Their native pools remain unchanged.','Original item properties remain unfiltered in the numeric graph. Retention is not permission to execute skill/event/aura/state/charge/resource consumers. Exact current initializer parity remains unverified;the captured current birth helper and dispatch linkage have separate evidence.','Sequence IDs are validated table metadata,not completed sequence/event execution. Active sunder,outer spawn callbacks/region cache generation and allocation-failure stat-list semantics remain separately scoped.'])
 encoded=json.dumps(result,indent=2)+'\n';out=ROOT/'tools/reference/shadow-initializer-implementation-evidence.json'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Prepared Shadow initialization:1344older whole-source contexts/6008current skill slots;current helper-before-item numeric graph;no live Shadow admission.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
