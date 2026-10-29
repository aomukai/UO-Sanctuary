#!/usr/bin/env python3
"""Assemble owned stat operations and exercise complete maximum-damage queries."""
import argparse
import csv
import hashlib
import json
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'item-maximum-operations-source'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def block(text, start):
    end = text.index('{', start) + 1; depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}'); end += 1
    return text[start:end]


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    treepath = BASE / 'd2moo-source-tree.json'; tree = json.loads(treepath.read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(treepath.relative_to(ROOT)): sha(treepath)}
    def source(name):
        p = BASE / 'd2moo-pinned-source' / name; raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        inputs[str(p.relative_to(ROOT))] = sha(p); return raw.decode()
    query = source('source/D2Common/src/D2StatList.cpp')
    loader = source('source/D2Common/src/DataTbls/ItemsTbls.cpp')
    mods = source('source/D2Common/src/Items/ItemMods.cpp')
    header = source('source/D2Common/include/DataTbls/ItemsTbls.h')
    assert 'uint16_t unk0x5E[64]' in header and 'D2OpStatDataStrc pOpStatData[16]' in header
    loop = block(loader, loader.index('\tfor (int nStatId = 0;'))
    predecessor = ROOT / 'tools/reference/npc-item-suppliers-source-evidence.json'
    old = json.loads(predecessor.read_text()); assert old['verified']['ownedComparedFields'] == 189
    inputs[str(predecessor.relative_to(ROOT))] = sha(predecessor)
    for name, digest in old['inputs'].items():
        if name.endswith('/itemstatcost.txt') or name.endswith('/properties.txt'):
            assert sha(ROOT / name) == digest; inputs[name] = digest
    directory = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((directory / 'sources.json').read_text())
    tables = {}
    compared = 0
    for table, key, fields in [('itemstatcost', 'Stat', ['*ID', 'op', 'op param', 'op base', 'op stat1', 'op stat2', 'op stat3', 'ValShift', 'damagerelated']), ('properties', 'code', ['func1', 'stat1'])]:
        p = directory / 'json' / (table + '.json'); assert sha(p) == manifest['files']['json/' + table + '.json']['sha256']
        inputs[str(p.relative_to(ROOT))] = sha(p); tables[table] = json.loads(p.read_text())
        ownedpath = BASE / 'npc-item-suppliers-source' / (table + '.txt')
        owned = {r[key]: r for r in csv.DictReader(ownedpath.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t') if r.get(key)}
        if table == 'itemstatcost':
            for name, row in tables[table].items():
                for field in fields:
                    assert str(row.get(field, '')) == owned[name].get(field, ''), (name, field); compared += 1
        else:
            for name in ['dmg%', 'dmg/lvl', 'dmg%/lvl']:
                for field in fields:
                    assert str(tables[table][name].get(field, '')) == owned[name].get(field, ''), (name, field); compared += 1
    assert compared == 3318 and len(tables['itemstatcost']) == 368
    ids = {name: row['*ID'] for name, row in tables['itemstatcost'].items()}
    assert [ids[n] for n in ['level', 'item_maxdamage_percent', 'item_mindamage_percent', 'mindamage', 'maxdamage', 'secondary_mindamage', 'secondary_maxdamage', 'item_throw_mindamage', 'item_throw_maxdamage', 'item_maxdamage_perlevel', 'item_maxdamage_percent_perlevel']] == [12, 17, 18, 21, 22, 23, 24, 159, 160, 218, 219]
    cpp = r'''
#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))
#define TRUE 1
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,STAT_MAXHP=7,STAT_MAXMANA=9,STAT_MAXSTAMINA=11,STAT_MAXDAMAGE=22,STAT_POISONMAXDAM=58,STAT_POISON_COUNT=326;
constexpr int ITEMSTATCOSTFLAGINDEX_DAMAGERELATED=2,ITEMSTATCOSTFLAGINDEX_HP_MANA_STAMINA=5,ITEMSTATCOSTFLAGINDEX_HP=6,ITEMSTATCOSTFLAGINDEX_MANA=7,ITEMSTATCOSTFLAGINDEX_STAMINA=8;
constexpr int STAT_OP_NONE=0,STAT_OP_APPLY_TO_ITEM=4,STAT_OP_APPLY_TO_ITEM_PCT=5;
constexpr auto gdwBitMasks=[](){std::array<unsigned,32>a{};for(int i=0;i<32;i++)a[i]=1u<<i;return a;}();
struct D2SLayerStatIdStrc{using PackedType=uint32_t;uint32_t nPackedValue=0;uint16_t nStat=0;static D2SLayerStatIdStrc MakeFromStatId(int n){return{uint32_t(n),uint16_t(n)};}static D2SLayerStatIdStrc FromPackedType(uint32_t n){return{n,uint16_t(n)};}};
struct D2StatStrc{int nValue=0;};struct D2StatsArrayStrc{std::map<uint32_t,D2StatStrc> values;};
struct D2UnitStrc;
struct D2StatListStrc{D2StatsArrayStrc Stats,FullStats;D2UnitStrc*pOwner=nullptr,*pUnit=nullptr;int dwOwnerType=4;D2StatListStrc*pMyLastList=nullptr,*pPrevLink=nullptr;unsigned dwFlags=0;bool extended=true;};using D2StatListExStrc=D2StatListStrc;
struct Seed{};struct D2UnitStrc{int dwUnitType=4,dwClassId=0;D2StatListExStrc*pStatListEx=nullptr;void*pDrlgAct=nullptr;Seed pSeed;};
struct D2OpStatDataStrc{uint16_t nOpBase=65535,nStat=0;uint8_t nOp=0,nOpParam=0;};
struct D2ItemStatCostTxt{uint16_t wStatId=0;unsigned dwItemStatFlags=0;bool bHasOpStatData=false,bIsBaseOfOtherStatOp=false,bHasOpApplyingToItem=false;D2OpStatDataStrc pOpStatData[16];int nValShift=0;uint8_t nOp=0,nOpParam=0;uint16_t wOpBase=65535,wOpStat[3]={65535,65535,65535},unk0x5E[64];};std::vector<D2ItemStatCostTxt>rows;
struct Tables{int nItemStatCostTxtRecordCount=0;D2ItemStatCostTxt*pItemStatCostTxt=nullptr;bool bCompileTxt=true;void*pItemStatCostLinker=nullptr;}tables;Tables*sgptDataTables=&tables;
struct D2CharStatsTxt{int nManaPerMagic=4,nStaminaPerVitality=4,nLifePerVitality=16;};
const char*FOG_10255(void*,int,int){return "stat";}void FOG_Trace(const char*,int,const char*){assert(false);}
bool STATLIST_IsExtended(D2StatListStrc*p){return p->extended;}
D2ItemStatCostTxt*ITEMS_GetItemStatCostTxtRecord(int n){return n>=0&&n<int(rows.size())?&rows[n]:nullptr;}
int Value(D2StatsArrayStrc&a,uint32_t n){auto it=a.values.find(n);return it==a.values.end()?0:it->second.nValue;}
int STATLIST_GetBaseStat_6FDB6340(D2StatListStrc*p,uint32_t n,D2ItemStatCostTxt*){return Value(p->Stats,n);}
int STATLIST_GetTotalStat_6FDB63E0(D2StatListStrc*p,uint32_t n,D2ItemStatCostTxt*){return Value(p->extended?p->FullStats:p->Stats,n);}
D2StatStrc*STATLIST_FindStat_6FDB6920(D2StatsArrayStrc*p,uint32_t n){auto it=p->values.find(n);return it==p->values.end()?nullptr:&it->second;}
int ENVIRONMENT_GetPeriodOfDayFromAct(void*,int*){assert(false);return 0;}int ITEMMODS_GetByTimeAdjustment(int,int,int,void*,void*,void*){assert(false);return 0;}D2CharStatsTxt*UNITS_GetCharStatsTxtRecord(int){assert(false);return nullptr;}
int sub_6FDB5830(D2StatListExStrc*,uint32_t);
struct D2PropertyStrc{int nMin=0,nMax=0,nLayer=0;};D2StatListStrc*writeList=nullptr;int writes=0,draws=0,ticket=0;
D2StatListStrc*ITEMMODS_GetOrCreateStatList(D2UnitStrc*,D2UnitStrc*,int,int){return writeList;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*p,int stat,int value,int layer){assert(layer==0);p->FullStats.values[stat]={value};writes++;}
void STATLIST_AddStat(D2StatListStrc*p,int stat,int value,int layer){assert(layer==0);p->FullStats.values[stat].nValue+=value;writes++;}
int STATLIST_GetStatValue(D2StatListStrc*p,int stat,int layer){assert(layer==0);return Value(p->FullStats,stat);}
Seed*ITEMS_GetItemSeed(D2UnitStrc*p){return&p->pSeed;}
int SEED_RollLimitedRandomNumber(Seed*,int bound){assert(bound>0);draws++;return ticket%bound;}
int ITEMMODS_PropertyFunc06(int,D2UnitStrc*,D2UnitStrc*,const D2PropertyStrc*,int,short,int,int,int,int,D2UnitStrc*){assert(false);return 0;}
'''
    routines = {}
    for raw, name in [(query, 'ComputeStatPercentage'), (query, 'sub_6FDB5830'), (mods, 'ITEMMODS_AddPropertyToItemStatList'), (mods, 'ITEMMODS_PropertyFunc17')]:
        body = function(raw, name); cpp += body + '\n'; routines[name] = hashlib.sha256(body.encode()).hexdigest()
    cpp += 'void Assemble(){int nOpBase=0;\n' + loop + '\n}\nvoid Init(){rows.assign(368,{});\n'
    for name, row in tables['itemstatcost'].items():
        n = ids[name]
        destinations = [ids[row['op stat' + str(i)]] if row.get('op stat' + str(i)) else 65535 for i in [1, 2, 3]]
        cpp += f'rows[{n}].wStatId={n};rows[{n}].nOp={row.get("op",0)};rows[{n}].nOpParam={row.get("op param",0)};rows[{n}].wOpBase={ids[row["op base"]] if row.get("op base") else 65535};rows[{n}].nValShift={row.get("ValShift",0)};rows[{n}].dwItemStatFlags={4 if row.get("damagerelated",0) else 0};\n'
        for i, destination in enumerate(destinations):
            cpp += f'rows[{n}].wOpStat[{i}]={destination};\n'
    cpp += r'''
for(auto&r:rows)std::fill(std::begin(r.unk0x5E),std::end(r.unk0x5E),65535);
tables.nItemStatCostTxtRecordCount=rows.size();tables.pItemStatCostTxt=rows.data();Assemble();}
int main(){Init();printf("{\"plans\":[");bool first=true;
for(int id:{21,22,23,24,159,160}){printf("%s[%d,[",first?"":",",id);first=false;bool comma=false;for(auto op:rows[id].pOpStatData){if(!op.nOp)break;printf("%s[%d,%d,%d,%d]",comma?",":"",op.nOp,op.nStat,op.nOpBase,op.nOpParam);comma=true;}printf("]]");}
printf("],\"cases\":[");first=true;
for(int channel:{0,1,2})for(int base:{1,2,7,15,23,100,109})for(int flat:{0,1,5,19})for(int ed:{0,1,5,29,50,111,200})for(int level:{1,37,99})for(int perflat:{0,1,4,8})for(int perpct:{0,1,8,20,24}){
int low=channel==0?21:channel==1?23:159,high=low+1;
D2StatListStrc list,recipientStats,child;D2UnitStrc item{4,0,&list},recipient{1,0,&recipientStats};list.pOwner=&item;list.dwOwnerType=4;list.pUnit=&recipient;list.pMyLastList=&child;list.Stats.values[low]={base};list.Stats.values[high]={base};recipientStats.FullStats.values[12]={level};
child.FullStats.values[low]={flat};child.FullStats.values[high]={flat};child.FullStats.values[17]={ed};child.FullStats.values[18]={ed};writeList=&child;D2PropertyStrc prop;prop.nLayer=perflat;ITEMMODS_PropertyFunc17(0,nullptr,&item,&prop,0,218,0,0,0,0,nullptr);prop.nLayer=perpct;ITEMMODS_PropertyFunc17(0,nullptr,&item,&prop,0,219,0,0,0,0,nullptr);
int minimum=sub_6FDB5830(&list,low),maximum=sub_6FDB5830(&list,high);
printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d]]",first?"":",",channel,base,flat,ed,level,perflat,perpct,minimum,maximum);first=false;
}printf("],\"writer\":[");first=true;
for(int parameter:{0,1,8,24})for(int coefficient:{0,1,3})for(int set:{0,1})for(int alloc:{0,1}){
D2UnitStrc item;D2StatListStrc child;child.FullStats.values[219]={11};writeList=alloc?&child:nullptr;writes=draws=0;ticket=2;D2PropertyStrc prop{coefficient,coefficient+2,parameter};int result=ITEMMODS_PropertyFunc17(0,nullptr,&item,&prop,set,219,0,0,0,0,nullptr);
printf("%s[[%d,%d,%d,%d],[%d,%d,%d,%d]]",first?"":",",parameter,coefficient,set,alloc,result,Value(child.FullStats,219),writes,draws);first=false;
}printf("]}\n");}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    path = STAGE / 'source.cpp'; path.write_text(cpp); compiler = STAGE / 'compiler.log'; sanitizer = STAGE / 'sanitizer.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g1', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True); sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    data = json.loads(run.stdout)
    assert [r[0] for r in data['plans']] == [21, 22, 23, 24, 159, 160]
    for n, plan in data['plans']:
        expected = [[13, 18, 65535, 0]] if n in [21, 23, 159] else [[13, 17, 65535, 0], [4, 218, 12, 3], [5, 219, 12, 3], [6, 272, 65535, 0]]
        if n in [22, 23, 159]:
            expected += [[7, 273, 65535, 0]]
        assert plan == expected, (n, plan)
    assert len(data['cases']) == 35280 and len(data['writer']) == 48
    combined = 0; integer_base = 0
    for row, (minimum, maximum) in data['cases']:
        _, base, flat, ed, level, perflat, perpct = row
        baseline = base + int(base * (ed / 100.0)) + flat
        assert minimum == baseline
        assert maximum == baseline + (level * perflat >> 3) + int(base * (level * perpct >> 3) / 100.0), row
        combined += maximum != base * (100 + ed + (level * perpct >> 3)) // 100 + flat + (level * perflat >> 3)
        integer_base += minimum != base * (100 + ed) // 100 + flat
    for row, out in data['writer']:
        parameter, coefficient, set_flag, allocate = row
        raw = parameter or coefficient + 2
        assert out == [raw, (raw if set_flag else 11 + raw) if allocate else 11, allocate, int(parameter == 0)], (row, out)
    fixture = BASE / 'item-maximum-operations-source-probe.json'; encoded = json.dumps(data, separators=(',', ':')) + '\n'
    if check:
        assert fixture.read_text() == encoded
    else:
        fixture.write_text(encoded)
    inputs[str(Path(__file__).resolve().relative_to(ROOT))] = sha(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Maximum-only level-scaled item damage operation assembly', currentExecutableParity=False, nativeConsumerAccepted=False,
        inputs=inputs, compiledUnchangedRoutineHashes=routines, compiledLoaderBlockHash=hashlib.sha256(loop.encode()).hexdigest(),
        verified=dict(ownedComparedFields=3318, currentStatRows=368, damagePlans=data['plans'], sourceQueryContexts=len(data['cases']), sourceWriterContexts=len(data['writer']), combinedPercentageDifferences=combined, integerBasePercentageDifferences=integer_base),
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, compiler, sanitizer, fixture, *sorted((STAGE / 'diagnostics').rglob('*'))] if p.is_file()},
        established=['The unchanged dependency-assembly loop builds the current max channels in stat-ID order:op13item ED,op4level flat maximum,op5level maximum percent. Primary minimum has only op13;current by-time rows also appendop6to maxima andop7to primary maximum/secondary minimum/throw minimum exactly as owned data specifies. By-time branches are guarded off in this matrix. All3318selected current stat/property fields match owned tables. This is the full assembly loop,not the entire archive/table loader.',
            'Complete recursive query/percentage and property17/add-property bodies run with declared base/full-stat lookup and stat-list allocation/write services. Property17honors a fixed parameter before fallback rolling and ignores the nested writer failure in its own return. Max-only percentages use a separate percentage application after flat level damage; combining percentages changes outcomes.',
            'The source double percentage helper can differ from an integer base-ED implementation. Those differences are recorded explicitly;this does not prove the same rounding in the pinned modern executable.'],
        remaining=['Native max-only routing and actual item equip/weapon/combat/save acceptance are still required. Full loader/property dispatcher/stat-list merge/callbacks,by-time operations6/7and their asymmetric current channel targets,base/ethereal initialization and unsupported writer branches remain separate. Property17flat-max special branch delegates to an unexecuted func06service. Do not silently change all established base-ED rounding from this bounded older-source result.'])
    path = ROOT / 'tools/reference/item-maximum-operations-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Maximum item operations:', json.dumps(result['verified']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); build(parser.parse_args().check)
