#!/usr/bin/env python3
"""Pin current type-level quiver and inventory-variation initialization facts."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
 folder=ROOT/'tools/reference/d2r-named-equipment';meta=json.loads((folder/'sources.json').read_text());path=folder/'json/itemtypes.json';raw=path.read_bytes()
 assert hashlib.sha256(raw).hexdigest()==meta['files']['json/itemtypes.json']['sha256']
 tables=json.loads(raw);types={r['Code']:r for r in tables.values()if r.get('Code')};assert len(types)==105
 assert types['gold']['lineNumber']==4 and types['elix']['lineNumber']==11
 result=dict(referenceVersion='3.3.93847',types=[dict(code=code,ordinal=r['lineNumber'],quiver=bool(r.get('Quiver')),varieties=r.get('VarInvGfx',0))for code,r in sorted(types.items())])
 assert all(0<=r['varieties']<=255 for r in result['types'])
 encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'data/Sanctuary/Balance/item-initialization-types.json'
 if check:assert target.read_text()==encoded,'Item initialization type facts stale'
 else:target.write_text(encoded)
 print('Item initialization:105 pinned current primary-type ordinals/quiver/variation facts;no full factory or quality admission.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
