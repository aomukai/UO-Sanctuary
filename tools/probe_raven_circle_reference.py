#!/usr/bin/env python3
"""Execute complete circle-offset path5/6 in the existing source initializer."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes,blocks={},{}
    def source(relative):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def routine(text,name):
        match=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
        end,depth=text.index('{',match.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[match.start():end];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
    misc=source('source/D2Common/src/Path/PathMisc.cpp')
    path=source('source/D2Common/src/Path/Path.cpp')
    offsets=path[path.index('static const int32_t gaOffsetForPathType'):path.index('typedef int(__fastcall* PathFunctionType)')]
    assert re.search(r'/\*\[ 5\]\*/2,',offsets) and re.search(r'/\*\[ 6\]\*/-2,',offsets)
    cpp=(STAGE/'toward-path-source-probe.cpp').read_text()
    for name in ['sub_6FDAA720','sub_6FDAA880','PATH_Toward_6FDAA9F0','PATH_GetDirections_6FDAB790']:
        assert routine(cpp,name)==routine(misc,name)
    old='scpfnPathFunction[18]={nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,PATH_ComputePathOrSlideAlongObstacles,nullptr,nullptr}'
    new='scpfnPathFunction[18]={nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,nullptr,PATH_Toward_6FDAA9F0,PATH_Toward_6FDAA9F0,nullptr,nullptr,nullptr,nullptr,nullptr,nullptr,PATH_Toward_6FDAA9F0,nullptr,PATH_ComputePathOrSlideAlongObstacles,nullptr,nullptr}'
    assert cpp.count(old)==1 and cpp.count('for(int type:{2,13})')==1
    cpp=cpp.replace(old,new).replace('for(int type:{2,13})','for(int type:{5,6})')
    assert cpp.count('p.dwPathType=type;')==1
    cpp=cpp.replace('p.dwPathType=type;','p.dwPathType=type;p.nDirOffset=type==5?2:-2;')
    file=STAGE/'raven-circle-source-probe.cpp';file.write_text(cpp);binary=file.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(file),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'raven-circle-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    data=json.loads(run.stdout);assert len(data)==6480 and {x['input'][5] for x in data}=={5,6}
    for row in data:assert row['state'][0]==len(row['points'])
    fixture=STAGE/'raven-circle-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,installed=False,
        sourceHashes=hashes,compiledRoutineHashes=blocks,parentHarnessEvidence='tools/reference/toward-path-engine-evidence.json',
        parentCppSha256=hashlib.sha256((STAGE/'toward-path-source-probe.cpp').read_bytes()).hexdigest(),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),contexts=6480),
        generatedSourceSha256=hashlib.sha256(file.read_bytes()).hexdigest(),sanitizers='ASan/UBSan exit0 and empty runtime stderr',
        established=[
            'Complete direction-offset helper, direction-index/table choice, ordered collision probes and toward dispatcher execute inside the existing full general initializer. Path5uses+2 and path6uses-2, matching source type-offset table; both retain type flags0.',
            'Eight obstacle grids, nine horizontal/five vertical offsets, three fractional origins and budgets1/5/20 capture exact point arrays, failures, direction/velocity/flags, active/final/previous target state and collision query order.',
            'Circle segments intentionally retain repeated origin/end waypoints and the source turn/180-degree/blocked/budget termination rules. No trigonometric orbit or wall-follow simplification is substituted.'
        ],limits=[
            'The inherited general initializer is marked BROKEN in D2MOO. Monster point target, zero collision footprint, supplied room/grid and explicit path-type flags/offset are bounded caller services.',
            'Native motion/actor/AI unit-target repathing, room recache, mode/event ordering and current D2R executable/client behavior remain separate acceptance. No learned Raven or other handler is enabled.'])
    out=ROOT/'tools/reference/raven-circle-source-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Raven circle paths:6480complete source initializer/offset/direction/collision contexts pass; native controller acceptance is separate.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
