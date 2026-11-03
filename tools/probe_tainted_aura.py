#!/usr/bin/env python3
"""Execute shared NPC do65/search and full state/callback resistance delivery."""
import argparse
import ast
import csv
import hashlib
import json
import re
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-aura-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def probe(check=False):
    STAGE.mkdir(exist_ok=True); inputs = {}; routines = {}
    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path); return path.read_text()
    tree = json.loads(pin(ROOT / '.local/class-skills/d2moo-source-tree.json'))
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(relative):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / relative; raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        return pin(path)
    skills = source('source/D2Game/src/SKILLS/Skills.cpp'); pal = source('source/D2Game/src/SKILLS/SkillPal.cpp'); nec = source('source/D2Game/src/SKILLS/SkillNec.cpp')
    def body(text, name):
        block = function(text, name); routines[name] = hashlib.sha256(block.encode()).hexdigest(); return block
    blocks = {n: body(s, n) for s, n in [(skills, 'sub_6FD10EC0'), (pal, 'SKILLS_AuraCallback_BasicAura'), (nec, 'sub_6FD0B3D0')]}
    states = source('source/D2Common/include/D2States.h').split('enum D2C_States\n{', 1)[1].split('#ifdef', 1)[0]
    names = re.findall(r'^\s*(STATE_\w+)', states, re.M)
    assert names.index('STATE_PASSIVE_RESISTFIRE') == 181
    parent_path = ROOT / 'tools/reference/might-callback-source-evidence.json'; parent = json.loads(pin(parent_path))
    shared = ROOT / '.local/class-skills/might-callback-source/probe.cpp'; assert sha(shared) == parent['artifacts'][str(shared.relative_to(ROOT))]
    header = pin(shared).split(blocks['sub_6FD10EC0'])[0]
    cpp = '#include <algorithm>\n' + header + '\n'.join(blocks.values()) + r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int kind:{0,1})for(int self:{0,1})for(int oldSkill:{100,420})for(int newSkill:{100,420})for(int old:{0,1,2,3,20,99})for(int rank:{1,2,3,20,31,99})for(int creator:{101,202}){
owner={};target={};owner.dwUnitId=202;target.dwUnitId=303;D2UnitStrc* receiver=self?&owner:&target;receiver->dwUnitType=kind;
mon={};mon2={};present=old>0;list={};list.state=3;list.skill=oldSkill;list.rank=old;list.owner=creator;list.until=9999;list.values[39]=777;list.values[40]=oldSkill==100?8:0;
writes=allocations=frees=refresh=gfx=events=rateupdates=0;selectedCallback=nullptr;for(int i=0;i<200;i++)states[i]={};
D2BasicAuraParamStrc args;args.nSkillId=newSkill;args.nSkillLevel=rank;args.nAuraState=3;args.nPassiveState=181;args.nAuraStat[0]=39;args.nAuraStat[1]=40;
args.nAuraStatCalcValue[0]=15+55*std::min(100,110*rank/(rank+6))/100;args.nAuraStatCalcValue[1]=newSkill==100?rank:0;
D2AuraCallbackStrc cb{&game,&owner,&args};int result=SKILLS_AuraCallback_BasicAura(&cb,receiver);
int callback=selectedCallback==SKILLS_CurseStateCallback_BasicAura?1:selectedCallback==sub_6FD10E50?2:0;
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,self,oldSkill,newSkill,old,rank,creator,result,list.skill,list.rank,list.owner,list.until,list.values[39],list.values[40],writes,allocations,frees,events,args.field_40,rateupdates,callback);first=false;
}printf("]\n");}
'''
    artifacts = []
    def run(name, text):
        path = STAGE / (name + '.cpp'); path.write_text(text); binary = STAGE / name
        compile = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], capture_output=True)
        log = STAGE / (name + '.compiler.log'); log.write_bytes(compile.stdout + compile.stderr); assert compile.returncode == 0
        result = subprocess.run([str(binary)], capture_output=True); error = STAGE / (name + '.sanitizer.log'); error.write_bytes(result.stderr)
        assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
        rows = json.loads(result.stdout); out = STAGE / (name + '.json'); out.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
        artifacts.extend([path, binary, log, error, out]); return rows
    callback = run('callback', cpp); assert len(callback) == 1152
    for row in callback:
        kind, self, oldSkill, newSkill, old, rank, creator = row[:7]
        ok = not old or oldSkill != newSkill or old <= rank; reuse = old > 0 and oldSkill == newSkill and old == rank; allocated = ok and not reuse
        value = 15 + 55 * min(100, 110 * rank // (rank + 6)) // 100
        expected = [int(ok), newSkill if ok else oldSkill, rank if ok else old, 202 if allocated else creator, 151 if ok else 9999,
            value if ok else 777, (rank if newSkill == 100 else 0) if ok else (8 if oldSkill == 100 else 0),
            (3 + int(newSkill == 100)) if ok else 0, int(allocated), int(allocated and old > 0), int(ok), int(ok) * (1 + int(newSkill == 100)), int(ok), (1 if self else 2) if allocated else 0]
        assert row[7:] == expected, (row, expected)
    scaffold_path = ROOT / 'tools/probe_lightning_fury_reference.py'; scaffold = ast.parse(pin(scaffold_path))
    prefix = next(n.value for n in ast.walk(scaffold) if isinstance(n, ast.Constant) and isinstance(n.value, str) and n.value.startswith('#include <algorithm>'))
    prefix = prefix.replace('#include <vector>', '#include <vector>\n#include <initializer_list>')
    prefix = prefix.replace('struct D2GameStrc{}', 'struct D2GameStrc{int dwGameFrame=0;}')
    old = 'struct D2SkillsTxt{int dwAuraRangeCalc=0,dwCalc[1]={1},dwAuraFilter=42371;};'; assert old in prefix
    prefix = prefix.replace(old, 'struct D2SkillsTxt{int dwAuraRangeCalc=1,dwPerDelay=3,nPassiveState=181,nAuraState=3,wAuraTargetState=3;unsigned dwAuraFilter=73731;int wAuraStat[6]={39,-1,-1,-1,-1,-1},dwAuraStatCalc[6]={2},nPassiveStat[5]={-1,-1,-1,-1,-1},dwPassiveCalc[5]={};};')
    old = 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int which,int,int rank){return which==0?15:rank+1;}'; assert old in prefix
    prefix = prefix.replace(old, 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int which,int,int rank){return which==1?16+2*(rank-1):which==2?15+55*std::min(100,110*rank/(rank+6))/100:50;}')
    cpp = prefix + r'''
