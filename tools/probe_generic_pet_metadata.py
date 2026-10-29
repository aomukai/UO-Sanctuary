#!/usr/bin/env python3
"""Compose owned table fields with complete cached preparation code offline."""
import argparse,json
from pathlib import Path
import unicorn
from unicorn import UC_HOOK_MEM_READ
from probe_generic_pet_preparation import Preparation,STATES,row,sample as buff_sample
from probe_generic_pet_search_groups import SearchGroups,sample as search_sample
from probe_warlock_runtime import ROOT,digest
from build_generic_pet_metadata import build as metadata
OUT=ROOT/'.local/class-skills/generic-pet-metadata-source-probe.json'
class FullStates:
 def extend(self):
  # The reused harness already observes IDs0..31. Extend its same immutable
  # raw-state observer to the rest of this owned232-row table.
  self.uc.hook_add(UC_HOOK_MEM_READ,self.state_hook,begin=STATES+68*32,end=STATES+68*232-1)
class BuffMetadata(FullStates,Preparation):
 def __init__(self):super().__init__();self.extend()
class SearchMetadata(FullStates,SearchGroups):
 def __init__(self):super().__init__();self.extend()
def build(check=False):
 skills,states=metadata(check=True);groups=[r['group'] for r in states];buff=BuffMetadata();search=SearchMetadata();cases=[]
 for selector in [1,2,3]:
  for skill in skills:
   sid=skill['id'];candidate=row(skill=sid,kind=1);p=buff_sample(selector=selector,owner=0,states=groups,skills={str(sid):[skill['auraState'],skill['flags39']]},has={},visits=[],nested={})
   b=buff.execute(p,[candidate]);assert not b[0]
   candidate=row(skill=sid,kind=2);p=search_sample(selector=selector,states=groups,skills={str(sid):[skill['auraTargetState'],skill['auraState'],skill['flags39']]})
   s=search.execute(p,[candidate],[[[1,2],True]]);assert not s[0]
   cases.append([selector,sid,b,s])
 result=dict(skills=skills,states=states,cases=cases);encoded=json.dumps(result,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/build_generic_pet_metadata.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/reference/generic-pet-metadata-data-evidence.json',*buff.artifacts,*search.artifacts]
 proof=dict(referenceVersion='3.3.93847',component='Owned metadata composed with cached GenericPet buff/search preparation',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,worldSelectorBindingVerified=False,
  verified=dict(skillRows=429,stateRows=232,explicitSelectorViews=3,buffCompositions=1287,searchCompositions=1287,totalCompositions=2574,unsupportedBoundaries=0),
  emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
  services=['Each owned skill row is supplied once as a type1 buff candidate and once as a type2 search candidate for each explicitly bound selector view. Supplied categories isolate each preparation contract;they do not claim every actual actor gathers every skill in either category.',
   'Actual compiled self/target state IDs,raw flags39 and all232signed state groups supply the captured routines. Buff state membership is explicitly false with owner0;search begins with a prior owned group. The429skill/232state table fields are not inferred from formula text.',
   'Captured routines execute via the existing offline harnesses and declared metadata/memory services. The three explicit views bind the same controlled fixture table;this does not establish that real game selectors alias a single table or map to difficulty.'],
  remaining=['Native catalog/view and composed preparation must match these controls. Actual world selector binding,skill ownership/levels,state membership and owner traversal remain separate.',
   'Search acceptance stops at preparation return. Post-cleanup callback consumption and world target selection remain unverified. No learned summon or complete actor is enabled.'])
 p=ROOT/'tools/reference/generic-pet-metadata-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Owned pet metadata source composition:1287buff+1287search cases;world selector mapping remains unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
