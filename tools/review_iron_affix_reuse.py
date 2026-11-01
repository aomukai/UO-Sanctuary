#!/usr/bin/env python3
"""Pin existing Iron-affix reuse contracts without enabling incomplete affixes."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def record(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative='source/D2Game/src/MONSTER/MonsterUnique.cpp'
    source=BASE/'d2moo-pinned-source'/relative
    raw=source.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==relative)
    names=('MONSTERUNIQUE_UMod5_Strong','MONSTERUNIQUE_ApplyElementalDamage','sub_6FC6D440')
    bodies={n:function(raw.decode(),n) for n in names}
    paths=[Path(__file__),source,*[ROOT/'src/Sanctuary.Content'/(n+'.cs') for n in (
        'GolemPetDamage','IronGolemBody','IronGolemUpgrades','EncounterCreature','EncounterProfiles','EliteCombat','ClassStats',
        'NpcEquipmentDamage','NpcEquipmentElements','NpcEquipmentImpact','GearEffects')]]
    encounter=(ROOT/'src/Sanctuary.Content/EncounterCreature.cs').read_text()
    assert 'EncounterProfiles.ExtraStrong(_difficulty,_grade==EncounterGrade.Minion)' in encounter
    strength=(ROOT/'src/Sanctuary.Content/EncounterProfiles.cs').read_text()
    for value in ('Normal=>inherited?(67,45):(135,90)', 'Nightmare=>inherited?(56,37):(112,75)', 'Hell=>inherited?(49,33):(99,66)'):
        assert value in strength
    assert 'SummonDifficulty,false)' in (ROOT/'src/Sanctuary.Content/IronGolemUpgrades.cs').read_text()
    elite=(ROOT/'src/Sanctuary.Content/EliteCombat.cs').read_text()
    assert 'int amount = ElementRoll(source, false, random);' in elite and 'switch (random.Next(5))' in elite
    assert elite.index('Damage(source, target, physical, fire, cold, lightning, magic, true, ranged);') < elite.index('if (poison > 0) { GearEffects.ApplyPoison')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],installed=False,
        scope='Static complete-function/current-consumer review; no source function execution or native acceptance is supplied by this receipt.',
        ironBaselineImplemented=True,extraStrongEnabled=True,spectralHitEnabled=False,
        functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        inputs={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'Approved use-existing-affixes rule takes precedence. Current direct Extra Strong ED/AR is135/90,112/75,99/66by difficulty;inherited boss-minion branch is67/45,56/37,49/33. Iron now uses the direct branch at live hard10. Its Extra Strong AR joins the ordinary item percentage pool over the complete flat/Dexterity basis;its ED joins the shared NPC percent pool. Wild grade composition remains unchanged;native acceptance is recorded separately.',
            'Current wild Spectral Hit rolls inclusive66percent raw basis minimum to100percent maximum,then one of5channels in magic/fire/lightning/cold/poison order. It uses shared resistance bonuses and1600msminimum cold;both chill and poison apply after the direct hit only to surviving recipients. Merely adding resistances is incomplete.',
            'Ordinary NPC item poison adds its rate to the initial prepared HP total,and NPC intrinsic critical can double typed channels. Existing wild Spectral addition after the incoming physical roll/post-direct poison cannot be copied into that item path without defining critical/HP/timer ordering.',
            'Complete older Strong routine adds stats sourced from MonUMod rows and difficulty champion-damage factor. Complete older Spectral routine instead uses level-table damage,fire/lightning/magic/cold/poison order,and writes the selected stat range/adds40frames to an existing cold/poison length. These differ from the approved current native affix. Do not replace the existing affix globally with this older route.'],
        remaining=[
            'Direct Extra Strong extraction/activation has separate native acceptance. Next extract/reuse Spectral producers and verify all5typed outcomes,cold/poison/critical/order,live hard20,save and natural contact without changing the existing wild rule.',
            'Current MonUMod/difficulty constant tables are not established by this static review. Full older/current executable/room/event/RNG/seed/stat-list/client equivalence is not certified;the baseline curve receipt is separate.'])
    output=ROOT/'tools/reference/iron-affix-reuse-review.json'
    rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Iron affix reuse:direct Extra Strong enabled;Spectral typed/poison contracts pinned and still pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
