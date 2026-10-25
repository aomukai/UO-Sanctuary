#!/usr/bin/env python3
"""Execute complete older monster-stat/ratio bodies with current corpse HP data."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
model = json.loads((ROOT / 'tools/reference/d2r-local-sentry-model-evidence.json').read_text())
level_proof = json.loads((ROOT / 'tools/reference/d2r-local-monlvl-evidence.json').read_text())
paths = [ROOT / model['files']['monstats']['path'], ROOT / level_proof['file']['path']]
for path, digest in zip(paths, [model['files']['monstats']['sha256'], level_proof['file']['sha256']]):
    assert hashlib.sha256(path.read_bytes()).hexdigest() == digest
monsters = {r['Id']: r for r in csv.DictReader(paths[0].read_text(encoding='utf-8-sig').splitlines(), delimiter='\t') if r['Id']}
levels = list(csv.DictReader(paths[1].read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
assert len(levels) == 111 and [int(r['Level']) for r in levels] == list(range(111))
rules_path = ROOT / 'data/Sanctuary/Balance/corpse-source-rules.json'
ids = sorted(r['source'] for r in json.loads(rules_path.read_text()) if r['selectable'])
assert len(ids) == 521 and all(key in monsters for key in ids)
pin_path = ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json'
pin_raw = pin_path.read_bytes()
manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
assert hashlib.sha256(pin_raw).hexdigest() == manifest['files']['json/monstats.json']['sha256']
pinned = json.loads(pin_raw)
hp_fields = ['noRatio', 'minHP', 'maxHP', 'MinHP(N)', 'MaxHP(N)', 'MinHP(H)', 'MaxHP(H)']
compared = 0
for key in ids:
    for field in hp_fields:
        actual = monsters[key][field]
        if field in pinned[key]:
            assert actual == str(pinned[key][field]), (key, field, actual, pinned[key][field])
            compared += 1
        else:
            assert actual in ('', '0'), (key, field, actual)
assert compared == 2991
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
hashes, routine_hashes = {}, {}


def block(relative, name, enum=False):
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    hashes[relative] = hashlib.sha256(raw).hexdigest()
    text = raw.decode()
    pattern = r'^.*\b' + name + (r'\s*\n\{' if enum else r'\([^;\n]*\)\s*\n\{')
    m = re.search(pattern, text, re.M)
    assert m, name
    end, depth = text.index('{', m.start()) + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    body = text[m.start():end] + (';' if enum else '')
    routine_hashes[name] = hashlib.sha256(body.encode()).hexdigest()
    return body + '\n'


cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int TRUE=1,FALSE=0;
uint32_t gdwBitMasks[32];
struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;int nMinHP[3]={},nMaxHP[3]={},nAC[3]={},nExp[3]={},nA1TH[3]={},nA1MinD[3]={},nA1MaxD[3]={},nA2TH[3]={},nA2MinD[3]={},nA2MaxD[3]={},nS1TH[3]={},nS1MinD[3]={},nS1MaxD[3]={},nElMinD[3][3]={},nElMaxD[3][3]={},nElDur[3][3]={};}mon;
struct D2MonLvlTxt{int dwHP[6]={},dwAC[6]={},dwXP[6]={},dwTH[6]={},dwDM[6]={};};
struct Tables{int nMonLvlTxtRecordCount=111;D2MonLvlTxt pMonLvlTxt[111];}tables;auto* sgptDataTables=&tables;
struct D2MonStatsInitStrc{int nMinHP=0,nMaxHP=0,nAC=0,nExp=0,nTH=0,nA1MinD=0,nA1MaxD=0,nA2MinD=0,nA2MaxD=0,nS1MinD=0,nS1MaxD=0,nElMinD=0,nElMaxD=0,nElDur=0;};
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int id){assert(id==0);return &mon;}
'''
cpp += block('source/D2Common/include/DataTbls/MonsterTbls.h', 'D2C_MonStatsTxtFlagIndices', True)
for name in ['DATATBLS_ApplyRatio', 'DATATBLS_CalculateMonsterStatsByLevel']:
    cpp += block('source/D2Common/src/DataTbls/MonsterTbls.cpp', name)
hp_columns = ['HP', 'HP(N)', 'HP(H)', 'L-HP', 'L-HP(N)', 'L-HP(H)']
hp = [[int(row[c] or 0) for c in hp_columns] for row in levels]
source_rows = [[int(monsters[key][field] or 0) for field in hp_fields] for key in ids]
cpp += 'int hp[][6]={' + ','.join('{' + ','.join(map(str, row)) + '}' for row in hp) + '};\n'
cpp += 'int monsters[][7]={' + ','.join('{' + ','.join(map(str, row)) + '}' for row in source_rows) + '};\n'
cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
for(int i=0;i<111;i++)for(int j=0;j<6;j++)tables.pMonLvlTxt[i].dwHP[j]=hp[i][j];
std::printf("[");int count=0;
for(int id=0;id<521;id++)for(int game:{0,1})for(int difficulty:{-1,0,1,2,3})for(int level:{-1,0,1,3,20,50,99,110,111,999}){
 mon.dwMonStatsFlags=monsters[id][0]?gdwBitMasks[MONSTATSFLAGINDEX_NORATIO]:0;
 for(int i=0;i<3;i++){mon.nMinHP[i]=monsters[id][1+2*i];mon.nMaxHP[i]=monsters[id][2+2*i];}
 D2MonStatsInitStrc out;int result=DATATBLS_CalculateMonsterStatsByLevel(0,game,difficulty,level,1,&out);
 int64_t base=(int64_t(out.nMinHP)+out.nMaxHP)*128;
 assert(base>=0&&base<=INT32_MAX);
 std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%lld]",count?",":"",id,game,difficulty,level,result,out.nMinHP,out.nMaxHP,(long long)base);++count;
}std::printf("]\n");}
'''
source = STAGE / 'corpse-base-life-source-probe.cpp'
source.write_text(cpp)
binary = source.with_suffix('')
subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
run = subprocess.run([str(binary)], capture_output=True)
(STAGE / 'corpse-base-life-sanitizer.log').write_bytes(run.stderr)
assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
cases = json.loads(run.stdout)
assert len(cases) == 52100
fixture = STAGE / 'corpse-base-life-source-probe.json'
fixture.write_text(json.dumps(dict(ids=ids, monsterInputs=source_rows, levelHp=hp, cases=cases), separators=(',', ':')) + '\n')
evidence = dict(
    referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=routine_hashes,
    currentMonstatsEvidence='tools/reference/d2r-local-sentry-model-evidence.json', currentMonlvlEvidence='tools/reference/d2r-local-monlvl-evidence.json',
    currentHpFieldsCompared=compared, corpseSourceRows=len(ids),
    inputHashes={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in [*paths, pin_path, rules_path]},
    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(cases)),
    established=[
        'All2991nonmetadata active HP/noRatio fields for521currently selectable corpse source rows match the pinned JSON; omitted fields have no extra active local values.',
        'Complete unchanged monster-stat calculator and ratio helper execute flags1 over all521rows, classic/expansion columns, five difficulty clamp contexts and ten level contexts, including negative/zero/over-limit levels.',
        'Corpse base life is (calculated minHP+maxHP)*128. No spawned-life roll, elite/player-count bonus or native maximum-life cap is substituted. Source level above110 and difficulty outside0..2 clamp; negative level returns failure.'
    ],
    limits=[
        'Current text inputs and complete older calculator bodies are composed through declared row/layout services, not a modern executable or binary-table ABI certificate.',
        'Only flags1 HP lanes execute; other complete stat-calculator branches are retained but not claimed covered. Row lookup is an explicit service, including the synthetic row id0.',
        'The list follows current corpse-source rules; actual world selection, native profile/facet/level mapping, corpse snapshot persistence, RNG and damage remain separate.'
    ], sanitizers='ASan/UBSan exit0 with empty runtime stderr required', handlerEnabled=False)
path = ROOT / 'tools/reference/corpse-base-life-source-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print('Corpse base life:521current source rows/2991fields;52100complete flags1 calculator/ratio contexts; clean sanitizers. Native snapshot bridge remains separate.')
