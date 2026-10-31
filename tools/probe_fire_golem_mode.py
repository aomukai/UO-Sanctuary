#!/usr/bin/env python3
"""Execute the unchanged raw Fire Golem mode writer; pin current formula symbols."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/fire-golem-mode-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(cache, local_root, reader, check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    installation = json.loads((ROOT / 'tools/reference/d2r-local-installation-evidence.json').read_text())
    location = Path(installation['installationPath'])
    assert sha(location / 'D2R.exe') == installation['executable']['sha256']
    assert sha(location / '.build.info') == installation['buildInfo']['sha256']
    reader_proof = json.loads((ROOT / 'tools/reference/d2r-local-monmode-evidence.json').read_text())
    assert sha(reader.with_suffix('.cpp')) == reader_proof['reader']['sourceSha256']
    archive = 'data:data/global/excel/skillcalc.txt'
    symbols_path = STAGE / 'skillcalc.txt'
    run = subprocess.run([str(reader), str(location), archive, str(symbols_path)], capture_output=True)
    assert run.returncode == 0, run.stderr.decode()
    symbols = list(csv.DictReader(symbols_path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    symbol_rows = {name: next(i for i, row in enumerate(symbols) if row['code'] == name)
                   for name in ['edmn', 'edmx', 'edns', 'edxs']}
    assert symbol_rows == dict(edmn=17, edmx=18, edns=38, edxs=39)

    tree = json.loads((cache / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    sources, routines = {}, {}

    def read(relative):
        raw = (cache / 'd2moo-pinned-source' / relative).read_bytes()
        blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
        assert blob == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    prior = json.loads((ROOT / 'tools/reference/beast-mode-stream-engine-evidence.json').read_text())
    parent = cache / 'beast-mode-stream-source-probe.cpp'
    assert sha(parent) == prior['fixture']['generatedSourceSha256']
    cpp = parent.read_text().split('\nvoid prepare(int species)', 1)[0]
    for relative, names in [
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage', 'SKILLS_GetToHitFactor']),
        ('source/D2Common/src/DataTbls/MonsterTbls.cpp', ['DATATBLS_CalculateMonsterStatsByLevel']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD14D20']),
        ('source/D2Game/src/MONSTER/MonsterMode.cpp', ['sub_6FC627B0']),
    ]:
        text = read(relative)
        for name in names:
            body = function(text, name)
            assert cpp.count(body) == 1, name
            routines[name] = hashlib.sha256(body.encode()).hexdigest()

    common = read('source/D2Common/src/D2Skills.cpp')
    blocks = {}
    for index, expression in [(17, 'SKILLS_GetMinElemDamage(pUnit, nSkillId, nSkillLevel, 0) >> 8'),
                              (18, 'SKILLS_GetMaxElemDamage(pUnit, nSkillId, nSkillLevel, 0) >> 8'),
                              (38, 'SKILLS_GetMinElemDamage(pUnit, nSkillId, nSkillLevel, 0)'),
                              (39, 'SKILLS_GetMaxElemDamage(pUnit, nSkillId, nSkillLevel, 0)')]:
        block = f'case {index}:\n\t\t\treturn {expression};'
        assert block in common
        blocks[str(index)] = hashlib.sha256(block.encode()).hexdigest()
    damage = read('source/D2Game/src/UNIT/SUnitDmg.cpp')
    fill = function(damage, 'SUNITDMG_FillDamageValues')
    for stat in ['STAT_FIREMINDAM', 'STAT_FIREMAXDAM']:
        assert f'STATLIST_UnitGetStatValue(pAttacker, {stat}, 0) << 8' in fill

    reference = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((reference / 'sources.json').read_text())
    tables, local_pins = {}, {}
    for table, filename, key in [('monstats', 'monstats-sentries-verified.txt', 'Id'), ('skills', 'skills.txt', 'skill')]:
        path = reference / 'json' / (table + '.json')
        assert sha(path) == manifest['files']['json/' + table + '.json']['sha256']
        tables[table] = json.loads(path.read_text())
        cached = tables[table]['firegolem' if table == 'monstats' else '94']
        local_path = local_root / filename
        local = next(r for r in csv.DictReader(local_path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t')
                     if r[key] == cached[key])
        for field, value in cached.items():
            if field in local and field != 'lineNumber':
                assert str(value).strip('"') == local[field].strip('"'), (table, field)
        local_pins[table] = dict(filename=filename, localSha256=sha(local_path), cachedSha256=sha(path))
    row = tables['monstats']['firegolem']
    assert row['noRatio'] == row['Align'] == 1 and row.get('SkillDamage', '') == ''
    assert row['El1Mode'] == 'A1' and row['El1Type'] == 'fire'
    assert row['El1MinD'] == 5 and row['El1MaxD'] == 10
    assert all(row.get(f'El{i}Mode', '') == '' for i in [2, 3])

    old_rng = 'unsigned ITEMS_RollRandomNumber(int*){__builtin_trap();}'
    assert cpp.count(old_rng) == 1
    cpp = cpp.replace(old_rng, 'unsigned sample=0;int draws=0;unsigned ITEMS_RollRandomNumber(int*){draws++;return sample;}')
    cpp += '\nvoid prepare(){mon={};skill={};mon.wSkillDamage=0;\n'
    for member, field in [('nA1TH', 'A1TH'), ('nA1MinD', 'A1MinD'), ('nA1MaxD', 'A1MaxD'),
                          ('nA2TH', 'A2TH'), ('nA2MinD', 'A2MinD'), ('nA2MaxD', 'A2MaxD'),
                          ('nS1TH', 'S1TH'), ('nS1MinD', 'S1MinD'), ('nS1MaxD', 'S1MaxD')]:
        for d, suffix in enumerate(['', '(N)', '(H)']):
            cpp += f'mon.{member}[{d}]={row.get(field + suffix, 0)};\n'
    cpp += 'mon.nElMode[0]=MONMODE_ATTACK1;mon.nElType[0]=ELEMTYPE_FIRE;\n'
    for d, suffix in enumerate(['', '(N)', '(H)']):
        for member, field in [('nElMinD', 'El1MinD'), ('nElMaxD', 'El1MaxD'), ('nElPct', 'El1Pct'), ('nElDur', 'El1Dur')]:
            cpp += f'mon.{member}[0][{d}]={row.get(field + suffix, 0)};\n'
    cpp += r'''}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int diff:{0,1,2})for(int expansion:{0,1})for(int mode=0;mode<16;mode++)
for(unsigned roll:{0u,79u,80u,99u,4294967295u})for(int control=0;control<=5;control++){
prepare();pet={};owner={};monsterData={};pet.dwClassId=292;pet.stats[STAT_LEVEL]=99;
game.nDifficulty=diff;game.nGameType=1;game.dwGameType=0;game.bExpansion=expansion;
list={};list.stats[STAT_MINDAMAGE]=123;list.stats[STAT_MAXDAMAGE]=456;list.stats[STAT_TOHIT]=789;
list.stats[STAT_FIREMINDAM]=1234;list.stats[STAT_FIREMAXDAM]=5678;
removed=writes=ownerQueries=rankQueries=draws=0;sample=roll;listPresent=control!=5;
if(control==2)pet.dwUnitType=UNIT_PLAYER;if(control==3)pet.pMonsterData=nullptr;if(control==4)monsterData.pMonstatsTxt=nullptr;
sub_6FC627B0(control==1?nullptr:&pet,mode);
printf("%s[[%d,%d,%d,%u,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",diff,expansion,mode,roll,control,
list.stats[STAT_MINDAMAGE],list.stats[STAT_MAXDAMAGE],list.stats[STAT_TOHIT],list.stats[STAT_FIREMINDAM],list.stats[STAT_FIREMAXDAM],
removed,writes,draws,ownerQueries,rankQueries);first=false;}
printf("]\n");}
'''
    path = STAGE / 'probe.cpp'
    path.write_text(cpp)
    run = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(run.stdout + run.stderr)
    assert run.returncode == 0, run.stderr.decode()
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()
    cases = json.loads(run.stdout)
    assert len(cases) == 2880
    for (diff, expansion, mode, roll, control), output in cases:
        if control:
            expected = [123, 456, 789, 1234, 5678, 0, 0, 0, int(control == 5), 0]
        else:
            lane = 'A2' if mode == 5 else 'S1' if mode in [6, 7, 8] else 'A1'
            physical = [row.get(lane + k + ['', '(N)', '(H)'][diff], 0) for k in ['MinD', 'MaxD', 'TH']]
            # The unchanged noRatio helper writes nS1Min/MaxD, leaving the
            # nElMin/MaxD fields read by the mode writer zero. Do not repair it.
            active = mode == 4
            expected = [*physical, 0, 0, 1, 3 + (2 if active and roll % 100 < 80 else 0), int(active), 1, 0]
        assert output == expected, (diff, expansion, mode, roll, control, output, expected)
    (STAGE / 'cases.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], runtimeChanged=False, probeSha256=sha(Path(__file__)),
                  ownedSymbols=dict(archivePath=archive, sha256=sha(symbols_path), size=symbols_path.stat().st_size,
                                    rows=symbol_rows, readerSourceSha256=sha(reader.with_suffix('.cpp')),
                                    readerBinarySha256=sha(reader), installationEvidence='tools/reference/d2r-local-installation-evidence.json'),
                  localTables=local_pins, sourceHashes=sources, unchangedRoutineHashes=routines,
                  reviewedCallbackBlocks=blocks, reviewedFillDamageValuesSha256=hashlib.sha256(fill.encode()).hexdigest(),
                  reusedFixtureSha256=sha(parent), cases=len(cases), ordinaryCases=480, guardCases=2400,
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, STAGE / 'cases.json', STAGE / 'compiler.log', STAGE / 'sanitizer.log']},
                  established=[
                      'Owned current skillcalc rows17/18 map edmn/edmx;rows38/39 map edns/edxs. The reviewed legacy callbacks shift17/18right8;FillDamageValues shifts accrued fire stats left8. Existing Fire birth scalar endpoints are consistent with that legacy ordinary-hit unit chain.',
                      'The complete unchanged raw mode writer and monster noRatio helper execute480ordinary and2400guard contexts. Actual Fire monstats,all16raw modes,all3difficulties,classic/expansion controls and80percent random boundaries are supplied. A1consumes one raw RNG draw even when the optional fire range is zero;other modes consume none.',
                      'The complete legacy noRatio helper puts optional elemental endpoints into S1fields instead of the elemental fields subsequently read by the caller. All tested Fire optional fire outputs therefore remain zero,including successful Normal80percent selections. No repair is applied to manufacture a result.'],
                  remaining=[
                      'Current executable optional-monster-element behavior is uncertain. Do not infer either an extra5..10fire or zero extra fire for3.3.93847from the tables and old implementation alone.',
                      'Raw mode requests are direct controls,not proof every tested mode is reachable. Layout,stat lists,random draw values,game and skill lookup are declared services. This does not certify modern bytecode dispatch,stat aggregation,full mode entry or seed allocation.',
                      'No native actor or runtime data changes. Fire unarmed/critical/contact,G3clock,Holy Fire weapon/pulse and delayed death/replacement explosion still need implementation/acceptance. Baseline classes remain unfinished;pet upgrades remain a later hard-point pass.'])
    destination = ROOT / 'tools/reference/fire-golem-mode-source-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Fire raw mode:480ordinary/2400guard contexts pass ASan+UBSan;current optional-fire discrepancy retained.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache-root', type=Path, required=True)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--reader', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.cache_root, args.local_root, args.reader, args.check)
