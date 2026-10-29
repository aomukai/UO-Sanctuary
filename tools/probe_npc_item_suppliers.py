#!/usr/bin/env python3
"""Check recipient-level item operations against full source and owned tables."""
import argparse
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'npc-item-suppliers-source'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    treepath = BASE / 'd2moo-source-tree.json'; tree = json.loads(treepath.read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(treepath.relative_to(ROOT)): sha(treepath)}
    def source(name):
        p = BASE / 'd2moo-pinned-source' / name; raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        inputs[str(p.relative_to(ROOT))] = sha(p); return raw.decode()
    stat = source('source/D2Common/src/D2StatList.cpp')
    header = source('source/D2Common/include/DataTbls/ItemsTbls.h')
    assert re.search(r'ITEMSTATCOSTFLAGINDEX_SEND_OTHER\s*=\s*0,\s*ITEMSTATCOSTFLAGINDEX_SIGNED,\s*ITEMSTATCOSTFLAGINDEX_DAMAGERELATED', header)
    installationpath = ROOT / 'tools/reference/d2r-local-installation-evidence.json'
    installation = json.loads(installationpath.read_text()); assert installation['referenceVersion'] == '3.3.93847'
    animationpath = ROOT / 'tools/reference/d2r-local-animation-evidence.json'; animation = json.loads(animationpath.read_text())
    reader = BASE / 'warlock-foundation/casc-reference-reader'
    assert sha(reader.with_suffix('.cpp')) == animation['reader']['sourceSha256']
    game = Path(installation['installationPath']); exe = game / installation['executable']['filename']; info = game / '.build.info'
    owned = {}; tables = {}; owned_files = []
    directory = ROOT / 'tools/reference/d2r-named-equipment'; manifest = json.loads((directory / 'sources.json').read_text())
    for name, key in [('properties', 'code'), ('itemstatcost', 'Stat')]:
        p = STAGE / (name + '.txt')
        if not p.exists():
            assert not check, 'Missing preserved owned table: ' + name
            assert sha(exe) == installation['executable']['sha256'] and sha(info) == installation['buildInfo']['sha256']
            subprocess.run([str(reader), str(game), 'data:data/global/excel/' + name + '.txt', str(p)], capture_output=True, check=True)
            assert sha(exe) == installation['executable']['sha256'] and sha(info) == installation['buildInfo']['sha256']
        owned[name] = {r[key]: r for r in csv.DictReader(p.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t') if r.get(key)}
        owned_files.append(p)
        p = directory / 'json' / (name + '.json'); assert sha(p) == manifest['files']['json/' + name + '.json']['sha256']
        inputs[str(p.relative_to(ROOT))] = sha(p); tables[name] = json.loads(p.read_text())
    runtime = ROOT / 'src/Sanctuary.Content/NamedPropertyRuntime.cs'; text = runtime.read_text()
    block = text[text.index('_scaled ='):text.index('public static bool Supported')]
    mapping = re.findall(r'\["([^"\n]+)"\] = "([^"\n]+)"', block); assert len(mapping) == 27
    profiles = []; compared = 0
    for code, target in mapping:
        prop = tables['properties'][code]; local = owned['properties'][code]
        for field in ['func1', 'stat1']:
            assert str(prop[field]) == local[field], (code, field); compared += 1
        statrow = tables['itemstatcost'][prop['stat1']]; local = owned['itemstatcost'][prop['stat1']]
        for field in ['op', 'op param', 'op base', 'op stat1']:
            assert str(statrow[field]) == local[field], (code, field); compared += 1
        assert int(statrow.get('ValShift', 0)) == int(local.get('ValShift') or 0), (code, 'ValShift'); compared += 1
        shift = statrow['op param']; assert shift == (1 if code in ['att/lvl', 'att%/lvl', 'att-dem/lvl', 'att-und/lvl'] else 3)
        assert prop['func1'] == 17 and statrow['op base'] == 'level' and statrow['op'] in [2, 4, 5]
        profiles.append(dict(Code=code, Target=target, SourceStat=prop['stat1'], Operation=statrow['op'], Shift=shift, ValueShift=statrow.get('ValShift', 0)))
    assert compared == 189
    cpp = r'''
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <map>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,STAT_MAXSTAMINA=11,ITEMSTATCOSTFLAGINDEX_DAMAGERELATED=2;
constexpr unsigned gdwBitMasks[3]={1,2,4};
struct D2SLayerStatIdStrc{using PackedType=uint32_t;uint32_t nPackedValue=0;uint16_t nStat=0;static D2SLayerStatIdStrc MakeFromStatId(int n){return{uint32_t(n),uint16_t(n)};}static D2SLayerStatIdStrc FromPackedType(uint32_t n){return{n,uint16_t(n)};}};
struct D2StatStrc{int nValue=0;};struct D2StatsArrayStrc{std::map<uint32_t,D2StatStrc> values;};
struct D2UnitStrc;
struct D2StatListStrc{D2StatsArrayStrc Stats,FullStats;D2UnitStrc*pOwner=nullptr,*pUnit=nullptr;int dwOwnerType=4;D2StatListStrc*pMyLastList=nullptr,*pPrevLink=nullptr;unsigned dwFlags=0;bool extended=true;};using D2StatListExStrc=D2StatListStrc;
struct D2UnitStrc{int dwUnitType=4,dwClassId=0;D2StatListExStrc*pStatListEx=nullptr;void*pDrlgAct=nullptr;};
struct D2OpStatDataStrc{uint16_t nOpBase=12,nStat=55;uint8_t nOp=0,nOpParam=3;};
struct D2ItemStatCostTxt{unsigned dwItemStatFlags=0;bool bHasOpStatData=false;D2OpStatDataStrc pOpStatData[6];int nValShift=0;};std::map<int,D2ItemStatCostTxt> rows;
struct D2CharStatsTxt{int nManaPerMagic=4,nStaminaPerVitality=4,nLifePerVitality=16;};
bool STATLIST_IsExtended(D2StatListStrc*p){return p->extended;}
D2ItemStatCostTxt*ITEMS_GetItemStatCostTxtRecord(int n){auto it=rows.find(n);return it==rows.end()?nullptr:&it->second;}
int Value(D2StatsArrayStrc&a,uint32_t n){auto it=a.values.find(n);return it==a.values.end()?0:it->second.nValue;}
int STATLIST_GetBaseStat_6FDB6340(D2StatListStrc*p,uint32_t n,D2ItemStatCostTxt*){return Value(p->Stats,n);}
int STATLIST_GetTotalStat_6FDB63E0(D2StatListStrc*p,uint32_t n,D2ItemStatCostTxt*){return Value(p->extended?p->FullStats:p->Stats,n);}
D2StatStrc*STATLIST_FindStat_6FDB6920(D2StatsArrayStrc*p,uint32_t n){auto it=p->values.find(n);return it==p->values.end()?nullptr:&it->second;}
int ENVIRONMENT_GetPeriodOfDayFromAct(void*,int*){assert(false);return 0;}
int ITEMMODS_GetByTimeAdjustment(int,int,int,void*,void*,void*){assert(false);return 0;}
D2CharStatsTxt*UNITS_GetCharStatsTxtRecord(int){assert(false);return nullptr;}
int sub_6FDB5830(D2StatListExStrc*,uint32_t);
'''
    routines = {}
    for name in ['ComputeStatPercentage', 'sub_6FDB5830']:
        body = function(stat, name); cpp += body + '\n'; routines[name] = hashlib.sha256(body.encode()).hexdigest()
    cpp += r'''
int main(){bool first=true;printf("[");
for(int operation:{2,4})for(int ownerType:{0,1,4})for(int recipientType:{-1,0,1,4})for(int ownLevel:{0,1,37,99})for(int recipientLevel:{0,1,47,99})for(int coefficient:{0,1,3,8,17,255})for(int shift:{1,3})for(int base:{-13,0,17})for(int valueShift:{0,8}){
rows.clear();D2ItemStatCostTxt output;output.bHasOpStatData=true;output.pOpStatData[0]={12,55,uint8_t(operation),uint8_t(shift)};rows[5]=output;rows[12]={};rows[55]={};
D2StatListStrc list,recipientStats;D2UnitStrc owner{ownerType,0,&list},recipient{recipientType,0,&recipientStats};
list.pOwner=&owner;list.dwOwnerType=ownerType;list.pUnit=recipientType<0?nullptr:&recipient;
list.Stats.values[5]={base};list.Stats.values[55]={coefficient<<valueShift};list.FullStats.values[12]={ownLevel};recipientStats.FullStats.values[12]={recipientLevel};
int result=sub_6FDB5830(&list,5);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],%d]",first?"":",",operation,ownerType,recipientType,ownLevel,recipientLevel,coefficient,shift,base,valueShift,result);first=false;
}printf("]\n");}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    path = STAGE / 'source.cpp'; path.write_text(cpp); compiler = STAGE / 'compiler.log'; sanitizer = STAGE / 'sanitizer.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True); sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    cases = json.loads(run.stdout); assert len(cases) == 27648
    for row, result in cases:
        operation, owner, recipient, ownlevel, recipientlevel, coefficient, shift, basis, valueshift = row
        level = ownlevel if operation == 2 and owner in [0, 1] else recipientlevel if operation == 4 and recipient in [0, 1] else 0
        assert result == basis + ((coefficient << valueshift) * level >> shift), row
    fixture = BASE / 'npc-item-suppliers-source-probe.json'; encoded = json.dumps(dict(cases=cases, profiles=profiles), separators=(',', ':')) + '\n'
    if check:
        assert fixture.read_text() == encoded
    else:
        fixture.write_text(encoded)
    for p in [installationpath, animationpath, reader, reader.with_suffix('.cpp'), Path(__file__).resolve(), runtime, *owned_files]:
        inputs[str(p.relative_to(ROOT))] = sha(p)
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Recipient-level equipment stat operations', currentExecutableParity=False,
        localReadOnly=True, processMemoryRead=False, processInput=False, inputs=inputs, compiledUnchangedRoutineHashes=routines,
        verified=dict(sourceContexts=len(cases), scaledProperties=27, ownedComparedFields=189, halfScaleProperties=['att/lvl', 'att%/lvl', 'att-dem/lvl', 'att-und/lvl']),
        profiles=profiles, artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, compiler, sanitizer, fixture, *sorted((STAGE / 'diagnostics').rglob('*'))] if p.is_file()},
        established=['Complete recursive query and percentage helper retain unchanged source. Declared base/full-stat lookup services distinguish own-level op2 from recipient-level op4, missing/wrong recipients, both level shifts and FP256 coefficient units. The matrix exercises those two operations, not all other branches of the compiled body.',
            'Owned3.3.93847properties/itemstatcost corroborate189fields for27registered scaled properties. Four to-hit properties use halves, not eighths. Item-local ac/lvl and dmg/lvl use recipient context; hp/mana carry FP256 through the source op before whole-number projection.'],
        remaining=['Full stat graph posting, damageRelated/primary weapon arbitration, all other op families, dynamic callbacks and actual NPC combat consumers remain separate. dmg%/lvl is an op5max-damage-only supplier;its existing general dmg%projection remains an explicit unresolved local-damage routing gap. No learned Iron Golem or other NPC equipment handler is enabled.'])
    path = ROOT / 'tools/reference/npc-item-suppliers-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Item recipient operations:27648complete-source contexts/27scaled properties/189owned fields;four to-hit divisors corrected;full graph/combat remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); build(parser.parse_args().check)
