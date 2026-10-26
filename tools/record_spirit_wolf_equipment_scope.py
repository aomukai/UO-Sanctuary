#!/usr/bin/env python3
"""Record the pinned no-equipment scope for Spirit Wolf, without clearing other pets."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    table_hashes = {}
    tables = {}
    for name in ['monequip', 'monstats']:
        path = ROOT / 'tools/reference/d2r-named-equipment/json' / (name + '.json')
        table_hashes[name] = sha(path)
        assert table_hashes[name] == manifest['files']['json/' + name + '.json']['sha256']
        tables[name] = json.loads(path.read_text())
    wolf = tables['monstats']['spiritwolf']
    assert wolf['Id'] == wolf['BaseId'] == 'spiritwolf'
    rows = list(tables['monequip'].values())
    assert len(rows) == 45
    matches = [row for row in rows if row.get('monster') == 'spiritwolf']
    assert matches == []
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    source_hashes, blocks = {}, {}
    for path, name in [('source/D2Common/src/DataTbls/MonsterTbls.cpp', 'DATATBLS_LoadMonEquipTxt'),
                       ('source/D2Game/src/SKILLS/SkillAss.cpp', 'sub_6FCF9580')]:
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == path)
        source_hashes[path] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        found = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert found, name
        end = text.index('{', found.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[found.start():end]
    loader = blocks['DATATBLS_LoadMonEquipTxt']
    assert 'nMonEquipTxtRecordId = -1;' in loader
    assert 'DATATBLS_GetMonStatsTxtRecord(pMonEquipTxtRecord->nMonster)' in loader
    assert 'pMonStatsTxtRecord->nMonEquipTxtRecordId = i;' in loader
    dispatch = blocks['sub_6FCF9580']
    assert dispatch.index('pMonStatsTxtRecord->nMonEquipTxtRecordId < 0') < dispatch.index('pMonEquipTxtRecord =')
    prior_path = ROOT / 'tools/reference/valkyrie-equipment-engine-evidence.json'
    prior = json.loads(prior_path.read_text())
    assert hashlib.sha256(dispatch.encode()).hexdigest() == prior['blockHashes']['sub_6FCF9580']
    probe_path = ROOT / prior['probe']['fixture']
    assert sha(probe_path) == prior['probe']['sha256']
    assert json.loads(probe_path.read_text())['guards'][2] == [2, 0, 0]
    evidence = dict(referenceVersion='3.3.93847', tableCommit=manifest['commit'], legacyCommit=tree['sha'],
        monster='spiritwolf', equipmentRows=len(rows), matchingRows=matches,
        sourceHashes=source_hashes, tableHashes=table_hashes,
        routineHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
        reusedCompiledGuard=dict(evidence=str(prior_path.relative_to(ROOT)), sha256=sha(prior_path),
            probe=str(probe_path.relative_to(ROOT)), probeSha256=sha(probe_path), case=[2, 0, 0]),
        established=[
            'The pinned45-row equipment table contains no Spirit Wolf entry. Its monstats identity and base identity are both spiritwolf.',
            'The complete legacy loader initializes every monster equipment index to-1 and sets it only for the monster resolved from an equipment row. Absence leaves the wolf index negative.',
            'The complete legacy equipment dispatcher returns before any row selection/item creation when that index is negative. The previously compiled unchanged dispatcher guard2 records zero created and zero removed items.',
            'Under this pinned table/dispatch model, generated NPC equipment, equipment IAS and equipment affix/stat consumers are not dependencies of Spirit Wolf. Keep its requested item-level snapshot without inventing equipped items.'],
        remaining=[
            'This is a current-table plus legacy-dispatch inference, not a current-executable equipment trace. A future changed table or dispatcher requires review.',
            'Do not clear Valkyrie, Shadow or other equipped-summon generation/stat-consumer gaps. Actor buffs, inherited passive fields, cold sunder and attack scheduling retain their separate acceptance requirements.'])
    target = ROOT / 'tools/reference/spirit-wolf-equipment-scope.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Spirit Wolf:45 pinned equipment rows, no matching row; loader/dispatch and reused compiled negative-index guard verified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
