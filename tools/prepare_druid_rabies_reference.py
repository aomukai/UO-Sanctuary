#!/usr/bin/env python3
"""Pin Rabies's separate bite, infection marker and spreading missile routes."""
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
    pin_path = ROOT / 'tools/reference/creeper-preflight.json'
    pin = json.loads(pin_path.read_text())
    public_root = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public_root / 'sources.json').read_text())
    assert manifest['mechanicsReferenceVersion'] == pin['referenceVersion'] == '3.3.93847'
    rows, hashes, compared = {}, {}, 0
    for table, key, names in [('skills', 'skill', ['Rabies']), ('missiles', 'Missile', ['rabiesplague', 'rabiescontagion']), ('states', 'state', ['rabies', 'wolf'])]:
        primary_pin = pin['localTables'][table]
        primary = ROOT / '.local/research/d2r-3.3.93847' / primary_pin['localFilename']
        assert sha(primary) == primary_pin['sha256']
        local = list(csv.DictReader(primary.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        public = public_root / 'json' / (table + '.json')
        assert sha(public) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(public.read_text())
        rows[table] = {}
        for name in names:
            row = next(x for x in local if x.get(key) == name)
            other = next(x for x in cached.values() if x.get(key) == name)
            for field, value in other.items():
                if field not in row or field == 'lineNumber':
                    continue
                text = str(value)
                if table == 'skills' and text.startswith('"') and text.endswith('"'):
                    text = text[1:-1]
                assert row[field] == text, (table, name, field, row[field], text)
                compared += 1
            rows[table][name] = {field:value for field,value in row.items() if field and value != ''}
        hashes[table] = dict(filename=primary_pin['localFilename'], sha256=sha(primary), publicSha256=sha(public))
    row = rows['skills']['Rabies']
    assert [row[key] for key in ['*Id', 'srvstfunc', 'srvdofunc', 'anim', 'seqtrans', 'UseAttackRate', 'mana', 'ResultFlags', 'HitFlags', 'HitShift', 'EType', 'auratargetstate', 'srvmissilea', 'State1']] == ['238', '57', '121', 'S3', 'S3', '1', '10', '1', '2', '3', 'pois', 'rabies', 'rabiesplague', 'wolf']
    assert not row.get('SrcDam')
    assert not row.get('State2') and row['EDmgSymPerCalc'] == "(skill('Plague Poppy'.blvl))*par8" and row['Param8'] == '20'
    plague = rows['missiles']['rabiesplague']
    child = rows['missiles']['rabiescontagion']
    assert [plague[key] for key in ['pSrvDoFunc', 'Param1', 'Param2', 'SubMissile1']] == ['30', '4', '7', 'rabiescontagion']
    assert [child[key] for key in ['pSrvDoFunc', 'pSrvHitFunc', 'pSrvDmgFunc', 'Vel', 'Range', 'Size', 'Activate', 'Skill']] == ['1', '53', '11', '5', '20', '1', '2', 'Rabies']
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    source_hashes, bodies, routines = {}, {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvSt57_Rabies', 'sub_6FCFEDD0', 'SKILLS_SrvDo121_Rabies', 'sub_6FCFF2E0', 'sub_6FD00370']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD155E0', 'sub_6FD11D90', 'D2GAME_RollElementalDamage_6FD14DD0']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvDo30_RabiesPlague', 'MISSMODE_SrvHit53_RabiesContagion', 'MISSMODE_SrvDmg11_RabiesContagion']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp', ['SUNITDMG_AllocCombat', 'SUNITDMG_DrainItemDurability']),
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_GetElementalLength']),
    ]:
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        source_hashes[relative] = sha(path)
        for name in names:
            body = function(raw.decode(), name)
            bodies[name] = body
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
    body = bodies['SKILLS_SrvDo121_Rabies']
    anchors = ['SKILLS_SetParam1(pSkill, 0)', 'sub_6FD00370', 'SUNITDMG_DrainItemDurability', 'SKILLS_GetElementalLength', 'STATLIST_AllocStatList', 'sub_6FCFF2E0', 'sub_6FD155E0', 'D2GAME_RollElementalDamage_6FD14DD0', 'sub_6FD11D90', 'SUNITDMG_AllocCombat']
    positions = [body.index(anchor) for anchor in anchors]
    assert positions == sorted(positions)
    assert body.count('SUNITDMG_DrainItemDurability') == 2
    for name in ['MISSMODE_SrvHit53_RabiesContagion', 'MISSMODE_SrvDmg11_RabiesContagion']:
        assert '>= 10' in bodies[name] and '<= SKILLS_GetElementalLength' in bodies[name]
    assert 'STATLIST_GetStatListFromUnitAndState(pTarget' in bodies['sub_6FCFEDD0']
    clock_path = ROOT / 'tools/reference/druid-s3-clock-engine-evidence.json'
    clock = json.loads(clock_path.read_text())
    assert clock['contexts'] == 28800 and clock['referenceVersion'] == '3.3.93847'
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, handlerEnabled=False, executedContexts=0,
        currentExecutableParity=False, legacyCommit=tree['sha'], publicCommit=manifest['commit'],
        sourcePinSha256=sha(pin_path), localTables=hashes, publicFieldsCompared=compared, currentRows=rows,
        sourceHashes=source_hashes, completeRoutineHashes=routines, orderedDoAnchors=anchors, s3ClockEvidenceSha256=sha(clock_path),
        reuseCandidates={name:sha(ROOT / 'src/Sanctuary.Content' / name) for name in ['DruidHunger.cs', 'DruidFormAttacks.cs', 'DruidChargedAttacks.cs', 'PoisonPackets.cs', 'PoisonStates.cs', 'PoisonJavelins.cs', 'DruidCreeperPoison.cs']},
        established=[
            'Current Rabies238uses start57/do121,wolf-only S3,10mana and hard-only Poison Creeper20percent synergy. Poison uses HitShift3and duration100+10per extra rank. Current ResultFlags1and HitFlags2are essential route inputs,not ordinary weapon-poison merging instructions. The skill SrcDam field is blank; the ordinary helper inherits form SrcDam128and the later poison allocator explicitly receives128. Do not invent a skill-row SrcDam value.',
            'Older start57checks the skill/target/form-state and one result roll,then stores param1success. Do121requires that flag and clears it,allocates/drains a separate ordinary transformed weapon attack,applies a separate rabies target marker/emitter if absent,then allocates/drains the skill poison packet. The second packet uses current forced-success/fill-once flags. Do not collapse these into one Hunger-style weapon packet or silently omit the first ordinary attack.',
            'The rabies marker state112is distinct from actual poison state. A new host gets only the remaining original infection lifetime; existing rabies markers are not refreshed by the spread helper. The emitter follows the infected host and ends when the host dies,the original caster unit is missing or its frame is expired. A dead original caster is not the same source condition as a missing unit.',
            'Plague515spawns contagion516every remaining-frame multiple of4. Each child chooses two independent offsets in[-7,7],has speed5/range20/size1/activation2,and belongs to the original caster,with the source marker absolute expiry copied to child targetX. These are missile/path units,not a deterministic7-tile circular pulse. Native world scaling and collision require separate evidence.',
            'Both child hit53and damage11accept remaining expiry only when10<=remaining<=current elemental length. Hit53creates the host marker and emitter with that remaining timeout; damage11sets poison duration to the remaining deadline or zeros its poison packet outside the window. Poison damage/cure/replacement and spread-marker lifetimes must remain independent.',
        ], remaining=[
            'Execute complete start/do,host-marker/expiry,emitter and child hit/damage functions with explicit services. Establish the separate ordinary-attack/poison event ordering,initial payment/miss/block/failure behavior,source modifiers and child owner/deadline propagation before a native handler. Initial rank1minimum-mana and modern target-loss/failed-start behavior remain uncertain.',
            'Reuse verified S3progress/scheduler and poison packet/state/mitigation services where semantics match. Capture or refresh skill damage/mastery/pierce/gear/synergy only at the source-supported phase. Do not use a weapon-poison average,reset every infected host to full duration,assume one uniform AoE pulse,or make curing poison silently clear its independent rabies marker.',
            'Exact modern spread path/event/seed/geometry,caster death/logout/facet policy and exceptional recipients remain open. The earlier low-mana ordinary-form shield omission is corrected in the frozen Hunger candidate; human/ranged common attack scope remains separate. Keep that running canonical package unchanged while preparing Rabies.',
            'No Rabies handler,installation or final art is enabled by this static preparation. Wolf S3bite,infected-host/plague/contagion/poison/contact/audio and separate talent/left/right/quick icons remain pending with identified placeholders. Other seven Druid/earlier-class/Warlock gaps stay open.',
        ])
    path = ROOT / 'tools/reference/druid-rabies-preflight.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print(f'Rabies preflight:{compared}current fields/{len(routines)}complete older routines; separate bite/poison/marker/missile scopes, no runtime changes.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
