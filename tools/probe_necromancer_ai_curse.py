#!/usr/bin/env python3
"""Execute complete do30/receiver/factory bodies for Dim Vision and Terror.

AI permission and dispatch, formula evaluation and radial traversal are declared
services. This probes their contracts; it does not certify the actual AI tick.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/necromancer-ai-curse-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    prior = ROOT / 'tools/reference/amplify-damage-source-evidence.json'
    receipt = json.loads(prior.read_text())
    ancestor = ROOT / '.local/class-skills/amplify-damage-source/probe.cpp'
    assert sha(ancestor) == receipt['artifacts'][str(ancestor.relative_to(ROOT))]
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == receipt['legacyCommit']
    blobs = {row['path']: row['sha'] for row in tree['tree'] if row['type'] == 'blob'}
    sources, bodies = {}, {}

    def routine(relative, name):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blobs[relative]
        sources[str(path.relative_to(ROOT))] = sha(path)
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        bodies[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    table = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    states = ROOT / 'tools/reference/d2r-named-equipment/json/states.json'
    rows, state_rows = json.loads(table.read_text()), json.loads(states.read_text())
    for ident, state, params, mana in [('71', 'dimvision', [4, 1, 175, 50], 9), ('77', 'terror', [4, 0, 200, 25], 7)]:
        row = rows[ident]
        assert (row['srvdofunc'], row['aurafilter'], row['auratargetstate'], row['mana']) == (30, 2, state, mana)
        assert [row['Param' + str(i)] for i in range(1, 5)] == params
        assert row['aurarangecalc'] == 'ln12' and row['auralencalc'] == 'ln34'
        assert not any(row.get('aurastat' + str(i)) for i in range(1, 7))
    assert [state_rows[k]['*ID'] for k in ['dimvision', 'terror', 'taunt']] == [23, 56, 27]
    difficulty_tables = []
    for filename, url, digest in [
        ('difficulty-levels.json', 'https://raw.githubusercontent.com/blizzhackers/d2data/fa1286949266498b294c40c2942521a7c0d7207a/json/difficultylevels.json', '28ff37334e2ea3bfd6fa62116e11f7cdf25d1be34c8f16a94cc7ee75b8a84764'),
        ('difficulty-levels.tsv', 'https://raw.githubusercontent.com/pinkufairy/D2R-Excel/1f16064e09b97e3e65abd6943662207cff00b07f/difficultylevels.txt', '8dc24e9f15c236f79dba3b039899155cf8e512ba387d97473583882358340c3e')]:
        path = STAGE / filename
        assert sha(path) == digest
        difficulty_tables.append(dict(path=str(path.relative_to(ROOT)), url=url, sha256=digest))
    difficulty_json = json.loads((STAGE / 'difficulty-levels.json').read_text())
    difficulty_tsv = list(csv.DictReader((STAGE / 'difficulty-levels.tsv').read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    divisors = dict(Normal=1, Nightmare=2, Hell=4)
    for name, value in divisors.items():
        assert difficulty_json[name]['AiCurseDivisor'] == value == int(next(r for r in difficulty_tsv if r['Name'] == name)['AiCurseDivisor'])
    nec = 'source/D2Game/src/SKILLS/SkillNec.cpp'
    cpp = ancestor.read_text().split('int main(){')[0]

    def replace(old, new):
        nonlocal cpp
        assert cpp.count(old) == 1, old
        cpp = cpp.replace(old, new)

    replace('struct D2GameStrc{int dwGameFrame=100;}', 'struct D2GameStrc{int nDifficulty=0,dwGameFrame=100;}')
    replace('int monType=0,baseResistance=0,hireling=0;', 'int alignment=1;bool aiAllowed=true;int monType=0,baseResistance=0,hireling=0;')
    replace('dwAuraLenCalc=1,wAuraStat[6]', 'dwAuraRangeCalc=2,wAuraEvent[3]={-1,-1,-1},wAuraEventFunc[3]={},dwAuraLenCalc=1,wAuraStat[6]')
    replace('int STATLIST_GetUnitAlignment(D2UnitStrc*){return 1;}', 'int STATLIST_GetUnitAlignment(D2UnitStrc* p){return p->alignment;}')
    replace('int AiSpecialStateFromAuraState(int){return 0;}int sub_6FD15190(D2UnitStrc*,int){return 0;}',
            'constexpr int STATE_DIMVISION=23,STATE_TERROR=56,STATE_TAUNT=27,AISPECIALSTATE_DIMVISION=10,AISPECIALSTATE_TERROR=11,AISPECIALSTATE_TAUNT=12,AISPECIALSTATE_NONE=0;\n'
            + routine(nec, 'AiSpecialStateFromAuraState')
            + '\nint permissionCalls=0,permissionState=0;int sub_6FD15190(D2UnitStrc* p,int state){permissionCalls++;permissionState=state;return p->aiAllowed;}')
    replace('void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int){}',
            'int aiCalls=0,aiState=0;void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int state){aiCalls++;aiState=state;}')
    replace('bool SKILLS_GetItemStatCostTxtRecord(int s){return s>=0;}',
            'struct D2ItemStatCostTxt{unsigned dwItemStatFlags=0;} cost;D2ItemStatCostTxt* SKILLS_GetItemStatCostTxtRecord(int s){return s>=0?&cost:nullptr;}')
    replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int,int r){formulaUnit=p->dwUnitId;return f==1?300+60*(r-1):f==2?-50-2*(r-1):f==3?-25-(r-1):0;}',
            'int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int skill,int r){formulaUnit=p?p->dwUnitId:-1;return f==1?(skill==71?175+50*(r-1):200+25*(r-1)):f==2?(skill==71?4+r-1:4):0;}')
    # Do30 initializes a zeroed packet, not the hand-filled Amplify fixture.
    replace('nAuraStat[6]={20,-1,-1,-1,-1,-1},nAuraStatCalc[6]={-100}', 'nAuraStat[6]={},nAuraStatCalc[6]={}')
    cpp += r'''
constexpr int UNITFLAG_SKSRVDOFUNC=8,ITEMSTATCOSTFLAGINDEX_UPDATEANIMRATE=0;
struct D2DifficultyLevelsTxt{int dwAiCurseDiv=0;} difficulty;
bool validDifficulty=true;D2DifficultyLevelsTxt* DATATBLS_GetDifficultyLevelsTxtRecord(int){return validDifficulty?&difficulty:nullptr;}
D2UnitStrc* recipient=nullptr;int traversals=0,filterArg=-1,rangeArg=-1,receiverResult=-1;
int sub_6FD10360(D2GameStrc*,int filter,D2UnitStrc*,int radius,int(*callback)(D2UnitStrc*,void*),void* args){
 traversals++;filterArg=filter;rangeArg=radius;receiverResult=callback(recipient,args);return receiverResult;}
'''
    cpp += routine(nec, 'SKILLS_SrvDo030_Curse') + r'''
int main(){D2UnitStrc owner,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=target.room=&room;target.dwFlags=7;recipient=&target;bool first=true;printf("[");
for(int skill:{71,77})for(int rank:{1,3,20,21,255})for(int divisor:{-1,0,1,2,4})for(int allowed:{0,1})for(int alignment:{0,1,2})for(int attract:{0,1})for(int resistance:{0,50,99,100})for(int oldKind=0;oldKind<5;oldKind++){
 D2StatListStrc old;int oldRank=oldKind==0?0:oldKind==1?rank:oldKind==2?rank+1:oldKind==3?rank-1:rank;
 int state=skill==71?23:56;old.skill=oldKind==4?82:skill;old.state=oldKind==4?58:state;old.rank=oldRank;old.expire=777;old.creator=999;
 target.list=oldKind==0?nullptr:&old;target.stats[8]=resistance;target.alignment=alignment;target.aiAllowed=allowed;target.attract=attract;
 row.wAuraTargetState=state;row.dwAuraFilter=2;row.wAuraStat[0]=-1;validDifficulty=divisor>=0;difficulty.dwAiCurseDiv=divisor;
 removals=events=updates=permissionCalls=aiCalls=traversals=0;permissionState=aiState=filterArg=rangeArg=-1;
 int result=SKILLS_SrvDo030_Curse(&game,&owner,skill,rank);auto* p=target.list;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",skill,rank,divisor,allowed,alignment,attract,resistance,oldKind,result,p?p->rank:0,p?p->creator:0,p?p->expire:0,permissionCalls,permissionState,aiCalls,aiState,filterArg,rangeArg,p?int(p->stats.size()):0);first=false;
}printf("]\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True)
    source = STAGE / 'probe.cpp'
    source.write_text(cpp)
    compiled = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(compiled.stdout + compiled.stderr)
    assert compiled.returncode == 0, compiled.stderr.decode()
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()
    data = json.loads(run.stdout)
    for skill, rank, divisor, allowed, alignment, attract, resistance, old_kind, result, stored, creator, expiry, calls, permission_state, ai_calls, ai_state, aura_filter, radius, stats in data:
        eligible = alignment != 0 and allowed
        old_rank = 0 if old_kind == 0 else rank if old_kind in [1, 4] else rank + 1 if old_kind == 2 else rank - 1
        accepted = bool(eligible and not attract and resistance < 100 and old_kind != 2)
        frames = 175 + 50 * (rank - 1) if skill == 71 else 200 + 25 * (rank - 1)
        if divisor > 0:
            frames //= divisor
        frames -= frames * resistance // 100
        expected = (rank, 999 if old_kind == 1 else 7, 100 + frames) if accepted else (old_rank, 999, 777) if old_kind else (0, 0, 0)
        special = 10 if skill == 71 else 11
        assert (result, stored, creator, expiry, calls, permission_state, ai_calls, ai_state, aura_filter, radius, stats) == (
            int(accepted), *expected, int(alignment != 0), special if alignment else -1, int(accepted), special if accepted else -1, 2, 4 + rank - 1 if skill == 71 else 4, 0)
    assert len(data) == 12000
    cases = STAGE / 'cases.json'
    cases.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], contexts=len(data), installed=False,
        reusedReference=dict(path=str(prior.relative_to(ROOT)), sha256=sha(prior)),
        reusedReceiverCpp=dict(path=str(ancestor.relative_to(ROOT)), sha256=sha(ancestor)),
        routines=bodies, difficultyTables=difficulty_tables, difficultyDivisors=divisors,
        inputs={**sources, **{str(p.relative_to(ROOT)): sha(p) for p in [Path(__file__), table, states, STAGE / 'difficulty-levels.json', STAGE / 'difficulty-levels.tsv']}},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [source, cases, STAGE / 'compiler.log', STAGE / 'sanitizer.log']},
        established=[
            'Complete unchanged do30, shared receiver and curse factory execute together with current Dim Vision/Terror table inputs. Difficulty division precedes curse-resistance reduction. Missing/zero difficulty divisor leaves base duration.',
            'AI dispatch occurs only after a successful factory result, including equal-rank refresh; weaker same-skill, neutral, permission-denied, Attract and full resistance contexts do not dispatch AI. Both skills use no primary stat.',
            'Pinned source aura-state mapping selects Dim Vision AI10 and Terror AI11; current range/filter/cost/length rows are pinned.',
            'Independent immutable d2data and D2R-Excel3.3.93847difficulty extractions agree on AiCurseDivisor Normal1/Nightmare2/Hell4;both file hashes and source URLs are pinned.'],
        remaining=[
            'AI eligibility permission, dispatch/init/tick, formula VM, radial traversal, stat storage, alignment and clock/event services remain declared. Existing Howl evidence separately executes common Terror AI; this receipt does not equate its apply behavior with learned Terror.',
            'Missing/zero difficulty records are synthetic service controls. Values1/2/4are independently table-corroborated,not observed current executable AI behavior;facet selection and runtime integration remain unverified.',
            'No C# changes, native acceptance, deployment or completed class. Source room/species/flags/geometry, actual AI coexistence/removal, native natural motion and save/reload/current graphical-client parity remain open.'])
    destination = ROOT / 'tools/reference/necromancer-ai-curse-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert destination.read_text() == rendered
    else:
        destination.write_text(rendered)
    print('Necromancer AI curses:12000 complete do30/receiver/factory contexts passed ASan/UBSan; AI services explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
