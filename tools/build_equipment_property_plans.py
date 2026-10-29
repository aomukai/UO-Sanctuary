#!/usr/bin/env python3
"""Project owned numeric property plans while keeping unsupported writers explicit."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
    r=json.loads((ROOT/'tools/reference/item-property-writers-source-evidence.json').read_text())
    for group in ['inputs','artifacts']:
        for name,digest in r[group].items():assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,name
    source=json.loads((ROOT/'.local/class-skills/item-property-writers-source-probe.json').read_text());rows=[]
    for p in source['plans']:
        rows.append(dict(Code=p['Code'],Ordinal=p['Ordinal'],Supported=p['Supported'],Steps=[dict(Function=f,Stat=s,Set=bool(n),Value=v) for f,s,n,v in p['Steps']]))
    assert len(rows)==284 and sum(x['Supported'] for x in rows)==191
    path=ROOT/'data/Sanctuary/Balance/equipment-property-plans.json';text=json.dumps(rows,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Equipment property plans:191numeric/284total;unsupported writers remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
