#!/usr/bin/env python3
"""Rebuild the frozen complete-source Grim Ward lifecycle proof in a fresh stage."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/grim-ward-lifecycle';OUT=ROOT/'.local/class-skills/grim-ward-lifecycle-source-probe.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);old=ROOT/'tools/probe_grim_ward_reference.py';module=ast.parse(old.read_text());prefix=[]
 # Execute only construction of the original reviewed harness. Stop before its
 # first output assignment,so historical source,fixtures and receipts stay frozen.
 for node in module.body:
  if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='probe' for t in node.targets):break
  prefix.append(node)
 else:raise AssertionError('Original source-construction boundary missing')
 ns={'__file__':str(old)};exec(compile(ast.Module(body=prefix,type_ignores=[]),str(old),'exec'),ns)
 source=ns['cpp'];historical=ROOT/'.local/class-skills/grim-ward-source-probe.cpp'
 assert subprocess.check_output(['git','show','10e9c27:tools/probe_grim_ward_reference.py'],cwd=ROOT)==old.read_bytes()
 if historical.exists():assert historical.read_text()==source
 evidencePath=ROOT/'tools/reference/grim-ward-engine-evidence.json';evidence=json.loads(evidencePath.read_text())
 for name,h in evidence['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/name)==h,name
 for name,h in evidence['pinnedTables'].items():assert sha(ROOT/name)==h,name
 for name in ['MISSILE_GetRemainingFrames','MISSILE_GetCurrentFrame','MISSILE_SetCurrentFrame','SKILLS_SrvDo075_GrimWard','MISSMODE_SrvHit26_GrimWardStart','MISSMODE_SrvDo14_GrimWard']:
  assert hashlib.sha256(ns['blocks'][name].encode()).hexdigest()==evidence['probe']['blockHashes'][name]
 cpp=STAGE/'lifecycle-source-probe.cpp';binary=STAGE/'lifecycle-source-probe';log=STAGE/'source-build.log'
 if check:assert cpp.read_text()==source
 else:
  cpp.write_text(source)
  with log.open('w') as out:subprocess.run(['g++','-std=c++20','-O2',str(cpp),'-o',str(binary)],stdout=out,stderr=out,check=True)
 data=json.loads(subprocess.check_output([str(binary)]));oldData=ROOT/'.local/class-skills/grim-ward-source-probe.json';assert data==json.loads(oldData.read_text())
 encoded=json.dumps(data,indent=2)+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 owned=json.loads((ROOT/'tools/reference/barbarian-grim-ward-preflight.json').read_text());assert owned['skill']['calc1']==1000 and owned['skill']['mana']==4
 byName={x['Missile']:x for x in owned['missiles']}
 for size,start in [('small',5),('medium',8),('large',8)]:
  assert byName['grimward'+size+'start']['Range']==start and byName['grimward'+size]['Param1']==6 and byName['grimward'+size]['Param2']==30
 assert data['timeline']==list(range(0,1000,6))
 artifacts=[old,evidencePath,ROOT/'tools/reference/barbarian-grim-ward-preflight.json',oldData,cpp,binary,log,OUT,Path(__file__).resolve()]
 artifacts += [ROOT/'.local/class-skills/d2moo-pinned-source'/n for n in evidence['sourceHashes']]+[ROOT/n for n in evidence['pinnedTables']]
 result=dict(referenceVersion='3.3.93847',legacyCommit='0c0bbd1e6bef9181a3da1c046523af622a925416',component='Prepared Grim Ward placement,start-child and periodic lifecycle kernel',currentExecutableParityVerified=False,handlerEnabled=False,historicalCppAvailable=historical.exists(),canonicalGeneratorCommit='10e9c27',verified=dict(compiledWholeRoutines=6,lifecycleCases=3144,placementCases=1024,childCases=800,periodCases=1320,pulseValues=167,ownedStartFrames=[5,8,8],ownedBodyFrames=1000,ownedPeriodFrames=6,ownedDispatch=30),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},services=['Room/free coordinates,corpse/mode/row eligibility,missile allocation and curse/collision dispatch are declared services. Source performs no-draw/no-select/refresh even when placement allocation fails. This does not authorize hiding the retained native loot container.','Start child duration evaluates calc1against the original owner;periodic dispatch instead receives the body missile as its unit. Modern curse formula owner/synergy/AI context remains unresolved and is not replaced with the caster.','The elapsed-frame accessor and body do14precede the common collision decrement. The matrix retains167eligible elapsed values0,6,...,996. Exact child insertion/global clock and live scheduling remain outside scope.'],remaining=['Native lifecycle comparison and world/casting/resource/size/corpse/terrain/clock adapters required. Modern periodic curse owner/hard-Find-Potion synergy and Terror target context block learned Grim Ward. Final effects/audio/icons remain separately tracked placeholders.'])
 out=ROOT/'tools/reference/grim-ward-lifecycle-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Grim Ward lifecycle source:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
