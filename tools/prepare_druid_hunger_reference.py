#!/usr/bin/env python3
"""Pin Hunger's current rows and older complete routines before native work."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    forms_path = ROOT / 'tools/reference/druid-forms-preflight.json'
    forms = json.loads(forms_path.read_text())
    public_root = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public_root / 'sources.json').read_text())
    assert manifest['mechanicsReferenceVersion'] == forms['referenceVersion'] == '3.3.93847'
    pin = forms['localTables']['skills']
    local = ROOT / '.local/research/d2r-3.3.93847' / pin['filename']
    assert sha(local) == pin['sha256']
    row = next(x for x in csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t') if x['skill'] == 'Hunger')
    public = public_root / 'json/skills.json'
    assert sha(public) == manifest['files']['json/skills.json']['sha256']
    other = next(x for x in json.loads(public.read_text()).values() if x['skill'] == 'Hunger')
    compared = 0
    for key, value in other.items():
        if key not in row or key == 'lineNumber':
            continue
        text = str(value)
        if text.startswith('"') and text.endswith('"'):
            text = text[1:-1]
        assert row[key] == text, (key, row[key], text)
        compared += 1
    row = {key:value for key,value in row.items() if key and value != ''}
    assert [row[key] for key in ['*Id', 'srvdofunc', 'anim', 'seqtrans', 'UseAttackRate', 'SrcDam', 'mana', 'ToHit', 'LevToHit', 'restrict', 'State1', 'State2']] == ['242', '122', 'S3', 'S3', '1', '128', '3', '50', '10', '2', 'wolf', 'bear']
    assert not row.get('srvstfunc') and not row.get('EType') and not row.get('srvoverlay')
    assert [row[key] for key in ['calc1', 'calc2', 'calc3', 'Param1', 'Param2', 'Param3', 'Param4', 'Param5']] == ['par5', 'dm12', 'dm34', '50', '200', '50', '200', '-75']
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == forms['legacyCommit']
    hashes, routines, bodies = {}, {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo122_Hunger']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD155E0', 'D2GAME_RollElementalDamage_6FD14DD0', 'D2GAME_SKILLS_Handler_6FD12BA0']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp', ['SUNITDMG_FillDamageValues', 'SUNITDMG_AllocCombat', 'SUNITDMG_DrainItemDurability', 'SUNITDMG_ExecuteEvents']),
        ('source/D2Game/src/MONSTER/MonsterUnique.cpp', ['MONSTERUNIQUE_CalculatePercentage']),
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_GetToHitFactor', 'SKILLS_GetManaCosts']),
    ]:
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        for name in names:
            body = function(raw.decode(), name)
            bodies[name] = body
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
    body = bodies['SKILLS_SrvDo122_Hunger']
    anchors = ['sub_6FD155E0', 'damage.dwHitFlags |= 2', 'SUNITDMG_FillDamageValues', 'D2GAME_RollElementalDamage_6FD14DD0', 'damage.dwPhysDamage += MONSTERUNIQUE_CalculatePercentage', 'damage.dwLifeLeech +=', 'damage.dwManaLeech +=', 'SUNITDMG_AllocCombat', 'SUNITDMG_DrainItemDurability']
    positions = [body.index(anchor) for anchor in anchors]
    assert positions == sorted(positions)
    animations = {x['code']:x for x in forms['animation']['records'] if x['code'] in ['40S3HTH', 'TGS3HTH']}
    assert len(animations) == 2 and all(x['frames'] == 10 and x['events'] == [[6, 1]] for x in animations.values())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], publicCommit=manifest['commit'],
        sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        formsPreflightSha256=sha(forms_path), localTable=dict(filename=pin['filename'], sha256=sha(local)),
        publicTableSha256=sha(public), publicFieldsCompared=compared, currentRow=row,
        animation=animations, sourceHashes=hashes, completeRoutineHashes=routines, orderedAnchors=anchors,
        established=[
            'Hunger242uses do122 with no start routine, S3/attack-rate, three mana, SrcDam128 and both wolf/bear. It has no elemental damage of its own or impact overlay. It is not a shared A1 action.',
            'On successful contact the complete older do122 fills weapon/critical values once, calls the elemental roller, then adds the -75percent physical adjustment to the filled physical packet. This is a post-fill multiplier, not additive off-weapon enhanced damage. Life/mana drain formulas are then added before allocation/mitigation and durability/combat drain. Misses do not call that success-only drain path.',
            'The complete shared percentage helper truncates negative division toward zero and changes division order above physical256=1048576. Reuse SourcePercentage.Calculate rather than multiplying the ordinary damage scalar or using the nonnegative Damage helper.',
            'Current S3metadata has10frames and a melee event at frame6 for both forms; raw animation speeds are240wolf/208bear. The existing A1rate normalization uses the modern author model, not raw animation speed. S3needs its own corroborated rate/scheduler composition before native acceptance.',
        ], remaining=[
            'Execute bounded do122/percentage/result/allocator composition and validate diminishing life/mana formulas, real gear, physical-only reduction, elemental retention, critical ordering, existing Feral/Maul channels, miss/block, immunity/drain/difficulty, timing, cancellation, actual input and saved state.',
            'Modern exact event/seed/geometry/reentry, low-mana fallback and target-loss timing remain uncertain. Old do122selects its target and rolls combat at the action event, unlike start58FireClaws. Do not silently carry start58preparation into Hunger. Item-only/foreign classes/PvP/unmapped recipients remain separate.',
            'Source preparation enables no handler and changes no installed build/saves. Hunger bite/body/contact/leech/audio and separate talent/left/right/quick icons remain pending; existing generic effects are identified placeholders.',
        ])
    path = ROOT / 'tools/reference/druid-hunger-preflight.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Hunger preflight: {compared} current fields, {len(routines)} complete older routines, S3frame6/post-fill physical reduction; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
