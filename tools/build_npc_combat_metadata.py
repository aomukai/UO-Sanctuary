#!/usr/bin/env python3
"""Project pinned class identities and item event registrations without inventing handlers."""
import argparse
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def build(check=False):
    source=ROOT/'tools/reference/d2r-named-equipment/json'
    stats=json.loads((source/'itemstatcost.json').read_text())
    monsters=json.loads((source/'monstats.json').read_text())
    models=json.loads((source/'monstats2.json').read_text())
    events=[]
    for name,row in stats.items():
        registrations=[dict(Event=row['itemevent'+str(n)],Function=row['itemeventfunc'+str(n)])
            for n in [1,2] if row.get('itemevent'+str(n))]
        if registrations:events.append(dict(Stat=row['*ID'],Name=name,Events=registrations))
    identities=[dict(Source=name,Class=row['*hcIdx'],Base=monsters[row['BaseId']]['*hcIdx'],
        HitClass=models[row['MonStatsEx']].get('HitClass',0),
        ModeFlags=sum(1<<n for n,field in [(3,'mGH'),(6,'mBL'),(13,'mKB')] if models[row['MonStatsEx']].get(field)),
        Velocity=row.get('Velocity',0),Killable=bool(row.get('killable')),Town=bool(row.get('inTown')))
        for name,row in monsters.items() if row.get('BaseId')]
    assert len(events)==21 and len(identities)==751
    assert next(r for r in events if r['Stat']==78)['Events']==[dict(Event='attackedinmelee',Function=6)]
    for name,rows in [('npc-item-combat-events',sorted(events,key=lambda r:r['Stat'])),('monster-return-identities',sorted(identities,key=lambda r:r['Class']))]:
        path=ROOT/'data/Sanctuary/Balance'/(name+'.json');text=json.dumps(rows,indent=2)+'\n'
        if check:assert path.read_text()==text
        else:path.write_text(text)
    print('Pinned NPC combat metadata:21item event stats and751class/base identities;no event implementation inferred.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
