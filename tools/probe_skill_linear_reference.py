#!/usr/bin/env python3
"""Execute the four unchanged legacy linear-formula switch arms, including rank zero."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Common/src/D2Skills.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    source = raw.decode(); start = source.index('int __stdcall SKILLS_GetSpecialParamValue(')
    end = source.index('//D2Common.', start)
    function = source[start:end]
    arms = {}
    for selector in [0, 2, 4, 6]:
        match = re.search(r'\t\tcase ' + str(selector) + r':\n.*?\n\t\t\tbreak;', function, re.S)
        assert match, selector
        arms[selector] = match[0]
        assert f'dwParam[{selector}] + (nSkillLevel - 1) * pSkillsTxtRecord->dwParam[{selector + 1}]' in match[0]
    cpp = '''#include <cstdio>
#include <initializer_list>
struct D2SkillsTxt{int dwParam[8];};
int linear(D2SkillsTxt*pSkillsTxtRecord,int nSkillLevel,int nParamId){switch(nParamId){
''' + '\n'.join(arms.values()) + '''
}return 0;}
int main(){bool first=true;printf("[");for(int selector:{0,2,4,6})for(int rank=-1;rank<=255;rank++)for(int base:{0,25,100})for(int increment:{0,10,25}){
D2SkillsTxt row;for(int&n:row.dwParam)n=12345;row.dwParam[selector]=base;row.dwParam[selector+1]=increment;
printf("%s[%d,%d,%d,%d,%d]",first?"":",",selector,rank,base,increment,linear(&row,rank,selector));first=false;
}printf("]\\n");}
'''
    path = BASE / 'skill-linear-source-probe.cpp'; path.write_text(cpp); binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'skill-linear-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    data = json.loads(run.stdout); assert len(data) == 9252
    for selector, rank, base, increment, actual in data:
        assert actual == (base + (rank - 1) * increment if rank > 0 else 0)
    fixture = BASE / 'skill-linear-source-probe.json'; fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes={relative: hashlib.sha256(raw).hexdigest()},
                    scope='Four complete unchanged switch arms of SKILLS_GetSpecialParamValue, not the full formula VM or callback function.',
                    armHashes={str(k): hashlib.sha256(v.encode()).hexdigest() for k, v in arms.items()},
                    generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 with empty runtime stderr.',
                    established=['Linear callback selectors0/2/4/6 return0 at nonpositive rank. Positive rank computes parameter baseline plus(rank-1)*increment. All ranks-1through255 and independent parameter-channel sentinels are captured.',
                                 'This is relevant to cross-skill .ln references: an absent Grizzly must not contribute its positive baseline-minus-increment as a damage synergy. The same rank-zero rule applies to golem mastery references.'],
                    limits=['Function-table/formula symbol compilation, nested skill lookup, current executable equivalence and modern extra parameter slots are outside this four-arm proof. No native or installed-build acceptance is inferred here.'])
    path = ROOT / 'tools/reference/skill-linear-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Linear skill formula:9252 unchanged switch-arm cases; nonpositive ranks return zero.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
