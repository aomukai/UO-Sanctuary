#!/usr/bin/env python3
"""Execute complete older Maul/Feral charge flow with explicit combat/formula services."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preparation_path = ROOT / 'tools/reference/druid-charged-attacks-preflight.json'
    preparation = json.loads(preparation_path.read_text())
    prior_path = ROOT / 'tools/reference/druid-form-modes-engine-evidence.json'
    prior = json.loads(prior_path.read_text())
    original = BASE / 'druid-form-modes-source-probe.cpp'
    assert sha(original) == prior['generatedSourceSha256']
    cpp = original.read_text().split('bool first=true;\nvoid emit', 1)[0]
    routines = dict(prior['completeRoutineHashes'])
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == preparation['legacyCommit'] == prior['legacyCommit']
    hashes = {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def extract(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def replace(before, after):
        nonlocal cpp
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)

    for pin in preparation['localTables'].values():
        assert sha(ROOT / '.local/research/d2r-3.3.93847' / pin['filename']) == pin['sha256']
    states_pin = preparation['localTables']['states']
    states = list(csv.DictReader((ROOT / '.local/research/d2r-3.3.93847' / states_pin['filename']).read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    size = max(int(row['*ID']) for row in states if row.get('*ID', '').isdigit()) + 1
    group = [(row['state'], int(row['*ID'])) for row in states if row.get('group') == '3']
    assert group == [('maul', 117), ('feralrage', 120), ('wolf', 139), ('bear', 140), ('monsterset', 176), ('delerium', 177)]
    unrelated = next(row for row in states if row.get('state') == 'enchant')
    assert unrelated['*ID'] == '16' and unrelated['group'] == ''
    maul, feral = preparation['currentRows']['skills']['Maul'], preparation['currentRows']['skills']['Feral Rage']
    assert [maul[key] for key in ['Param1', 'Param3', 'Param4', 'Param5', 'Param6', 'Param7', 'Param8']] == ['3', '30', '500', '10', '100', '2', '3']
    assert [feral['Param' + str(i)] for i in range(1, 9)] == ['500', '4', '10', '70', '50', '5', '2', '3']
    replace('#define __fastcall', '#define __fastcall\n#define __stdcall\n#define D2_ASSERT assert\n#define D2_MAYBE_UNUSED(x) (void)(x)')
    replace('STAT_MANA=9};', 'STAT_MANA=9,STAT_SKILL_FRENZY=10,DAMAGERESULTFLAG_SUCCESSFULHIT=1};')
    replace('std::array<int,10> stats{};', 'std::array<int,16> stats{};')
    replace('rank=1;};', 'rank=1,nPar1=0;};')
    replace('wMana=15,wLevelMana=0,nManaShift=8;', 'wMana=15,wLevelMana=0,nManaShift=8,nEType=0,nSrcDam=128;int dwCalc[4]={102,101,0,103};unsigned dwToHitCalc=unsigned(-1);int dwToHit=40,dwLevToHit=15;')
    replace('int nStatesTxtRecordCount=150;D2StatesTxt states[150];', f'int nStatesTxtRecordCount={size};D2StatesTxt states[{size}];')
    replace('std::array<bool,150> states{};std::array<D2StatListStrc*,150> lists{};', f'std::array<bool,{size}> states{{}};std::array<D2StatListStrc*,{size}> lists{{}};')
    replace('s>=0&&s<150?', f's>=0&&s<{size}?')
    replace('s>=0&&s<10?', 's>=0&&s<16?')
    replace('D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return hasSkill?&skill:nullptr;}',
        'extern D2SkillStrc formSkill;D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){assert(id==223||id==228||id==232||id==233);return !hasSkill?nullptr:id==223||id==228?&formSkill:&skill;}')
    formula = cpp[cpp.index('int SKILLS_EvaluateSkillFormula('):cpp.index('void* SKILLS_GetItemStatCostTxtRecord')].rstrip()
    replace(formula, r'''
// Explicit current-row formula service. Skill parser/Fog/stat callbacks are
// outside this probe; record formula level to distinguish rank from counter.
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int level){
 events.push_back({40,f,level});assert(level>0);
 if(f==100)return 500;if(f==101)return level/2+3;if(f==102)return form?50+5*(level-1):0;
 if(f==103){assert(false);return 0;}
 if(!form){if(f==0)return 30*level;if(f==1)return 10+90*std::min(100,110*level/(level+6))/100;if(f==2)return 3*level;}
 else{if(f==0)return 10+60*std::min(100,110*level/(level+6))/100;if(f==1||f==2)return 4*level;}
 assert(false);return 0;
}
'''.strip())
    replace('void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}', r'''
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*p){assert(p==&unit);return &game;}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int kind,int state){assert(kind==1);events.push_back({41,state,0});}
bool SUNIT_IsDead(D2UnitStrc*){return false;}
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc*,int){assert(false);return false;}
void SKILLS_RefreshPassiveSkills(D2UnitStrc*){events.push_back({42,0,0});}
void sub_6FC7DF40(D2GameStrc*,D2UnitStrc*){events.push_back({43,0,0});}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int);
''' + extract('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD10E50'))
    cpp += r'''
struct D2DamageStrc{int wResultFlags=0,dwConvPct=0,nConvType=0,dwEnDmgPct=0;};
D2UnitStrc target;D2DamageStrc packet;D2SkillStrc formSkill;
bool targetPresent=true;int hitFlags=1,packetCount=0,drains=0,srcDam=0,skillId=233;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==232||id==233);return SKILLS_GetSkillsTxtRecord(id);}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*p){assert(p==&unit);return targetPresent?&target:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*p,const char*,int){assert(p==&skill);return skillId;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*t,int ar,int mode)
{assert(p==&unit&&t==&target&&!mode);assert(ar==(form?20+10*(rank-1):40+15*(rank-1)));events.push_back({44,ar,hitFlags});return hitFlags;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*p,uint8_t source)
{packet=*p;packetCount++;srcDam=source;events.push_back({45,p->dwEnDmgPct,source});}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int n)
{assert(!n&&packetCount==1);drains++;events.push_back({46,0,0});}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int state,int on){assert(on);events.push_back({47,state,on});}
'''
    common = 'source/D2Common/src/D2Skills.cpp'
    for name in ['SKILLS_GetParam1', 'SKILLS_SetParam1', 'SKILLS_GetToHitFactor']:
        cpp += extract(common, name)
    druid = 'source/D2Game/src/SKILLS/SkillDruid.cpp'
    for name in ['SKILLS_SrvSt56_FeralRage_Maul', 'SKILLS_SrvDo120_FeralRage_Maul']:
        cpp += extract(druid, name)
    cpp += r'''
void add(int state,int count,Callback callback){auto*p=&storage[allocated++];assert(allocated<=8);*p={};p->state=state;p->expire=400;p->posted=true;p->callback=callback;p->stats[STAT_SKILL_FRENZY]=count;p->stats[STAT_MODIFIERLIST_LEVEL]=255;p->stats[STAT_MODIFIERLIST_SKILL]=state==139?223:state==140?228:state==117?233:232;unit.states[state]=true;unit.lists[state]=p;}
int main(){
 for(int state:{117,120,139,140,176,177})tables.states[state].wGroup=3;
 printf("[");bool first=true;
 for(int family:{0,1})for(int level:{1,2,3,20,28,60,255})for(int previous:{0,1,3,13,130})for(int scene=0;scene<16;scene++){
  form=family;rank=level;unit={};target={};skill={};formSkill={};skill.mode=formSkill.mode=7;row={};allocated=0;packet={};events.clear();packetCount=drains=srcDam=0;
  failure=scene==4?1:scene==6?3:0;used=scene!=5;hasSkill=true;targetPresent=scene!=3;hitFlags=scene==1?0:scene==2?2:1;skillId=form?232:233;
  int state=form?120:117;row.nAuraState=state;row.dwAuraLenCalc=100;row.dwToHit=form?20:40;row.dwLevToHit=form?10:15;row.nSrcDam=scene==15?0:128;
  for(int i=0;i<6;i++){row.wAuraStat[i]=-1;row.dwAuraStatCalc[i]=i;}if(!form){row.wAuraStat[0]=0;row.wAuraStat[1]=2;row.wAuraStat[2]=STAT_ATTACKRATE;}else{row.wAuraStat[0]=3;row.wAuraStat[1]=4;row.wAuraStat[2]=5;}
  if(previous){add(state,previous,sub_6FD10E50);sub_6FCFE0E0(&unit,unit.lists[state],&row,skillId,previous);}add(form?139:140,0,sub_6FCFE030);add(16,0,nullptr);
  if(scene==11){add(form?117:120,1,sub_6FD10E50);unit.states[176]=unit.states[177]=true;}
  events.clear();
  const int start=SKILLS_SrvSt56_FeralRage_Maul(&game,&unit,form?232:233,rank);const int latch=skill.nPar1;
  if(scene==7)targetPresent=false;if(scene==9)skillId=0;if(scene==13)used=false;
  int effectRank=scene==8?std::max(1,rank/2):scene==10?std::min(255,rank*2):rank;
  int done=start?SKILLS_SrvDo120_FeralRage_Maul(&game,&unit,form?232:233,effectRank):-1;
  auto*list=unit.lists[state];int count=list?list->stats[STAT_SKILL_FRENZY]:0,expiry=list?list->expire:0,stored=list?list->stats[STAT_MODIFIERLIST_LEVEL]:0;
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",family,level,previous,scene,start,done,latch,packetCount,drains,count,expiry,stored,effectRank,packet.dwEnDmgPct,srcDam);first=false;
  for(int i=0;i<16;i++)printf("%s%d",i?",":"",list?list->stats[i]:0);
  if(scene==11&&start&&done){auto*shape=unit.lists[form?139:140];D2Common_10474(&unit,shape);STATLIST_FreeStatList(shape);for(int s:{117,120,139,140,176,177})assert(!unit.states[s]&&!unit.lists[s]);assert(unit.states[16]&&unit.lists[16]&&formSkill.mode==PLRMODE_CAST&&skill.mode==7);}
  if(scene==12&&list){D2Common_10474(&unit,list);STATLIST_FreeStatList(list);assert(!unit.states[state]&&!unit.lists[state]&&unit.states[form?139:140]&&unit.states[16]);}
  printf("],[");for(unsigned i=0;i<events.size();i++)printf("%s[%d,%d,%d]",i?",":"",events[i][0],events[i][1],events[i][2]);printf("]]");
 }printf("]\n");
}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    generated = BASE / 'druid-charged-attacks-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-charged-attacks-compiler.log'
    with compiler.open('wb') as output:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-charged-attacks-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    cases = json.loads(process.stdout)
    assert len(cases) == 1120
    for family, level, previous, scene, start, done, latch, packets, drains, count, expiry, stored, effect_rank, ed, src, stats, events in cases:
        valid = scene not in [3, 4, 5]
        assert start == int(valid) and packets == int(valid)
        assert latch == int(valid and scene not in [1, 2])
        assert ed == (50 + 5 * (level - 1) if valid and latch and family else 0)
        assert src == (128 if valid else 0)
        do_valid = valid and scene not in [9, 13]
        assert done == (-1 if not valid else int(do_valid))
        assert drains == int(do_valid and scene != 7)
        gain = do_valid and latch and not (scene == 6 and previous == 0)
        expected = min(effect_rank // 2 + 3, previous + 1) if gain else previous
        assert count == expected and expiry == (600 if gain else 400 if previous else 0)
        assert stored == (effect_rank if gain else 255 if previous else 0)
        if count:
            dm = min(100, 110 * count // (count + 6))
            assert [stats[i] for i in ([0, 2, 1, 6] if not family else [3, 4, 5])] == ([30 * count, 10 + 90 * dm // 100, 3 * count, 3 * count] if not family else [10 + 60 * dm // 100, 4 * count, 4 * count])
        if gain:
            writes = [i for i, e in enumerate(events) if e[0] == 6 and e[1] == 10]
            if drains:
                assert next(i for i, e in enumerate(events) if e[0] == 46) < writes[0]
    fixture = BASE / 'druid-charged-attacks-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, handlerEnabled=False,
        legacyCommit=tree['sha'], preflightSha256=sha(preparation_path), predecessor=dict(path=str(prior_path.relative_to(ROOT)), sha256=sha(prior_path)),
        sourceHashes=hashes, completeRoutineHashes=routines, stateTableSize=size, group3Members=group, unrelatedFlagControl=dict(state='enchant', stateId=16, group=0, fullEnchantCallbackExecuted=False),
        contexts=len(cases), generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        established=[
            'Complete start56/do120,Param1getter/setter,ToHit getter,shared aura writer,generic expiry and form-group removal callbacks execute1120bounded contexts. Seven ranks,two skills,five prior counters and16scenarios compare hit latch,source128fallback,damage-percent field,allocation,cap/downrank/uprank,expiry and all mapped aura channels.',
            'A successful latch refreshes500frames and sets counter=min(current effective-rank cap,old+1),overwriting stored rank. Aura formula LEVEL is the charge count. Maul mirrors3-per-charge attackrate into OTHER_ANIMRATE; Feral adds velocity and matched life-drain endpoints without that haste channel. A failed hit does not refresh existing state; aura allocation failure retains prior combat/drain behavior without new state.',
            'Generic charge expiry removes only the charge state while retaining the form. The real form-removal callback clears matching group3charge states/lists and current-form state,retaining an unrelated group. The current full table includes monsterset176anddelerium177in group3as well; flag-only synthetic controls verify traversal without executing foreign transformation callbacks.',
        ], declaredServices=[
            'Formula evaluation is an explicitly injected current-row integer service with recorded formula IDs/levels,not a Fog/compiler execution claim. Skill/state/stat storage use declared minimal structures and stat-channel IDs; current state IDs/groups are byte-pinned. Blank ToHitCalc is represented by the conventional no-calc sentinel. Runtime parser/stat aggregation and source ABI layouts are not compiled here.',
            'Hit-result flags,combat allocation and durability/combat drain are ordered recording services. Complete AR/block/dodge/mitigation/contact/HP/item/mana and event queue processing are NOT executed. Nonzero conversion type is guarded unexecuted because both current rows lack it. Invalid state IDs remain unsupported; the legacy impossible-AND guard is not repaired.',
            'Animation/velocity/passive refresh and player skill packets are recording services. Normal live-player charge expiry executes; dead/stay-death and foreign-form callbacks remain outside scope. Existing form lifecycle bodies retain predecessor hashes; transform entry itself is not driven by these charge cases.',
        ], remaining=[
            'Legacy target loss after a successful start skips drain but still adds the latched charge. Treat this as a current-D2Runcertainty,not a silently adopted mechanical rule. Live rank changes and start/do/contact payment need complete wrapper and native acceptance.',
            'Native charge/stat/leech/velocity/stun/attack-rate consumers,action/input/gear/expiry/form cleanup/save/reload and exact current timing/owner/client remain to implement. Assassin cap3/consume-one/retained-rank rules are not equivalent. No new handler,class completion,approved deviation or final art is claimed.',
        ])
    path = ROOT / 'tools/reference/druid-charged-attacks-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Maul/Feral:1120complete charge/start/removal contexts,ASan/UBSan clean; combat/formula services explicit,no native handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
