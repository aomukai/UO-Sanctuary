#!/usr/bin/env python3
"""Pin current Sunder inputs and explicit consumer/acquisition boundaries."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def record(check=False):
    paths=['data/Sanctuary/Balance/class-skills.json','data/Sanctuary/Balance/equipment-stat-costs.json','data/Sanctuary/Balance/unique-items.json','src/Sanctuary.Content/NamedGear.cs','src/Sanctuary.Content/NamedElements.cs','src/Sanctuary.Content/NecromancerSkeletonBirth.cs','src/Sanctuary.Content/NecromancerMageBirth.cs','src/Sanctuary.Content/IronGolemBodyNumbers.cs','src/Sanctuary.Content/IronGolemContact.cs','src/Sanctuary.Content/ReviveCombat.cs','docs/necro-sunder-preflight.md',str(Path(__file__).resolve().relative_to(ROOT))]
    rows=json.loads((ROOT/paths[0]).read_text());passives={}
    for r in rows:
        if r['characterClass']!='Necromancer':continue
        fields=r['sourceFields'];chosen={k:v for k,v in fields.items()if k.startswith('passivestat')and str(v).startswith('item_pierce_')}
        if chosen:passives[r['talentId']]={k:dict(stat=v,formula=fields.get(k.replace('stat','calc')))for k,v in chosen.items()}
    items=json.loads((ROOT/paths[2]).read_text());charms={r['sourceKey']:[dict(code=p['code'],minimum=p['sourceMin'],maximum=p['sourceMax'])for p in r['properties']if p['code'].startswith('pierce-immunity-')]for r in items if any(p['code'].startswith('pierce-immunity-')for p in r['properties'])}
    assert len(charms)==18 and all(p['minimum']==p['maximum']==300 for a in charms.values()for p in a)
    costs=json.loads((ROOT/paths[1]).read_text());stats={r['Name']:r['Id']for r in costs if r['Name'].startswith('item_pierce_')and r['Name'].endswith('_immunity')}
    assert stats=={'item_pierce_cold_immunity':187,'item_pierce_fire_immunity':189,'item_pierce_light_immunity':190,'item_pierce_poison_immunity':191,'item_pierce_damage_immunity':192,'item_pierce_magic_immunity':193}
    result=dict(referenceVersion='3.3.93847',status='Nonzero consumer unsupported;unaffected work continues',processMemoryRead=False,processInput=False,currentReducerVerified=False,approvedSummonTimeSnapshot=True,charmsExcludedByExistingPolicy=True,stats=stats,passives=passives,charms=charms,inputs={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest()for p in paths},sources=[dict(url='https://news.blizzard.com/en-us/article/23827590/diablo-ii-resurrected-ladder-season-two-has-concluded',scope='Historical official95percent/before-other-modifiers/nonplayer/summon description;not an exact current numeric reducer.'),dict(url='https://diabloforfans.com/mechanics',scope='User-supplied mechanics page reviewed;no300numeric consumer established.')])
    out=ROOT/'tools/reference/necro-sunder-preflight.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print(f'{len(passives)} Necromancer passive maps;18 current Sunder definitions pinned;no consumer inferred.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
