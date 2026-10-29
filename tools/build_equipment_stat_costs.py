#!/usr/bin/env python3
"""Project source-assembled, owned stat operation metadata for the NPC graph."""
import argparse
import hashlib
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
    receipt=json.loads((ROOT/'tools/reference/item-stat-values-source-evidence.json').read_text())
    assert receipt['verified']['statRows']==368 and receipt['verified']['ownedComparedFields']==4784
    for group in ['inputs','artifacts']:
        for name,digest in receipt[group].items():assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,name
    probe=json.loads((ROOT/'.local/class-skills/item-stat-values-source-probe.json').read_text())
    source=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json').read_text());names={r['lineNumber']:name for name,r in source.items()}
    rows=[]
    for r in probe['plans']:
        n,flags,keep,minimum,shift,isbase,hasops,applies,op,param,targets,bases,ops=r
        rows.append(dict(Id=n,Name=names[n],Flags=flags,KeepZero=bool(keep),Minimum=minimum,Shift=shift,BaseOfOther=bool(isbase),HasOperations=bool(hasops),AppliesToItem=bool(applies),Operation=op,Parameter=param,Targets=targets,Dependents=bases,Operations=[dict(Operation=x[0],Stat=x[1],Base=x[2],Parameter=x[3]) for x in ops]))
    assert len(rows)==368
    p=ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json';encoded=json.dumps(rows,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Equipment stat costs:368source-assembled owned rows.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
