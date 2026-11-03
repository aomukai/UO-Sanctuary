#!/usr/bin/env python3
"""Export pinned Ward body dispatch metadata without choosing a formula policy."""
import argparse,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build():
    source=json.loads((ROOT/'tools/reference/barbarian-grim-ward-preflight.json').read_text())
    assert source['referenceVersion']=='3.3.93847'
    rows=[]
    for row in source['missiles']:
        if row['Missile'] not in ('grimwardsmall','grimwardmedium','grimwardlarge'):continue
        assert row['pSrvDoFunc']==14 and row['pSrvHitFunc']==27 and row['Param1']==6 and row['Param2']==30
        rows.append(dict(Id=row['*ID'],Name=row['Missile'],Period=row['Param1'],Dispatch=row['Param2']))
    assert sorted(r['Id'] for r in rows)==[250,253,256]
    return sorted(rows,key=lambda r:r['Id'])
def main():
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');a=p.parse_args()
    path=ROOT/'data/Sanctuary/Balance/grim-ward-bodies.json';encoded=json.dumps(build(),indent=2)+'\n'
    if a.check:assert path.read_text()==encoded,'Stale pinned Ward body metadata'
    else:path.write_text(encoded)
    print('Three pinned Ward body rows retain period6/dispatch30;no clock or formula policy selected.')
if __name__=='__main__':main()
