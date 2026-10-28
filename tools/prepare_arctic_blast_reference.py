#!/usr/bin/env python3
"""Pin Arctic Blast and identify the shared, still-incomplete player channel."""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pin = json.loads((ROOT / 'tools/reference/druid-shock-wave-preflight.json').read_text())
    public = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public / 'sources.json').read_text())
    assert pin['referenceVersion'] == manifest['mechanicsReferenceVersion'] == '3.3.93847'
    tables, rows, compared = {}, {}, 0
    for table, key, names in [
        ('skills', 'skill', ['Arctic Blast', 'Inferno']),
        ('missiles', 'Missile', ['arcticblast1', 'arcticblast2', 'infernoflame1']),
        ('states', 'state', ['inferno']),
    ]:
        table_pin = pin['localTables'][table]
        local = ROOT / '.local/research/d2r-3.3.93847' / table_pin['localFilename']
        remote = public / 'json' / (table + '.json')
        assert sha(local) == table_pin['sha256']
        assert sha(remote) == manifest['files']['json/' + table + '.json']['sha256']
        licensed = list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        published = list(json.loads(remote.read_text()).values())
        rows[table] = {}
        for name in names:
            row = next(x for x in licensed if x.get(key) == name)
            other = next(x for x in published if x.get(key) == name)
            for field, value in other.items():
                if field not in row or field == 'lineNumber' or field.startswith('*') and field not in ['*Id', '*ID']:
                    continue
                text = str(value)
                if table == 'skills' and text.startswith('"') and text.endswith('"'):
                    text = text[1:-1]
                assert row[field] == text, (table, name, field, row[field], text)
                compared += 1
            rows[table][name] = {k: v for k, v in row.items() if k and v != ''}
        tables[table] = dict(localFilename=table_pin['localFilename'], sha256=sha(local), publicSha256=sha(remote))
    skill = rows['skills']['Arctic Blast']
    assert [skill[k] for k in ['*Id', 'srvstfunc', 'srvdofunc', 'seqnum', 'seqinput', 'startmana', 'mana', 'lvlmana', 'manashift', 'HitShift', 'EType']] == ['230', '11', '19', '18', '10', '6', '24', '1', '2', '3', 'cold']
    assert skill['calc1'] == 'ln12/2' and skill['EDmgSymPerCalc'] == "skill('Cyclone Armor'.blvl)*par8"
    assert skill['Param2'] == '2' and skill['Param8'] == '15'
    missile = rows['missiles']['arcticblast1']
    assert [missile[k] for k in ['*ID', 'pSrvDoFunc', 'Vel', 'Size', 'LastCollide', 'DamageRate']] == ['459', '1', '12', '1', '1', '41']
    assert not missile.get('CanSlow') and rows['missiles']['infernoflame1']['DamageRate'] == '82'

    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    source_hashes, routines = {}, {}
    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        source_hashes[relative] = sha(path)
        return raw.decode()
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillSor.cpp', ['SKILLS_SrvSt11_Inferno_ArcticBlast', 'SKILLS_StartInferno', 'SKILLS_SrvDo019_Inferno_ArcticBlast', 'SKILLS_DoInferno']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['D2GAME_SKILLMANA_Consume_6FD10A50', 'sub_6FD12FD0', 'sub_6FD13410', 'D2GAME_SKILLS_Handler_6FD12BA0']),
        ('source/D2Game/src/UNIT/SUnit.cpp', ['sub_6FCBD120']),
    ]:
        text = source(relative)
        for name in names:
            routines[name] = hashlib.sha256(function(text, name).encode()).hexdigest()
    seq = source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    sequences = {}
    for name in ['gPlayerSequenceArcticBlast', 'gPlayerSequenceInferno']:
        match = re.search(r'D2AnimSeqTxt ' + name + r'\[15\] =\s*\{.*?\n\};', seq, re.S)
        assert match, name
        points = re.findall(r'\{\s*0,\s*(\w+),\s*(\d+),\s*0,\s*(\w+)\s*\}', match[0])
        assert len(points) == 15
        assert [i for i, p in enumerate(points) if p[2] != 'ANIMSEQ_EVENT_NONE'] == [10]
        sequences[name] = dict(sha256=hashlib.sha256(match[0].encode()).hexdigest(), points=[list(p) for p in points])
    assert [p[1:] for p in sequences['gPlayerSequenceArcticBlast']['points']] == [p[1:] for p in sequences['gPlayerSequenceInferno']['points']]
    assert {p[0] for p in sequences['gPlayerSequenceArcticBlast']['points']} == {'PLRMODE_SPECIAL1'}
    assert {p[0] for p in sequences['gPlayerSequenceInferno']['points']} == {'PLRMODE_CAST'}
    result = dict(
        referenceVersion='3.3.93847', sourcePreparation=True, handlerEnabled=False, nativeAccepted=False,
        executedContexts=0, currentExecutableParity=False, legacyCommit=tree['sha'], publicCommit=manifest['commit'],
        localTables=tables, currentRows=rows, publicFieldsCompared=compared, sourceHashes=source_hashes,
        completeRoutineHashes=routines, legacySequences=sequences,
        reuseCandidates=['InfernoFlames.cs', 'AssassinInfernoControl.cs', 'AssassinInfernoBurst.cs', 'BladeFurySequence.cs', 'BladeFuryResources.cs', 'MissileVectors.cs', 'SkillCombat.cs'],
        reusableEvidence=['tools/reference/inferno-source-evidence.json', 'tools/reference/blade-fury-sequence-source-evidence.json', 'tools/reference/inferno-sentry-controller-source-evidence.json'],
        established=[
            'Current Arctic Blast230 shares player start11/do19 with Inferno41, but selects SQ18 instead of SQ6. Older compiled-in sequences have matching frame/event coordinates across15points and sole action at index10, but Arctic Blast uses SPECIAL1 poses while Inferno uses CAST. This is older sequence evidence, not a modern sequence or channel trace.',
            'The complete older start wrapper tests startmana6 as a threshold, bypassed while the Inferno state bit is active. StartInferno allocates a20frame list on first entry; existing-list reentry renews it for6frames and invokes DoInferno. The first do suppresses emission, sets param1 and rewinds; subsequent do creates flags0x8020 with a skill-derived lifetime. Do not invent a six-mana upfront payment or use the monster-only duration branch for a player.',
            'Arctic Blast has cold HitShift3 endpoints, hard Cyclone Armor15percent synergy,100+15/rank cold length, and floor((35+2*(rank-1))/2) missile lifetime with minimum1. Source missile459 uses Size1/LastCollide1 and DamageRate41; Inferno uses82 and CanSlow1. Shared flight consumers require explicit cold, collision and damage integration rather than an element-only copy of Inferno.',
        ],
        remaining=[
            'Blocked individually on the shared player channel: compose actual SQ/action/end scheduling, held reentry, stop/state-list removal and fractional mana debit with modern2.4+ free-form input. The older rank1 consume routine has a zero/minimum branch; current payment must not be silently inferred from that legacy quirk. Existing Blade Fury and NPC Inferno proofs provide components, not acceptance for this player route.',
            'Execute current cold endpoint/mastery/pierce/chill and hard-only synergy capture, complete missile factory/Size1/LastCollide/DamageRate delivery, live aim, collision, owner/facet/offline/expiry and native input/save behavior before enabling learned Arctic Blast. No damage model, channel implementation, native tests or installation is certified by this preflight.',
            'Final arctic cast overlay, stream/contact/chill/expiry/audio and talent/left/right/quick icons are missing in the separate effects and icon registers. Keep the talent gated for the mechanical channel gap and continue unaffected Creeper/Vine skills.',
        ],
    )
    for name in result['reuseCandidates']:
        assert (ROOT / 'src/Sanctuary.Content' / name).is_file(), name
    for name in result['reusableEvidence']:
        assert (ROOT / name).is_file(), name
    path = ROOT / 'tools/reference/arctic-blast-preflight.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Arctic Blast preflight: {compared} licensed/public fields, {len(routines)} complete routine hashes; shared player channel remains gated, no native acceptance.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
