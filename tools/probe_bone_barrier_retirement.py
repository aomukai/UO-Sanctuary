#!/usr/bin/env python3
"""Compose complete barrier AI initialization, pet scheduling and idle bodies.

World/mode dispatch remains a declared service; no native retirement is changed
from an animation length or an isolated modifier callback.
"""
import argparse,hashlib,json,shutil,struct,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'bone-barrier-retirement-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);inputs={}
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    ai=source('source/D2Game/src/AI/AiThink.cpp');tactics=source('source/D2Game/src/AI/AiTactics.cpp')
    skills=source('source/D2Game/src/SKILLS/Skills.cpp');mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp')
    general=source('source/D2Game/src/AI/AiGeneral.cpp');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    spawn=source('source/D2Game/src/MONSTER/MonsterSpawn.cpp')
    bodies={n:function(s,n) for s,n in [(skills,'D2GAME_SummonPet_6FD14430'),(ai,'AITHINK_ExecuteAiFn'),
        (ai,'D2GAME_AI_Unk084_6FCDEC70'),(ai,'AITHINK_Fn084_BoneWall'),(tactics,'AITACTICS_Idle'),
        (tactics,'AITACTICS_IdleInNeutralMode'),(tactics,'AITACTICS_ChangeModeAndTargetCoordinates')]}
    assert 'pGame->dwGameFrame + 25' in bodies['D2GAME_SummonPet_6FD14430']
    reviewed={n:function(s,n) for s,n in [(mode,'D2GAME_ModeChange_6FC65220'),(mode,'sub_6FC63B30'),
        (mode,'sub_6FC63E80'),(mode,'sub_6FC641D0'),(mode,'sub_6FC64280'),(general,'AIGENERAL_UpdateMinionList'),
        (general,'AIGENERAL_ExecuteCallbackOnMinions'),(general,'AIGENERAL_GetMinionOwner'),(general,'AIGENERAL_FreeMinionList'),
        (unique,'MONSTERUNIQUE_KillMinions'),(unique,'MONSTERUNIQUE_MinionCallback_KillMinion'),(unique,'sub_6FC6E780'),
        (spawn,'D2GAME_SpawnNormalMonster_6FC68E30')]}
    assert reviewed['D2GAME_SpawnNormalMonster_6FC68E30'].split('//')[0].strip().endswith('return nullptr;')
    assert reviewed['sub_6FC63E80'].index('AIGENERAL_UpdateMinionList')<reviewed['sub_6FC63E80'].index('PATH_SetUnitDeadCollision')
    refs=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((refs/'sources.json').read_text())['files'];tables={}
    for name in ['skills','monstats','monstats2']:
        p=refs/f'json/{name}.json';assert sha(p)==manifest[f'json/{name}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    assert tables['skills']['78']['Param2']==tables['skills']['88']['Param2']==600
    assert tables['monstats']['bonewall']['Skill1']=='Bone Wall' and tables['monstats']['bonewall']['Code']=='BW'
    assert all(tables['monstats']['bonewall'][k]==15 for k in ['aidel','aidel(N)','aidel(H)'])
    assert tables['monstats2']['bonewall'].get('deadCol',0)==0

    # Reuse the accepted complete common-pet ABI and callback without copying
    # its room/allocation/registration service implementation into this probe.
    parent=BASE/'bone-barrier-cast-source/probe.cpp';parent_receipt=ROOT/'tools/reference/bone-barrier-cast-source-evidence.json'
    assert sha(parent)==json.loads(parent_receipt.read_text())['artifacts'][str(parent.relative_to(ROOT))]
    inputs[str(parent.relative_to(ROOT))]=sha(parent);inputs[str(parent_receipt.relative_to(ROOT))]=sha(parent_receipt)
    cpp=parent.read_text().split('int main(){',1)[0];assert bodies['D2GAME_SummonPet_6FD14430'] in cpp
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new)
    replace('#define __fastcall','#define FARPROC void*\n#define __fastcall')
    replace('int dwUnitType=0,','int dwClassId=344,dwAnimMode=8,dwUnitType=0,')
    replace('struct D2SkillsTxt{',r'''struct D2AiTickParamStrc;
using AIPARAMFN=void(*)(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*);
struct D2MonStatsTxt{int nSkill[1]={78};}monstats;struct D2MonStats2Txt{}monstats2;
struct D2AiControlStrc{int dwAiParam[3]={0,0,0},nAiSpecialState=0;AIPARAMFN pAiParamFn=nullptr;}control;
struct D2AiTickParamStrc{D2AiControlStrc*pAiControl=nullptr;D2MonStatsTxt*pMonstatsTxt=nullptr;D2MonStats2Txt*pMonstats2Txt=nullptr;};
struct D2AiTableStrc{int unk0x00=0;AIPARAMFN unk0x04=nullptr,pAiParamFn=nullptr,unk0x0C=nullptr;}aiTable;
using D2C_AiSpecialState=int;constexpr int AISPECIALSTATE_TABLE_COUNT=10,AISPECIALSTATE_NO_MON_STATS=1,MONMODE_DEATH=0,MONMODE_NEUTRAL=1;
struct D2ModeChangeStrc{D2UnitStrc*pUnit=nullptr,*pTargetUnit=nullptr;int nMode=0,nX=0,nY=0;};
int nextFrame=-1,deathRequests=0,neutralChanges=0,eventDeletes=0,eventWrites=0;
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc*p,int mode,D2ModeChangeStrc*s){s->pUnit=p;s->nMode=mode;}
int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc*s,int flag){assert(flag==1);s->pUnit->dwAnimMode=s->nMode;if(s->nMode==0)deathRequests++;else{assert(s->nMode==1);neutralChanges++;}return 1;}
D2AiTableStrc*AITHINK_GetAiTableRecord(D2UnitStrc*,int state){assert(state==0);return &aiTable;}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int id){assert(id==344);return &monstats;}
D2MonStats2Txt*MONSTERREGION_GetMonStats2TxtRecord(int id){assert(id==344);return &monstats2;}
void AIGENERAL_FreeAllAiCommands(D2GameStrc*,D2UnitStrc*){}
void FOG_DisplayWarning(const char*,const char*,int){assert(false);}void FOG_DisplayAssert(const char*,const char*,int){assert(false);}
bool IsBadCodePtr(void*){return false;}
void AITHINK_Fn001_100_Idle_Buffy(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*){assert(false);}
void AITACTICS_Idle(D2GameStrc*,D2UnitStrc*,int);
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int);
int AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc*,D2UnitStrc*,int,int,int);
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int);
struct D2SkillsTxt{''')
    replace('int nPetType=0,','int dwParam[2]={0,600},nPetType=0,')
    replace('int nPetTypeTxtRecordCount=20,','int nMonStatsTxtRecordCount=1000,nPetTypeTxtRecordCount=20,')
    replace('int id,int,int radius','int id,int mode,int radius')
    replace('p->dwUnitType=UNIT_MONSTER;','p->dwAnimMode=mode;p->dwUnitType=UNIT_MONSTER;')
    replace('void*AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return nullptr;}','D2AiControlStrc*AIGENERAL_GetAiControlFromUnit(D2UnitStrc*){return &control;}')
    replace('void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,void*,int){}','')
    replace('void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}',
            'void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){assert(type==EVENTTYPE_AITHINK);nextFrame=-1;eventDeletes++;}')
    replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}',
            'void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int,int){assert(type==EVENTTYPE_AITHINK);nextFrame=frame;eventWrites++;}')
    cpp+='\n'.join(b for n,b in bodies.items() if n!='D2GAME_SummonPet_6FD14430')
    cpp+=r'''int main(){bool first=true;printf("[");
for(int kind:{78,88})for(int birth:{0,1000,12345,1000000})for(int lag=0;lag<=15;lag++){
 game.dwGameFrame=birth;control={};aiTable={0,D2GAME_AI_Unk084_6FCDEC70,AITHINK_Fn084_BoneWall,nullptr};
 failCount=spawnCalls=created=passives=missiles=deathRequests=neutralChanges=eventDeletes=eventWrites=0;roomOk=1;nextFrame=-1;
 D2SummonArgStrc arg;arg.dwFlags=9;arg.nMonMode=kind==78?8:1;arg.pOwner=&owner;arg.pPosition.nX=108;arg.pPosition.nY=100;
 auto*pet=D2GAME_SummonPet_6FD14430(&game,&arg);assert(pet&&control.pAiParamFn==AITHINK_Fn084_BoneWall);
 int deadline=control.dwAiParam[0],initial=nextFrame,thinks=0;
 D2AiTickParamStrc tick{&control,&monstats,&monstats2};
 while(!deathRequests){assert(nextFrame>=game.dwGameFrame&&thinks<100);game.dwGameFrame=nextFrame+(thinks==0?lag:0);nextFrame=-1;
  control.pAiParamFn(&game,pet,&tick);thinks++;
 }
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,birth,lag,deadline,initial,game.dwGameFrame,thinks,neutralChanges,eventDeletes,eventWrites);first=false;
}printf("]\n");}'''
    assert all(b in cpp for b in bodies.values()),'An executed source body was altered'
    p=STAGE/'probe.cpp';p.write_text(cpp)
    with (STAGE/'compiler.log').open('w') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(p.with_suffix(''))],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    cases=json.loads(run.stdout);assert len(cases)==128
    for kind,birth,lag,deadline,initial,death,thinks,neutral,deletes,writes in cases:
        start=25+lag;idle=(600-start)//15+1;expected=start+idle*15
        assert (deadline,initial,death,thinks,neutral,deletes,writes)==(birth+600,birth+25,birth+expected,idle+1,int(kind==78),idle+1,idle+1)
        if lag==0:assert death-birth==610
        if lag==5:assert death-birth==615  # A think exactly at600 must idle again.
    (STAGE/'cases.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')

    evidence=ROOT/'tools/reference/d2r-local-animation-evidence.json';prior=json.loads(evidence.read_text());inputs[str(evidence.relative_to(ROOT))]=sha(evidence)
    raw_path=STAGE/'AnimData.d2'
    if not raw_path.exists():
        saved=Path('/media/aomukai/FILES/Sanctuary')/prior['localRawPath'];assert sha(saved)==prior['sha256'];shutil.copyfile(saved,raw_path)
    assert sha(raw_path)==prior['sha256'];raw=raw_path.read_bytes();cursor=0;records=[];count=0
    for _ in range(256):
        n=struct.unpack_from('<I',raw,cursor)[0];cursor+=4
        for _ in range(n):
            record=raw[cursor:cursor+160];assert len(record)==160;cursor+=160;count+=1;code=record[:8].split(b'\0')[0].decode('ascii')
            if code.startswith('BW'):
                frames,speed=struct.unpack_from('<II',record,8);records.append(dict(code=code,frames=frames,speed=speed,events=[[i,v] for i,v in enumerate(record[16:]) if v]))
    assert cursor==len(raw) and count==3661 and len(records)==5
    lookup={r['code']:r for r in records};assert lookup['BWDTHTH']==dict(code='BWDTHTH',frames=15,speed=256,events=[])
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],scope='source-retirement-schedule',runtimeChanged=False,
        inputs=inputs,executed={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        staticallyReviewed={n:hashlib.sha256(b.encode()).hexdigest() for n,b in reviewed.items()},
        cases=128,animation=dict(source='Pinned local CASC raw reused read-only',path=str(raw_path.relative_to(ROOT)),sha256=sha(raw_path),parsedRecords=count,records=records),
        established=[
            'Seven complete unchanged bodies compose pet allocation/AI initialization/initial event25/BoneWall deadline600/strict expiry/neutral mode request/idle15. Normal active uninterrupted callback service enters DEATH at birth+610frames, not600. Wall S1 and Prison NU share the species skill0 initializer; absolute birth frame does not change the610offset.',
            'Declared late first observations cover all0..15frame phases: lag1 yields611,lag5 reaches deadline600 and idles until615,lag7 yields602,lag15 yields610. Equality at the deadline still idles; do not replace the think cadence with a fixed24second expiry or assume that every delayed world resumes at610.',
            'The pinned local animation archive contains five BW records: S1=24,DT=15,GH=5,NU=1,DD=1,all speed256 with no frame markers. DT length alone does not execute mode/death/minion/removal events.'],
        services=[
            'Reuses accepted complete common-pet ABI. Room/allocation/registration, skill/stat rows, AI-table selection, command clearing, event storage and mode transition are declared services. Full ExecuteAiFn initialization runs; subsequent active undisturbed callbacks are explicitly dispatched from the stored function pointer, not through the full room/stun/leash/event engine.',
            'Mode service records requested neutral/death state only. No minion graph, collision removal, corpse/invisibility/deletion, current executable or native retirement is executed. Reviewed cleanup/dispatcher/minion functions are not included in executed count.'],
        remaining=[
            'Source low-level spawn is a null stub with commented decompile. Initial minion owner/flag graph and modifier15 interaction need a complete controlled graph/current-build check before changing child/root death behavior. The graph helpers mutate owner/list relationships during death; an isolated KillMinions call is insufficient.',
            'Mode-entry/event routing, per-child deadlines, exact collision release,15frame DT completion/hidedead/removal, save/reload during retirement, world inactivity/stun/leash and current executable remain unverified. Native root-shared24second deletion remains explicitly prior behavior until this composition is integrated.',
            'Reuse SourceAnimationEvents and existing original-actor retirement patterns where their contracts match. Do not borrow Fire Golem160ms timing or infer full mode scheduling from animation metadata. No class completion/deployment or asset creation.'],
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),STAGE/'probe.cpp',STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log',raw_path]})
    out=ROOT/'tools/reference/bone-barrier-retirement-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Barrier retirement source evidence stale'
    else:out.write_text(encoded)
    print('Barrier retirement:128 complete pet/AI/idle compositions; normal active death request610frames;5 pinned BW animation rows. Native retirement unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