constexpr int STAT_MANA=8,STATE_NOMANAREGEN=10;
struct Tables{int nStatesTxtRecordCount=256,nItemStatCostTxtRecordCount=512;}tables;auto* sgptDataTables=&tables;
struct D2StatListStrc{}list;
struct D2BasicAuraParamStrc{int nSkillId=0,nSkillLevel=0,nDuration=0,nPassiveState=0,nAuraState=0,nAuraStat[6]={},nAuraStatCalcValue[6]={},field_40=0;D2StatListStrc*pStatList=nullptr;};
struct Event{int unit,state,rank,value,duration;};std::vector<Event>calls;
int SKILLS_GetManaCosts(int,int){return 0;}int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){}void STATES_ToggleState(D2UnitStrc*,int,int){}
void D2GAME_SKILLMANA_AuraConsume_6FD10C90(D2UnitStrc*,int){__builtin_trap();}
int SKILLS_AuraCallback_BasicAura(D2AuraCallbackStrc* cb,D2UnitStrc*target){auto* a=(D2BasicAuraParamStrc*)cb->pArgs;calls.push_back({target->id,a->nAuraState,a->nSkillLevel,a->nAuraStatCalcValue[0],a->nDuration});a->pStatList=&list;a->field_40++;return 1;}
'''
    for name in ('sub_6FD0FA00', 'sub_6FD0FE50', 'sub_6FD0FE80', 'SKILL_ComputePeriodicRate'):
        cpp += body(skills, name) + '\n'
    cpp += body(pal, 'SKILLS_SrvDo065_BasicAura') + r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool comma=false;
for(int rank:{1,2,3,20,30,31,99})for(int frame:{0,1,49,50,1001})for(int kind=0;kind<=10;kind++)for(int town:{0,1}){
caster={};caster.dwUnitType=UNIT_MONSTER;caster.dwClassId=1;caster.id=100;caster.hostile=false;caster.room=&rooms[0];
for(auto&m:monsters)m={};for(auto&r:rooms)r={};rooms[0].town=town;rooms[0].pUnitFirst=&caster;adjacent[0]=&rooms[0];adjacent[1]=&rooms[1];
targets[0]={};auto&t=targets[0];t.id=200;t.dwUnitType=kind==0?UNIT_PLAYER:UNIT_MONSTER;t.hostile=kind==6;t.room=&rooms[1];t.x=caster.x+1;t.y=caster.y;
rooms[1].pUnitFirst=kind==10?nullptr:&t;rooms[1].town=kind==1;
if(kind==2)t.dwAnimMode=MONMODE_DEAD;if(kind==3)monsters[0].dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NPC];if(kind==4)monsters[0].dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NOAURA];
if(kind==5)t.x=caster.x+16+2*(rank-1)+1;if(kind==9)t.x=caster.x+16+2*(rank-1);if(kind==8)t.dwFlags=0;wall=kind==7;
game.dwGameFrame=frame;calls.clear();int next=SKILL_ComputePeriodicRate(&game,&caster,&skill,420,rank);int result=SKILLS_SrvDo065_BasicAura(&game,&caster,420,rank);
printf("%s[%d,%d,%d,%d,%d,%d,[",comma?",":"",rank,frame,kind,town,next,result);comma=true;bool sep=false;
for(auto c:calls){printf("%s[%d,%d,%d,%d,%d]",sep?",":"",c.unit,c.state,c.rank,c.value,c.duration);sep=true;}printf("]]");}printf("]\n");}
'''
    pulse = run('pulse', cpp); assert len(pulse) == 770
    for rank, frame, kind, town, next_frame, result, calls in pulse:
        value = 15 + 55 * min(100, 110 * rank // (rank + 6)) // 100
        expected = [[100, 3, rank, value, 51]]
        if kind in (0, 7, 8, 9):
            expected.append([200, 3, rank, value, 51])
        assert next_frame == frame + 50 and result == 1 and calls == expected
    # Pin the owned NPC row and the absence of maximum/passive stats (the passive-state removal label remains).
    owned_proof = json.loads(pin(ROOT / 'tools/reference/tainted-foundation-source-evidence.json'))
    owned_path = ROOT / '.local/research/d2r-3.3.93847/skills.txt'; assert sha(owned_path) == owned_proof['inputs'][str(owned_path.relative_to(ROOT))]
    row = next(r for r in csv.DictReader(pin(owned_path).lstrip('\ufeff').splitlines(), delimiter='\t') if r['skill'] == 'Tainted Resist Fire')
    assert [row[k] for k in ('srvdofunc', 'aurafilter', 'aurastate', 'auratargetstate', 'immediate', 'perdelay', 'aurastat1')] == ['65', '73731', 'resistfire', 'resistfire', '1', '50', 'fireresist']
    assert row['passivestate'] == 'passive_resistfire'
    assert all(not row[k] for k in ('aurastat2', 'passivestat1')) and all(row[k] == '0' for k in ('mana', 'minmana', 'lvlmana'))
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], currentExecutableParity=False, handlerEnabled=False,
        fullCallbackFactoryModifierCases=1152, fullDo65FilterIteratorCases=770, compiledUnchangedRoutineHashes=routines, inputs=inputs,
        established=['Owned NPC420 is immediate do65/state3/stat39,period50,filter0x12003,zero mana,no maximum stat and passive_resistfire removal metadata. Shared source timing gives50-frame pulses and51-frame leases.',
            'Full unchanged callback, state factory and resistance modifier execute together. Same-skill weaker ranks refuse; equal rank retains creator and refreshes expiry; higher or different skill replaces. NPC420 and Paladin100 share state3 and can replace each other regardless of cross-skill rank. A new NPC420 list contains no maximum-fire-resistance stat.',
            'Full unchanged do65/filter/iterator execute with an explicit callback recorder. Living allies/player/monster, exact radius, town/NPC/NOAURA/dead/hostile and no-LOS controls are retained. Callback-recorder and full-callback matrices are separate compositions,not a single full world pipeline.'],
        limitations=['Allocation, state storage, room/list/hostility, formula and event services are declared fixtures. Both aura skills name passive_resistfire for removal; the fixture has no passive list. Removal callbacks are recorded,not invoked; passive-list removal/restoration history remains outside this composition.',
            'Source do65 can self-apply in town; Sanctuary approved safe-area parking suppresses the parked emitter instead. Native2subtile/height/world order, offline/park/resume phase, current executable/global scheduler and complete client/stat-list acceptance remain separate.',
            'NPC mana and owner mana are not consumed. No learned summon or full GenericPet AI is enabled by this source proof.'],
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts})
    target = ROOT / 'tools/reference/tainted-aura-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Tainted aura:1152 complete callback/factory/modifier and770 do65/filter cases;shared state3,distinct420/100,no maximum bonus;current parity separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); probe(parser.parse_args().check)
