#!/usr/bin/env python3
"""Export ordinary Revive attack1 critical/innate-element metadata from pinned rows."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
    directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((directory/'sources.json').read_text());source=directory/'json/monstats.json'
    assert manifest['mechanicsReferenceVersion']=='3.3.93847'and hashlib.sha256(source.read_bytes()).hexdigest()==manifest['files']['json/monstats.json']['sha256']
    rows=json.loads(source.read_text());assert len(rows)==751;result=[]
    for name,row in rows.items():
        critical=row.get('Crit',0);assert isinstance(critical,int)and 0<=critical<=255,(name,critical)
        elements=[dict(mode=row.get(f'El{i}Mode',''),type=row.get(f'El{i}Type',''))for i in range(1,4)]
        assert all(isinstance(e['mode'],str)and isinstance(e['type'],str)for e in elements),name
        result.append(dict(source=name,critical=critical,elements=elements))
    path=ROOT/'data/Sanctuary/Balance/revive-offense-sources.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Pinned Revive offense metadata:751 source rows;critical and three innate-element slots.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
