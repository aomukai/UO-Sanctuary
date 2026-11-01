#!/usr/bin/env python3
"""Compose complete older do18/group/list/removal and freeze-only event02."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/cold-armor-source'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def probe(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    names = {'source/D2Game/src/SKILLS/Skills.cpp': ('sub_6FD10EC0', 'sub_6FD11C90'),
             'source/D2Game/src/SKILLS/SkillSor.cpp': ('SKILLS_CurseStateCallback_DefensiveBuff', 'SKILLS_SrvDo018_DefensiveBuff', 'SKILLS_EventFunc02_FrozenArmor')}
    sources, blocks = {}, {}
    for rel, symbols in names.items():
        p = ROOT / '.local/class-skills/d2moo-pinned-source' / rel; raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        sources[rel] = sha(p)
        for name in symbols:
            blocks[name] = function(raw.decode(), name)
    prior = json.loads((ROOT / 'tools/reference/enchant-source-evidence.json').read_text())
    shared = ROOT / '.local/class-skills/enchant-source/probe.cpp'
    assert sha(shared) == prior['artifacts'][str(shared.relative_to(ROOT))]
    header = shared.read_text().split(blocks['sub_6FD10EC0'])[0]
    # Reuse the explicit service fixture; no extracted engine body is edited.
    header = header.replace('#define __fastcall', '#define __fastcall\n#define D2_MAYBE_UNUSED(x) (void)(x)')
    header = header.replace('int dwUnitType=0,dwClassId=0,dwUnitId=0;', 'int dwUnitType=0,dwClassId=0,dwUnitId=0,dwFlags=0;')
    header = header.replace('void* pMemoryPool=nullptr;', 'void* pMemoryPool=nullptr;D2GameStrc* pGame=&game;')
    header = header.replace('int nStateFlags[1]={0};', 'int nStateFlags[1]={0};int wGroup=0;')
    header = header.replace('} state;', '} state,states[200];')
    header = header.replace('int wAuraStat[6]', 'int dwCalc[4]={42,0,0,0};int wAuraEvent[3]={1,-1,-1},wAuraEventFunc[3]={2,0,0},wCltOverlayA=0;int wAuraStat[6]')
    header = header.replace('int nItemStatCostTxtRecordCount=256,nStatesTxtRecordCount=200;', 'int nItemStatCostTxtRecordCount=256,nStatesTxtRecordCount=200,nOverlayTxtRecordCount=200;')
    header = header.replace('void(*)()', 'void(*)(D2UnitStrc*,int,D2StatListStrc*)')
    header = header.replace('void sub_6FD10E50(){}', 'void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}')
    header = header.replace('D2StatesTxt* SKILLS_GetStatesTxtRecord(int){return &state;}', 'bool active[200]={};int clears=0,unregisters=0,registrations=0,statpasses=0,rateupdates=0;bool dead=false,stay=false;D2StatesTxt* SKILLS_GetStatesTxtRecord(int id){return id>=0&&id<200?&states[id]:nullptr;}')
    header = header.replace('bool STATES_CheckState(D2UnitStrc*,int){return false;}', 'bool STATES_CheckState(D2UnitStrc*,int id){return active[id];}')
    header = header.replace('void STATES_ToggleState(D2UnitStrc*,int,int){}', 'void STATES_ToggleState(D2UnitStrc*,int id,int on){active[id]=on;clears+=on==0;}')
    header = header.replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return present?&list:nullptr;}', 'D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int id){return present&&list.state==id?&list:nullptr;}')
    header = header.replace('void STATLIST_FreeStatList(D2StatListStrc*){frees++;present=false;}', 'void STATLIST_FreeStatList(D2StatListStrc*){frees++;present=false;}')
    header = header.replace('int formula,int,int){return', 'int formula,int,int rank){if(formula==42){return 1000+rank;}return')
    header += r'''
constexpr int UNIT_PLAYER=0,UNITFLAG_SKSRVDOFUNC=256,STAT_MODIFIERLIST_SKILL=10,STAT_MODIFIERLIST_LEVEL=11;
struct D2DamageStrc{int dwPhysDamage=0,wResultFlags=0,dwFrzLen=0;} captured;
int emitted=0,overlays=0;bool missing=false;
using D2C_UnitEventTypes=int;
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){unregisters++;}
bool SUNIT_IsDead(D2UnitStrc*){return dead;}
bool STATES_CheckStateMaskStayDeathOnUnitByStateId(D2UnitStrc*,int){return stay;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){rateupdates++;}
void sub_6FD14C30(D2UnitStrc*){rateupdates++;}
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){statpasses++;}
void sub_6FCFE200(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){statpasses++;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){registrations++;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc* p){emitted++;captured=*p;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){overlays++;}
'''
    cpp = header + '\n'.join(blocks.values())
    cpp += r'''
int main(){bool first=true;printf("{\"cast\":[");owner.dwUnitId=202;
for(int guard=0;guard<5;guard++)for(int group:{0,1})for(int old:{0,1,10,20,30})for(int rank:{1,10,20})for(int auras:{0,1,3}){
hasRow=guard!=1;row.nAuraState=guard==2?-1:guard==3?200:16;row.wAuraEvent[0]=auras?1:-1;row.wAuraEvent[1]=auras==3?2:-1;row.wAuraEvent[2]=auras==3?3:-1;
for(int i=0;i<200;i++){states[i].wGroup=0;active[i]=false;}states[16].wGroup=group;states[17].wGroup=group;states[18].wGroup=group?2:0;
active[16]=old>0;active[17]=old>0;active[18]=true;present=old>0;list={};list.rank=old;list.state=16;list.skill=52;list.owner=101;list.until=9999;
writes=allocations=frees=refresh=gfx=events=clears=unregisters=registrations=statpasses=0;duration=3600;owner.dwFlags=8;mode=0;mon.dwMonStatsFlags=0;owner.dwUnitType=0;
if(guard==4){row.nAuraState=16;mon.dwMonStatsFlags=1;owner.dwUnitType=1;}
int ret=SKILLS_SrvDo018_DefensiveBuff(&game,&owner,52,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,group,old,rank,auras,ret,owner.dwFlags,clears,frees,allocations,unregisters,registrations,statpasses,writes,gfx,list.rank,list.owner,list.until,active[17],active[18]);first=false;}
printf("],\"freeze\":[");first=true;
for(int guard=0;guard<4;guard++)for(int type:{0,1,2,3})for(int packet:{0,1})for(int physical:{-1,0,1,255,256})for(int rank:{-1,0,1,20})for(int overlay:{-1,0,199,200}){
hasRow=guard!=3;target.dwUnitType=type;row.wCltOverlayA=overlay;D2DamageStrc damage;damage.dwPhysDamage=physical;captured={};emitted=overlays=0;duration=81;
int ret=SKILLS_EventFunc02_FrozenArmor(&game,1,guard==1?nullptr:&owner,guard==2?nullptr:&target,packet?&damage:nullptr,40,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,type,packet,physical,rank,overlay,ret,emitted,captured.wResultFlags,captured.dwFrzLen,overlays,damage.dwPhysDamage);first=false;}
printf("],\"remove\":[");first=true;
for(int isdead:{0,1})for(int retain:{0,1}){dead=isdead;stay=retain;active[16]=true;unregisters=rateupdates=clears=0;SKILLS_CurseStateCallback_DefensiveBuff(&owner,16,&list);printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",",isdead,retain,unregisters,rateupdates,clears,active[16]);first=false;}
printf("]}\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True); p = STAGE / 'probe.cpp'; p.write_text(cpp); binary = STAGE / 'probe'
    subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
    data = json.loads(subprocess.check_output([str(binary)]))
    assert len(data['cast']) == 450 and len(data['freeze']) == 2560 and len(data['remove']) == 4
    for r in data['cast']:
        guard, group, old, rank, auras = r[:5]; valid = guard in (0,4); accepted = guard == 0 and (group or not old or old<=rank)
        reused = bool(accepted and not group and old==rank); allocated = bool(accepted and not reused)
        expected = [int(accepted),264 if valid else 8,2 if valid and group and old else 0,
                    int(valid and group and old>0 or allocated and old>0),int(allocated),int(accepted and auras>0),auras if accepted else 0,
                    2 if accepted else 0,2 if accepted else 0,int(accepted),rank if accepted else old,
                    101 if reused or not accepted else 202,3700 if accepted else 9999,
                    int(old>0 and not(valid and group)),1]
        # When a grouped NPC fails list allocation eligibility, its old list
        # has nevertheless already been removed by the complete outer do18.
        assert r[5:] == expected, (r,expected)
    for r in data['freeze']:
        guard,typ,packet,physical,rank,overlay = r[:6]
        ok = guard==0 and typ in (0,1) and (not packet or physical>0)
        # Formula service returns1000+rank: control flow and packet
        # flags are source evidence; current formula goldens are native tests.
        assert r[6:] == [int(ok),int(ok),32 if ok else 0,(1000 + max(1,rank) if rank<0 else 1000+rank) if ok else 0,int(ok and 0<=overlay<200),physical],r
    for dead,stay,unregisters,rates,clears,active in data['remove']:
        retain = dead and stay
        assert (unregisters,rates,clears,active)==(1,0 if retain else 2,0 if retain else 1,int(retain))
    san = STAGE / 'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(san)],check=True)
    log = STAGE / 'sanitizer.log'
    with log.open('wb') as err:
        assert json.loads(subprocess.check_output([str(san)],stderr=err)) == data
    assert not log.read_bytes(); fixture = STAGE / 'cases.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    tables = ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((tables/'sources.json').read_text())
    for name in ('skills','states'):
        assert sha(tables/'json'/(name+'.json'))==manifest['files']['json/'+name+'.json']['sha256']
    rows = json.loads((tables/'json/skills.json').read_text()).values()
    current = {r['skill']:r for r in rows if r.get('skill') in ('Frozen Armor','Shiver Armor','Chilling Armor')}
    assert [current[n]['srvdofunc'] for n in ('Frozen Armor','Shiver Armor','Chilling Armor')]==[18]*3
    states = json.loads((tables/'json/states.json').read_text()).values()
    current_states = {r['state']:r for r in states if r.get('state') in ('frozenarmor','shiverarmor','chillingarmor')}
    assert {n:(r['*ID'],r['group'],r.get('curse',0)) for n,r in current_states.items()} == {'frozenarmor':(10,1,0),'shiverarmor':(88,1,0),'chillingarmor':(20,1,0)}
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,
                routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},
                currentTable={str((tables/'json'/(n+'.json')).relative_to(ROOT)):sha(tables/'json'/(n+'.json')) for n in ('skills','states')},
                currentRows=current,currentStates=current_states,castCases=len(data['cast']),freezeCases=len(data['freeze']),removalCases=len(data['remove']),sanitizers='ASan/UBSan clean',
                established=['Complete do18 clears state group including itself before list creation; weaker grouped recasts replace. Invalid row/state refuses before writes; monster eligibility failure can follow group removal.',
                             'Complete event02 admits player/monster targets, requires positive physical damage when a packet exists, normalizes negative rank and sends freeze-only flags32 through events; no direct damage or melee attack is inserted.',
                             'Removal always unregisters its state event; retained death-state skips clearing/rate/stat cleanup.'],
                declaredServices=['Pinned older source, not modern executable evidence. State groups/list allocation/stat writes/event registration/formula/overlay/owner eligibility are explicit services; curse-only branches and allocation failure are not inferred.',
                                  'The retained Enchant service scaffold is adapted without modifying extracted bodies. Frozen callback formula values are supplied, not full compiler/mastery/resistance or sibling-subscriber ordering proof. Shiver/event03 and Chilling/event01 missile creation remain separate.'],
                artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),shared,p,binary,san,log,fixture)})
    out=ROOT/'tools/reference/cold-armor-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Cold armor:450complete cast/group/list,2560freeze-only callback and4removal cases; ASan/UBSan clean; dependent services/modern chronology explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');probe(p.parse_args().check)
