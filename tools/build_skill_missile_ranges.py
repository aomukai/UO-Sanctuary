#!/usr/bin/env python3
"""Pinned full missile-ID/range view for shared NPC skill targeting."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
 source='tools/reference/d2r-named-equipment/json/missiles.json';raw=(ROOT/source).read_bytes();digest=hashlib.sha256(raw).hexdigest()
 catalog=json.loads((ROOT/'data/Sanctuary/Balance/item-skill-execution.json').read_text());assert catalog['ReferenceVersion']=='3.3.93847' and catalog['SourceHashes'][source]==digest
 rows=sorted((dict(id=r['*ID'],name=r['Missile'],range=r.get('Range',0) if isinstance(r.get('Range',0),int) else None,expression=r.get('Range') if isinstance(r.get('Range'),str) else None)for r in json.loads(raw).values()),key=lambda r:r['id'])
 assert len(rows)==len(catalog['MissileIds'])==742 and all(r['id']==i and catalog['MissileIds'][r['name']]==i and ((isinstance(r['range'],int) and 0<=r['range']<=65535 and r['expression'] is None) or (r['range'] is None and isinstance(r['expression'],str) and r['expression'])) for i,r in enumerate(rows))
 result=dict(referenceVersion='3.3.93847',sourceSha256=digest,rows=rows);text=json.dumps(result,indent=2)+'\n';target=ROOT/'data/Sanctuary/Balance/skill-missile-ranges.json'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('742pinned missile names/IDs match execution;694literal ranges and48explicit unresolved range expressions.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
