#!/usr/bin/env python3
"""Rebuild the unchanged historical Find Potion oracle without replacing it."""
import ast,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/find-potion'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
 STAGE.mkdir(exist_ok=True)
 original=ROOT/'tools/probe_find_potion_reference.py';prefix=[]
 for node in ast.parse(original.read_text()).body:
  if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='path' for t in node.targets):break
  prefix.append(node)
 else:raise AssertionError('Historical output boundary missing')
 assert original.read_bytes()==subprocess.check_output(['git','show','83e9e41:tools/probe_find_potion_reference.py'],cwd=ROOT)
 ns={'__file__':str(original)};exec(compile(ast.Module(body=prefix,type_ignores=[]),str(original),'exec'),ns)
 evidence=ROOT/'tools/reference/find-potion-engine-evidence.json';old=json.loads(evidence.read_text())
 for name,h in old['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/name)==h,name
 for name,block in ns['blocks'].items():assert hashlib.sha256(block.encode()).hexdigest()==old['probe']['blockHashes'][name],name
 cpp=STAGE/'find-potion-source.cpp';binary=STAGE/'find-potion-source';log=STAGE/'source-build.log';cpp.write_text(ns['cpp'])
 with log.open('w') as out:subprocess.run(['g++','-std=c++20','-Wno-multichar','-O2',str(cpp),'-o',str(binary)],stdout=out,stderr=out,check=True)
 data=json.loads(subprocess.check_output([str(binary)]))
 historical=ROOT/'.local/class-skills/find-potion-source-probe.json';assert data==json.loads(historical.read_text())
 output=ROOT/'.local/class-skills/find-potion-native-source-probe.json';output.write_text(json.dumps(data,indent=2)+'\n')
 artifacts=[original,evidence,cpp,binary,log,historical,output,Path(__file__).resolve()]
 artifacts += [ROOT/'.local/class-skills/d2moo-pinned-source'/p for p in old['sourceHashes']]
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=old['legacyCommit'],canonicalGeneratorCommit='83e9e41',currentExecutableParityVerified=False,
  caseCounts={k:len(v) for k,v in data.items()},wholeRoutineHashes={name:old['probe']['blockHashes'][name] for name in ns['blocks']},
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},
  limits=['Whole legacy routines run with declared target/state/room/Act/formula/drop services; not modern executable parity.',
   'Native policy consumes the existing SkillValues chance curve. Its modern integer rounding and shared owner RNG sequence remain separate uncertainties.',
   'Generic item-code zero is represented by the oracle; the learned native route instead requires an explicit valid region Act before payment, as approved.'])
 (ROOT/'tools/reference/find-potion-native-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n')
 print('Rebuilt complete Find Potion source:',receipt['caseCounts'])
if __name__=='__main__':build()
