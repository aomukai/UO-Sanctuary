#!/usr/bin/env python3
"""Check authored published-rule metadata and independently pinned passive curves."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'tools/reference/sorceress-passive-rules.json';r=json.loads(p.read_text())
assert r['referenceVersion']=='3.3.93847' and 'not a compiled modern executable' in r['referenceKind']
expected={37:(30,12,'manarecoverybonus'),61:(30,7,'passive_fire_mastery'),63:(50,12,'passive_ltng_mastery'),65:(20,5,'passive_cold_pierce')}
rows=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text())
assert {x['sourceId'] for x in r['passives']}==expected.keys()
for x in r['passives']:
 sid=x['sourceId'];base,increment,stat=expected[sid];row=rows[str(sid)]
 assert (x['base'],x['increment'],x['stat'])==(base,increment,stat)
 assert (row['Param1'],row['Param2'],row['passivestat1'],row['passivecalc1'])==(base,increment,stat,'ln12')
 assert x['rankValues']==[base+increment*(rank-1) for rank in range(1,256)]
for name,digest in r['pinnedInputs'].items():assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,name
assert r['references']==['https://news.blizzard.com/en-us/article/23899624/diablo-ii-resurrected-ladder-season-three-has-concluded','https://diabloforfans.com/mechanics']
print('Four passives:1020 independent pinned rank values;published modern rule and T09contract identified;not executable evidence.')
