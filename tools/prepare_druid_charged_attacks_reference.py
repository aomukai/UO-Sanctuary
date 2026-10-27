#!/usr/bin/env python3
"""Pin Maul/Feral Rage data and review complete older routines; no runtime claim."""
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
    manifest_path = ROOT / 'tools/reference/d2r-named-equipment/sources.json'
    manifest = json.loads(manifest_path.read_text())
    assert manifest['mechanicsReferenceVersion'] == forms['referenceVersion'] == '3.3.93847'
    rows, pins, compared = {}, {}, 0
    for table, key, names in [('skills', 'skill', ['Maul', 'Feral Rage']), ('states', 'state', ['maul', 'feralrage', 'wolf', 'bear'])]:
        pin = forms['localTables'][table]
        local = ROOT / '.local/research/d2r-3.3.93847' / pin['filename']
        assert sha(local) == pin['sha256']
        current = list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        public = ROOT / 'tools/reference/d2r-named-equipment/json' / (table + '.json')
        assert sha(public) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(public.read_text())
        rows[table] = {}
        for name in names:
            row = next(row for row in current if row.get(key) == name)
            cached_row = next(row for row in cached.values() if row.get(key) == name)
            for field, value in cached_row.items():
                if field not in row or field == 'lineNumber':
                    continue
                value = str(value)
                if table == 'skills' and value.startswith('"') and value.endswith('"'):
                    value = value[1:-1]
                assert value == row[field], (table, name, field, value, row[field])
                compared += 1
            rows[table][name] = {key: value for key, value in row.items() if key and value != ''}
        pins[table] = dict(filename=pin['filename'], sha256=sha(local), publicSha256=sha(public))
    for name, state in [('Maul', 'bear'), ('Feral Rage', 'wolf')]:
        row = rows['skills'][name]
        assert [row[key] for key in ['srvstfunc', 'srvdofunc', 'anim', 'seqtrans', 'UseAttackRate', 'restrict', 'State1', 'mana', 'SrcDam']] == ['56', '120', 'A1', 'A1', '1', '2', state, '3', '128']
        assert row['calc2'] == 'lvl/par7 + par8' and row['Param7'] == '2' and row['Param8'] == '3'
    assert [rows['states'][name]['group'] for name in ['maul', 'feralrage', 'wolf', 'bear']] == ['3'] * 4
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == forms['legacyCommit']
    hashes, routines, bodies = {}, {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvSt56_FeralRage_Maul', 'SKILLS_SrvDo120_FeralRage_Maul', 'sub_6FCFE0E0', 'sub_6FCFE030']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD11C90']),
    ]:
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
        hashes[relative] = sha(path)
        for name in names:
            body = function(raw.decode(), name)
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
            bodies[name] = body
    start, effect = bodies['SKILLS_SrvSt56_FeralRage_Maul'], bodies['SKILLS_SrvDo120_FeralRage_Maul']
    assert start.index('SUNITDMG_GetResultFlags') < start.index('SUNITDMG_AllocCombat') < start.index('SKILLS_SetParam1')
    anchors = ['SUNITDMG_DrainItemDurability', 'if (!SKILLS_GetParam1', 'const int32_t nExpireFrame', 'STATLIST_AllocStatList',
        'STAT_SKILL_FRENZY, 0) + 1', 'STATLIST_SetStatIfListIsValid(pStatList, STAT_SKILL_FRENZY',
        'STATLIST_SetStatIfListIsValid(pStatList, STAT_MODIFIERLIST_LEVEL', 'sub_6FCFE0E0(pUnit, pStatList, pSkillsTxtRecord, nSkillId, nParam)']
    assert [effect.index(anchor) for anchor in anchors] == sorted(effect.index(anchor) for anchor in anchors)
    assert 'sub_6FD11C90(pUnit, nStateId, 1)' in bodies['sub_6FCFE030']
    assert 'pRecord->wGroup == pStatesTxtRecord->wGroup' in bodies['sub_6FD11C90']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    for talent in ['druid.maul', 'druid.feralrage']:
        assert next(row for row in status['skills'] if row['talentId'] == talent)['implementation'] == 'pending'
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, handlerEnabled=False, executedContexts=0,
        sourceScope='Byte-pinned current tables and static review of five complete older routine bodies; not a compiled charge/melee pipeline.',
        legacyCommit=tree['sha'], publicCommit=manifest['commit'], formsPreflightSha256=sha(forms_path), localTables=pins,
        publicFieldsCompared=compared, currentRows=rows, sourceHashes=hashes, reviewedRoutineHashes=routines,
        reuseCandidates={name:sha(ROOT / 'src/Sanctuary.Content' / name) for name in ['AssassinCharges.cs', 'TigerStrikes.cs', 'DruidPlayerForms.cs', 'DruidFormAttacks.cs', 'DruidFormRates.cs', 'ClassStats.cs', 'WeaponLeech.cs']},
        reviewed=[
            'Both skills use start56/do120,A1/attack-rate,3mana,source128weapon packets and form-only restrict2. Start allocates the resolved combat packet and stores a success latch. Do120requests durability/combat drain before checking that latch or extending/gaining the aura. Complete target/hit/block/wear/mana and action timing still need execution/composition.',
            'On successful-latch do120,new/old aura expiry refreshes before counter update. Counter becomes min(current effective-rank calc2,old counter+1); stored modifier skill level is replaced by the current effective rank. The shared aura writer receives the COUNTER as its formula level. Maul uses30damage and3attack-rate per counter plus charge-level dm56stun. Feral Rage uses charge-level dm34velocity and4life drain per counter; its separate attack calc1uses the learned effective rank.',
            'Current Maul,Feral Rage,Werewolf and Werebear states all have group3. The complete form-removal callback invokes group cleanup,whose full body removes every active state/list in the matching group. Therefore charge cleanup must compose with form removal; do not assume the buffs survive unshift from their500frame duration alone. This is table/static-call-chain evidence pending full composed charge execution.',
            'Assassin charge persistence/UTC expiry,source weapon packets,form A1rate/shape checks and shared leech/stat consumers are reuse candidates. Assassin fixed cap3,consume-one finishers,retained initial-rank policy and per-charge ARincrement are different mechanics and must not be copied to Maul/Feral Rage. These buffs are not shown consumed by ordinary attacks in the reviewed do120path.',
        ], assets={name:dict(state=row['aurastate'], overlays=[rows['states'][row['aurastate']].get('overlay'+str(i)) for i in range(1,5)],
            status='Final charge-orb stages/front-back motion,contact/audio and tree/left/right/quick icons remain in separate existing effects/icon inventories; placeholders must not own gameplay.') for name, row in rows['skills'].items()},
        next=[
            'Execute complete start56/do120/shared aura writer/removal composition with success/miss,allocation/cap/live-rank/expiry/group cleanup before enabling either handler. The old invalid-state guard uses an impossible AND; invalid IDs remain explicitly unsupported rather than silently normalized.',
            'Compose actual form A1clock,once-only start/do payment,source weapon allocation/contact/wear and shared stat consumers. Maul stun/damage/IAS and Feral velocity/leech need their real outgoing/movement consumers,including form-removal cleanup. Modern exact hit-latch/contact order,soft ranks,mode/reentry/owner/client acceptance remain separate; no new source guess is an approved deviation.',
        ])
    output = ROOT / 'tools/reference/druid-charged-attacks-preflight.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Maul/Feral Rage:current skills/four group3states pinned,{compared}public fields compared,five complete routines reviewed; no runtime handler or execution claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
