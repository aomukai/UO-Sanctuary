#!/usr/bin/env python3
"""Compose complete ordinary Fury mode entry/action/end with source scheduling.

The per-owner event container is a declared FIFO service, not a complete game
queue. Accepted start/stop intents are explicit; client reentry remains separate.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
    def source(rel):
        raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def extract(rel,name):
        text=source(rel);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        i=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
        body=text[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    base=(STAGE/'blade-fury-release-source-probe.cpp').read_text().split('int main()',1)[0]
    evidence=json.loads((ROOT/'tools/reference/blade-fury-release-source-evidence.json').read_text())
    for name,h in evidence['compiledRoutineHashes'].items():
        candidates=[p for p in evidence['sourceHashes'] if re.search(r'\b'+name+r'\([^;\n]*\)\s*\n\{',source(p))]
        assert len(candidates)==1,name
        assert extract(candidates[0],name).strip() in base and routines[name]==h,name
    def replace(a,b):
        nonlocal base
        assert a in base,a;base=base.replace(a,b,1)
    base='#include <vector>\n#define D2_ASSERT(x) assert(x)\n'+base
    replace('STATE_INFERNO=1,','EVENTTYPE_MODECHANGE=2,EVENTTYPE_ENDANIM=3,STATE_INFERNO=1,')
    replace('struct D2StatListExStrc;', 'struct D2StatListExStrc;struct D2AnimSeqTxt{int wSequence,nMode,nFrame,nDir,nEvent;};\nstruct Anim{int dwFrames=19;uint8_t pFrameFlags[32]={};}anim;')
    replace('D2StatListExStrc* pStatListEx=nullptr;}owner,created;',
            'D2StatListExStrc* pStatListEx=nullptr;int dwClassId=6,dwAnimMode=18,nActionFrame=0,nSeqCurrentFramePrecise=0,dwSeqSpeed=256,dwSeqFrameCount=19*256,wAnimSpeed=256,dwFrameCountPrecise=19*256;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=&anim;void* pInventory=nullptr;}owner,created,targetUnit;')
    replace('int wSrvStartFunc=26,','int wSrvStartFunc=26,')
    replace('dwDelay=-1,nAnim=18;}row;','dwDelay=-1,nAnim=18;}row;\nbool used=true;D2UnitStrc* selected=nullptr;int actionOrdinal=0;std::string trace;\nstruct Event{int type,frame,kind,param;};std::vector<Event> events;')
    replace('void sub_6FCBD120(D2GameStrc*,D2UnitStrc*,int one){assert(one==1);rewinds++;}',
            'void sub_6FCBD120(D2GameStrc*,D2UnitStrc*,int);')
    replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int until,int,int){assert(type==EVENTTYPE_REMOVESTATE&&until==state.until);}',
            'void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int until,int kind,int param){events.push_back({type,until,kind,param});}')
    replace('void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){assert(false);}',
            'void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc* p){used=p!=nullptr;}')
    replace('void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){assert(false);}',
            'void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int);')
    replace('return learned?&skill:nullptr;}\nint SKILLS_GetFlags', 'return used&&learned?&skill:nullptr;}\nint SKILLS_GetFlags')
    replace('D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){assert(false);return nullptr;}',
            'D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected;}')
    replace('void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}',
            'void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int custom){std::erase_if(events,[&](auto e){return e.type==type&&(!custom||e.kind==custom);});}')
    seq='source/D2Common/src/DataTbls/SequenceTbls.cpp';unit='source/D2Game/src/UNIT/SUnit.cpp';plr='source/D2Game/src/PLAYER/PlrModes.cpp';skills='source/D2Game/src/SKILLS/Skills.cpp'
    text=source(seq);a=text.index('D2AnimSeqTxt gPlayerSequenceBladeFury[19]');base+=r'''
constexpr int PLRMODE_CAST=10,ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_MELEE_ATTACK=1,UNITFLAG_TARGETABLE=512;
constexpr int PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_THROW=12,PLRMODE_DEATH=0,PLRMODE_DEAD=17,PLRMODE_NEUTRAL=1,PLRMODE_KNOCKBACK=19;
constexpr int MONMODE_DEAD=12,MONMODE_SEQUENCE=14,UNIT_MONSTER_ALIAS=1,SKILLSFLAGINDEX_TARGETABLEONLY=8,SKILLSFLAGINDEX_TARGETCORPSE=9,SKILLSFLAGINDEX_TARGETALLY=10,SKILLSFLAGINDEX_DURABILITY=11;
constexpr int BODYLOC_RARM=4,BODYLOC_LARM=5,CLIENTSAVEFLAG_DEAD=1,CLIENTSAVEFLAG_HARDCORE=2;
using D2ClientStrc=void;void* gpD2EventCallbackTable_6FD45830=nullptr;
struct D2UnkPlrModeStrc2{int nLeftSkillOwnerGUID,nLeftSkillId,nRightSkillOwnerGUID,nRightSkillId;};
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*);
void UNITS_InitializeSequence(D2UnitStrc* p){p->pAnimSeq=DATATBLS_GetMonSeqTxtRecordFromUnit(p);p->dwSeqSpeed=256;p->dwSeqFrameCount=p->dwFrameCountPrecise=19*256;p->nSeqCurrentFramePrecise=0;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){}
void UNITS_SetAnimData(D2UnitStrc*,int,int,int){assert(false);}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* p,int mode){p->dwAnimMode=mode;}
void D2COMMON_10170_PathSetTargetPos(void*,int,int){}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*,D2UnitStrc* p){selected=p;}
void UNITS_SetAnimationFrame(D2UnitStrc* p,int frame){p->dwSeqFrame=frame*256+(p->dwSeqFrame&255);actionOrdinal=frame;}
int SUNIT_IsDead(D2UnitStrc*){return 0;}
void SKILLS_SetFlags(D2SkillStrc*,unsigned){assert(false);}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){assert(false);}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
int sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);return 0;}
bool SUNIT_CanPetBeTargetedBySkill(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){assert(false);return false;}
bool SUNIT_CanAllyBeTargetedBySkill(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){return false;}
bool SKILLS_GetSkillInfo(D2SkillStrc*,void*,void*,int*,int*){return false;}
int sub_6FD13260(D2GameStrc*,D2UnitStrc*,int,int,int){return 0;}
void sub_6FC80E10(D2GameStrc*,D2UnitStrc*){assert(false);}
D2SkillStrc* UNITS_GetLeftSkill(D2UnitStrc*){assert(false);return nullptr;}
D2SkillStrc* UNITS_GetRightSkill(D2UnitStrc*){assert(false);return nullptr;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int){assert(false);return nullptr;}
void sub_6FC80B90(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}
void sub_6FC81560(D2UnitStrc*,D2UnkPlrModeStrc2*){assert(false);}
void D2GAME_CORPSE_Handler_6FC7FBD0(D2GameStrc*,D2UnitStrc*,int,int,D2ActiveRoomStrc*){assert(false);}
void D2GAME_KillPlayerPets_6FC7CD10(D2GameStrc*,D2UnitStrc*){assert(false);}
void CLIENTS_ToggleFlag(void*,int,int){assert(false);}
bool CLIENTS_CheckFlag(void*,int){assert(false);return false;}
const char* CLIENTS_GetName(void*){assert(false);return "";}
void D2GAME_SAVE_WriteFile_6FC8A500(D2GameStrc*,D2UnitStrc*,const char*,int){assert(false);}
void sub_6FC814F0(D2GameStrc*,D2UnitStrc*){assert(false);}
'''
    base+=text[a:text.index('};',a)+2]+'\n'
    base+='D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc* p){return p->dwAnimMode==18?gPlayerSequenceBladeFury:nullptr;}\n'
    base+=extract(seq,'DATATBLS_GetSequenceEvent')
    for name in ['sub_6FCBC4D0','D2GAME_DeletePlayerPerFrameEvents_6FCBCE50','sub_6FCBCE70','sub_6FCBD120']:base+=extract(unit,name)
    base+='int32_t sub_6FD12FD0(D2GameStrc*,D2UnitStrc*);\nvoid sub_6FD13410(D2GameStrc*,D2UnitStrc*);\n'
    base+=extract(plr,'sub_6FC81250')
    base+='void sub_6FC817D0(D2GameStrc* g,D2UnitStrc* p,int,int,int,int,int){trace+="F";sub_6FC81250(g,p,0,0);}\n'
    for rel,name in [(skills,'sub_6FD12FD0'),(skills,'sub_6FD13410'),(plr,'PLRMODE_StartXY_AttackCastThrowKickSpecialSequence'),
                     (plr,'PLRMODE_StartID_AttackCastThrowKickSpecialSequence'),(plr,'sub_6FC80EE0')]:base+=extract(rel,name)
    # The declared queue service preserves insertion order, as the pinned timer
    # append/executor bodies do. Their full scheduler is not compiled here.
    reviewed={}
    for name in ['D2GAME_InitTimer_6FC351D0','EVENT_ExecuteEventsImpl']:
        extract('source/D2Game/src/GAME/Event.cpp',name);reviewed[name]=routines.pop(name)
    base+=r'''
void PrintState(){printf("[%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,[",owner.dwAnimMode,used,STATES_CheckState(&owner,STATE_INFERNO)!=0,STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)!=nullptr,state.until,skill.param,owner.dwFlags,mana,emits,made,paid,owner.nSeqCurrentFramePrecise);bool sep=false;
for(auto e:events){int type=e.type==EVENTTYPE_MODECHANGE?1:e.type==EVENTTYPE_ENDANIM?2:3;printf("%s[%d,%d,%d,%d]",sep?",":"",type,e.frame,e.kind,e.param);sep=true;}printf("]]");}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;gpSkillSrvStartFnTable_6FD408B0[26]=SKILLS_SrvSt26_BladeFury;gpSkillSrvDoFnTable_6FD40A20[48]=SKILLS_SrvDo048_BladeFury;
printf("[");bool first=true;
for(int level:{1,3,20})for(int amount:{0,255,256,1535,1536,4096})for(int id:{0,1})for(int stop:{-1,1001,1012,1016,1020})for(int target:{0,1})for(int make:{0,1})for(int callback:{-1,1006,1013,1020}){
owner={};owner.pStatListEx=&ex;owner.dwFlags=192;owner.pAnimSeq=gPlayerSequenceBladeFury;ex={};state={};row={};skill={};events.clear();used=true;selected=nullptr;rank=level;mana=amount;game.dwGameFrame=1000;actionOrdinal=0;record=learned=allocation=true;aim=target;factory=make;calc=5;emits=made=paid=rewinds=allocs=0;order.clear();trace.clear();row.dwFlags[1]=gdwBitMasks[SKILLSFLAGINDEX2_USEMANAONDO];
targetUnit={};targetUnit.dwUnitType=1;targetUnit.dwAnimMode=1;targetUnit.dwFlags=UNITFLAG_TARGETABLE;
if(id)PLRMODE_StartID_AttackCastThrowKickSpecialSequence(&game,&owner,18,&targetUnit);else PLRMODE_StartXY_AttackCastThrowKickSpecialSequence(&game,&owner,18,110,120);
printf("%s[%d,%d,%d,%d,%d,%d,%d,[",first?"":",",level,amount,id,stop,target,make,callback);first=false;bool sep=false;
for(int frame=1000;frame<=1032;frame++){
game.dwGameFrame=frame;if(frame==stop)D2GAME_PACKETCALLBACK_Rcv0x12_6FC84670(&game,&owner,nullptr,1);
if(frame==callback&&used&&STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO))sub_6FD12FD0(&game,&owner);
while(true){int index=-1,best=2147483647;for(int i=0;i<(int)events.size();i++)if(events[i].frame<=frame&&events[i].frame<best){index=i;best=events[i].frame;}if(index<0)break;auto event=events[index];events.erase(events.begin()+index);
if(event.type==EVENTTYPE_REMOVESTATE){if(STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)&&state.until<=frame){ex.pMyLastList=ex.pMyStats=nullptr;SKILLS_StatRemoveCallback_RemoveState(&owner,STATE_INFERNO,&state);}}
else if(event.type==EVENTTYPE_ENDANIM)sub_6FC81250(&game,&owner,0,0);
else if(event.type==EVENTTYPE_MODECHANGE){int result=sub_6FC80EE0(&game,&owner,event.kind,event.param);if(result==2)sub_6FC81250(&game,&owner,event.kind,event.param);}
}
printf("%s",sep?",":"");PrintState();sep=true;
}printf("]]");}printf("]\n");}
'''
    p=STAGE/'blade-fury-sequence-source-probe.cpp';p.write_text(base);binary=p.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'blade-fury-sequence-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout);assert len(rows)==2880 and all(len(r[7])==33 for r in rows)
    fixture=STAGE/'blade-fury-sequence-source-probe.json';fixture.write_bytes(run.stdout)
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],nativeVerifiedByThisSourceProbe=False,sourceHashes=hashes,compiledRoutineHashes=routines,reviewedRoutineHashes=reviewed,
             fixture=dict(path=str(fixture.relative_to(ROOT)),contexts=2880,frames=95040,sha256=hashlib.sha256(run.stdout).hexdigest()),
             established=['Complete XY/ID mode entry, source initialization wrapper, initial/rewind schedulers, ordinary start wrapper and action/end callbacks compose with unchanged channel/resource/state helpers. Normal SQ23 rate/profile services reuse the separate24-case proof.',
                          'Fresh mode clears flag64 before start; first source action is frame12. Action dispatch honors flag64/kind and obtains the live used-skill rank. Stop clears only the bit; pending action, no rewind, end-to-neutral and independent timed-list expiry remain separate.',
                          'Per-owner FIFO event service retains insertion order and duplicate removal callbacks. Earlier stale removal callbacks check the actual renewed stat-list deadline.'],
             limits=['The full game timer allocator/executor are reviewed and hashed but not executed: the driver is an explicit per-owner FIFO scheduler. Cross-owner dispatch, accepted network input/reentry and same-frame packet ordering are not proved.',
                     'SQ23 initializer/rate metadata, path target storage/validation, extended-list ABI, nonperiodic dispatch, world room and missile factory remain declared services. Death, durability, weapon reassignments, progressive skill flags and other modes reject execution.',
                     'Current-table rank1 debit overrides the old consumer quirk in native comparison. No current executable/client or production held-channel persistence claim.'],
             sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
    path=ROOT/'tools/reference/blade-fury-sequence-source-evidence.json';text=json.dumps(out,indent=2)+'\n'
    if check:assert path.read_text()==text,'Stale Fury sequence source evidence'
    else:path.write_text(text)
    print('2880 complete mode/action/end compositions /95040 frames verified; current accepted input remains separate.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
