#!/usr/bin/env python3
"""Build bounded authoring evidence; never regenerate or load runtime catalogues.

Affix selection comes from FirstLoopCatalogueChecks and the actual C# APIs.
This tool verifies pins/links and projects source rows and query summaries.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REFERENCE = ROOT / 'tools/reference'
BALANCE = ROOT / 'data/Sanctuary/Balance'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def source_tables(source):
    pins = json.loads((BALANCE / 'sources.json').read_text())
    tables = {}
    for name in ['Weapons', 'Armor', 'Misc', 'Properties', 'ItemStatCost', 'MagicPrefix', 'MagicSuffix', 'ItemTypes']:
        path = source / (name + '.txt')
        require(digest(path) == pins[name]['sha256'], f'{name}: source differs from pinned SHA-256')
        with path.open(encoding='utf-8-sig', newline='') as stream:
            tables[name] = list(csv.DictReader(stream, delimiter='\t'))
    return pins, tables


def row_evidence(name, rows, key, value, fields):
    matches = [(index, row) for index, row in enumerate(rows) if row.get(key) == value]
    require(len(matches) == 1, f'Expected one {name}.{key}={value}')
    index, row = matches[0]
    return dict(table=name, rowIndex=index, line=index + 2,
                fields={field: row[field] for field in fields if field in row})


def build(source, audit_path, root=ROOT):
    pins, tables = source_tables(source)
    manifest_path = REFERENCE / 'first-loop-cohort.json'
    manifest = json.loads(manifest_path.read_text())
    audit = json.loads(audit_path.read_text())
    require(audit['ManifestSha256'] == digest(manifest_path), 'Stale affix audit: export the current manifest through the server fixture')
    for name, expected in audit['InputHashes'].items():
        require(digest(root / name) == expected, f'Stale runtime/input evidence: {name}')
    assembly = (root / audit.get('ContentAssemblyPath', '.local/content/Sanctuary.Content.dll')).resolve()
    require(assembly.is_relative_to(root.resolve()), 'Content assembly path escapes workspace')
    require(audit['ContentAssemblySha256'] == digest(assembly), 'Content DLL differs from audited assembly')
    units = {e['code']: e for e in manifest['effects']}
    require(len(units) == len(manifest['effects']), 'Duplicate effect code')
    require(set(units) == set(audit['ImplementedCodes']), 'Consumer units differ from audited guard')
    for effect in units.values():
        path, _, symbol = effect['consumer'].partition('#')
        require((root / path).is_file(), f'Missing consumer source: {path}')
        require(not symbol or symbol in (root / path).read_text(), f'Missing consumer symbol: {effect["consumer"]}')
    materials = {m['id']: m for m in manifest['materials']}
    require(len(materials) == len(manifest['materials']), 'Duplicate material')
    gear = {r['type']: r for r in json.loads((BALANCE / 'uo-equipment.json').read_text())}
    bases = {r['code']: r for r in json.loads((BALANCE / 'normal-bases.json').read_text())}
    affixes = {r['id']: r for r in json.loads((BALANCE / 'affixes.json').read_text())}
    observations = {r['Type']: r for r in audit['Items']}
    require(len(observations) == len(manifest['items']), 'Item audit has duplicate/missing types')
    items, source_rows = [], {}
    base_fields = ['name', 'code', 'type', 'level', 'levelreq', 'normcode', 'ubercode', 'ultracode',
                   'reqstr', 'reqdex', 'mindam', 'maxdam', 'minac', 'maxac', 'speed', 'block',
                   'StrBonus', 'DexBonus', 'durability', 'gemsockets', 'cost', 'gamble cost']
    for item in manifest['items']:
        name, code = item['type'], item['baseCode']
        definition, basis = gear[name], bases[code]
        material = materials[item['material']]
        expected_maker = 'Tinkering' if item['progression'] == 'jewellery' else material['maker']
        require(item['maker'] == expected_maker, f'Maker/material mismatch: {name}')
        require(definition['baseCode'] == code and definition['slot'] == item['slot']
                and definition['tier'] == item['baseTier'] == 'Normal', f'Runtime mapping drift: {name}')
        effective_slot = item.get('effectiveSlot', item['slot'])
        require(observations[name]['EffectiveSlot'] == effective_slot, f'Effective-slot drift: {name}')
        require(item['targetSocketCap'] == (0 if effective_slot in ['Ring', 'Amulet', 'Cosmetic'] else 5), f'Socket-policy drift: {name}')
        source_path = root / 'upstream/ModernUO/Projects/UOContent' / definition['source']
        require(source_path.is_file(), f'Missing UO source: {name}')
        table = 'Misc' if item['progression'] == 'jewellery' else 'Weapons' if item['slot'] == 'Weapon' else 'Armor'
        evidence = row_evidence(table, tables[table], 'code', code, base_fields)
        raw = evidence['fields']
        require(int(raw['level']) == basis['qualityLevel'] and int(raw['levelreq'] or 0) == basis['requiredLevel'], f'D2 level drift: {code}')
        for source_key, runtime_key in [('reqstr', 'strength'), ('reqdex', 'dexterity'), ('mindam', 'minDamage'),
                                        ('maxdam', 'maxDamage'), ('minac', 'minDefense'), ('maxac', 'maxDefense'),
                                        ('speed', 'speed'), ('block', 'block'), ('StrBonus', 'strengthBonus'), ('DexBonus', 'dexterityBonus')]:
            require(int(raw.get(source_key) or 0) == basis[runtime_key], f'D2 base field drift: {code}/{source_key}')
        source_rows[f'{table}:{code}'] = evidence
        if table != 'Misc':
            for column in ['ubercode', 'ultracode']:
                tier_code = raw[column]
                source_rows[f'{table}:{tier_code}'] = row_evidence(table, tables[table], 'code', tier_code, base_fields)
        items.append(dict(type=name, baseCode=code, uoSource=definition['source'], uoSourceSha256=digest(source_path),
                          current=basis, observation=observations[name],
                          target=dict(maker=item['maker'], material=item['material'], weight=item['targetWeight'],
                                      progression=item['progression'], socketCap=item['targetSocketCap'])))
    pool_keys, pools = set(), []
    for query in audit['Pools']:
        key = (query['BaseCode'], query['ItemLevel'], query['Rarity'])
        require(key not in pool_keys, f'Duplicate pool: {key}')
        pool_keys.add(key)
        require(query['Rarity'] in ['Magic', 'Rare'], 'Unsupported rarity in audit')
        rows = [affixes[id] for id in query['AffixIds']]
        require(len(rows) == len(set(query['AffixIds'])) and rows, f'Empty/duplicate affix pool: {key}')
        require(all(m['mode'] in ('Numeric', 'Parameter', 'DamageRange') and m['code'] in units and m['code'] != 'sock'
                    for row in rows for m in row['modifiers']), f'Unsupported compound effect: {key}')
        require(all(row['level'] <= query['AffixLevel'] and (not row['maxLevel'] or row['maxLevel'] >= query['AffixLevel'])
                    and (query['Rarity'] != 'Rare' or row['rare']) for row in rows), f'Level/rarity gate drift: {key}')
        pools.append(dict(baseCode=query['BaseCode'], itemLevel=query['ItemLevel'], affixLevel=query['AffixLevel'],
                          rarity=query['Rarity'], count=len(rows),
                          effectCodes=sorted({m['code'] for row in rows for m in row['modifiers']}),
                          maximumSingleAffixRequiredLevel=max(row['requiredLevel'] for row in rows)))
    expected_keys = {(i['baseCode'], level, rarity) for i in manifest['items']
                     for level in manifest['affixQueryItemLevels'] for rarity in ['Magic', 'Rare']}
    require(pool_keys == expected_keys, 'Missing or extra affix query')
    property_fields = ['code'] + [f'{prefix}{i}' for i in range(1, 8) for prefix in ['func', 'stat', 'val']]
    properties = []
    stats = set()
    for code in sorted(set(units) | {'balance1', 'balance2', 'balance3', 'mag%', 'gold%', 'lifesteal', 'manasteal'}):
        row = row_evidence('Properties', tables['Properties'], 'code', code, property_fields)
        properties.append(row)
        stats.update(value for key, value in row['fields'].items() if key.startswith('stat') and value)
    stat_rows = [row_evidence('ItemStatCost', tables['ItemStatCost'], 'Stat', stat,
                             ['Stat', 'ID', 'ValShift', 'Save Bits', 'Save Add', 'Save Param Bits']) for stat in sorted(stats)]
    used_tables = {r['table'] for r in source_rows.values()} | {'Properties', 'ItemStatCost', 'MagicPrefix', 'MagicSuffix', 'ItemTypes'}
    return dict(schemaVersion=1, status='authoring-reference-only', manifestSha256=digest(manifest_path),
                affixAudit=dict(path='tools/reference/first-loop-affix-audit.json', sha256=digest(audit_path)),
                sources={name: pins[name] for name in sorted(used_tables)},
                baseSourceRows=[source_rows[k] for k in sorted(source_rows)], propertySourceRows=properties,
                statEncodingSourceRows=stat_rows, items=items, pools=pools,
                guardCodesAbsentFromCompleteSpawnableAffixes=sorted(set(units) - set(audit['SpawnableCodes'])))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, required=True)
    parser.add_argument('--affix-audit', type=Path, default=REFERENCE / 'first-loop-affix-audit.json')
    parser.add_argument('--output', type=Path, default=REFERENCE / 'first-loop-evidence.json')
    parser.add_argument('--check', action='store_true', help='Check committed evidence without rewriting it')
    args = parser.parse_args()
    text = json.dumps(build(args.source_dir, args.affix_audit), indent=2) + '\n'
    if args.check:
        require(args.output.read_text() == text, 'Generated evidence differs; review and regenerate')
    else:
        args.output.write_text(text)
    print(f'{"Checked" if args.check else "Wrote"} bounded evidence: 8 items, 5 materials, 24 authoritative affix queries')


if __name__ == '__main__':
    main()
