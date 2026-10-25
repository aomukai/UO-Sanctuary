#!/usr/bin/env python3
"""Extract current boot kick endpoints separately from legacy equipment projections."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];SOURCE=ROOT/'tools/reference/d2r-named-equipment'
def build():
 manifest=json.loads((SOURCE/'sources.json').read_text());hashes={};tables={}
 for name in ['armor','itemtypes']:
  relative=f'json/{name}.json';p=SOURCE/relative;sha=hashlib.sha256(p.read_bytes()).hexdigest();assert sha==manifest['files'][relative]['sha256'],relative;hashes[relative]=sha;tables[name]=json.loads(p.read_bytes())
 parents={r['Code']:[r[k] for k in ['Equiv1','Equiv2'] if r.get(k)] for r in tables['itemtypes'].values() if r.get('Code')}
 def boot(code,seen=()):return code=='boot' or code not in seen and any(boot(p,(*seen,code)) for p in parents.get(code,[]))
 rows=[dict(code=r['code'],min=int(r.get('mindam') or 0),max=int(r.get('maxdam') or 0),strengthPercent=int(r.get('StrBonus') or 0),dexterityPercent=int(r.get('DexBonus') or 0)) for r in tables['armor'].values() if r.get('code') and boot(r.get('type',''))]
 rows.sort(key=lambda r:r['code']);assert len(rows)==15 and len({r['code'] for r in rows})==15;assert next(r for r in rows if r['code']=='lbt')['min']==3
 metadata=dict(schemaVersion=1,referenceVersion='3.3.93847',sourceManifest='tools/reference/d2r-named-equipment/sources.json',sourceHashes=hashes,bootRows=len(rows),scope='Kick base damage and strength/dexterity coefficients only. Legacy equipment projections remain separate, as with smite-shields; real active boot source identity selects current kick values. No kick action/timing or property eligibility certificate.')
 return {ROOT/'data/Sanctuary/Balance/kick-boots.json':rows,ROOT/'tools/reference/kick-data-source.json':metadata}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
 for path,value in build().items():
  encoded=json.dumps(value,indent=2)+'\n'
  if args.check:assert path.is_file() and path.read_text()==encoded,'Stale kick data: '+str(path)
  else:path.write_text(encoded)
 print('Pinned current kick damage:15 boot bases extracted separately from legacy equipment projections.')
