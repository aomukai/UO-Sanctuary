#!/usr/bin/env python3
"""Exercise literal current masks through unmodified corpse/search source code."""
import argparse,itertools,json
from pathlib import Path
from probe_generic_pet_search_producers import Producers,sample
from probe_warlock_runtime import ROOT,digest
LOCAL=ROOT/'.local/class-skills/generic-pet-state-mask';OUT=LOCAL/'generic-pet-state-masks-source-probe.json'
def build(check=False):
 em=Producers();profiles=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-state-masks.json').read_text());cases=[];pipeline=[]
 def setup(selector):
  p=sample(selector=selector,mask410=profiles[selector-1]['words'],wideCount=232)
  for u in p['units'].values():u[9]=selector
  return p
 for selector,state,active,operation in itertools.product([1,2,3],range(232),[False,True],['mask','corpse']):
  p=setup(selector);words=[0]*8;words[state//32]=1<<(state&31);p['world']['1']['stats']=[-1 if active else 0,words]
  p['units']['1'][4]=12 if operation=='corpse' else 0;p['units']['1'][8]=0
  em.producerActive=False;args=[['u',1]]+([['m',0]] if operation=='mask' else [])
  result=em.execute_nested(p,operation,args);assert result[0]==0
  cases.append([p,operation,result])
 for selector,mask,owner,explicit,state in itertools.product([1,2,3],[0,1,16,63],[0,2],[0,3,4],[0,207,227]):
  p=setup(selector);p.update(mask=mask,owner=owner,explicit=explicit)
  p['units']['3'][4]=12;p['units']['3'][8]=0;words=[0]*8;words[state//32]=1<<(state&31);p['world']['3']['stats']=[-1,words]
  result=em.execute_pipeline(p);assert result[0]==0;pipeline.append([p,result])
 encoded=json.dumps(dict(cases=cases,pipeline=pipeline),separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 import unicorn
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 paths=[Path(__file__).resolve(),OUT,ROOT/'data/Sanctuary/Balance/generic-pet-state-masks.json',ROOT/'tools/reference/generic-pet-state-mask-data-evidence.json',*em.artifacts]
 paths += [ROOT/('tools/'+n+'.py') for n in ['probe_generic_pet_search_producers','probe_generic_pet_search_nested','probe_generic_pet_search_services','probe_generic_pet_search_bound','probe_generic_pet_search_wrapper','probe_generic_pet_search_callback','probe_generic_pet_search_groups','probe_generic_pet_preparation','probe_generic_pet_decisions','probe_warlock_runtime']]
 proof=dict(referenceVersion='3.3.93847',component='Current literal masks through actual state intersection,corpse helper and full search',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
  verified=dict(maskCorpseContexts=len(cases),pipelineContexts=len(pipeline),unsupported=0,selectors=3,stateCount=232),
  emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(paths))},
  remaining=['Real world unit membership,room graph,registry,selector meaning,ordering and full actor lifecycle remain unverified. The masks are static metadata profiles,not new learned handlers.'])
 path=ROOT/'tools/reference/generic-pet-state-mask-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
