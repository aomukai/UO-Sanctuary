#!/usr/bin/env python3
"""Reuse the unchanged complete transition composition for all admitted indices."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/curse-ai-catalogue-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent_path=ROOT/'tools/reference/curse-ai-transitions-source-evidence.json';parent=json.loads(parent_path.read_text())
    for p,h in parent['evidence'].items():assert sha(ROOT/p)==h,p
    catalogue_path=ROOT/'tools/reference/curse-ai-catalogue-source-evidence.json';catalogue=json.loads(catalogue_path.read_text())
    for p,h in catalogue['evidence'].items():assert sha(ROOT/p)==h,p
    indices=sorted(r['index']for r in catalogue['selected']);assert len(indices)==92
    original=ROOT/'.local/class-skills/curse-ai-transitions-source/probe.cpp';cpp=original.read_text()
    old='int indices[]={2,3,6,7,12,89};';assert cpp.count(old)==1
    cpp=cpp.replace(old,'int indices[]={'+','.join(map(str,indices))+'};')
    STAGE.mkdir(exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp)
    compiled=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(compiled.stdout+compiled.stderr);assert compiled.returncode==0,compiled.stderr.decode()[-3000:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    cases=json.loads(run.stdout);assert len(cases)==len(indices)*48==4416
    (STAGE/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',cases=len(cases),families=len(indices),routines=parent['routines'],
        sourceBodiesUnchanged=True,onlyCppChange='main index array expanded from six established indices to all92 catalogue-admitted indices',
        evidence={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__),parent_path,catalogue_path,original,source,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        limits=parent['limits']+['Source table lookup/dispatch and its Sand Maggot deferred-exit counterexample execute. Referenced AI bodies remain declared pointer services;current code execution and all native world behavior are separate.'])
    target=ROOT/'tools/reference/curse-ai-catalogue-dispatch-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Curse AI catalogue dispatch:4416 complete unchanged source lookup/transition contexts;92 indices;ASan/UBSan clean.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
