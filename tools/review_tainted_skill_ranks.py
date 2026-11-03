#!/usr/bin/env python3
"""Reuse the complete NPC getter proof and execute the live-rank do dispatcher."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-ranks-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def review(check=False):
    STAGE.mkdir(exist_ok=True); inputs = {}
    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path); return path.read_text()
    prior = json.loads(pin(ROOT / 'tools/reference/oak-skill-rank-engine-evidence.json'))
    assert prior['referenceVersion'] == '3.3.93847' and prior['fixture']['cases'] == 2040
    fixture = ROOT / prior['fixture']['path']; assert sha(fixture) == prior['fixture']['sha256']; cases = json.loads(pin(fixture))
    assert len(cases) == 2040
    for base, bonus, owner, enabled, value in cases:
        assert value == min(99, base + (bonus if owner == -1 and enabled else 0))
    tree = json.loads(pin(ROOT / '.local/class-skills/d2moo-source-tree.json'))
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(relative):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative; raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        return pin(path)
    common = source('source/D2Common/src/D2Skills.cpp'); game = source('source/D2Game/src/SKILLS/Skills.cpp')
    grants = function(source('source/D2Game/src/SKILLS/SkillNec.cpp'), 'D2GAME_SetSummonPassiveStats_6FD0C530')
    assert 'if (nValue > 0)' in grants and 'D2GAME_SetSkills_6FD14C60(pPet, pSkillsTxtRecord->wSumSkill[i], nValue, 0);' in grants
    assert 'D2GAME_AssignSkill_6FD13800(pPet, 0, pSkillsTxtRecord->wSumSkill[i], -1);' in grants
    owned = json.loads(pin(ROOT / 'tools/reference/d2r-local-sentry-model-evidence.json'))['files']['monstats']
    path = ROOT / owned['path']; assert sha(path) == owned['sha256']
    monster = next(r for r in csv.DictReader(pin(path).lstrip('\ufeff').splitlines(), delimiter='\t') if r['Id'] == 'warbighead')
    assert monster['Skill1'] == 'Tainted Fire Ball' and monster['Skill2'] == 'Tainted Resist Fire'
    assert all(not monster['Skill' + str(i)] for i in range(3, 9))
    routines = {}
    for name in ('SKILLS_GetBonusSkillLevel', 'SKILLS_GetSkillLevel'):
        routines[name] = hashlib.sha256(function(common, name).encode()).hexdigest()
        assert routines[name] == prior['routineHashes'][name]
    dispatcher = function(game, 'sub_6FD13410'); routines['sub_6FD13410'] = hashlib.sha256(dispatcher.encode()).hexdigest()
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __fastcall
struct D2SkillStrc{int id;};struct D2UnitStrc{};struct D2GameStrc{};
D2SkillStrc skill;D2UnitStrc unit;D2GameStrc game;int present,liveRank,calls,phase;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc* p){assert(p==&unit&&phase++==0);return present?&skill:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* s,const char*,int){assert(s==&skill&&phase++==1);return s->id;}
int SKILLS_GetSkillLevel(D2UnitStrc* p,D2SkillStrc* s,int bonuses){assert(p==&unit&&s==&skill&&bonuses==1&&phase++==2);return liveRank;}
int D2GAME_SKILLS_Handler_6FD12BA0(D2GameStrc* g,D2UnitStrc* p,int id,int rank,int a,int b,int c){assert(g==&game&&p==&unit&&id==skill.id&&rank==liveRank&&a==1&&b==0&&c==0&&phase++==3);calls++;return 1;}
void sub_6FD13260(D2GameStrc* g,D2UnitStrc* p,int id,int rank,int mode){assert(g==&game&&p==&unit&&id==skill.id&&rank==liveRank&&mode==0&&phase++==4);}
''' + dispatcher + r'''
int main(){printf("[");bool first=true;for(int has:{0,1})for(int id:{380,420,422})for(int rank:{1,2,3,98,99}){
present=has;skill.id=id;liveRank=rank;calls=0;phase=0;sub_6FD13410(&game,&unit);
assert(calls==has&&phase==(has?5:1));printf("%s[%d,%d,%d,%d,%d]",first?"":",",has,id,rank,calls,phase);first=false;}printf("]\n");}
'''
    # The range-for fixture needs initializer_list; no source body is altered.
    cpp = '#include <initializer_list>\n' + cpp
    path = STAGE / 'dispatcher.cpp'; path.write_text(cpp); binary = STAGE / 'dispatcher'
    compiler = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(compiler.stdout + compiler.stderr); assert compiler.returncode == 0
    run = subprocess.run([str(binary)], capture_output=True); (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    rows = json.loads(run.stdout); assert len(rows) == 30
    out = STAGE / 'dispatcher.json'; out.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], currentExecutableParity=False, handlerEnabled=False,
        reusedCompleteGetterCases=2040, newCompleteDispatcherCases=30, compiledDispatcherRoutineHash=routines['sub_6FD13410'],
        reviewedSummonGrantRoutineHash=hashlib.sha256(grants.encode()).hexdigest(),
        reviewedGetterRoutineHashes={k: v for k, v in routines.items() if k != 'sub_6FD13410'}, inputs=inputs,
        established=[
            'Reuse the existing complete innate NPC getter proof and current common maximum99. No duplicate Druid/Warlock rank formula or second cap table is introduced. Item-owned and bonus-disabled controls are retained.',
            'The complete unchanged used-skill dispatcher obtains the skill id and a fresh bonus-enabled skill rank at action execution, then passes that rank to the handler and cooldown dispatcher. Thirty synthetic contexts prove callback order, level transport and the absent-skill path with ASan/UBSan.',
            'Tainted owns assigned NPC420/422 and optional Blood Boil380 ranks captured at birth. Only its own Battle Command allskills state contributes in the mapped no-equipment context. An absent optional skill is not created; Blood Boil hard synergy and captured owner mastery are not rewritten.',
            'The reviewed complete summon-passive routine grants a listed skill only for a positive calculated rank and selects a positively granted aura. Owned warbighead has only Fire Ball and Resist Fire in its default monster slots; Blood Boil is absent there. This reviewed creation path is not a newly executed creation oracle.',
            'The registered GenericPet/A2 adapter must refresh effective Fire Ball rank at the skill event. Its emitted missile retains that rank after later buff changes. Existing explicit supplied-rank fixtures remain a separate prepared API.'],
        remaining=[
            'Complete current executable mode admission/initialization, owner iterator/control, GenericPet world/skill-list/extended-passive and stat/event parity remain separate. Getter and dispatcher evidence is older source plus pinned current tables, not current executable equivalence.',
            'NPC gear, skill shrine, per-skill/oskill/elemental bonuses, item-owned production skills and foreign summoners are unsupported here. The registered mapped Tainted has no such source suppliers.',
            'Full NPC420 aura/Blood Boil, learned summoning/cap/payment and world lifecycle remain unfinished. No handler or class certificate is added.'],
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in (path, binary, out, STAGE / 'compiler.log', STAGE / 'sanitizer.log')})
    target = ROOT / 'tools/reference/tainted-ranks-source-evidence.json'; text = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == text
    else:
        target.write_text(text)
    print('Tainted innate ranks:2040 reused complete getter cases;30 complete live-rank dispatcher cases;common99 ceiling;current mode admission remains open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); review(parser.parse_args().check)
