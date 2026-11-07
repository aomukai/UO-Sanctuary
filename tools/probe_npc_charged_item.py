#!/usr/bin/env python3
"""Compose complete common item executor and do17 with explicit NPC services."""
import argparse, hashlib, json, subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-charged-item-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    parentpath=ROOT/'tools/reference/npc-item-spell-origins-source-evidence.json';parent=json.loads(parentpath.read_text())
    template=BASE/'npc-item-spell-origins-source/probe.cpp';assert sha(template)==parent['artifacts'][str(template.relative_to(ROOT))]
    cpp=template.read_text().split('void setup(',1)[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==parent['legacyCommit']
    relative='source/D2Game/src/SKILLS/SkillSor.cpp';p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==relative)
    body=function(raw.decode(),'SKILLS_SrvDo017_ChargedBolt_BoltSentry')
    old='nAttBonus=0;};';assert cpp.count(old)==1
    cpp=cpp.replace(old,'nAttBonus=0,pInitArgs=-1;void(*pInitFunc)(D2UnitStrc*,int32_t)=nullptr;};')
    cpp=cpp.replace('return ordinal==failure?nullptr:&child;','return failure==-2||ordinal==failure?nullptr:&child;')
    cpp=cpp.replace('return sid==53?(rank+25)/5:0;','return sid==38?std::min(24,rank+2):sid==53?(rank+25)/5:0;')
    cpp='#include <algorithm>\n'+cpp
    cpp+=r'''
int queries=0;
void SKILLS_MissileInit_ChargedBolt(D2UnitStrc*,int32_t){} // Identity-only leaf;existing initializer/path proofs execute the actual bodies.
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*u,int*x,int*y){assert(u==&actor);int i=queries++;*x=targetMode==1?0:400+i;*y=targetMode==2?0:700-i;return targetMode!=0&&(targetMode!=4||i%2==1);}
'''+body+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;gpSkillSrvDoFnTable_6FD40A20[17]=SKILLS_SrvDo017_ChargedBolt_BoltSentry;bool comma=false;printf("[");
for(int rank=1;rank<=63;rank++)for(int outgoing:{0,1})for(int mode=0;mode<5;mode++)for(int fail:{-1,0,-2})for(int ar:{0,77}){
currentSkill=38;targetMode=mode;failure=fail;attackRating=ar;record={};record.wSrvDoFunc=17;record.wItemEffect=1;record.wSrvMissileA=56;actor={};child={};D2DynamicPathStrc path;actor.pDynamicPath=&path;evals=queries=0;emitted.clear();
int result=D2GAME_SKILLS_Handler_6FD12BA0(&game,&actor,38,rank,0,1,outgoing);
printf("%s[[%d,%d,%d,%d,%d],[%d,%u,%d,%d],[",comma?",":"",rank,outgoing,mode,fail,ar,result,actor.dwFlags,evals,queries);comma=true;
for(size_t i=0;i<emitted.size();i++){auto&p=emitted[i];assert(p.pInitFunc==SKILLS_MissileInit_ChargedBolt);printf("%s[%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",p.dwFlags,p.pOwner->dwUnitId,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nVelocity,p.nSkill,p.nSkillLevel,p.nMissile,p.nAttBonus,p.pInitArgs);}printf("]]");}printf("]\n");}
'''
    assert function(cpp,'SKILLS_SrvDo017_ChargedBolt_BoltSentry')==body
    source=STAGE/'probe.cpp';source.write_text(cpp);binary=STAGE/'probe'
    r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],capture_output=True)
    compiler=STAGE/'compiler.log';compiler.write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()
    r=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()
    rows=json.loads(r.stdout);assert len(rows)==3780
    for args,state,packets in rows:
        rank,outgoing,mode,fail,ar=args;count=min(24,rank+2);assert state==[1,0x8040,1,count]
        indices=[i for i in range(count)if mode in(3,4)and(mode!=4 or i%2==1)]
        assert packets==[[33,231,380,680,400+i,700-i,0,38,rank,56,0,i]for i in indices]
    current=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';row=json.loads(current.read_text())['38'];assert row['srvdofunc']==17 and row['ItemEffect']==1 and row['calc1']=='"min(24,ln12)"'and row['Param1']==3 and row['Param2']==1
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=3780,sourceHashes={relative:sha(p)},routineHashes={'SKILLS_SrvDo017_ChargedBolt_BoltSentry':hashlib.sha256(body.encode()).hexdigest()},
        inputs={str(p.relative_to(ROOT)):sha(p)for p in[Path(__file__).resolve(),parentpath,template,current]},artifacts={str(p.relative_to(ROOT)):sha(p)for p in[source,compiler,san,fixture]},
        established=['Complete common item executor composes actual do17 for all63nonzero proc ranks,both incoming/outgoing flags,per-ordinal failed/zero/valid/alternating aim,creation failure and nonzero own AR. Source flags33 omit the helper AR flag;InitArg retains actual source ordinal even when earlier aims skip allocation.','All target lookups occur;failed creation never aborts later ordinals or changes successful do return. Proc frame uses no learned skill,mana,quantity or owner RNG.'],
        services=['Final factory and initializer identity are declared leaves here;actual initializer45/path2160proofs and native post-path seed checks are separate. This is supplementary older source,not full current executable. Reused complete common helper/ring/alternate bodies are unchanged but only common+do17 execute in this matrix.'])
    target=ROOT/'tools/reference/npc-charged-item-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('NPC charged item:3780complete common+do17 contexts,all63ranks/flags/ordinal/failure/AR;ASan+UBSan clean.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
