#!/usr/bin/env python3
"""Reuse the accepted complete affix harness for owned group306 selection."""
import argparse
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grimoire-automatic-source'
REF = ROOT / 'tools/reference/d2r-named-equipment'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def routine(text, name):
    # Same complete-body extractor as the reused Valkyrie probe; signatures
    # such as MagicItemRollAffix span multiple source lines.
    match = re.search(r'^(?:static |inline )?[A-Za-z_][^;\n]*\b' + name + r'\([^;{]*?\)\s*\n\{', text, re.M)
    assert match, name
    end = text.index('{', match.start()) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[match.start():end]


def probe(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    inputs = {}

    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return path.read_bytes()

    priorpath = ROOT / 'tools/reference/valkyrie-affix-engine-evidence.json'
    prior = json.loads(pin(priorpath))
    assert prior['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    original = ROOT / '.local/class-skills/valkyrie-affix-source-probe.cpp'
    raw = pin(original).decode()
    prefix, main = raw.split('int main(){', 1)
    initialization = main.split('bool first=true;std::printf("[");', 1)[0]
    names = ['ITEMS_ComputeCraftedMagicAffixLevel', 'ITEMMODS_AffixPlayerClassisCompatible', 'ITEMMODS_HasAffixWithGroup',
             'ComputeAffixFrequency', 'MagicItemRollAffix', 'ITEMS_RollMagicAffixesNew', 'ITEMMODS_CanItemHaveMagicAffix',
             'SEED_RollRandomNumber', 'SEED_RollLimitedRandomNumber']
    blocks = {name: hashlib.sha256(routine(raw, name).encode()).hexdigest() for name in names}
    assert blocks == prior['compiledUnchangedRoutines']
    for relative, digest in prior['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / relative) == digest
    manifest = json.loads(pin(REF / 'sources.json'))
    tables = {}
    for name in ('itemtypes', 'properties', 'armor', 'skills'):
        path = REF / 'json' / (name + '.json')
        assert sha(path) == manifest['files']['json/' + name + '.json']['sha256']
        tables[name] = json.loads(pin(path))
    extractionpath = ROOT / '.local/class-skills/warlock-hands-reference/grimoire-extraction.json'
    extraction = json.loads(pin(extractionpath))
    automagicpath = extractionpath.parent / 'automagic.txt'
    assert sha(automagicpath) == extraction['files']['automagic.txt']['sha256']
    rows = list(csv.DictReader(pin(automagicpath).decode().splitlines(), delimiter='\t'))
    assert len(rows) == 44 and sum(row['group'] == '306' for row in rows) == 8
    basespath = ROOT / 'data/Sanctuary/Balance/warlock-grimoire-bases.json'
    bases = json.loads(pin(basespath)); assert len(bases) == 15
    for basis in bases:
        source = next(row for row in tables['armor'].values() if row['code'] == basis['code'])
        assert source.get('magic lvl', 0) == 0 and source['level'] == basis['qualityLevel'] and source['auto prefix'] == 306
    types = {r['Code']: i + 1 for i, r in enumerate(tables['itemtypes'].values()) if r.get('Code')}
    properties = {name.casefold(): i for i, name in enumerate(tables['properties'])}
    match = re.search(r'constexpr int recordCount=(\d+),suffixCount=(\d+);', prefix)
    assert match
    old_count = int(match[1]); count = old_count + len(rows)
    prefix = prefix.replace('recordCount=' + str(old_count), 'recordCount=' + str(count), 1)
    prefix = prefix.replace('affixes+recordCount,recordCount};', 'affixes+' + str(old_count) + ',recordCount};', 1)
    prefix = prefix.replace('int ITEMS_GetMaxSockets(D2UnitStrc*){return 3;}', 'int ITEMS_GetMaxSockets(D2UnitStrc*){return 2;}')
    # Complete copied engine bodies remain identical; only declared scaffold
    # tables/socket capacity and main's controlled requests change.
    assert {name: hashlib.sha256(routine(prefix, name).encode()).hexdigest() for name in names} == blocks
    cpp = prefix + '\nint main(){\n' + initialization
    for i, row in enumerate(rows):
        at = old_count + i
        cpp += f'ids[{at}]=\"AutoMagic:{i}\";\n'
        fields = dict(wVersion=int(row['version'] or 0), wSpawnable=int(row['spawnable'] or 0), dwLevel=int(row['level'] or 0),
                      dwMaxLevel=int(row['maxlevel'] or 0), dwGroup=int(row['group'] or 0), nRare=int(row['rare'] or 0),
                      wFrequency=int(row['frequency'] or 0), nClassSpecific=255)
        assert not row['classspecific']
        for field, value in fields.items():
            cpp += f'affixes[{at}].{field}={value};\n'
        cpp += f'affixes[{at}].pProperties[0].nProperty={properties[row["mod1code"].casefold()]};\n'
        for source_prefix, member, maximum in (('itype', 'wIType', 7), ('etype', 'wEType', 5)):
            for slot in range(1, maximum + 1):
                code = row[source_prefix + str(slot)].strip()
                if not code:
                    break
                cpp += f'affixes[{at}].{member}[{slot - 1}]={types[code]};\n'
    seeds = [0, 1, 173, (666 << 32) | 173, (1 << 64) - 1, 0x80000000FFFFFFFF, 0x123456789ABCDEF0, 0x00000000FFFFFFFF]
    cpp += 'bool first=true;std::printf("[");\n'
    for basis in bases:
        cpp += f'item.qlvl={basis["qualityLevel"]};item.magic=0;item.type={types["grim"]};item.itemClass=7;\n'
        cpp += 'for(int level=1;level<=99;level++)for(uint64_t initial:{' + ','.join(str(n) + 'ULL' for n in seeds) + '}){\n'
        cpp += r'''D2ItemData data;item.pItemData=&data;item.level=level;item.quality=2;item.seed.lSeed=initial;
poolIds.clear();poolWeights.clear();totalWeight=0;
int selected=ITEMS_RollMagicAffixesNew(&item,0,1,0,1,0,306);uint64_t finalSeed=item.seed.lSeed;
if(!first)std::printf(",");first=false;
'''
        cpp += f'std::printf("[\\"{basis["code"]}\\",%d,\\"%llu\\",%d,\\"%s\\",\\"%llu\\",%d,[",level,(unsigned long long)initial,ITEMS_ComputeCraftedMagicAffixLevel(&item,&basis),selected?ids[selected-1].c_str():"",(unsigned long long)finalSeed,totalWeight);\n'
        cpp += r'''for(size_t i=0;i<poolIds.size();i++)std::printf(i?",[\"%s\",%d]":"[\"%s\",%d]",ids[poolIds[i]].c_str(),poolWeights[i]);std::printf("]]");}
'''
    cpp += 'std::printf("]\\n");}\n'
    sourcepath = STAGE / 'probe.cpp'; sourcepath.write_text(cpp)
    binary = STAGE / 'probe'
    run = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-Wno-literal-suffix', '-fsanitize=address,undefined', '-fno-sanitize-recover=all',
                          str(sourcepath), '-o', str(binary)], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(run.stdout + run.stderr); assert run.returncode == 0
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr); assert run.returncode == 0 and not run.stderr
    cases = json.loads(run.stdout); assert len(cases) == 11880
    for code, level, initial, alvl, selected, final, total, pool in cases:
        assert selected.startswith('AutoMagic:') and rows[int(selected.split(':')[1])]['group'] == '306'
        assert pool and all(rows[int(key.split(':')[1])]['group'] == '306' for key, weight in pool)
        assert total == sum(weight for key, weight in pool)
    fixture = STAGE / 'cases.json'; fixture.write_bytes(run.stdout)
    mapping = STAGE / 'rows.json'; mapping.write_text(json.dumps({f'AutoMagic:{i}': row['Name'] for i, row in enumerate(rows)}, indent=2) + '\n')
    warlock = sorted((row for row in tables['skills'].values() if row.get('charclass') == 'war'), key=lambda row: row['*Id'])
    assert len(warlock) == 30 and [row['*Id'] for row in warlock] == list(range(373, 403))
    groups = [[dict(id=row['*Id'], name=row['skill'], requiredLevel=row['reqlevel']) for row in warlock[i:i + 5]] for i in range(0, 30, 5)]
    assert any(len({row['requiredLevel'] for row in group}) > 1 for group in groups)
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', component='Normal Grimoire automatic selection source comparison', legacyCommit=prior['legacyCommit'],
                  compiledUnchangedRoutines=blocks, cases=11880, bases=15, itemLevels=99, seeds=8, quality=2, automaticGroup=306,
                  inputs=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in (sourcepath, binary, fixture, mapping, STAGE / 'compiler.log', STAGE / 'sanitizer.log',
                                                                                     STAGE / 'diagnostic-1/probe.py', STAGE / 'diagnostic-1/run.log')},
                  staffmodContiguousIdGroups=groups, currentExecutableParity=False, handlerEnabled=False,
                  established=['Complete accepted older selector,level/class/existing-group/item-property/type checks,weighted picker and seed helpers execute unchanged with all44owned automatic rows and15Grimoire bases. Each case records the selected source row,ordered pool,weights,affix level and final seed.',
                               'Caller supplies format100,normal quality2,empty existing prefixes/suffixes,force=true,assignment=false,group306 and the current Grimoire-equivalence type services. Forced selection retains the first parity draw and the totalWeight+1 endpoint assigned to the final eligible row.'],
                  limitations=['Supplementary older-code execution with declared current table/host services,not current executable parity. Item/owner/global seed allocation,property assignment,defense,staffmods and full generation/acquisition are not executed.',
                               'Valkyrie scaffold,existing current prefix/suffix/property/type initialization and normalized extraction IDs are reused. Source IDs are not claimed as modern binary IDs. Socket capacity is supplied as2;group306contains no socket property.',
                               'Owned Warlock IDs373..402are grouped by tree and their contiguous five-ID groups mix required levels. Applicability of the older linear-ID staffmod routine remains uncertain;no new sorted map,old ID arithmetic or grant-free public generator is invented.'])
    target = ROOT / 'tools/reference/grimoire-automatic-source-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == encoded
    else: target.write_text(encoded)
    print('Grimoire automatic:11880 complete source pool/weight/selection/seed cases;clean ASan/UBSan;Warlock staffmod mapping remains deferred.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    probe(parser.parse_args().check)
