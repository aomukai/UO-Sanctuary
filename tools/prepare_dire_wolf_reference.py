#!/usr/bin/env python3
"""Pin Dire Wolf and its separate corpse-rage route without enabling a handler."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(local_root, installation, check=False):
    beasts_path = ROOT / 'tools/reference/druid-beasts-preflight.json'
    beasts = json.loads(beasts_path.read_text())
    assert sha(installation / 'D2R.exe') == beasts['executableSha256']
    assert sha(installation / '.build.info') == beasts['buildInfoSha256']
    source_root = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((source_root / 'sources.json').read_text())
    assert manifest['commit'] == beasts['primaryDataCommit']
    rows, pins, compared, quoting = {}, {}, 0, []
    for table, filename, key, names in [
        ('skills', 'skills.txt', 'skill', ['Summon Fenris', 'fenris rage']),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', ['fenris']),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', ['fenris']),
        ('states', 'states-creeper-verified.txt', 'state', ['fenris_rage', 'corpse_nodraw']),
    ]:
        path = local_root / filename
        local = list(csv.DictReader(path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        cached_path = source_root / 'json' / (table + '.json')
        assert sha(cached_path) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(cached_path.read_text())
        rows[table] = {}
        for name in names:
            row = next(x for x in local if x[key] == name)
            prior = next(x for x in cached.values() if x.get(key) == name)
            rows[table][name] = {k: v for k, v in row.items() if k and v != ''}
            for field, value in prior.items():
                if field in row and field != 'lineNumber':
                    # Prior beast preflight documents this CSV quoting normalization.
                    text=str(value)
                    if table=='skills' and text.startswith('"') and text.endswith('"'):
                        quoting.append(dict(identity=name,field=field,cached=text,local=row[field],rule='Strip exactly one outer cached TSV quoting pair for comparison only.'));text=text[1:-1]
                    assert text == row[field], (table, name, field, value, row[field])
                    compared += 1
        pins[table] = dict(localFilename=filename, sha256=sha(path))
    summon, rage = rows['skills']['Summon Fenris'], rows['skills']['fenris rage']
    assert [summon[k] for k in ['srvdofunc', 'summon', 'pettype', 'petmax', 'calc1', 'calc2']] == ['119', 'fenris', 'fenris', 'min(lvl,par3)', '(par7 * lvl) + ln12', 'ulvl']
    assert [rage[k] for k in ['srvdofunc', 'aurastate', 'auralencalc', 'aurastat1', 'aurastatcalc1', 'Param1', 'Param2', 'TargetCorpse']] == ['137', 'fenris_rage', 'par1', 'damagepercent', 'par2', '500', '100', '1']
    assert rows['states']['fenris_rage']['*ID'] == '138' and rows['states']['corpse_nodraw']['*ID'] == '104'
    body = rows['monstats']['fenris']
    assert [body[k] for k in ['*hcIdx', 'BaseId', 'noRatio', 'minHP', 'AC', 'A1TH', 'DamageRegen', 'Skill1', 'Sk1mode', 'Sk1lvl']] == ['421', 'spiritwolf', '1', '216', '116', '150', '4', 'fenris rage', 'A1', '1']
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == beasts['legacyCommit']
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/AI/AiThink.cpp', ['AITHINK_Fn108_Fenris', 'AITHINK_Fn108_DruidWolf']),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo119_DruidSummon', 'SKILLS_SrvDo137_FenrisRage', 'SKILLS_FindUseableCorpse', 'sub_6FCFE0E0']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD10250']),
        ('source/D2Common/src/UselessOrdinals.cpp', ['D2COMMON_11018']),
        ('source/D2Common/src/D2Skills.cpp', ['D2Common_11020', 'SKILLS_CanUnitCorpseBeSelected']),
        ('source/D2Common/src/Units/Units.cpp', ['UNITS_IsSoftMonster', 'UNITS_IsCorpseUseable']),
    ]:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            routines[name] = hashlib.sha256(function(raw.decode(), name).encode()).hexdigest()
    result = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        scope='Dire Wolf/Fenris source preparation only; no new actor, corpse consumer or modern behavioral certificate.',
        executableSha256=beasts['executableSha256'], buildInfoSha256=beasts['buildInfoSha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], beastPreflightSha256=sha(beasts_path),
        localTables=pins, currentRows=rows, corroboratedFields=compared, cachedQuotingNormalizations=quoting, sourceHashes=hashes, reviewedRoutineHashes=routines,
        established=[
            'Current player237/do119 uses source fenris,its own capmin(rank,3),explicit owner level and effective-rank wolf/bear synergies. Body421has216life/116defense/150AR,noRatio,54FP regeneration before birth scaling and the shared WF animation family. BaseId spiritwolf does not make this the cold-only Spirit Wolf species.',
            'NPC314/do137 consumes a selectable soft monster corpse,sets corpse_nodraw104and refreshes it before attempting a self buff. Current fenris_rage138uses500frames and damagepercent100; its overlay is direwolfcharged. The routine contains no direct life or mana write.',
            'AI corpse search uses the first returned usable corpse plus its separate interaction predicate; it excludes DEADCOL but does not add the release soft flag. Release11018aliases11020,which combines selectable corpse and SOFT. These predicates must remain distinct.',
            'Buff wrapper allocates a fresh flag2stat list with current frame+duration,owner type/GUID,state/removal callback/event and the generic six-stat writer. do137returns success after consuming the corpse even if that later allocation fails; no invented consumption rollback is established.',
        ], remaining=[
            'Compile/compare full Fenris AI,corpse search/release/buff compositions and physical damage getters before native integration. Rooms,metadata/soft/selectable/udead,free positions and source event/list order need explicit services or adapters.',
            'Reuse Spirit Wolf birth/motion/recovery/accuracy/blocking/hit timing where source agrees; keep physical damage/rage,cap3,source421/237and distinct Fenris target/AI policy. Exact modern busy-A1/slowed timing and inherited physical sunder remain separate gates.',
            'Observed chill exclusion is keyed to actual source spiritwolf,not shared BaseId; user confirmed Dire Wolf and Grizzly can chill/freeze. Existing summons rescale with owner facets while preserving life percentage.',
            'Learned SC/payment/input/replacement,live versus birth/gear stats,corpse concurrency/loot preservation,owner/offline/save/client behavior and actual current-executable acceptance remain unfinished. No class complete.',
            'Final Dire Wolf body/emergence/walk/run/bite/critical/teleport/hurt/death,corpse-eating/direwolfcharged rage/expiry/audio are effects work; talent/right/quick icons remain separate. The internal rage skill does not require a separate talent or hotkey icon.',
        ])
    path = ROOT / 'tools/reference/dire-wolf-preflight.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Dire Wolf preparation:{compared}current fields/{len(routines)}reviewed routines; source corpse search/release differ; handler disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
