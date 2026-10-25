#!/usr/bin/env python3
"""Compose full summon passive addition and isolated common death-stat cleanup.

This closes a stat-list boundary, not complete death entry or registry lifetime.
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_valkyrie_creation_reference.py'), '--check'],
                   check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/valkyrie-creation-engine-evidence.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = {}, {}

    def source(relative):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def extract(text, name):
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    common = source('source/D2Common/src/D2StatList.cpp')
    header = source('source/D2Common/include/D2StatList.h')
    passive_source = source('source/D2Game/src/SKILLS/SkillNec.cpp')
    passive = extract(passive_source, 'D2GAME_SetSummonPassiveStats_6FD0C530')
    assert routines['D2GAME_SetSummonPassiveStats_6FD0C530'] == parent['compiledUnchangedRoutines']['D2GAME_SetSummonPassiveStats_6FD0C530']
    flags = {}
    for name in ['STATLIST_BASIC', 'STATLIST_OVERLAY', 'STATLIST_UNK_0x100', 'STATLIST_EXTENDED']:
        match = re.search(r'\b' + name + r'\s*=\s*(0x[0-9a-fA-F]+)', header)
        assert match, name
        flags[name] = int(match[1], 16)
    inline = re.search(r'^inline bool STATLIST_IsExtended[^\n]+', header, re.M)[0]
    routines['STATLIST_IsExtended'] = hashlib.sha256(inline.encode()).hexdigest()
    pins, tables = {}, {}
    for name in ['skills', 'itemstatcost']:
        path = ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json'
        manifest = json.loads((path.parent.parent / 'sources.json').read_text())
        pins[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(path.read_bytes())
    row = tables['skills']['262']
    names = [row['passivestat' + str(i)] for i in range(1, 4)]
    assert names == ['item_pierce_fire_immunity', 'passive_fire_pierce', 'passive_fire_mastery']
    for i, name in enumerate(names, 1):
        assert row['passivecalc' + str(i)] == f"stat('{name}'.accr)"
        assert not any(value for key, value in tables['itemstatcost'][name].items() if key.lower().startswith('itemevent'))
    assert not any(row.get('aurastat' + str(i)) for i in range(1, 7))
    assert [row['sumskill'+str(i)] for i in range(1,4)] == ['Wake Of Destruction Sentry','Fire Trauma','Inferno Sentry']
    assert [row['sumsk'+str(i)+'calc'] for i in range(1,4)] == ['lvl',"skill('Fire Trauma'.blvl)","skill('Inferno Sentry'.blvl)"]
    cpp = (STAGE / 'valkyrie-creation-source-probe.cpp').read_text().split('int main()', 1)[0]

    def replace(before, after):
        nonlocal cpp
        assert before in cpp, before
        cpp = cpp.replace(before, after, 1)

    replace('#define __fastcall', '#include <cassert>\n#define __stdcall\n#define __fastcall')
    replace('struct D2UnitStrc{', 'struct D2StatListStrc;struct D2UnitStrc{D2StatListStrc* pStatListEx=nullptr;')
    replace('struct D2StatListStrc{}list;', r'''struct D2StatListStrc{unsigned dwFlags=0;int dwOwnerType=1,dwStateNo=0;D2StatListStrc* pMyLastList=nullptr;D2StatListStrc* pPrevLink=nullptr;int* values=nullptr;bool freed=false;}list,root;
constexpr int UNIT_ITEM=4;
'''+ '\n'.join(f'constexpr unsigned {name}={value}u;' for name, value in flags.items()) + '\n' + inline)
    replace('void STATLIST_AddUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]+=n;}',
            'void STATLIST_AddStat(D2StatListStrc* p,int s,int n,uint16_t){assert(p&&p->values);p->values[s]+=n;}\n' + extract(common, 'STATLIST_AddUnitStat'))
    formula = re.search(r'^int SKILLS_EvaluateSkillFormula[^\n]+', cpp, re.M)[0]
    replace(formula, 'int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int id,int){assert(id==262);if(f>=0&&f<3)return p->stats[20+f];if(f==12)return rank;if(f==13)return 3;if(f==14)return 4;return 0;}')
    cpp += r'''
int freed=0;
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc*,int state){return state==7;}
void D2Common_STATLIST_FreeStatListImpl_6FDB7050(D2StatListStrc* p){
 assert(p&&!p->freed&&!(p->dwFlags&STATLIST_EXTENDED));freed++;p->freed=true;
 auto** link=&root.pMyLastList;while(*link!=p){assert(*link);link=&(*link)->pPrevLink;}*link=p->pPrevLink;
}
'''
    cpp += extract(common, 'D2Common_10469')
    cpp += r'''
int main(){printf("[");bool first=true;
for(int valid:{0,1})for(int extended:{0,1})for(int order:{0,1,2})for(int item:{0,1})for(int protectedFlag:{0,1,128,256,129})for(int stay:{0,1})for(int inherited:{0,17,1000}){
 owner={};pet={};root={};root.values=pet.stats;root.dwFlags=extended?STATLIST_EXTENDED:0;pet.pStatListEx=&root;rank=21;freed=0;
 owner.stats[20]=inherited;owner.stats[21]=17;owner.stats[22]=25;pet.stats[STAT_MAXHP]=25600;
 skill={};for(int&i:skill.nPassiveStat)i=-1;for(int&i:skill.wAuraStat)i=-1;for(int&i:skill.wSumSkill)i=-1;
 for(int i=0;i<3;i++){skill.nPassiveStat[i]=5+i;skill.dwPassiveCalc[i]=i;skill.wSumSkill[i]=100+i;skill.dwSumSkCalc[i]=12+i;}
 for(int&i:skills)i=0;
 int created=D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,262,21,1);assert(created==1&&pet.stats[5]==inherited&&pet.stats[6]==17&&pet.stats[7]==25);
 D2StatListStrc lists[3];lists[1].dwFlags=protectedFlag;lists[1].dwOwnerType=item?4:1;lists[1].dwStateNo=stay?7:0;lists[2].dwFlags=STATLIST_BASIC;
 for(int i=0;i<3;i++){int j=(i+order)%3;lists[j].pPrevLink=root.pMyLastList;root.pMyLastList=&lists[j];}
 D2Common_10469(valid?&pet:nullptr);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",valid,extended,order,item,protectedFlag,stay,inherited,freed,int(lists[0].freed),int(lists[1].freed),int(lists[2].freed),pet.stats[5],pet.stats[6],pet.stats[7],skills[0],skills[1]+skills[2]);first=false;
}printf("]\n");}
'''
    path = STAGE / 'sentry-death-stats-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'sentry-death-stats-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 720
    for valid, extended, order, item, flag, stay, inherited, count, ordinary, special, basic, immunity, pierce, mastery, rank, hard in data:
        active = bool(valid and extended)
        remove_special = active and not item and not flag & (1 | 128 | 256) and not stay
        assert [count, ordinary, special, basic] == [int(active) + int(remove_special), int(active), int(remove_special), 0]
        assert [immunity, pierce, mastery, rank, hard] == [inherited, 17, 25, 21, 7]
    fixture = STAGE / 'sentry-death-stats-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(
        referenceVersion='3.3.93847', nativePolicyChanged=False, legacyCommit=tree['sha'],
        sourceHashes=hashes, compiledRoutineHashes=routines, pinnedInputHashes=pins,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=720),
        established=[
            'Complete summon passive helper sends all three current Wake inherited fire stats through complete AddUnitStat into the pet root stat list. They are not created as the removable aura-stat attachment; current Wake has no aura-stat entries and these stats have no item-event hooks.',
            'Complete D2Common_10469 only walks pMyLastList attachments on an extended root. Ordinary non-item/non-protected/non-stay-death attachments are freed in any tested list order; BASIC, OVERLAY, 0x100, item-owned and stay-death controls survive.',
            'The root passive values and separately assigned summon skills survive this isolated cleanup in all 720 cases. The earlier hypothesis that inherited Wake mastery necessarily disappears in this common cleanup is unsupported.',
            'Null cleanup unit and nonextended-root controls perform no attachment cleanup. The root itself is never passed to the free service.'
        ], services=[
            'Formula/accrued stats, row/itemstatcost lookup, skill assignment and event/equipment callbacks retain explicit summon harness services. Actual additive root storage is a declared array service reached through the unchanged AddUnitStat body.',
            'Stat-list freeing is a checked unlink/count service, not full total-stat recomputation or callback execution. Only nonextended attached lists are valid here; the source loop does not establish malformed extended-child behavior.',
            'This does not compose full trap death entry, StayDeath state flags, minion cleanup, corpse/GUID retention, owner deletion, current executable behavior or native retired-owner damage policy. Do not infer an immortal proxy from this root-stat result.'
        ], sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/sentry-death-stats-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Sentry death-stat evidence stale'
    else:
        out.write_text(encoded)
    print('Sentry death stats: 720 complete passive/root-add/attachment-cleanup contexts; root fire stats survive this isolated cleanup; full death/GUID lifetime pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
