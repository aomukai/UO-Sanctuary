#!/usr/bin/env python3
"""Publish literal current table masks without inferring selector meanings."""
import argparse,csv,json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,digest
LOCAL=ROOT/'.local/class-skills/generic-pet-state-mask'
def build(check=False):
 capture=LOCAL/'static-mask-capture.json';r=json.loads(capture.read_text())
 assert r['referenceVersion']=='3.3.93847' and r['failure'] is None and r['bytesRead']==4208
 assert r['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
 assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten','actorDataRead','inventoryDataRead'])
 artifacts=[Path(__file__).resolve(),capture]
 for n,h in r['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n;artifacts.append(ROOT/n)
 states=ROOT/'.local/class-skills/generic-pet-metadata/states.bin';text=states.with_suffix('.txt');raw=states.read_bytes();rows=list(csv.DictReader(text.open(),delimiter='\t'))
 assert len(rows)==struct.unpack_from('<I',raw)[0]==232 and len(raw)==4+232*68
 ids={i for i in range(232) if raw[4+i*68+20]&4};assert ids=={i for i,row in enumerate(rows) if row['udead']=='1'}
 profiles=[]
 for selector,v in enumerate(r['views'],1):
  assert v['selector']==selector and v['compatible'] and v['count']==232 and len(v['words'])==8 and v['words'][-1]&0xffffff00==0
  assert [x['id'] for x in v['rowSamples']]==[0,2,9,30,105,139,140,231]
  for x in v['rowSamples']:assert digest(raw[4+x['id']*68:4+(x['id']+1)*68])==x['sha256']
  observed={i for i in range(232) if v['words'][i//32]&(1<<(i&31))};assert sorted(observed)==v['stateIds']
  assert observed==ids-(set() if selector==3 else {207,227})
  profiles.append(dict(selector=selector,stateCount=232,words=v['words']))
 assert profiles[0]['words']==profiles[1]['words'] and profiles[1]['words']!=profiles[2]['words']
 output=ROOT/'data/Sanctuary/Balance/generic-pet-state-masks.json';encoded=json.dumps(profiles,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 artifacts += [states,text,output,LOCAL/'diagnostics/attempt0-capture_static_state_mask.py',LOCAL/'diagnostics/attempt0-capture.log']
 assert "AssertionError" in artifacts[-1].read_text()
 result=dict(referenceVersion='3.3.93847',component='Literal static mask410 profiles from current table slots',processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,actorDataRead=False,inventoryDataRead=False,handlerEnabled=False,
  verified=dict(selectors=3,stateCount=232,maskWordsPerSelector=8,readBytes=4208,rowSamplesPerSelector=8,compiledTextUdeadMatches=232,selector1StateIds=r['views'][0]['stateIds'],selector2StateIds=r['views'][1]['stateIds'],selector3StateIds=r['views'][2]['stateIds']),
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
  established=['Every raw mask and table header was read twice with stable process identity,known executable anchors and readable bounded mappings. Eight whole owned compiled rows match per view before following the known mask pointer. Only fixed static metadata was read.',
   'Selector3 exactly matches compiled byte20 bit2 and owned text udead across232rows. Selectors1/2 omit bind_demon207 and bind_demon_underling227. These are literal observed profiles;their generation rule and selector/game-mode mapping are not inferred.',
   'The first attempt stopped before creating a reader because an unrelated function had an invalid disassembly marker. The corrected preflight requires all complete fragments of each exact referenced function to be valid and retains all parent artifact hash checks. Both attempts are preserved.'],
  remaining=['The pointer aliases seen for selectors1/2 are snapshot observations only,not a universal alias guarantee. No selector is mapped to area difficulty. Real world state-list membership,registry,collision/event order and complete demon actor lifecycle remain separate.'])
 out=ROOT/'tools/reference/generic-pet-state-mask-data-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('State masks:3explicit views/8words each;232compiled udead fields;24whole-row samples;4208bounded read bytes.')
 return profiles
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
