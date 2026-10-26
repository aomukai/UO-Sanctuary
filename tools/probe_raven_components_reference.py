#!/usr/bin/env python3
"""Capture complete legacy Raven damage/AR/blind and explicit summon-level helpers."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def source(relative):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    common = source('source/D2Common/src/D2Skills.cpp')
    blind = routine(source('source/D2Game/src/SKILLS/SkillItem.cpp'), 'SKILLITEM_EventFunc09_Stupidity')
    base_helper = routine(source('source/D2Game/src/SKILLS/SkillNec.cpp'), 'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10')
    prefix = (STAGE / 'molten-boulder-damage-source-probe.cpp').read_text().split('void setup(', 1)[0]
    for name in ['SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage', 'SKILLS_CalculateDamageBonusByLevel']:
        assert routine(prefix, name) == routine(common, name)
    prefix = '#include <algorithm>\n' + prefix.replace('struct D2UnitStrc{', 'struct D2UnitStrc{int pSeed=0;', 1)
    prefix = prefix.replace('struct D2SkillsTxt{', 'struct D2SkillsTxt{unsigned dwToHitCalc=unsigned(-1);int dwToHit=100,dwLevToHit=30;', 1)
    old = 'int volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}'
    assert prefix.count(old) == 1
    prefix = prefix.replace(old, 'int spirit=0,dire=0,grizzly=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 12*(spirit+dire+grizzly);}')
    prefix += routine(common, 'SKILLS_GetToHitFactor')
    prefix += '''
struct D2GameStrc{};struct D2DamageStrc{};int forcedRoll=0,draws=0,applied=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int s,int){return p->stats[s];}
unsigned ITEMS_RollRandomNumber(int*){++draws;return forcedRoll;}
void sub_6FD0B9F0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int rank){applied=rank;}
'''+blind
    folder = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((folder / 'sources.json').read_text())
    pins, tables = {}, {}
    for name in ['skills', 'monlvl', 'properties', 'itemstatcost']:
        path = folder / ('json/' + name + '.json')
        pins[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert pins[name] == manifest['files']['json/' + name + '.json']['sha256']
        tables[name] = json.loads(path.read_text())
    row = tables['skills']['221']
    assert row['DmgSymPerCalc'] == "(skill('Summon Spirit Wolf'.blvl)+skill('Summon Fenris'.blvl)+skill('Summon Grizzly'.blvl))*par8" and row['Param8'] == 12
    assert tables['properties']['stupidity']['stat1'] == 'item_stupidity'
    assert tables['itemstatcost']['item_stupidity']['Signed'] == 1 and tables['itemstatcost']['item_stupidity']['itemeventfunc1'] == 9
    prefix += 'void setup(){skill={};skill.nSrcDam=0;'
    for dest, key in [('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('nToHitShift', 'HitShift'), ('dwToHit', 'ToHit'), ('dwLevToHit', 'LevToHit')]:
        prefix += f'skill.{dest}={row[key]};'
    for i in range(5):
        prefix += f'skill.dwMinLvlDam[{i}]={row.get("MinLevDam"+str(i+1),0)};skill.dwMaxLvlDam[{i}]={row.get("MaxLevDam"+str(i+1),0)};'
    prefix += '''}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\\"damage\\":[");
for(int rank=1;rank<=255;rank++)for(int s:{0,1,20})for(int d:{0,1,20})for(int g:{0,1,20})for(int item:{0,99999}){
setup();owner={};for(int&v:owner.stats)v=item;spirit=s;dire=d;grizzly=g;
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,s,d,g,item,SKILLS_GetMinPhysDamage(&owner,221,rank,1),SKILLS_GetMaxPhysDamage(&owner,221,rank,1),SKILLS_GetToHitFactor(&owner,221,rank));first=false;}
printf("],\\"blind\\":[");first=true;D2UnitStrc target;D2GameStrc game;
for(int a:{1,30,99,352})for(int t:{1,30,99,352})for(int bonus:{-1,0,1,5})for(int event:{5,6})for(int roll=0;roll<100;roll++){
owner={};target={};target.dwUnitType=UNIT_MONSTER;owner.stats[STAT_LEVEL]=a;target.stats[STAT_LEVEL]=t;owner.stats[113]=bonus;forcedRoll=roll;draws=applied=0;
int result=SKILLITEM_EventFunc09_Stupidity(&game,event,&owner,&target,nullptr,113<<16,0);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",a,t,bonus,event,roll,result,applied,draws);first=false;}
printf("]}\\n");}
'''
    base_cpp = '''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int STAT_LEVEL=0,STAT_ARMORCLASS=1,STAT_TOHIT=2;
struct D2UnitStrc{int stats[3]={};};struct D2GameStrc{uint8_t nDifficulty=0,nGameType=0;int dwGameType=0;};
struct D2MonLvlTxt{int dwLAC[3]={},dwAC[3]={},dwLTH[3]={},dwTH[3]={};};
struct Tables{int nMonLvlTxtRecordCount=111;D2MonLvlTxt pMonLvlTxt[111];}tables;auto*sgptDataTables=&tables;
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc*p,int s,int v,int){p->stats[s]=v;}
void STATLIST_AddUnitStat(D2UnitStrc*p,int s,int v,int){p->stats[s]+=v;}
'''+base_helper+'int main(){\n'
    for level in range(111):
        for member, field in [('dwLAC', 'L-AC'), ('dwAC', 'AC'), ('dwLTH', 'L-TH'), ('dwTH', 'TH')]:
            for diff, suffix in enumerate(['', '(N)', '(H)']):
                base_cpp += f'tables.pMonLvlTxt[{level}].{member}[{diff}]={tables["monlvl"][str(level)][field+suffix]};\n'
    base_cpp += '''bool first=true;printf("[");
for(int ownerLevel:{1,30,99})for(int rank=1;rank<=255;rank++)for(int difficulty:{0,1,2})for(int expansion:{0,1}){
D2UnitStrc owner,pet;D2GameStrc game;owner.stats[STAT_LEVEL]=ownerLevel;pet.stats[STAT_ARMORCLASS]=25;game.nDifficulty=difficulty;game.nGameType=expansion;
int explicitLevel=std::max(1,ownerLevel-2+rank);int ok=D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,explicitLevel,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",ownerLevel,rank,difficulty,expansion,ok,pet.stats[STAT_LEVEL],pet.stats[STAT_ARMORCLASS],pet.stats[STAT_TOHIT]);first=false;}
printf("]\\n");}
'''
    outputs, artifacts = {}, {}
    for name, cpp in [('raven-components', prefix), ('raven-base', base_cpp)]:
        path = STAGE / (name + '-source-probe.cpp'); path.write_text(cpp)
        binary = path.with_suffix('')
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
        run = subprocess.run([str(binary)], capture_output=True)
        (STAGE / (name + '-sanitizer.log')).write_bytes(run.stderr)
        assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
        outputs[name] = json.loads(run.stdout)
        artifacts[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    data = dict(**outputs['raven-components'], birth=outputs['raven-base'])
    assert len(data['damage']) == 13770 and len(data['blind']) == 12800 and len(data['birth']) == 4590
    assert all(r[5] == 1 for r in data['blind'] if r[2] != 0 and r[4] == 0)
    assert all(r[7] == (r[2] != 0) for r in data['blind'])
    assert max(r[5] for r in data['birth']) == 352
    fixture = STAGE / 'raven-components-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        sourceHashes=hashes, unchangedRoutineHashes=blocks, pinnedTableHashes=pins, generatedSourceHashes=artifacts,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), damage=13770, blind=12800, birth=4590),
        sanitizers='Both ASan/UBSan binaries exit0 with empty runtime stderr.',
        established=[
            'Complete source physical tier/min/max getters and to-hit getter run all255ranks, independent hard Spirit/Dire/Grizzly0/1/20 and unrelated99999item stats. Raven source-zero physical damage excludes weapon stats; mode AR is the flat100+30*(rank-1) result.',
            'Complete event09 accepts signed nonzero blindness, including-1, and consumes one RNG draw even when no curse level results. Zero stat consumes none. Melee/missile chance branches, level gaps and all100rolls are captured; successful curse callback is declared, not executed.',
            'Complete summon base helper receives explicit max(1,ownerLevel-2+rank), retains that level up to352 and clamps only the lookup to111current monlvl rows. All three difficulties and expansion/classic columns run separately.'
        ], services=[
            'Formula VM, stat/record access and weapon context remain declared bounded services inherited from the existing physical getter harness. Current Raven synergy expression and source-zero row are asserted before supplying those services.',
            'Blind RNG is a forced draw service with a counted call; the callback records requested curse rank and does not prove eligibility, duration, event subscription or health delivery. Native signed blind consumer acceptance remains separate.',
            'Explicit pet level is supplied by the asserted current calc2 expression; full spawn, pet ownership/FIFO and AI are not executed by these component captures.'
        ], remaining=['Actual native component comparisons, Raven actor/casting/AI/damage/lifecycle/persistence/input and installation remain pending; no learned handler or whole-skill parity is enabled.'])
    out = ROOT / 'tools/reference/raven-components-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check: assert out.read_text() == text
    else: out.write_text(text)
    print('Raven source components:13770damage/AR,12800signed blind,4590explicit-level captures; source services and modern/native limits remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
