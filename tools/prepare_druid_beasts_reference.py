#!/usr/bin/env python3
"""Corroborate wolf/bear rows and animation against the pinned local installation."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(local_root, installation, check=False):
    prior = json.loads((ROOT / 'tools/reference/d2r-local-installation-evidence.json').read_text())
    assert sha(installation / 'D2R.exe') == prior['executable']['sha256']
    assert sha(installation / '.build.info') == prior['buildInfo']['sha256']
    reference = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((reference / 'sources.json').read_text())
    assert manifest['commit'] == 'fa1286949266498b294c40c2942521a7c0d7207a'
    requests = [
        ('skills', 'skills.txt', 'skill', [('227', 'Summon Spirit Wolf'), ('237', 'Summon Fenris'), ('247', 'Summon Grizzly')]),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', [(n, n) for n in ['spiritwolf', 'fenris', 'druidbear']]),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', [(n, n) for n in ['spiritwolf', 'fenris', 'druidbear']])]
    tables, pins, quoting, corroborated = {}, {}, [], 0
    for table, filename, key, identities in requests:
        path = reference / 'json' / (table + '.json')
        assert sha(path) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(path.read_text())
        local = list(csv.DictReader((local_root / filename).read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        pins[table] = dict(archivePath='data:data/global/excel/' + table + '.txt', localFilename=filename,
                           sha256=sha(local_root / filename), cachedSha256=sha(path))
        tables[table] = {}
        for cached_key, identity in identities:
            row = next(x for x in local if x[key] == identity)
            tables[table][identity] = {k: v for k, v in row.items() if k and v != ''}
            for field, value in cached[cached_key].items():
                if field == 'lineNumber' or field not in row:
                    continue
                compared = str(value)
                if table == 'skills' and compared.startswith('"') and compared.endswith('"'):
                    quoting.append(dict(identity=identity, field=field, cached=compared, local=row[field],
                                        rule='Strip exactly one outer cached TSV-quoting pair for comparison only.'))
                    compared = compared[1:-1]
                assert compared == row[field], (table, identity, field, compared, row[field])
                corroborated += 1
    for name, monster, mana, cap in [('Summon Spirit Wolf', 'spiritwolf', 15, 'min(lvl,par3)'),
                                    ('Summon Fenris', 'fenris', 20, 'min(lvl,par3)'),
                                    ('Summon Grizzly', 'druidbear', 40, '1')]:
        row = tables['skills'][name]
        assert [row[k] for k in ['srvdofunc', 'summon', 'mana', 'petmax', 'calc2', 'InTown', 'leftskill', 'rightskill']] == ['119', monster, str(mana), cap, 'ulvl', '1', '0', '1']
        assert tables['monstats'][monster]['noRatio'] == '1'
        assert tables['monstats2'][monster]['isAtt'] == tables['monstats2'][monster]['isSel'] == '1'
    animation_path = local_root / 'AnimData.d2'
    assert sha(animation_path) == json.loads((ROOT / 'tools/reference/d2r-local-animation-evidence.json').read_text())['sha256']
    raw, offset, animations = animation_path.read_bytes(), 0, []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0]; offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith(('WF', 'B7')):
                animations.append(dict(code=code, frames=frames, speed=speed,
                                       events=[[i, v] for i, v in enumerate(raw[offset + 16:offset + 160]) if v]))
            offset += 160
    assert offset == len(raw) and animations
    stage = ROOT / '.local/class-skills'
    tree = json.loads((stage / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo119_DruidSummon']),
        ('source/D2Game/src/AI/AiThink.cpp', ['AITHINK_Fn108_DruidWolf', 'AITHINK_Fn108_SpiritWolf', 'AITHINK_Fn108_Fenris', 'AITHINK_Fn112_DruidBear']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD14D20', 'D2GAME_SummonPet_6FD14430'])]:
        data = (stage / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(data).hexdigest(); text = data.decode()
        for name in names:
            match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
            assert match, name
            end, depth = text.index('{', match.start()) + 1, 1
            while depth:
                depth += (text[end] == '{') - (text[end] == '}'); end += 1
            routines[name] = hashlib.sha256(text[match.start():end].encode()).hexdigest()
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, scope='Current tables/animation plus bounded older source preflight; no wolf/bear actor acceptance.',
                    executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
                    primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins, currentRows=tables,
                    corroboratedFields=corroborated, cachedQuotingNormalizations=quoting,
                    animation=dict(sha256=sha(animation_path), records=animations, scope='Raw metadata only; animation/mode/skill/current timing must be composed.'),
                    sourceHashes=hashes, reviewedRoutineHashes=routines,
                    userObservation=dict(date='2026-10-27', referenceVersion='3.3.93847',
                                         spiritEffectiveRank=28, spiritHardRank=20, direEffectiveRank=28, direHardRank=20,
                                         grizzlyEffectiveRank=28, grizzlyHardRank=20, mana=15, life=1040,
                                         coldMinimum=343, coldMaximum=359, maximumWolves=5,
                                         tooltipAttackRatingPercent=725, tooltipDefensePercent=320,
                                         reportedSynergyLabels=dict(direLifePercentPerRank=15, grizzlyDamagePercentPerRank=10),
                                         recastTargeting='Existing wolves keep their current targets when another Spirit Wolf is cast on a different monster; wolves act individually.',
                                         scope='User existing-character tooltip and confirmation of item-bonus wolf/bear synergies. Per-rank labels do not replace formula baseline terms or establish runtime stat consumers. Raven remains the explicit .blvl exception in the pinned row.'),
                    established=[
                        'All three use do119/right-only SC/town-enabled casting, explicit owner-level calc2, shared summon allocation, explicit-level base helper and shared passives. Spirit/Dire caps are5/3; bear cap1. Mana is15/20/40. Unlike Creeper, the explicit base helper is called and can reuse the verified Valkyrie numeric route.',
                        'Spirit Wolf is current cold damage, not a copied physical skeleton packet. Its six current passives include cold endpoints, cold duration and cold sunder; the older five-slot passive body cannot establish slot6. Wolf/bear synergy expressions use effective .lvl and must not be silently converted to hard-only synergies.',
                        'All monster bases are noRatio1. Current Spirit HP130 and Dire HP216 are equal across difficulties. Current bear HP is750Normal but650Nightmare/Hell; approved facet rescale must preserve current life percentage rather than assuming identical bases.',
                        'Legacy shared do119 explicitly validates target coordinates before allocation; missing target and allocation failure are separate returns. The shared owner-skill damage refresh reads physical endpoints/to-hit from current owner skill, while Spirit cold passives are a separate captured path.',
                        'Wolf AI dispatch distinguishes Spirit and Fenris. Dire corpse consumption/rage and bear sequence skill are distinct from ordinary attacks and cannot be replaced by a generic minion attack without recording that unsupported branch.'
                    ], remaining=[
                        'Compose complete creation/base/passive data including current sixth passive and captured versus live owner stats. Verify exact resistance, AR/defense percentage consumers, cold length/mastery and nonzero sunder before enabling unsupported routes.',
                        'Implement Spirit Wolf first, retaining source-derived target/follow/teleport/mode timing and shared persistent minion/facet/health rules. Keep other summon categories coexisting according to current pettype data; verify cap/replacement before allocation/payment.',
                        'Verify actual body/attacks/ownership/gear/resources/input/lifecycle/save reloads in a disposable world, then full candidate/installation acceptance. No handler count changes are justified by this preflight.',
                        'Keep Dire corpse behavior, Grizzly BearSmite, exact modern AI/global RNG/geometry/item/PvP/client and final effects/audio/icons explicit and nonblocking for unaffected work.'
                    ])
    path = ROOT / 'tools/reference/druid-beasts-preflight.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Druid beasts preflight:{corroborated}current fields,{len(animations)}animation records,{len(routines)}legacy routines; no handlers enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True); parser.add_argument('--check', action='store_true')
    args = parser.parse_args(); build(args.local_root, args.installation, args.check)
