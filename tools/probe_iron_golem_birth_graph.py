#!/usr/bin/env python3
"""Compose complete summon birth and item stat graph routines, with NPC HP callbacks."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'iron-golem-birth-graph-source'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    proof = ROOT / 'tools/reference/npc-equipment-resources-source-evidence.json'
    prior = json.loads(proof.read_text())
    seed = BASE / 'npc-equipment-resources-source/source.cpp'
    assert sha(seed) == prior['artifacts'][str(seed.relative_to(ROOT))]
    inputs = {str(path.relative_to(ROOT)): sha(path) for path in [proof, seed, Path(__file__).resolve()]}
    cpp = seed.read_text().split('int main(){')[0]
    inherited_proof = ROOT / 'tools/reference/item-stat-values-source-evidence.json'
    inherited = json.loads(inherited_proof.read_text())
    reused = inherited['compiledUnchangedRoutineHashes']
    for name, digest in reused.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    inputs[str(inherited_proof.relative_to(ROOT))] = sha(inherited_proof)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    routines = {}

    def body(relative, name):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        text = function(raw.decode(), name)
        routines[name] = hashlib.sha256(text.encode()).hexdigest()
        return text + '\n'

    ref = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((ref / 'sources.json').read_text())['files']
    tables = {}
    for name in ['skills', 'monlvl', 'itemstatcost']:
        path = ref / 'json' / (name + '.json')
        assert sha(path) == manifest['json/' + name + '.json']['sha256']
        inputs[str(path.relative_to(ROOT))] = sha(path)
        tables[name] = json.loads(path.read_text())
    birth_proof = ROOT / 'tools/reference/iron-golem-birth-source-evidence.json'
    birth = json.loads(birth_proof.read_text())
    for name, digest in birth['inputs'].items():
        assert sha(ROOT / name) == digest, name
    inputs[str(birth_proof.relative_to(ROOT))] = sha(birth_proof)
    metadata = ROOT / 'data/Sanctuary/Balance/generic-pet-states.json'
    assert next(row['id'] for row in json.loads(metadata.read_text()) if row['name'] == 'thorns') == 36
    inputs[str(metadata.relative_to(ROOT))] = sha(metadata)

    # Extend only fixture ABI declarations, preserving every reused routine.
    cpp = cpp.replace('struct D2UnitStrc;', 'struct D2MonStatsTxt{int dwDamageRegen=3;};struct MonsterData{D2MonStatsTxt*pMonstatsTxt=nullptr;};\nstruct D2UnitStrc;', 1)
    cpp = cpp.replace('void*pInventory=nullptr;', 'void*pInventory=nullptr;void*pMemoryPool=nullptr;int dwUnitId=1;MonsterData*pMonsterData=nullptr;', 1)
    cpp = cpp.replace('unk0x5E[64];}', 'unk0x5E[64];int wItemEvent[2]={},wItemEventFunc[2]={};}', 1)
    cpp = cpp.replace('struct Tables{', 'struct D2MonLvlTxt{int dwLAC[3],dwAC[3],dwLTH[3],dwTH[3];};\nstruct Tables{int nMonLvlTxtRecordCount=111,nStatesTxtRecordCount=200,nOverlayTxtRecordCount=500;D2MonLvlTxt pMonLvlTxt[111];', 1)
    full_skill = '''struct D2SkillsTxt{int nPassiveStat[5]={67,19,31,25,187},dwPassiveCalc[5]={0,1,2,3,4},wAuraStat[6]={78,181,-1,-1,-1,-1},dwAuraStatCalc[6]={5,6,0,0,0,0};
int nAuraState=36,dwCalc[1]={11},wSumSkill[5]={-1,-1,-1,-1,-1},dwSumSkCalc[5]={},dwFlags[1]={},wAuraEvent[3]={-1,-1,-1},wAuraEventFunc[3]={},wSumUMod=0,wSumOverlay=0;};'''
    assert cpp.count('struct D2SkillsTxt{int dwCalc[1];};') == 1
    cpp = cpp.replace('struct D2SkillsTxt{int dwCalc[1];};', full_skill)
    cpp = cpp.replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){assert(false);return 0;}', 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int);')
    declaration = 'void Changed(void*,D2UnitStrc*owner,D2UnitStrc*source,uint32_t packed,int old,int value){trace.push_back({owner?owner->id:0,source?source->id:0,int(packed),old,value});}'
    assert cpp.count(declaration) == 1
    cpp = cpp.replace(declaration, 'void ResourceChanged(D2UnitStrc*,int,int,int);\nvoid Changed(void*,D2UnitStrc*owner,D2UnitStrc*source,uint32_t packed,int old,int value){trace.push_back({owner?owner->id:0,source?source->id:0,int(packed),old,value});if(owner&&owner->dwUnitType==1)ResourceChanged(owner,packed>>16,old,value);}')
    cpp += r'''
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int STAT_LEVEL=12,STAT_TOHIT=19,STAT_HITPOINTS=6,STAT_HPREGEN=74,STAT_PASSIVE_SUMMON_RESIST=349,
STAT_ITEM_ABSORBFIRE_PERCENT=142,STAT_ITEM_ABSORBLIGHT_PERCENT=144,STAT_ITEM_ABSORBCOLD_PERCENT=148,
STAT_FIRERESIST=39,STAT_LIGHTRESIST=41,STAT_COLDRESIST=43,STAT_POISONRESIST=45,SKILLSFLAGINDEX_AURA=0;
using D2C_UnitEventTypes=int;
int D2Clamp(int value,int low,int high){return std::clamp(value,low,high);}
int STATLIST_GetTotalStatValue_Layer0(D2StatListStrc*,int);
int D2Common_11248(D2UnitStrc*,D2UnitStrc*,int);
struct D2GameStrc{void*pMemoryPool=nullptr;uint8_t nDifficulty=0,nGameType=0;int dwGameType=0;}game;
D2SkillsTxt skill;
int rank=1,mastery=0,thorns=0,equipmentLevel=0,eventCount=0,allocations=0;
D2StatListStrc allocated[4],casterRoot;D2UnitStrc caster;D2MonStatsTxt monster;MonsterData monsterData{&monster};
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){if(id<0)return nullptr;assert(id==90);return &skill;}
D2ItemStatCostTxt*SKILLS_GetItemStatCostTxtRecord(int id){return id>=0&&id<int(rows.size())?&rows[id]:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int f,int id,int r){assert(p==&caster&&id==90&&r==rank);switch(f){
case 0:return mastery>0?40*std::min(100,110*mastery/(mastery+6))/100:0;case 1:return 25*mastery+20*20;
case 2:return rank*35;case 3:return 6*20;case 4:return 0;case 5:return thorns;case 6:return 16;case 11:return 20*mastery+5*20;default:assert(false);return 0;}}
bool SUNITEVENT_GetEvent(D2GameStrc*,D2UnitStrc*,int,int,int){return false;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int event,int layer,int zero,int func,int type,int guid){assert(event==1&&layer==(78<<16)&&zero==0&&func==6&&type==2&&guid==1);eventCount++;}
D2StatListStrc*STATLIST_AllocStatList(void*,int flags,int,int kind,int){assert(allocations<4);auto*p=new(&allocated[allocations++])D2StatListStrc;p->dwFlags=flags;p->dwOwnerType=kind;return p;}
void STATES_ToggleState(D2UnitStrc*,int n,int enabled){assert(n==36&&enabled==1);}
void STATLIST_SetState(D2StatListStrc*p,int n){p->dwStateNo=n;}
void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int,int,int){assert(false);}
void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int,int){assert(false);}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}
void UNITS_SetOverlay(D2UnitStrc*,int,int){assert(false);}
void sub_6FCF9580(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int level,int){equipmentLevel=level;}
'''
    # Use the actual thin stat wrappers too, rather than treating setters as assignments.
    relative = 'source/D2Common/src/D2StatList.cpp'
    for name in ['STATLIST_SetStat', 'STATLIST_AddStat', 'STATLIST_SetStatIfListIsValid', 'STATLIST_SetUnitStat', 'STATLIST_AddUnitStat', 'STATLIST_GetTotalStatValue_Layer0', 'D2Common_11248', 'STATLIST_GetMaxLifeFromUnit']:
        cpp += body(relative, name)
    callback_path = BASE / 'd2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp'
    raw = callback_path.read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == 'source/D2Game/src/ITEMS/ItemMode.cpp')
    raw_callback = raw.decode()
    branch = raw_callback[raw_callback.index('    case STAT_MAXHP:'):raw_callback.index('    case STAT_MAXMANA:')]
    cpp += 'void ResourceChanged(D2UnitStrc*pOwner,int nStatId,int nPreviousValue,int nNewValue){switch(nStatId){\n' + branch + 'default:break;}}\n'
    inputs[str((BASE / 'd2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp').relative_to(ROOT))] = sha(BASE / 'd2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp')
    cpp += body('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage')
    for name in ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530', 'D2GAME_SetSummonResistance_6FD0C2E0']:
        cpp += body('source/D2Game/src/SKILLS/SkillNec.cpp', name)
    cpp += r'''
void Start(int ownerLevel,int r,int difficulty,int expansion,int gm,int profile,int initialLevel,int partial){
new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;new(&casterRoot)D2StatListStrc;trace.clear();
actor={1,1,0,&root,nullptr};actor.pMonsterData=&monsterData;weapon={2,4,0,&item,nullptr};caster={3,0,0,&casterRoot,nullptr};
root.pOwner=&actor;root.dwOwnerType=1;item.pOwner=&weapon;item.dwOwnerType=4;bonus.dwFlags=0x40;
root.pfOnValueChanged=item.pfOnValueChanged=Changed;casterRoot.pOwner=&caster;casterRoot.dwOwnerType=0;
int hp=(difficulty==0?306:difficulty==1?595:980)*256+17;
for(auto pair:{std::array{7,hp},std::array{6,partial?17:hp},std::array{12,initialLevel},std::array{31,140},std::array{19,0},std::array{74,hp*3>>12},std::array{76,50}}){Raw(root.Stats,pair[0],pair[1]);Raw(root.FullStats,pair[0],pair[1]);}
for(auto pair:{std::array{12,ownerLevel},std::array{349,52}}){Raw(casterRoot.Stats,pair[0],pair[1]);Raw(casterRoot.FullStats,pair[0],pair[1]);}
rank=r;mastery=gm;allocations=eventCount=equipmentLevel=0;game.nDifficulty=difficulty;game.nGameType=expansion;
if(profile==1||profile==4){Raw(bonus.Stats,7,60*256);Raw(item.Stats,31,150);Raw(bonus.Stats,16,100);}
if(profile==2||profile==4){Raw(bonus.Stats,76,40);}
if(profile==3||profile==4){Raw(bonus.Stats,216,2560);}
if(profile==4){Raw(bonus.Stats,19,80);Raw(bonus.Stats,119,30);}
if(profile==5){Raw(bonus.Stats,142,10);Raw(bonus.Stats,144,10);Raw(bonus.Stats,148,10);}
if(profile==6){Raw(bonus.Stats,76,100);Raw(bonus.Stats,216,768);}
sub_6FDB64A0(&root,D2SLayerStatIdStrc::MakeFromStatId(7).nPackedValue,&rows[7],nullptr);
D2COMMON_10475_PostStatToStatList(&weapon,&bonus,TRUE);STATLIST_MergeStatLists(&actor,&weapon,TRUE);
}
void Dump(){printf("[");Array(root.Stats);printf(",");Array(root.FullStats);printf(",");Array(item.Stats);printf(",");Array(item.FullStats);printf("]");}
int main(){Init();
'''
    for level in range(111):
        row = tables['monlvl'][str(level)]
        for member, field in [('dwAC', 'AC'), ('dwTH', 'TH'), ('dwLAC', 'L-AC'), ('dwLTH', 'L-TH')]:
            for diff, suffix in enumerate(['', '(N)', '(H)']):
                cpp += f'tables.pMonLvlTxt[{level}].{member}[{diff}]={row[field+suffix]};\n'
    cpp += 'rows[78].wItemEvent[0]=1;rows[78].wItemEventFunc[0]=6;int reflected[256]={};\n'
    row = tables['skills']['90']
    for rank in [1, 20, 40]:
        value, remaining = row['EMin'], rank - 1
        for i, width in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(remaining, width); value += count * row['EMinLev' + str(i)]; remaining -= count
        cpp += f'reflected[{rank}]={value};\n'
    cpp += r'''bool first=true;printf("[");
for(int ownerLevel:{24,99})for(int r:{1,20,40})for(int difficulty:{0,1,2})for(int expansion:{0,1})for(int gm:{0,20})for(int profile=0;profile<7;profile++)for(int initialLevel:{1,47})for(int partial:{0,1}){
Start(ownerLevel,r,difficulty,expansion,gm,profile,initialLevel,partial);thorns=reflected[r];
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",ownerLevel,r,difficulty,expansion,gm,profile,initialLevel,partial);first=false;Dump();
D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&caster,&actor,0,rank);int before=STATLIST_GetMaxLifeFromUnit(&actor);
D2GAME_SetSummonPassiveStats_6FD0C530(&game,&caster,&actor,90,rank,0);D2GAME_SetSummonResistance_6FD0C2E0(&caster,&actor);
printf(",");Dump();D2Common_ExpireStatList_6FDB6E30(&item);printf(",");Dump();printf("],[%d,%d,%d],",before,eventCount,equipmentLevel);Trace();printf("]");
}printf("]\n");}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    path = STAGE / 'source.cpp'; path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g1', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    cases = json.loads(run.stdout); assert len(cases) == 2016
    (STAGE / 'cases.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], cases=2016,
        inputs=inputs, compiledUnchangedRoutines=routines, reusedCompiledUnchangedGraphRoutines=reused, maxHpCallbackBranchSha256=hashlib.sha256(branch.encode()).hexdigest(),
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in [STAGE / name for name in ['source.cpp', 'cases.json', 'compiler.log', 'sanitizer.log']]},
        established=['Complete item graph/attachment/getters and source summon base/passive/resistance routines compose with the complete MAXHP callback branch, retaining exact before/birth/detach arrays and callback traces.',
            'Seven declared raw item profiles exercise flat/percentage/per-level life, armor/ED, AR and absorption; both initial levels and current-life conditions expose level-dependent recalculation before passive life reads.'],
        services=['Stat array allocation/lookup and successful list allocation, asserted current formulas, event/state recording and equipment-level request. Raw spawn/item stat arrays are supplied, not inferred item birth.',
            'NPC MAXHP callback executes the complete unchanged branch for positive maxima with declared monster DamageRegen3. Other outer event branches and live Mobile synchronization are not executed.',
            'Older passive loop executes five slots, with zero inherited sunder. Current extra slots and modern sunder mechanics remain outside this proof.'],
        currentExecutableParity=False, learnedHandlerEnabled=False)
    target = ROOT / 'tools/reference/iron-golem-birth-graph-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == encoded
    else: target.write_text(encoded)
    print('Iron Golem birth graph:2016 full source composition contexts, exact stat arrays and MAXHP callback traces;raw item/spawn inputs explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
