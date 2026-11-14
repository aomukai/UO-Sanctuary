#!/usr/bin/env python3
"""Retain the owned NPC405 row separately from the player skill catalogue."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
    base=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((base/'sources.json').read_text());p=base/'json/skills.json'
    assert hashlib.sha256(p.read_bytes()).hexdigest()==manifest['files']['json/skills.json']['sha256']
    row=json.loads(p.read_text())['405'];assert row['skill']=='Health Link'and row['srvdofunc']==162 and row['auraeventfunc1']==38
    out=dict(talentId='warlock.summondefiler',name='Health Link',characterClass='Warlock',tree='Demon',sourceId=405,sourceKey='Health Link',ability=218,sourceTab=21,
        passive=False,left=False,right=True,sourceFields=row,sourceParameters={str(i):row.get('Param'+str(i),0)for i in range(1,21)})
    target=ROOT/'data/Sanctuary/Balance/defiler-health-link.json';encoded=json.dumps([out],indent=2)+'\n'
    if check:assert target.read_text()==encoded,'NPC405 row stale'
    else:target.write_text(encoded)
    print('Owned3.3.93847HealthLink405/start70/do162/event38 retained;no player handler or callback enabled.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
