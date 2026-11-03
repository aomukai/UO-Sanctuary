#!/usr/bin/env python3
"""Check Tainted's exact malformed formula and NPC fire packet against pinned source."""
import argparse
import csv
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-foundation-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    inputs = {}
    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return path.read_text()
    directory = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads(pin(directory / 'sources.json'))
    tables = {}
    for name in ('skills', 'missiles', 'monstats', 'monlvl'):
        p = directory / 'json' / (name + '.json')
        assert sha(p) == manifest['files']['json/' + name + '.json']['sha256']
        tables[name] = json.loads(pin(p))
    talent = tables['skills']['376']; spell = tables['skills']['422']
    assert talent['summon'] == 'warbighead' and talent['srvdofunc'] == 56
    assert spell['skill'] == 'Tainted Fire Ball' and spell['srvmissile'] == 'taintedfireball'
    assert spell['EDmgSymPerCalc'] == "skill('Blood Boil'.blvl)*par8" and spell['Param8'] == 20
    assert talent['sumsk3calc'] == "skill('Blood Boil'.blvl)" and talent.get('Param9', 0) == talent.get('Param10', 0) == 0
    assert tables['missiles']['taintedfireball']['Skill'] == spell['skill']
    owned = ROOT / '.local/research/d2r-3.3.93847/skills.txt'
    foundation = json.loads(pin(ROOT / 'tools/reference/warlock-foundation-preflight.json'))
    assert sha(owned) == foundation['licensedSkillSha256']; pin(owned)
    with owned.open(encoding='utf-8-sig', newline='') as f:
        rows = {r['skill']: r for r in csv.DictReader(f, delimiter='\t')}
    for key in ('Summon Tainted', 'Tainted Resist Fire', 'Tainted Fire Ball'):
        cached = next(r for r in tables['skills'].values() if r.get('skill') == key)
        for field, value in cached.items():
            if field.startswith('*') or field == 'lineNumber':
                continue
            text = str(value)
            if text.startswith('"') and text.endswith('"'):
                text = text[1:-1]
            assert rows[key][field].strip() == text.strip(), (key, field)
    monster_proof = json.loads(pin(ROOT / 'tools/reference/generic-pet-parameter-emulation.json'))
    monsters = ROOT / '.local/class-skills/warlock-runtime-reference/monstats.txt'
    assert sha(monsters) == monster_proof['artifacts'][str(monsters.relative_to(ROOT))]; pin(monsters)
    with monsters.open(encoding='utf-8-sig', newline='') as f:
        current_monster = next(r for r in csv.DictReader(f, delimiter='\t') if r['Id'] == 'warbighead')
    compared_monster_fields = 0
    for field, value in tables['monstats']['warbighead'].items():
        # The public JSON adds this derived marker, absent as a literal column
        # in owned MonStats.txt. It is not a Tainted stat supplier.
        if field.startswith('*') or field in ('lineNumber', 'expansion'):
            continue
        assert current_monster[field].strip() == str(value).strip(), field
        compared_monster_fields += 1
    # Reuse the accepted complete Fog compiler/evaluator, changing only the
    # caller's declared current parameter context and the expression under test.
    goat = json.loads(pin(ROOT / 'tools/reference/goatman-source-evidence.json'))
    for name in ('formula.cpp', 'Calc.cpp', 'Calc.h', 'D2BasicTypes.h', 'Fog.h'):
        path = STAGE / ('seed-' + name if name == 'formula.cpp' else name)
        assert sha(path) == goat['artifacts']['.local/class-skills/goatman-foundation/' + name]
        pin(path)
    curve = ROOT / '.local/class-skills/warlock-foundation/skillcalc.txt'
    with curve.open(newline='') as f:
        curves = list(csv.DictReader(f, delimiter='\t'))
    pin(curve)
    ln91 = next(i for i, r in enumerate(curves) if r['code'] == 'ln91')
    cpp = (STAGE / 'seed-formula.cpp').read_text()
    cpp = cpp.replace('"ln56"))return 4', '"ln91"))return ' + str(ln91))
    cpp = cpp.replace('assert(index==4);return 10*rank;', 'assert(index==' + str(ln91) + ');return 0;')
    cpp = cpp.replace('(ln56 + (skill', '(ln91 + (skill')
    assert talent['passivecalc2'] in cpp
    def run(name, text, fog=False):
        path = STAGE / (name + '.cpp'); path.write_text(text)
        compiler = STAGE / (name + '.compiler.log'); sanitizer = STAGE / (name + '.sanitizer.log')
        args = ['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all']
        if fog:
            args += ['-fno-sanitize=alignment', '-I' + str(STAGE)]
        args += [str(path)]
        if fog:
            args += [str(STAGE / 'Calc.cpp')]
        with compiler.open('w') as f:
            subprocess.run(args + ['-o', str(STAGE / name)], stdout=f, stderr=subprocess.STDOUT, check=True)
        result = subprocess.run([str(STAGE / name)], capture_output=True)
        sanitizer.write_bytes(result.stderr)
        assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-2500:]
        output = STAGE / (name + '.json'); output.write_bytes(result.stdout)
        return json.loads(result.stdout), [path, compiler, sanitizer, output]
    formula, artifacts = run('formula', cpp, True)
    assert len(formula) == 3570
    for variant, rank, mastery, size, result, calls in formula:
        assert size > 0 and result == 10 * mastery and calls == 1
    prior = json.loads(pin(ROOT / 'tools/reference/miasma-packets-source-evidence.json'))
    seed = STAGE / 'seed-miasma-damage.cpp'
    assert sha(seed) == prior['artifacts']['.local/class-skills/miasma-packets/miasma-damage.cpp']; pin(seed)
    cpp = seed.read_text().split('void setup(', 1)[0]
    routines = prior['routines']
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    cpp = cpp.replace('return formula==2?0:(hard+volcano)*10;', 'return formula==2?0:hard*20;')
    cpp += 'void setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for destination, field in [('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        cpp += f'skill.{destination}={spell[field]};'
    for i in range(5):
        for destination, field in [('dwEMinLev', 'EMinLev'), ('dwEMaxLev', 'EMaxLev')]:
            cpp += f'skill.{destination}[{i}]={spell[field + str(i + 1)]};'
    cpp += 'row.nSkill=422;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.dwElemDmgSymPerCalc=-1;}\n'
    cpp += r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int boil:{0,1,20})for(int mastery:{0,1,17,255})for(int noise:{0,9999}){
 owner={};weapon={};missile={};missileData={};setup();owner.dwUnitType=UNIT_MONSTER;weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=734;missileData.nSkill=422;missileData.nLevel=rank;hard=boil;critical=true;critCalls=0;
 for(int& stat:owner.stats)stat=noise;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=10*mastery;
 D2MissileDamageDataStrc p;MISSILE_CalculateDamageData(&p,&owner,nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&p,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,boil,mastery,noise,p.nFireMinDamage,p.nFireMaxDamage,p.nMinDamage,p.nMaxDamage,p.nLightMaxDamage,p.nMagicMaxDamage,p.nFlags,critCalls);first=false;
}printf("]\n");}'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    packets, extra = run('fireball', cpp); artifacts += extra
    assert len(packets) == 6120
    def tier(maximum, rank):
        value = spell['EMax' if maximum else 'EMin']; remaining = rank - 1
        for i, length in enumerate((7, 8, 6, 6, 1000), 1):
            step = min(length, remaining); remaining -= step
            value += step * spell[('EMaxLev' if maximum else 'EMinLev') + str(i)]
        return value << spell['HitShift']
    for rank, boil, mastery, noise, minimum, maximum, *excluded in packets:
        expected = []
        for maximum_end in (False, True):
            raw = tier(maximum_end, rank); raw += raw * (boil * 20) // 100
            raw += raw * (mastery * 10) // 100; expected.append(raw)
        assert [minimum, maximum] == expected and excluded == [0] * 6
    # The original shared base and do56 controls are reused, not re-labelled as
    # modern passive writer, AI, NPC aura, placement or projectile execution.
    base = ROOT / '.local/class-skills/goatman-creation-source-probe.json'
    assert sha(base) == goat['artifacts'][str(base.relative_to(ROOT))]; pin(base)
    result = dict(referenceVersion='3.3.93847', component='Summon Tainted captured suppliers and NPC Fire Ball packet',
        handlerEnabled=False, currentExecutableParity=False, formulaCases=len(formula), packetCases=len(packets),
        ownedMonsterFieldComparisons=compared_monster_fields,
        fullUnchangedDamageRoutines=routines, inputs=inputs,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'The exact unclosed passivecalc2 and a balanced control both evaluate to ten times effective Demonic Mastery. Summon Tainted Param9/10 are absent/zero; do not invent a summon-rank damage increase.',
            'Complete unchanged legacy elemental getters, missile creator and stat writer execute with NPC skill422/missile734, the current own five-tier Fire Ball curve, own Blood Boil hard-rank synergy, and explicit caster fire mastery. No weapon, physical, lightning, magic, item or critical component is inherited.',
            'Formula callbacks, stat retrieval, zero monster-type layers and ratio services are declared. Packed Fog alignment is excluded as in its preserved predecessor; other ASan/UBSan checks remain enabled.'],
        remaining=[
            'Current GenericPet release/AI and extended passive/event transport are not proved by these suppliers. NPC Resist Fire do65, player Blood Boil do160 and actual cast, aura, travel/collision, payment and cap transactions need integration.',
            'Inherited physical/fire sunder fields remain separate. No automatic immunity aggregation, source stat-list ownership or modern mastery snapshot timing is inferred. Body uses the approved Succubus appearance only when a real actor is implemented.'])
    pin(Path(__file__).resolve())
    output = ROOT / 'tools/reference/tainted-foundation-source-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Tainted:3570 exact Fog controls and6120 complete NPC Fire Ball packets; shared base/do56 evidence retained, learned summon gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
