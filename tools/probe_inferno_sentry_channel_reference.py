#!/usr/bin/env python3
"""Execute NPC53/95, Inferno frame helpers and the shared complete factory.

The actor sequence/AI/event conductor remains separate. Current xx maps to14
in locally corroborated text/binary mode rows; mode0 is a comparison control.
"""
import argparse,csv,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_guided_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    subprocess.run([sys.executable,str(ROOT/'tools/corroborate_local_monmode.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    modes=json.loads((ROOT/'tools/reference/d2r-local-monmode-evidence.json').read_text());assert modes['sequence']['index']==14 and modes['sequence']['code']=='xx'
    shared=json.loads((ROOT/'tools/reference/guided-arrow-engine-evidence.json').read_text())
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
    def routine(relative,name):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end,depth=text.index('{',m.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        block=text[m.start():end];routines[name]=hashlib.sha256(block.encode()).hexdigest();return block+'\n'
    pin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pin/'sources.json').read_text());tables={};pins={}
    for name in ['skills','missiles','monstats2']:
        raw=(pin/'json'/f'{name}.json').read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(raw)
    sequence_path=ROOT/'.local/research/d2r-3.3.93847/monseq-inferno-verified.txt';sequence_raw=sequence_path.read_bytes()
    assert hashlib.sha256(sequence_raw).hexdigest()=='a48a884a654cab7cafe0be2764fa8a77f428a9b48855d27461d133a1f553898c'
    sequence=[r for r in csv.DictReader(sequence_raw.decode('utf-8-sig').splitlines(),delimiter='\t') if r.get('sequence')=='seq_infernosentry']
    assert len(sequence)==14 and [(i,r['frame'],r['event']) for i,r in enumerate(sequence) if r['event']]==[(9,'1','1')]
    actor=tables['skills']['311'];missile=tables['missiles']['inferno sentry 1'];model=tables['monstats2']['infernosentry']
    assert actor['srvstfunc']==53 and actor['srvdofunc']==95 and actor['monanim']=='xx'
    assert actor['calc1']=="ln34/2 + skill('Wake of Fire Sentry'.blvl)" and actor['Param3']==40 and not actor['Param4']
    assert actor['calc2']=="par1 + skill('Wake of Fire Sentry'.blvl)" and actor['Param1']==15 and actor['calc3']==3
    assert (missile['Vel'],missile['Range'],missile['AnimLen'],missile['DamageRate'])==(12,50,15,82)
    assert (model['InfernoLen'],model['InfernoAnim'])==(14,9)
    base=(STAGE/'guided-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
    def replace(old,new):
        nonlocal base
        assert base.count(old)==1,old;base=base.replace(old,new)
    replace('#include <algorithm>','#include <algorithm>\n#include <cassert>\n#define D2_ASSERT assert\n#define D2Clamp std::clamp')
    replace('struct D2GameStrc{};','struct D2GameStrc{int dwGameFrame=100;};')
    replace('nSeqCurrentFramePrecise=0,','dwSeqFrame=37,dwSeqFrameCount=3584,dwFrameCountPrecise=3840,wAnimSpeed=0,nSeqCurrentFramePrecise=777,')
    replace('nVel=24,','nAnimLen=15,nVel=24,')
    replace('int dwCalc[4]={};','int nMonAnim=0,dwCalc[4]={0,1,2,3};')
    replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return calc;}',
            'int formulas[3]={20,15,3};int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){assert(formula>=0&&formula<3);return formulas[formula];}')
    replace('bool STATES_CheckState(D2UnitStrc*,int){return slow;}',
            'bool inferno=false;bool STATES_CheckState(D2UnitStrc*,int state){return state==STATE_SLOWMISSILES?slow:inferno;}')
    base+=r'''
constexpr int STATE_INFERNO=2,MONMODE_SEQUENCE=14,EVENTTYPE_MODECHANGE=0,EVENTTYPE_ENDANIM=1;
struct D2SkillStrc{int param=0;}used;bool haveUsed=true,haveModel=true;
struct D2MonStats2Txt{int nInfernoAnim=9,nInfernoLen=14;}model;
struct Event{int type,frame;};std::vector<Event> events;int deletedModes=0,deletedEnds=0;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return haveUsed?&used:nullptr;}
void SKILLS_SetParam1(D2SkillStrc* s,int value){s->param=value;}int SKILLS_GetParam1(D2SkillStrc* s){return s->param;}
void STATES_ToggleState(D2UnitStrc*,int state,int value){assert(state==STATE_INFERNO);inferno=value;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return haveModel?&model:nullptr;}
void PATH_SetNewDistance(D2DynamicPathStrc* p,int value){p->flags=value;}
void UNITS_SetAnimationSpeed(D2UnitStrc* u,int value){u->wAnimSpeed=value;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){if(type==0)deletedModes++;else if(type==1)deletedEnds++;else assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int,int){events.push_back({type,frame});}
'''
    base+=routine('source/D2Common/src/Units/Units.cpp','UNITS_SetAnimationFrame')
    relative='source/D2Game/src/SKILLS/SkillMonst.cpp'
    base+='int32_t SKILLS_UpdateInfernoAnimationParameters(D2UnitStrc*,D2UnitStrc*,int32_t,int32_t);\n'
    for name in ['SKILLS_SrvSt53_MonInferno','SKILLS_SetInfernoFrame','SKILLS_SrvDo095_MonInferno','SKILLS_UpdateInfernoAnimationParameters']:
        base+=routine(relative,name)
    base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int rank:{1,20,255})for(int hard:{0,20})for(int phase:{-1,0,1})for(int active:{0,1})for(int aim:{0,1})for(int alloc:{0,1})for(int record:{0,1})for(int usedSkill:{0,1})for(int skillRow:{0,1})for(int metadata:{0,1})for(int anim:{0,14}){
 reset();game={};owner.dwUnitType=UNIT_MONSTER;owner.dwClassId=414;skill={};skill.wSrvMissileA=523;skill.nMonAnim=anim;row={};row.nVel=row.nMaxVel=12;row.wRange=50;row.wLevRange=0;row.dwDamageRate=82;
 formulas[0]=20+hard;formulas[1]=15+hard;formulas[2]=3;used={};haveUsed=usedSkill;haveModel=metadata;hasSkill=skillRow;hasRecord=record;allocated=alloc;coordinates=aim;inferno=false;events.clear();deletedModes=deletedEnds=0;
 int start=SKILLS_SrvSt53_MonInferno(&game,&owner,311,rank);int deadline=used.param;inferno=active;game.dwGameFrame=100+15+hard+phase;
 int result=SKILLS_SrvDo095_MonInferno(&game,&owner,311,rank);bool made=creates&&allocated;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,hard,phase,active,aim,alloc,record,usedSkill,skillRow,metadata,anim,start,deadline,result,creates,made?created.total:0,made?created.path.velocity:0,made?created.wAnimSpeed:0,made?created.path.flags:0,owner.dwSeqFrame,owner.dwFrameCountPrecise,owner.nSeqCurrentFramePrecise,events.empty()?-1:events[0].type,events.empty()?0:events[0].frame-game.dwGameFrame,deletedEnds,owner.dwSeqFrameCount);first=false;
}printf("]\n");}
'''
    path=STAGE/'inferno-sentry-channel-source-probe.cpp';path.write_text(base);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'inferno-sentry-channel-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    cases=json.loads(run.stdout);assert len(cases)==4608
    for rank,hard,phase,active,aim,alloc,record,used,row,metadata,anim,start,deadline,result,creates,frames,velocity,speed,distance,seq,count,visual,event,delay,deleted_end,sequence_length in cases:
        started=used and row;launch=started and record and aim;made=launch and alloc;repeat=made and active and phase<0
        assert (start,deadline,result,creates)==(int(started),115+hard if started else 0,int(made),int(launch))
        assert sequence_length==14*256
        duration=20+hard
        assert (frames,velocity,speed,distance)==((duration,2304,3840//duration,duration) if made else (0,0,0,0))
        assert (seq,count,visual)==((2085,2560,777) if made and metadata and anim==14 else (37,3840,2304) if made and metadata else (37,3840,777))
        expected_event=0 if repeat else 1 if started and record else -1
        assert (event,delay,deleted_end)==(expected_event,3 if repeat else (14 if metadata else 11) if expected_event==1 else 0,int(repeat))
    fixture=STAGE/'inferno-sentry-channel-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    hashes['source/D2Game/src/MISSILES/Missiles.cpp']=shared['sourceHashes']['source/D2Game/src/MISSILES/Missiles.cpp']
    routines['MISSILES_CreateMissileFromParams']=shared['blockHashes']['MISSILES_CreateMissileFromParams']
    evidence=dict(referenceVersion='3.3.93847',talentId='assassin.wakeofinferno',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=routines,pinnedInputHashes=pins,localModeEvidence='tools/reference/d2r-local-monmode-evidence.json',currentSequence=dict(path=str(sequence_path.relative_to(ROOT)),sha256=hashlib.sha256(sequence_raw).hexdigest(),points=14,actionIndex=9,records=sequence),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(cases)),
        established=['Complete NPC start53 stores current frame+15+hard Wake of Fire and enables Inferno state. Complete do95 emits through flags37/absolute aim/supplied velocity, then applies the duration helper and animation fields.',
            'The complete factory initially uses50frames and velocity12*256*75/100=2304FP8. The complete updater replaces total/current frames and path distance with20+hard Wake, and missile animation speed with3840/duration. DamageRate82 remains its source reduction-scaling stat.',
            'Before the deadline and with Inferno state present, successful emission deletes end/mode events and requests another mode event after3frames. At/after the deadline or with state absent, it still emits this shot, then schedules end animation after model InfernoLen14; missing model uses11.',
            'Missing aim or allocation schedules the same model14/fallback11 end and returns0. Missing skill/used skill/missile row returns0 without emission or new end event.',
            'The current xx/sequence14 branch rewrites dwSeqFrame to8 plus its existing37/256 fraction and dwFrameCountPrecise to10*256. The separate dwSeqFrameCount stays14*256, matching the current14-entry sequence with action1 at index9. The nonsequence control sets only visual frame9. These fields are not interchangeable; a generic non-rewinding firing controller is not equivalent.'],
        services=['Existing complete Guided Arrow factory harness supplies allocation/room/path/seed/damage/stat/event services. Formula service returns current table20+hard,15+hard and3; direct formula parsing, density conductor and outer AI101/sequence dispatch are separate.',
            'Used skill/state/event accessors and model presence are declared services. UNITS_SetAnimationFrame is the complete original body. Current local text/binary data identifies raw monanim xx as sequence index14. Mode14 is the current-row source branch; numeric mode0 is a comparison control. This does not claim a modern executable code trace.',
            'No native Inferno actor, source-zero beam damage/collision, LastCollide spacing, full sequence/event interleaving, retirement, learned input, modern executable or rendered graphics is certified. The separate damage-source proof remains linked by the preflight.'],
        sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/inferno-sentry-channel-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Inferno Sentry source evidence stale'
    else:out.write_text(encoded)
    print('Inferno Sentry:4608 complete start/do/frame-helper/factory contexts; current xx/sequence14 rewind identified; outer sequence/AI remains an explicit boundary.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
