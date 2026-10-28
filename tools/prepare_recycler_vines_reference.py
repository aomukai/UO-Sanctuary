#!/usr/bin/env python3
"""Pin Carrion/Solar vines, their corpse AI and delayed resource callbacks."""
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
    pin = json.loads((ROOT / 'tools/reference/creeper-preflight.json').read_text())
    public = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public / 'sources.json').read_text())
    assert pin['referenceVersion'] == manifest['mechanicsReferenceVersion'] == '3.3.93847'
    rows, tables, compared = {}, {}, 0
    for table, key, names in [
        ('skills', 'skill', ['Cycle of Life', 'Vines', 'CorpseCycler', 'VineCycler']),
        ('monstats', 'Id', ['cycleoflife', 'vinecreature']),
        ('monstats2', 'Id', ['cycleoflife', 'vinecreature']),
        ('missiles', 'Missile', ['recycler delay', 'vine recycler delay']),
        ('states', 'state', ['vine_beast', 'corpse_noselect']),
    ]:
        p = pin['localTables'][table]
        local = ROOT / '.local/research/d2r-3.3.93847' / p['localFilename']
        published = public / 'json' / (table + '.json')
        assert sha(local) == p['sha256']
        assert sha(published) == manifest['files']['json/' + table + '.json']['sha256']
        licensed = list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        cached = list(json.loads(published.read_text()).values())
        rows[table] = {}
        for name in names:
            row = next(x for x in licensed if x.get(key) == name)
            other = next(x for x in cached if x.get(key) == name)
            for field, value in other.items():
                if field not in row or field == 'lineNumber' or field.startswith('*') and field not in ['*Id', '*ID', '*hcIdx']:
                    continue
                text = str(value)
                if table == 'skills' and text.startswith('"') and text.endswith('"'):
                    text = text[1:-1]
                assert row[field] == text, (table, name, field, row[field], text)
                compared += 1
            rows[table][name] = {k: v for k, v in row.items() if k and v != ''}
        tables[table] = dict(localFilename=p['localFilename'], sha256=sha(local), publicSha256=sha(published))
    for name, identity, mana, level_mana, life_step in [('Cycle of Life', '231', '10', '0', '25'), ('Vines', '241', '14', '1', '20')]:
        row = rows['skills'][name]
        assert [row[k] for k in ['*Id', 'srvdofunc', 'pettype', 'petmax', 'anim', 'mana', 'lvlmana', 'Param3']] == [identity, '115', 'vine', '1', 'SC', mana, level_mana, life_step]
    carrion, solar = (rows['skills'][name] for name in ['CorpseCycler', 'VineCycler'])
    assert carrion['srvstfunc'] == solar['srvstfunc'] == '63'
    assert carrion['calc1'] == solar['calc2'] == 'ln12' and not solar.get('calc1')
    for name in ['cycleoflife', 'vinecreature']:
        row = rows['monstats'][name]
        assert row['AI'] == 'CycleOfLife'
        for suffix in ['', '(N)', '(H)']:
            assert [row['aip' + str(i) + suffix] for i in range(1, 6)] == ['50', '20', '25', '10', '35']
        body = rows['monstats2'][name]
        assert body['SizeX'] == body['SizeY'] == '0'
    for name, callback in [('recycler delay', '29'), ('vine recycler delay', '33')]:
        row = rows['missiles'][name]
        assert [row[k] for k in ['pSrvDoFunc', 'Param1', 'Range']] == [callback, '45', '47']

    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo115_Vines', 'SKILLS_SrvSt63_Corpse_VineCycler', 'SKILLS_FindUseableCorpse']),
        ('source/D2Game/src/AI/AiThink.cpp', ['D2GAME_AI_Unk_110_111_6FCECC40', 'AITHINK_Fn111_CycleOfLife']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvDo29_RecyclerDelay', 'MISSMODE_SrvDo33_VineRecyclerDelay']),
    ]:
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        for name in names:
            body = function(raw.decode(), name)
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
            if name == 'MISSMODE_SrvDo33_VineRecyclerDelay':
                assert 'pSkillsTxtRecord->dwCalc[0]' in body
    result = dict(
        referenceVersion='3.3.93847', sourcePreparation=True, handlerEnabled=False, nativeAccepted=False,
        executedContexts=0, currentExecutableParity=False, legacyCommit=tree['sha'], publicCommit=manifest['commit'],
        localTables=tables, currentRows=rows, publicFieldsCompared=compared, sourceHashes=hashes, completeRoutineHashes=routines,
        reuseCandidates=['DruidCreeperCasting.cs', 'DruidCreeperStats.cs', 'DruidCreeperBody.cs', 'DruidCreeperGround.cs', 'DruidDireWolfWorld.cs', 'SourcePetMovement.cs', 'DruidDireWolfCorpses.cs', 'CorpseSkillRules.cs'],
        established=[
            'Carrion231 and Solar241 share do115 and one vine category with Poison Creeper. They inherit effective-rank Vine Attack plus CorpseCycler307 or VineCycler325. Current Carrion costs10mana; Solar costs14+rank-1. Birth HP scales25percent or20percent per additional rank from95or165base life; body/source-state acceptance remains separate.',
            'AI111 first follows an owner at distance>=35, then searches corpse radius clamped5..50(current10), filters distance<20 and delegates common pet movement. It only starts eating in melee when the relevant owner resource is below maximum and the last-skill frame+50 is strictly less than current frame. It can still walk toward a corpse while the owner resource is full; do not replace this with an unconditional fixed healing aura.',
            'Complete start63 reserves corpse_noselect and refreshes the corpse before requesting a delay missile owned by the original minion owner and originating at the corpse. It invokes the skill-state packet even if missile allocation fails. The visible corpse reservation and delayed resource credit are distinct events.',
            'Delay missiles have47frame base lifetime and Param1=45. Older do29/do33 compare remainingFrames exactly to45, not age45, and grant a percentage of the living owner maximum after truncating current/max resource to whole points. Source callbacks clamp to max and delegate ordinary missile collision. Generic factory/update ordering must establish the actual activation time; a45frame wait is not justified by the Param label.',
        ],
        uncertainties=[
            'Solar formula-slot conflict: current VineCycler325 places ln12 in calc2 and leaves calc1 blank; complete older do33 reads dwCalc[0]. Copying that body with current rows would restore zero mana. Intended mana recovery is clear from current table labels, but modern executed slot/rounding/timing is not established. User has never used this skill; no observation resolves it. Keep the discrepancy explicit; do not silently patch the older body or promote an inferred implementation to exact parity.',
            'Shared VINE_BEAST underground exposure and incoming-damage timing remain unresolved from prepared Poison Creeper. Source state bits, zero-size collision and visible placeholder bodies do not prove modern underground invulnerability. Current source scan/list/ties/room/geometry, actor animation/corpse-reservation timing and owner facet/logout/death policies require dedicated execution/native checks.',
        ],
        next=[
            'Execute complete AI111/start63/do29/do33 using declared corpse, owner, movement, formula and collision services. Include full-resource, strict50frame gate, failed allocation, corpse reservation, remaining-frame equality, owner death/missing and fractional-resource controls; preserve the Solar zero-calc control as evidence of the conflict.',
            'Reuse saved summon stats/facet scaling, shared vine replacement, SC casting and corpse consumers only where source semantics agree. Then verify real actors, delayed credit, corpse contention/consumption, right/quick input and saves before enabling a learned handler. Final body/feed/resource-overlay/expiry/audio and talent/right/quick icons stay in separate existing asset registers.',
        ],
    )
    for name in result['reuseCandidates']:
        assert (ROOT / 'src/Sanctuary.Content' / name).is_file(), name
    path = ROOT / 'tools/reference/recycler-vines-preflight.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Recycler vines preflight: {compared} licensed/public fields, {len(routines)} complete routines; Solar formula conflict retained, learned handlers disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
