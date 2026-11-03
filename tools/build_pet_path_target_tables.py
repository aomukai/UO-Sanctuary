#!/usr/bin/env python3
"""Publish the pinned primary monster flags used by current path reset."""
import argparse,csv,json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/pet-path-targets-reference-20261103'
FLAGS=['npc','interact','inTown','lUndead','hUndead','demon','flying','killable']

def build(check=False):
    parent=ROOT/'tools/reference/generic-pet-search-tables-data-evidence.json'
    previous=json.loads(parent.read_text());assert previous['referenceVersion']=='3.3.93847'
    for ext in ['bin','txt']:
        assert digest((STAGE/('monstats.'+ext)).read_bytes())==previous['artifacts']['.local/class-skills/generic-pet-search-services/monstats.'+ext]
    b=(STAGE/'monstats.bin').read_bytes();assert struct.unpack_from('<I',b)[0]==751 and len(b)==4+751*508
    rows={int(r['*hcIdx']):r for r in csv.DictReader((STAGE/'monstats.txt').read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('*hcIdx','').isdigit()}
    assert set(rows)==set(range(751))
    output=[]
    for i in range(751):
        r=rows[i];value=sum(int(r[c] or 0)<<j for j,c in enumerate(FLAGS))
        assert value==b[4+i*508+0x3d]
        output.append(dict(id=i,name=r['Id'],flags61=value))
    path=ROOT/'data/Sanctuary/Balance/generic-pet-path-targets.json';encoded=json.dumps(output,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    sources=[parent,STAGE/'monstats.bin',STAGE/'monstats.txt',Path(__file__).resolve(),path]
    receipt=dict(referenceVersion='3.3.93847',component='Primary monster byte61 for current path-target reset',
        verified=dict(rows=751,stride=508,compiledTextFlagMatches=6008,inTownRows=sum(bool(r['flags61']&4) for r in output),resetMask=2,resetFlag='interact'),
        fields=dict(offset=61,bits=FLAGS),processMemoryRead=False,handlerEnabled=False,
        artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sources},
        limits=['Primary monstats flags; not secondary monster metadata. Views bind one explicit selector; actual selector aliases and world target acquisition remain separate.'])
    target=ROOT/'tools/reference/pet-path-targets-data-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Path target table:751 primary rows/6008 compiled text flag checks;reset tests interact bit1.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
