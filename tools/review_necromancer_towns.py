#!/usr/bin/env python3
"""Pin Necromancer InTown data and statically reviewed ordinary cast gates."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
    folder=ROOT/'tools/reference/d2r-named-equipment';source=folder/'json/skills.json';manifest=json.loads((folder/'sources.json').read_text())['files']
    assert sha(source)==manifest['json/skills.json']['sha256']
    raw=json.loads(source.read_text());catalog=ROOT/'data/Sanctuary/Balance/class-skills.json';rules=[]
    for row in json.loads(catalog.read_text()):
        if row['characterClass']!='Necromancer':continue
        original=raw[str(row['sourceId'])];permission=original.get('InTown',0)
        assert permission in (0,1)and row['sourceFields'].get('InTown',0)==permission
        rules.append(dict(talent=row['talentId'],ability=row['ability'],sourceId=row['sourceId'],inTown=permission,passive=row['passive']))
    allowed={r['talent']for r in rules if r['inTown']}
    assert len(rules)==30 and allowed=={'necromancer.bonearmor','necromancer.claygolem','necromancer.bloodgolem','necromancer.irongolem','necromancer.firegolem'}
    assert sum(not r['passive']and not r['inTown']for r in rules)==22
    legacy=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp';text=legacy.read_text()
    assert text.count('SKILLSFLAGINDEX_INTOWN')>=3 and 'DUNGEON_IsRoomInTown' in text and 'UNITS_SetUsedSkill(pUnit, nullptr)' in text
    files=[Path(__file__).resolve(),source,folder/'sources.json',catalog,legacy]
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',rules=rules,inputs={str(p.relative_to(ROOT)):sha(p)for p in files},
        legacyCommit='0c0bbd1e6bef9181a3da1c046523af622a925416',staticReview=['sub_6FD12950 ordinary start rejects missing room and caster town when InTown is absent;it clears the used skill before further casting work.',
            'D2GAME_SKILLS_Handler_6FD12BA0 also has a conditional town gate;an existing used-skill flag1 bypasses that branch. Other forced/charged/triggered contexts are not established by this review.'],
        established='Five town-enabled Necromancer actives;22active town-disabled and3passives. Native town projection uses existing SpellHelper.IsTown,also used by approved summon parking.',
        limitations=['Table identity and static source review,not a newly executed full source or modern executable cast dispatcher. Learned ordinary-player start/release is the runtime scope;special used-skill flags,charges,procs,NPC execution and region equivalence remain unverified.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/necromancer-town-reference.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Necromancer towns:30pinned rows;5town-enabled actives,22forbidden actives,3passives;ordinary gate and exceptional-context boundary recorded.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
