#!/usr/bin/env python3
"""Pin complete current skill metadata and charged-item level-bit context."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];REF=ROOT/'tools/reference/d2r-named-equipment'
def build(check=False):
 pins=json.loads((REF/'sources.json').read_text());tables={}
 for name in ('skills','itemstatcost'):
  p=REF/'json'/(name+'.json');assert hashlib.sha256(p.read_bytes()).hexdigest()==pins['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(p.read_text())
 rows=[]
 for key,r in tables['skills'].items():
  assert int(key)==r['*Id'];rows.append(dict(Id=int(key),Name=r.get('skill',''),RequiredLevel=r.get('reqlevel',0),MaximumLevel=r.get('maxlvl',0)))
 zero=next(r for r in tables['itemstatcost'].values() if r['lineNumber']==0);bits=zero.get('stuff',0);bits=bits if 1<=bits<=8 else 6
 assert len(rows)==429 and bits==6 and [r['Id'] for r in rows]==list(range(429)) and [r['Id'] for r in rows if not r['Name']]==[362]
 text=json.dumps(dict(ReferenceVersion='3.3.93847',ChargedLevelBits=bits,Skills=rows),indent=2)+'\n';p=ROOT/'data/Sanctuary/Balance/item-skill-properties.json'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('Item skill properties:429current rows including blank362;charged level bits6from pinned stat0;no proc/charge actor dispatch implied.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
