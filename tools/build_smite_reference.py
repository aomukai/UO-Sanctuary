#!/usr/bin/env python3
"""Extract Smite shield stats from the pinned modern tables, independent of equipment projections."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];SOURCE=ROOT/'tools/reference/d2r-named-equipment'
def build():
    manifest=json.loads((SOURCE/'sources.json').read_text());sources={}
    for name in ['armor','itemtypes']:
        relative=f'json/{name}.json';p=SOURCE/relative;sha=hashlib.sha256(p.read_bytes()).hexdigest()
        assert sha==manifest['files'][relative]['sha256'],relative
        sources[relative]=sha
    types=json.loads((SOURCE/'json/itemtypes.json').read_text());types=list(types.values()) if isinstance(types,dict) else types
    parents={r['Code']:[r[k] for k in ['Equiv1','Equiv2'] if r.get(k)] for r in types if r.get('Code')}
    def shield(code,seen=()):
        if code=='shld':return True
        if code in seen:return False
        return any(shield(p,(*seen,code)) for p in parents.get(code,[]))
    armor=json.loads((SOURCE/'json/armor.json').read_text());armor=list(armor.values()) if isinstance(armor,dict) else armor
    rows=[dict(code=r['code'],name=r['name'],type=r['type'],minDamage=int(r.get('mindam') or 0),maxDamage=int(r.get('maxdam') or 0),
        strengthBonus=int(r.get('StrBonus') or 0),dexterityBonus=int(r.get('DexBonus') or 0)) for r in armor if r.get('code') and shield(r.get('type',''))]
    rows.sort(key=lambda r:r['code']);assert len({r['code'] for r in rows})==len(rows)
    assert next(r for r in rows if r['code']=='buc')['minDamage']==1
    metadata={'schemaVersion':1,'referenceVersion':'3.3.93847','sourceManifest':'tools/reference/d2r-named-equipment/sources.json','sourceHashes':sources,'shieldRows':len(rows),
        'scope':'Smite damage/strength/dexterity only. Keeps existing normal/equipment-tier projected weapon and armor stats unchanged. Source shield eligibility still follows Sanctuary active gear/class rules.'}
    return {ROOT/'data/Sanctuary/Balance/smite-shields.json':rows,ROOT/'tools/reference/smite-data-source.json':metadata}
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    for p,value in build().items():
        s=json.dumps(value,indent=2)+'\n'
        if args.check:
            if not p.is_file() or p.read_text()!=s:raise SystemExit(f'Stale Smite data: {p}')
        else:p.write_text(s)
    print('Pinned shield Smite damage extracted independently of existing equipment projections.')
