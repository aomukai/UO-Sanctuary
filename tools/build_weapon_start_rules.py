#!/usr/bin/env python3
"""Project the pinned current monster Prevent Monster Heal immunity flag."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
    source=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json'
    rows=json.loads(source.read_text());assert len(rows)==751
    immune=sorted(k for k,r in rows.items() if r.get('nopreventmonsterheal',0))
    assert len(immune)==9
    path=ROOT/'data/Sanctuary/Balance/prevent-heal-immune-monsters.json';text=json.dumps(immune,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    stage=ROOT/'.local/class-skills/weapon-start-effects-source';stage.mkdir(parents=True,exist_ok=True)
    cases=[dict(Source=k,Immune=k in immune) for k in sorted(rows)]
    (stage/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
    print('Prevent Monster Heal:751 pinned current rows,9 immunity flags (including3 Colossal rows).')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
