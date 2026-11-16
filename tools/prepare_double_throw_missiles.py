#!/usr/bin/env python3
"""Pin ordinary thrown weapon/missile rows and complete older producer/hit functions."""
import argparse,hashlib,json
from pathlib import Path
from review_weapon_event_order import ROOT,STAGE,function

def render(check=False):
    source=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((source/'sources.json').read_text());tables={};hashes={}
    for name in ('weapons','missiles'):
        p=source/'json'/(name+'.json');raw=p.read_bytes();digest=hashlib.sha256(raw).hexdigest();assert digest==manifest['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(raw);hashes[str(p.relative_to(ROOT))]=digest
    missiles=tables['missiles'];names=('javelin','throwaxe','throwknife','glaive','pilum');ids={missiles[k]['*ID']:k for k in names}
    fields=('Vel','VelLev','Range','Activate','InitSteps','Size','ToHit','SrcDamage','pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','LastCollide','NextHit','NextDelay','Pierce','GetHit','ReturnFire','HitShift','ResultFlags','HitFlags','CanSlow')
    mappings={k:ids[v['missiletype']] for k,v in sorted(tables['weapons'].items()) if v.get('missiletype') in ids and v.get('type') in ('jave','ajav','taxe','tkni')}
    assert len(mappings)==30,len(mappings)
    data=[dict(ReferenceVersion='3.3.93847',SourceHashes=hashes,Weapons=mappings,Missiles={k:{f:missiles[k].get(f,0)for f in fields}for k in names})]
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';records={}
    paths={'source/D2Game/src/SKILLS/SkillBar.cpp':['SKILLS_SrvDo074_DoubleThrow'], 'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD11420'], 'source/D2Game/src/MISSILES/Missiles.cpp':['MISSILES_CreateMissileFromParams'],'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_SetDamageStats'], 'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvDo01_BasicMissile','MISSMODE_SrvDmgHitHandler','MISSMODE_FillDamageParams','MISSMODE_SetDamageFlags'], 'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_IsHitSuccessful']}
    for path,names in paths.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==path)
        for name in names:records[name]=dict(path=path,sha256=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest())
    review=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,functions=records,scope='Complete older functions and pinned archived rows;not modern executable parity.',established=['Do74 creates a quantity-checked ordinary thrown missile using the selected weapon row,then adds skill attack and damage percentages.','Ordinary knives/axes use20updates and javelin/glaive/pilum40;all use velocity24,size1,zero next delay,ordinary accuracy and source128 damage. InitSteps is not read by this server factory.','Source damage endpoints,elements,critical,drain and armor delta are retained by the missile. Accuracy reads the owner at contact with missile percentage and excludes weapon mastery AR.','Pierce budget is rolled at birth and used with the live owner pierce gate;accuracy misses terminate. Armor mutation follows the completed hit handler,including shield block.'],remaining=['Native source-grid/terrain/scheduler/owner mapping retains shared weapon-flight limits. Exact current D2R paired timing,payment,RNG and callback equivalence remain unverified.','Player/NPC item missile procs,exceptional recipients and other ranged paths remain separate;no new named item or class certification is implied.'])
    for relative,value in [('data/Sanctuary/Balance/double-throw-missiles.json',data),('tools/reference/double-throw-missile-source-review.json',review)]:
        p=ROOT/relative;text=json.dumps(value,indent=2)+'\n'
        if check:assert p.read_text()==text,relative
        else:p.write_text(text)
    print('Pinned Double Throw weapon/missile rows and complete older emission/collision bodies.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');render(p.parse_args().check)
