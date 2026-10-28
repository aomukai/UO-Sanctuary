#!/usr/bin/env python3
"""Execute Fury's complete shared start/do13 and scalar getters with declared services."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath=ROOT/'tools/reference/druid-fury-preflight.json';pin=json.loads(pinpath.read_text());assert pin['referenceVersion']=='3.3.93847'
    module=ast.parse((ROOT/'tools/probe_zeal_reference.py').read_text())
    header=next(n.value.value for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
    header=header.replace('#define __fastcall','#define __stdcall\n#define __fastcall').replace('dwParam[8]={0,100}','dwParam[8]={0,70}')
    header=header.replace('nEType=0,nSrcDam=128;', 'nEType=0,nSrcDam=128,dwToHitCalc=-1,dwToHit=50,dwLevToHit=10,wMana=4,wLevelMana=0,nManaShift=8;')
    header=header.replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?hitCount:336;}', 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){return calc==0?std::min(rank+1,5):100+17*(rank-1);}')
    header=header.replace('int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hitFlag;}', 'int lastAR;int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){lastAR=ar;return hitFlag;}')
    header=header.replace('int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 200;}', 'int SKILLS_GetToHitFactor(D2UnitStrc*,int,int);\nD2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){return SKILLS_GetSkillsTxtRecord(id);}')
    blocks={};cpp=header
    for relative,names in [
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10880','sub_6FD107F0']),
        ('source/D2Common/src/D2Skills.cpp',['SKILLS_GetToHitFactor','SKILLS_GetManaCosts']),
        ('source/D2Game/src/SKILLS/SkillAma.cpp',['SKILLS_SrvSt37_Zeal_Fury_BloodLordFrenzy','SKILLS_SrvDo013_Fend_Zeal_Fury']),
    ]:
        path=BASE/'d2moo-pinned-source'/relative;assert sha(path)==pin['sourceHashes'][relative]
        for name in names:
            block=function(path.read_text(),name);assert hashlib.sha256(block.encode()).hexdigest()==pin['completeRoutineHashes'][name]
            blocks[name]=hashlib.sha256(block.encode()).hexdigest();cpp+='\n'+block+'\n'
    cpp+=r'''
int main(){D2GameStrc game;D2UnitStrc owner,t[5];for(int i=0;i<5;i++)t[i].dwUnitId=(i+1)*10;
bool first=true;std::printf("{\"starts\":[");
for(int rank=1;rank<=255;rank++)for(int record:{0,1})for(int skill:{0,1})for(int direct:{0,1})for(int count:{0,1,2,5}){
 hasRow=record;hasSkill=skill;used={};selected=direct?&t[0]:nullptr;candidates.clear();for(int i=count-1;i>=0;i--){t[i].alive=t[i].inRange=true;candidates.push_back(&t[i]);}
 int result=SKILLS_SrvSt37_Zeal_Fury_BloodLordFrenzy(&game,&owner,248,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,record,skill,direct,count,result,used.p[0],used.p[2],SKILLS_GetToHitFactor(&owner,248,rank),SKILLS_GetManaCosts(248,rank));
}
std::printf("],\"strikes\":[");first=true;
for(int rank:{1,2,3,4,20,255})for(int record:{0,1})for(int skill:{0,1})for(int count:{0,1,2,5})for(int lost:{0,1,2})for(int hit:{0,1})for(int remaining:{1,2,5}){
 hasRow=record;hasSkill=skill;used={};used.p[0]=remaining;used.p[1]=1;used.p[2]=10;hitFlag=hit;candidates.clear();
 for(int i=count-1;i>=0;i--){t[i].alive=!(i==0&&lost==1);t[i].inRange=!(i==0&&lost==2);candidates.push_back(&t[i]);}
 allocations=drains=rollbacks=elementCalls=0;lastTarget=lastDamage=lastSource=lastRollback=lastAR=-1;
 int result=SKILLS_SrvDo013_Fend_Zeal_Fury(&game,&owner,248,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,record,skill,count,lost,hit,remaining,result,allocations,lastTarget,lastDamage,lastSource,drains,used.p[0],used.p[2],rollbacks,lastRollback,lastAR,elementCalls);
}
std::printf("]}\n");}
'''
    source=BASE/'druid-fury-source-probe.cpp';source.write_text(cpp);binary=BASE/'druid-fury-source-probe';stderr=BASE/'druid-fury-sanitizer.log'
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-sanitize-recover=all',str(source),'-o',str(binary)],check=True)
    with stderr.open('wb') as log:raw=subprocess.check_output([str(binary)],stderr=log)
    assert stderr.read_bytes()==b'';data=json.loads(raw);assert len(data['starts'])==8160 and len(data['strikes'])==1728
    for rank,record,skill,direct,count,result,remaining,target,ar,mana in data['starts']:
        valid=record and skill and (direct or count>0)
        assert result==int(bool(valid)) and remaining==(min(rank+1,5) if valid else 0) and target==(10 if valid else 0)
        assert ar==(50+10*(rank-1) if record else 0) and mana==(1024 if record else 0)
    for rank,record,skill,count,lost,hit,remaining,result,alloc,target,damage,src,drain,left,nxt,rollbacks,rollback,ar,elements in data['strikes']:
        ids=[10*(i+1) for i in range(count) if i or not lost];valid=bool(record and skill and ids)
        assert result==0 and elements==0
        if not valid:assert alloc==drain==rollbacks==0 and left==remaining
        else:
            actual=ids[0];again=remaining>1;next_target=next((x for x in ids if x>actual),ids[0])
            assert (alloc,target,damage,src,drain,left,nxt,rollbacks,rollback,ar)==(1,actual,100+17*(rank-1) if hit else 0,128,4,remaining-1,next_target if again else 10,int(again),70 if again else -1,50+10*(rank-1))
    fixture=BASE/'druid-fury-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,preflightSha256=sha(pinpath),
        legacyCommit=pin['legacyCommit'],compiledRoutineHashes=blocks,contexts=dict(startAndScalar=8160,do13=1728),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),ROOT/'tools/probe_zeal_reference.py',source,fixture,stderr]},
        established=[
            'Complete shared start37 stores min(rank+1,5)and the selected target,or uses the complete cyclic selector when there is no direct target. Missing table/used skill/target returns failure without a hit count. All255ranks use the complete source AR and mana getters against current row metadata.',
            'Complete do13 revalidates each remembered target,reselects on death/range loss,allocates SrcDam128 ordinary combat on hit or miss,drains wear4,decrements remaining count,and requests rollback70only if another strike and eligible target remain. A sole survivor can be selected again; missing rows/used skill/targets allocate nothing. Current Fury has no elemental conversion branch.',
        ],services=[
            'Reuses only the declared Zeal fixture header. World eligibility/aura enumeration,melee geometry,server target lookup,result flags,packet allocation,durability and rollback event delivery are explicit services. Skill formula evaluation supplies current pinned count and physical ED expressions; complete AR/mana getters are executed.',
            'This probe records the requested rollback70; it does not execute outer animation anchors,transformed modern rate/scheduler,real weapon packets/critical/leech/procs/charges,payment timing or a native series. Shared selection bodies are complete but the aura eligibility scan remains a declared service.',
        ],remaining=[
            'Compose current wolf normalized progress,rate-scaled rollback and outer event delivery before native multi-swing timing. Existing isolated Zeal/Fend clocks cannot certify transformed Fury. Current input/interruption/extra-AR/payment/rank capture and complete weapon/owner/death/client behavior remain open.',
            'No learned handler,installed change,class completion or final assets are claimed. Effects/audio and talent/left/right/quick icons remain separate.',
        ])
    target=ROOT/'tools/reference/druid-fury-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Fury:8160complete start/AR/mana and1728do13 contexts;ASan/UBSan clean; declared rollback70,outer wolf clock/native casting still pending.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
