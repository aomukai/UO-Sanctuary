#!/usr/bin/env python3
"""Bind Tail's composed action to existing full-body controls and pinned states."""
import argparse,hashlib,json,re
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};bodies={}
    for rel,names in {
        'source/D2Game/src/SKILLS/SkillAss.cpp':['SKILLS_SrvSt27_DragonTail','SKILLS_SrvDo050_DragonTail','sub_6FCF77E0','sub_6FCF5BC0','sub_6FCF5870'],
        'source/D2Common/src/DataTbls/DataTbls.cpp':['DATATBLS_LoadStatesTxt']}.items():
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p)
        for name in names:bodies[name]=function(raw.decode(),name)
    compiled=json.loads((ROOT/'tools/reference/dragon-tail-source-evidence.json').read_text())
    for name in ('SKILLS_SrvSt27_DragonTail','SKILLS_SrvDo050_DragonTail'):
        assert hashlib.sha256(bodies[name].encode()).hexdigest()==compiled['compiledRoutines'][name]
    body=bodies['SKILLS_SrvDo050_DragonTail'];tokens=['memcpy(&physDamage','sub_6FCF77E0','SUNITDMG_DrainItemDurability','SUNIT_IsDead','MONSTERUNIQUE_CalculatePercentage','sub_6FD10200']
    assert [body.index(t)for t in tokens]==sorted(body.index(t)for t in tokens)
    assert 'pDamage->dwFrzLen += pDamage->dwColdLen / pSkillsTxtRecord->dwParam[4]'in bodies['sub_6FCF5BC0']
    assert 'for (int i = 0; i < sgptDataTables->nStatesTxtRecordCount; ++i)'in bodies['DATATBLS_LoadStatesTxt']
    assert 'pProgressiveStates[sgptDataTables->nProgressiveStates] = i'in bodies['DATATBLS_LoadStatesTxt']
    pin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pin/'sources.json').read_text());tables={}
    for name in ('states','skills'):
        p=pin/'json'/(name+'.json');assert sha(p)==manifest['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(p.read_text());inputs[str(p.relative_to(ROOT))]=sha(p)
    skills={254:'TigerStrike',265:'CobraStrike',280:'PhoenixStrike',259:'FistsOfFire',274:'BladesOfIce',269:'ClawsOfThunder'}
    order=[]
    for key,ability in skills.items():
        skill=tables['skills'][str(key)];state=tables['states'][skill['aurastate']];assert state['pgsv']==1
        order.append(dict(ability=ability,sourceId=key,state=state['state'],stateId=state['*ID']))
    order.sort(key=lambda x:x['stateId']);assert [x['stateId']for x in order]==list(range(122,128))
    native=(ROOT/'src/Sanctuary.Content/DragonTails.cs').read_text();actual=re.search(r'ProgressiveOrder=\[(.*?)\]',native).group(1)
    assert re.findall(r'CombatAbility\.(\w+)',actual)==[x['ability']for x in order]
    assert tables['skills']['274']['Param5']==1
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,
        inputs=inputs,reviewedUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},progressiveOrder=order,
        reusedCompiledControls=['tools/reference/dragon-tail-source-evidence.json','tools/reference/dragon-tail-rate-source-evidence.json','tools/reference/dragon-tail-clock-source-evidence.json','tools/reference/dragon-tail-lifetime-source-evidence.json','tools/reference/kick-fill-source-evidence.json','tools/reference/martial-cold-source-evidence.json','tools/reference/player-melee-geometry-source-evidence.json','tools/reference/dragon-tail-burst-source-evidence.json'],
        established=['Pinned states/load loop order is Tiger,Cobra,Phoenix,Fire,Ice,Thunder. Current native code uses that order; modern consumption is the explicit Sanctuary policy,not the older full-list removal.',
                     'Complete previously compiled do50 copies allocated mitigated physical before progressive effects,then contacts/durability,then checks owner death before deriving fire from the unchanged copy. Progressive checks melee bonus0 and contact independently checks bonus1.',
                     'Full inline Ice prgdam4 adds freeze from the entire accumulated cold length,including item cold,at count3 under pinned Param5=1. The existing compiled3456-context consumer controls are reused; native ordered contact now commits the captured freeze duration.',
                     'Do50flags9 include source KNOCKBACK8. The copied-area router adds GETHIT4 for admitted interruptible NPCs; flags13 use ordered spell HP/return and the existing saved knockback controller. Zero fire still requests displacement; no melee/Thorns/stun event is added.'],
        adaptation='tools/reference/dragon-tail-sanctuary-decision.json',
        limits=['This receipt adds table/routine/order binding,not a new full executable oracle. Current executable event/stat/RNG/owner/room/PvP equivalence remains unverified.',
                'Native geometry maps two source cells per tile,uses current source footprints/canonical base IDs for mapped monsters,and a provisional tile/LOS bridge for unclassified UO actors. No D2 identity or footprint is invented for them.',
                'Unsupported incoming modes,item effects and old signed-overflow contexts remain refusals. Ground/actual-client,external pre-event death/deletion,unheld concurrent saves and broader world scripts require further acceptance.'])
    path=ROOT/'tools/reference/dragon-tail-action-source-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:assert path.read_text()==text,'Tail action source binding stale'
    else:path.write_text(text)
    print('Dragon Tail action: pinned six-state order,complete do50 reuse and item-plus-Ice freeze composition bound; current executable parity unclaimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
