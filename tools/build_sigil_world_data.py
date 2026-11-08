#!/usr/bin/env python3
"""Pin the nine current Sigil missile rows for the shared world host."""
import argparse,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
 folder=ROOT/'tools/reference/d2r-named-equipment/json';raw=json.loads((folder/'missiles.json').read_text());skills=json.loads((folder/'skills.json').read_text());ids={r['*ID']for r in raw.values()};count=max(ids)+1;assert ids==set(range(count));rows=[]
 for skill,expected in [(393,[699,686,702]),(396,[700,687,703]),(400,[701,688,704])]:
  selected=[raw[skills[str(skill)]['srvmissile'+letter]]for letter in 'abc'];assert [r['*ID']for r in selected]==expected
  for row in selected:
   assert row['pSrvDoFunc']==14 and row['Param1']==6 and row['Param2']==(61 if skill==396 else 30)and row['Range']=='100+(lvl*25)';rows.append(dict(Key=row['Missile'],Fields=row))
 output=json.dumps([dict(MissileCount=count,Missiles=rows)],indent=2)+'\n';p=ROOT/'data/Sanctuary/Balance/sigil-world.json'
 if check:assert p.read_text()==output
 else:p.write_text(output)
 print(f'Pinned Sigil world data:9complete rows;current missile count {count};owned primary tables only.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
