#!/usr/bin/env python3
"""Execute the unchanged curse receiver/factory with pinned Amplify Damage inputs."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/amplify-damage-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    blobs = {r['path']: r['sha'] for r in tree['tree'] if r['type'] == 'blob'}
    hashes, blocks = {}, {}

    def source(name):
        p = ROOT / '.local/class-skills/d2moo-pinned-source' / name
        raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blobs[name]
        hashes[name] = sha(p)
        return raw.decode()

    def routine(text, name):
        m = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert m, name
        end = text.index('{', m.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[m.start():end]
        return blocks[name] + '\n'

    # Reuse the declared Battle Cry storage/filter services without executing
    # that probe or replacing any of its accepted artifacts.
    scaffold = ROOT / 'tools/probe_battlecry_reference.py'
    prefix = next(n.value for n in ast.walk(ast.parse(scaffold.read_text()))
                  if isinstance(n, ast.Constant) and isinstance(n.value, str) and n.value.startswith('#include <algorithm>'))
    prefix = prefix.replace('using Callback=void(*)(D2UnitStrc*);', 'using Callback=void(*)(D2UnitStrc*,int,D2StatListStrc*);')
    prefix = prefix.replace('void sub_6FD10E50(D2UnitStrc*){}', 'void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}')
    prefix = prefix.replace('struct D2UnitStrc{', 'struct D2UnitStrc{int monType=0,baseResistance=0,hireling=0;bool dead=false;')
    prefix = prefix.replace('uint32_t dwFlags=1;} modes;', 'uint32_t dwFlags=1,dwModeFlags=4;} modes;')
    prefix = prefix.replace('int nStatesTxtRecordCount=256;', 'int nStatesTxtRecordCount=256,nItemStatCostTxtRecordCount=64;')
    percent_stub = 'int MONSTERUNIQUE_CalculatePercentage(int v,int p,int d){return int(int64_t(v)*p/d);}'
    assert percent_stub in prefix
    skills = source('source/D2Game/src/SKILLS/Skills.cpp')
    nec = source('source/D2Game/src/SKILLS/SkillNec.cpp')
    unique = source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    prefix = prefix.replace(percent_stub, routine(unique, 'MONSTERUNIQUE_CalculatePercentage'))
    cpp = prefix + r'''
using D2C_AiSpecialState=int;using D2C_States=int;using D2C_UnitEventTypes=int;
constexpr int UNIT_ALIGNMENT_NEUTRAL=0,UNITFLAG_TARGETABLE=4,STAT_DAMAGERESIST=20,STAT_MAGICRESIST=21,STAT_FIRERESIST=22,STAT_LIGHTRESIST=23,STAT_COLDRESIST=24,STAT_POISONRESIST=25;
struct D2UnkNecSkillStrc{int nDuration=0;D2GameStrc* pGame=nullptr;int nSkillId=66;D2UnitStrc* pUnit=nullptr;int unk0x08=0,nSkillLevel=1,bUpdateAnimRate=0,nAuraStat[6]={20,-1,-1,-1,-1,-1},nAuraStatCalc[6]={-100},nAuraTargetState=9,nAuraEvent[3]={-1,-1,-1},nAuraEventFunc[3]={};};
int STATLIST_GetUnitAlignment(D2UnitStrc*){return 1;}
int AiSpecialStateFromAuraState(int){return 0;}int sub_6FD15190(D2UnitStrc*,int){return 0;}
int STATLIST_GetUnitBaseStat(D2UnitStrc* p,int,int){return p->baseResistance;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p->hireling;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,unsigned f){return (p->monType&f)!=0;}
bool SUNIT_IsDead(D2UnitStrc* p){return !p||p->dead;}
void sub_6FD0B250(D2UnitStrc*,int,D2StatListStrc*){}
void* AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return nullptr;}
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int){}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){}
'''
    for text, name in [(source('source/D2Game/src/UNIT/SUnit.cpp'), 'sub_6FCBD900'),
                       (skills, 'sub_6FD10EC0'), (nec, 'sub_6FD0B2B0'), (nec, 'sub_6FD0B450')]:
        cpp += routine(text, name)
    cpp += r'''
int main(){D2UnitStrc owner,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=target.room=&room;target.dwFlags=7;bool first=true;std::printf("{\"receiver\":[");
for(int type:{0,1})for(int hire:{0,1})for(int basis:{-100,0,99,100,119,120,200})for(int oldRank:{0,1,20,255})for(int rank:{1,3,20,21,255})for(int other:{0,1})for(int resistance:{0,50,99,100})for(int attract:{0,1}){
 D2StatListStrc old;old.skill=other?87:66;old.state=other?60:9;old.rank=oldRank;old.expire=777;old.creator=999;old.stats[20]=-33;
 target.dwUnitType=type;target.hireling=hire;target.baseResistance=basis;target.stats[8]=resistance;target.attract=attract;target.list=oldRank?&old:nullptr;removals=events=0;
 D2UnkNecSkillStrc arg;arg.pGame=&game;arg.pUnit=&owner;arg.nSkillLevel=rank;arg.nDuration=200+75*(rank-1);
 int result=sub_6FD0B450(&target,&arg);auto* p=target.list;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",type,hire,basis,oldRank,rank,other,resistance,attract,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,p?p->stats[20]:0,removals,events);
}
std::printf("],\"gates\":[");first=true;
for(int flags=0;flags<8;flags++)for(int walk:{0,1})for(int possessed:{0,1})for(int dead:{0,1})for(int npc:{0,1})for(int attackable:{0,1})for(int aligned:{0,1})for(int row:{0,1}){
 target.dwUnitType=1;target.hireling=0;target.baseResistance=0;target.stats[8]=0;target.attract=false;target.list=nullptr;target.dwFlags=flags;target.monType=possessed?32:0;target.dead=dead;target.aligned=aligned;
 modes.dwModeFlags=walk?4:0;modes.dwFlags=attackable;mon.dwMonStatsFlags=npc?4:0;validMon=validModes=row;
 D2UnkNecSkillStrc arg;arg.pGame=&game;arg.pUnit=&owner;arg.nDuration=200;int result=sub_6FD0B450(&target,&arg);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,walk,possessed,dead,npc,attackable,aligned,row,result);
}std::printf("]}\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True)
    path, binary = STAGE / 'probe.cpp', STAGE / 'probe'
    path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer',
                        str(path), '-o', str(binary)], check=True, stdout=log, stderr=log)
    with (STAGE / 'sanitizer.log').open('w') as log:
        data = json.loads(subprocess.check_output([str(binary)], stderr=log))
    for typ, hire, basis, old, rank, other, resistance, attract, result, stored, owner, expiry, value, removed, events in data['receiver']:
        accept = resistance < 100 and not attract and (not old or other or rank >= old)
        equal = old and not other and rank == old
        if not accept:
            expected = (old, 999, 777, -33) if old else (0, 0, 0, 0)
        else:
            duration = 200 + 75 * (rank - 1)
            duration -= duration * resistance // 100
            expected = (rank, 999 if equal else 7, 100 + duration,
                        -33 if equal else -20 if typ == 1 and not hire and basis >= 100 else -100)
        assert (result, stored, owner, expiry, value, removed, events) == (
            int(accept), *expected, int(accept and old > 0 and not equal), int(accept)), (typ,hire,basis,old,rank,other,resistance,attract,result,stored,owner,expiry,value,removed,events,expected)
    for flags, walk, possessed, dead, npc, attackable, aligned, row, result in data['gates']:
        assert result == int(flags == 7 and walk and not possessed and not dead and not npc and attackable and not aligned and row)
    output = STAGE / 'cases.json'
    output.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    # The do30/radial composition remains a separately identified next proof.
    for name in ['SKILLS_SrvDo030_Curse']:
        routine(nec, name)
    routine(skills, 'sub_6FD10360')
    monster_header = source('source/D2Common/include/D2Monsters.h')
    monster_tables = source('source/D2Common/src/DataTbls/MonsterTbls.cpp')
    assert 'MONTYPEFLAG_POSSESSED = 0x20' in monster_header and '{ "mWL", TXTFIELD_BIT, 2, 240, NULL }' in monster_tables
    tables = {}
    for name in ['skills', 'monstats', 'monstats2']:
        p = ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json'
        tables[name] = json.loads(p.read_text())
    row = tables['skills']['66']
    assert (row['srvdofunc'], row['aurafilter'], row['auralencalc'], row['aurarangecalc'], row['aurastatcalc1']) == (30, 3, 'ln34', 'ln12', '-par5')
    assert [row.get('Param' + str(i)) for i in range(1, 6)] == [3, 1, 200, 75, 100]
    exclusions = sorted(k for k, r in tables['monstats'].items() if r.get('MonStatsEx') and not tables['monstats2'][r['MonStatsEx']].get('mWL'))
    target = ROOT / 'data/Sanctuary/Balance/source-curse-no-walk.json'
    rendered = json.dumps(exclusions, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    receipt = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes,
                   scaffold=dict(path=str(scaffold.relative_to(ROOT)), sha256=sha(scaffold)),
                   compiledRoutines=[k for k in blocks if k not in ['SKILLS_SrvDo030_Curse', 'sub_6FD10360']],
                   reviewedRoutines=['SKILLS_SrvDo030_Curse', 'sub_6FD10360'],
                   routineHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
                   cases={k: len(v) for k, v in data.items()},
                   noWalkSpecies=len(exclusions),
                   artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, output, STAGE / 'compiler.log', STAGE / 'sanitizer.log', target]},
                   pinnedTables={name: sha(ROOT / f'tools/reference/d2r-named-equipment/json/{name}.json') for name in tables},
                   established=[
                       'Complete curse callback/eligibility/shared factory and percentage helper execute together. Weaker same-skill rank rejects; equal rank refreshes only expiry, preserving creator and first resistance stat; higher rank or different curse replaces.',
                       'Physical resistance penalty is chosen from supplied base resistance at application: nonplayer/nonhireling immune recipients receive -20, otherwise -100. Subsequent aggregate changes do not rewrite this stored stat.',
                       'Curse resistance reduces duration before arbitration; resistance100 or Attract preserves the old state. Possessed flag0x20, missing walk mode bit2, NPC/unattackable species, missing rows, dead/ineligible flags and alignment reject.',
                       'Current skill66 pins radius3+rank-1, duration200+75*(rank-1) frames, cost4 and do30/filter3. Current monstats2 mWL is bit2;135 stationary-species exclusions are generated without changing Inner Sight data.'],
                   limits=[
                       'Declared services cover base/accrued stat storage, alignment/owner graph, state rows/masks, bounded allocation and event scheduling. No special AI is executed for Amplify Damage. The formula VM, do30/radial traversal and current executable are not executed by this probe.',
                       'Native base-resistance/species/geometry/client and full event lifecycle require independent verification. Native UO/encounter traits are explicit projections, not inferred D2 species certificates.'])
    dest = ROOT / 'tools/reference/amplify-damage-source-evidence.json'
    rendered = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert dest.read_text() == rendered
    else:
        dest.write_text(rendered)
    print('Amplify Damage complete receiver/factory:', receipt['cases'], 'stationary species:', len(exclusions))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
