#!/usr/bin/env python3
"""Compose retained death entry with ordinary animation scheduling and ENDANIM.

Current owned animation data supplies the frame inputs. Outer mode-change/rate
initialization and the modern corpse-pool insertion callback remain unproven.
"""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'

def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_death_entry_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    subprocess.run([sys.executable,str(ROOT/'tools/corroborate_local_sentry_animation.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    parent=json.loads((ROOT/'tools/reference/sentry-death-entry-source-evidence.json').read_text())
    animation=json.loads((ROOT/'tools/reference/d2r-local-sentry-animation-evidence.json').read_text())
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    hashes=dict(parent['sourceHashes']);routines=dict(parent['compiledRoutineHashes'])
    def source(path):
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==path)
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def extract(text,name):
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    common=source('source/D2Common/src/Units/Units.cpp')
    unit=source('source/D2Game/src/UNIT/SUnit.cpp')
    mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp')
    header=source('source/D2Game/include/GAME/Event.h')
    event_enum=re.search(r'enum D2C_EventTypes : uint8_t\s*\{.*?^\};',header,re.M|re.S)[0]
    # Reuse all compiled death/state/ownership bodies and their declared ABI.
    cpp=(STAGE/'sentry-death-entry-source-probe.cpp').read_text().split('int main()',1)[0]
    cpp=cpp.replace('struct D2UnitStrc{','struct D2AnimSeqTxt{};struct ClockAnim{uint8_t pFrameFlags[144]={};};struct D2UnitStrc{int dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=23*256,nSeqCurrentFramePrecise=0;D2AnimSeqTxt* pAnimSeq=nullptr;ClockAnim* pAnimData=nullptr;bool frozen=false;',1)
    cpp=cpp.replace('struct D2GameStrc{','struct D2GameStrc{int dwGameFrame=100;',1)
    cpp=cpp.replace('int unk0x14[1]={0};','int unk0x14[1]={0};int nMode=0;',1)
    cpp+=r'''
#include <vector>
constexpr int UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7,NUM_WEAPON_CLASSES=14,WEAPONCLASS_HTH=0;
constexpr int PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,PLRMODE_SPECIAL3=14,PLRMODE_SPECIAL4=15,MONMODE_DEAD=12,MONMODE_NEUTRAL=1,PLRMODE_DEATH=0,PLRMODE_DEAD=17;
constexpr int STATE_FREEZE=1;
EVENT_ENUM_PLACEHOLDER
struct ClockEvent{int type,frame,arg,index;};std::vector<ClockEvent> events;
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int* type,int*,int*,const char*,int){assert(*type==UNIT_MONSTER);}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){assert(false);return nullptr;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){assert(false);return 0;}
bool UNITS_CanDualWield(D2UnitStrc*){assert(false);return false;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int t,int f,int a,int i){events.push_back({t,f,a,i});}
bool STATES_CheckState(D2UnitStrc* u,int s){assert(s==STATE_FREEZE);return u->frozen;}
int32_t SUNIT_IsDead(D2UnitStrc*);
void UNITS_SetUsedSkill(D2UnitStrc*,void*){assert(false);}
int sub_6FC64310(D2GameStrc*,D2ModeChangeStrc*){assert(false);return 0;}
struct D2MonModeCallbackTableStrc{int(*unk0x00)(D2GameStrc*,D2ModeChangeStrc*);int(*unk0x04)(D2GameStrc*,D2UnitStrc*);void(*unk0x08)(D2GameStrc*,D2UnitStrc*);int flag;};
const D2MonModeCallbackTableStrc* MONSTERMODE_GetCallbackTableRecord(D2UnitStrc*,int mode){assert(mode==0);static const D2MonModeCallbackTableStrc row{sub_6FC63B30,nullptr,sub_6FC641D0,1};return &row;}
'''
    # Check the exact source chart row and death scheduler selection, while
    # retaining lookup/outer mode-change as explicit checked services.
    assert '{ sub_6FC63B30, sub_6FC64280,' in mode
    assert re.search(r'constexpr D2MonModeInfoTableStrc stru_6FD28738\[16\]\s*=\s*\{\s*\{ 0, 1, 0 \}',mode)
    assert 'if (pMonModeInfoTableRecord->unk0x04 || !pMonModeInfoTableRecord->unk0x08)' in mode
    assert 'sub_6FCBCE70(pGame, pUnit);' in mode
    cpp=cpp.replace('EVENT_ENUM_PLACEHOLDER',event_enum)
    cpp+=extract(unit,'SUNIT_IsDead')+extract(common,'UNITS_GetFrameBonus')+extract(unit,'sub_6FCBCE70')
    cpp+=extract(mode,'D2GAME_MONSTERS_AiFunction02_6FC65150')
    cpp+='using MonEventFunc=void(*)(D2GameStrc*,D2UnitStrc*,int,int);MonEventFunc gpMonsterEventFunctions[15]={nullptr,D2GAME_MONSTERS_AiFunction02_6FC65150};\n'
    cpp+=extract(mode,'MONSTERMODE_EventHandler')
    cpp+=r'''
int main(){printf("[");bool first=true;
for(int frames:{1,23,25})for(int speed:{0,128,256,341,512})for(int freeze:{0,1})for(int inherited:{0,17,1000}){
 game={};owner={};pet={};root={};room={};region={};monster={};model={};ClockAnim anim;D2PathStrc path;D2MonsterRegionStrc* regions[]={&region};game.pMonReg=regions;
 owner.dwUnitType=0;owner.dwUnitId=1;pet.dwUnitId=17;pet.dwUnitType=1;pet.room=&room;pet.pDynamicPath=&path;pet.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;pet.dwFlags=UNITFLAG_NOTC;pet.dwOwnerType=0;pet.dwOwnerGUID=1;
 pet.ai=AIGENERAL_AllocAiControl(&game);AIGENERAL_SetOwnerData(&game,&pet,1,0,0,0);game.pUnitList[0][1]=&owner;game.pUnitList[1][17]=&pet;
 root.values=pet.stats;root.dwFlags=STATLIST_EXTENDED;pet.pStatListEx=&root;pet.stats[5]=inherited;pet.pAnimData=&anim;pet.wAnimSpeed=speed;pet.dwFrameCountPrecise=frames*256;pet.frozen=freeze;
 arrayPresent=true;boss=false;deathEvents=0;events.clear();D2ModeChangeStrc change{&pet,{0}};
 assert(sub_6FC63B30(&game,&change)==1&&pet.dwAnimMode==0&&deathEvents==0);assert(UNITS_GetFrameBonus(&pet)==0);
 sub_6FCBCE70(&game,&pet);assert(events.size()==1&&events[0].type==EVENTTYPE_ENDANIM);
 int end=events[0].frame-100;assert(end>0);if(frames==23&&speed==256)assert(end==23);
 game.dwGameFrame=events[0].frame;MONSTERMODE_EventHandler(&game,&pet,EVENTTYPE_ENDANIM,0,0);
 assert(pet.dwAnimMode==12&&deathEvents==1&&pet.stats[5]==inherited&&SUNIT_GetServerUnit(&game,1,17)==&pet&&SUNIT_GetOwner(&game,&pet)==&owner&&AIGENERAL_GetMinionOwner(&pet)==&owner);
 printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",frames,speed,freeze,inherited,end,pet.dwAnimMode,deathEvents);first=false;delete pet.ai;
}printf("]\n");}
'''
    path=STAGE/'sentry-death-clock-source-probe.cpp';path.write_text(cpp)
    binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-death-clock-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout);assert len(rows)==90
    fixture=STAGE/'sentry-death-clock-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    dt=next(r for r in animation['records'] if r['code']=='LGDTHTH');assert dt==dict(code='LGDTHTH',frames=23,speed=256,events=[])
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],nativePolicyChanged=False,sourceHashes=hashes,routineHashes=routines,
        animationEvidence='tools/reference/d2r-local-sentry-animation-evidence.json',currentDeathAnimation=dt,
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=90),
        established=['Complete retained death-entry/cleanup/owner bodies compose with the ordinary frame scheduler, full ENDANIM dispatch and regular death-end body.',
        'At supplied current LGDTHTH frames23/speed256 and the unchanged NPC frame-bonus routine, ENDANIM occurs23 frames after entry. Frozen dead actors still receive ENDANIM; inherited root stats and original registered owner survive.',
        'Zero speed, single-frame, alternate-rate, Wake-length and freeze controls exercise scheduler bounds without inventing a fixed corpse expiry.'],
        limits=['Outer mode change, animation/rate initialization, callback lookup and event queue execution are checked services; the complete current executable is not executed.',
        'The source chart selects ordinary death scheduling, but current metadata plus reconstructed engine composition is not a proof of all modern modifiers or callback ordering.',
        'Modern corpse-pool insertion phase, natural exhaustion/replacement death entry, cross-family removal, room unload and native/client acceptance remain separate. No public skill is enabled.'],
        sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/sentry-death-clock-source-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('90 composed death-entry/scheduler/ENDANIM cases; supplied current LG death23; original stats/owner retained; no native policy change.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
