#!/usr/bin/env python3
"""Bind Ward size selection to pinned MonStats/MonStats2, not UO body art."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
 paths=[ROOT/'tools/reference/d2r-named-equipment/json'/name for name in ['monstats.json','monstats2.json']]
 monsters,modes=[json.loads(p.read_text())for p in paths]
 rows=[]
 for name,m in monsters.items():
  source=modes.get(m.get('MonStatsEx',''))
  if source is not None:
   rows.append(dict(Source=name,Large=bool(source.get('large',0)),Small=bool(source.get('small',0))))
 assert len(rows)>700 and len({r['Source']for r in rows})==len(rows)
 rows.sort(key=lambda r:r['Source'])
 out=ROOT/'data/Sanctuary/Balance/grim-ward-species.json';text=json.dumps(rows,indent=2)+'\n'
 if check:assert out.read_text()==text,'Stale Ward species sizes'
 else:out.write_text(text)
 print(f'{len(rows)} pinned Ward species sizes;large takes precedence over small;UO art does not select size.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
