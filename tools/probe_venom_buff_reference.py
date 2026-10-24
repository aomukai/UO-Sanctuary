#!/usr/bin/env python3
"""Run full legacy Venom dispatch/writers, with explicit factory/formula services."""
import argparse, ast, hashlib, json, re, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def routine(path, name):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == path)
        hashes[path] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^\w[^;\n]*\b' + name + r'\([^;{]*?\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    sor = 'source/D2Game/src/SKILLS/SkillSor.cpp'
    druid = 'source/D2Game/src/SKILLS/SkillDruid.cpp'
    skills = 'source/D2Game/src/SKILLS/Skills.cpp'
    bodies = [routine(skills, 'sub_6FD11C90'), routine(druid, 'sub_6FCFE0E0'),
              routine(druid, 'sub_6FCFE200'), routine(sor, 'SKILLS_CurseStateCallback_DefensiveBuff'),
              routine(sor, 'SKILLS_SrvDo018_DefensiveBuff')]
    scaffold = ROOT / 'tools/probe_assassin_buff_reference.py'
    prefix = next(x.value for x in ast.walk(ast.parse(scaffold.read_text()))
                  if isinstance(x, ast.Constant) and isinstance(x.value, str) and x.value.startswith('#include <algorithm>'))
    start = prefix.index('D2StatListStrc* sub_6FD10EC0(')
    # The factory is an explicitly declared service, not a second implementation
    # asserted to prove the source factory's equal/lower/higher-rank arbitration.
    prefix = prefix[:start] + r'''
int factoryMode=0;
D2StatListStrc* sub_6FD10EC0(D2CurseStrc* c){
 factory++;if(factoryMode==1)return nullptr;
 auto* old=STATLIST_GetStatListFromUnitAndState(c->pTarget,c->nState);
 if(factoryMode==2 && old)return old;
 if(old){c->pTarget->lists.erase(c->nState);delete old;}
 auto* p=new D2StatListStrc{c->nState,c->nSkillLevel,c->nSkill,game.dwGameFrame+c->nDuration,{},c->pStateRemoveCallback};
 c->pTarget->lists[c->nState]=p;c->pTarget->active[c->nState]=true;return p;
}
'''
    cpp = prefix + ''.join(bodies) + r'''
int main(){states[157].wGroup=states[159].wGroup=2;bool first=true;std::printf("[");
for(int old:{0,1})for(int other:{0,1,2,3})for(int mode:{0,1,2})for(int invalid:{0,1,2})for(int rank:{1,20}){
 D2UnitStrc owner;
 for(int state:{31,157,159})if((state==31 && old)||(state==157 && (other&1))||(state==159 && (other&2))){
  owner.active[state]=true;owner.lists[state]=new D2StatListStrc{state,99,278,999,{{5,777},{6,888},{7,9}},nullptr};}
 skill=D2SkillsTxt{};skill.nAuraState=invalid==2?200:31;
 for(int i=0;i<6;i++){skill.wAuraStat[i]=i<3?5+i:-1;skill.dwAuraStatCalc[i]=1+i;}
 values[0]=3000;values[1]=1536;values[2]=2048;values[3]=10;
 factoryMode=mode;guard=invalid==1?1:0;frees=updates=refreshes=unregisters=gfx=factory=0;
 int ok=SKILLS_SrvDo018_DefensiveBuff(&game,&owner,278,rank);auto* current=STATLIST_GetStatListFromUnitAndState(&owner,31);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",old,other,mode,invalid,rank,ok,owner.active[31],owner.active[157],owner.active[159],frees,factory,gfx,current?current->stats[5]:0,current?current->stats[6]:0,current?current->stats[7]:0,current?current->stats[STAT_MODIFIERLIST_LEVEL]:0,current?current->expiry:0,updates);
 for(auto& e:owner.lists)delete e.second;
}std::printf("]\n");}
'''
    path = STAGE / 'venom-buff-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O2', str(path), '-o', str(binary)], check=True)
    rows = json.loads(subprocess.check_output([str(binary)]))
    assert len(rows) == 144
    for old, other, mode, invalid, rank, ok, active, quick, fade, freed, factory, gfx, minimum, maximum, length, modifier, expiry, updates in rows:
        success = not invalid and mode != 1
        assert (ok, quick, fade, freed, factory, gfx, updates) == (int(success), int(bool(other & 1)), int(bool(other & 2)), 0, int(not invalid), int(success), 2 * int(success))
        if success:
            assert (active, minimum, maximum, length, modifier, expiry) == (1, 1536, 2048, 10, rank, 999 if mode == 2 and old else 3400)
        else:
            assert (active, minimum, maximum, length, modifier, expiry) == (old, 777 if old else 0, 888 if old else 0, 9 if old else 0, 0, 999 if old else 0)
    fixture = STAGE / 'venom-buff-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], talentId='assassin.venom',
                    proofEnablesHandler=False, sourceHashes=hashes, compiledUnchangedRoutines=blocks,
                    scaffold=dict(path=str(scaffold.relative_to(ROOT)), sha256=hashlib.sha256(scaffold.read_bytes()).hexdigest()),
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(rows)),
                    established=[
                        'Full do18/group/writer bodies leave Quickness/Fade unchanged for group0 venomclaws. Invalid skill/state or failed factory does not remove old Venom.',
                        'A successful new or reused factory statlist receives all three supplied poison-rate/override-length stats plus current modifier skill/rank. Returning an old list does not skip aura-stat reevaluation.',
                        'Factory rejection/reuse/allocation are explicit service modes. Their synthetic expiry values isolate dispatch/writer behavior and do not establish real same-rank refresh or lower-rank rejection.'
                    ], services=[
                        'Formula outputs3000frames/1536minimum/2048maximum/10override frames and symbolic test stat IDs5/6/7 are opaque inputs, not rank, mastery, real stat-ID or compiler oracles.',
                        'Full shared curse factory and exact resource/payment wrapper are separate evidence. This probe does not infer whether a failed lower-rank recast spends mana.',
                        'Poison weapon/missile collection, mastery, scaling, RNG, mitigation, application, event/room/client acceptance and learned runtime integration remain separate.'
                    ])
    output = ROOT / 'tools/reference/venom-buff-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Venom:144 complete do18/group/writer cases with declared formula/factory services; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
