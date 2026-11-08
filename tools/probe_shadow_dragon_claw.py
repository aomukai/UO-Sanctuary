#!/usr/bin/env python3
"""New NPC do46/normal sequence composition, reusing existing complete packet proof."""
import csv,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-dragon-claw-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);parent=json.loads((ROOT/'tools/reference/shadow-claw-sequence-source-evidence.json').read_text());template=BASE/'shadow-claw-sequence-source/source.cpp';assert sha(template)==parent['artifacts'][str(template.relative_to(ROOT))]
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());old=json.loads((ROOT/'tools/reference/dragon-claw-source-evidence.json').read_text());texts={};review={}
 for rel,h in old['sourceHashes'].items():
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert sha(p)==h and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);texts[rel]=raw.decode()
 for name,h in old['compiledRoutines'].items():
  found=[]
  for text in texts.values():
   try:body=function(text,name)
   except(AssertionError,ValueError):continue
   found.append(hashlib.sha256(body.encode()).hexdigest())
  assert h in found,name;review[name]=h
 mp=ROOT/'.local/research/d2r-3.3.93847/monstats.txt';assert sha(mp)==parent['ownedInputs'][str(mp.relative_to(ROOT))];monsters={r['Id']:r for r in csv.DictReader(mp.read_text().splitlines(),delimiter='\t')}
 for name in ['shadowwarrior','shadowmaster']:assert monsters[name]['Skill4']=='Dragon Claw'and monsters[name]['Sk4mode']=='seq_swtigerfist'
 cpp=template.read_text().split('int main(){')[0];start=cpp.index('void sub_6FD13410(');end=cpp.index('\nint32_t __fastcall sub_6FC64E90',start)
 cpp=cpp[:start]+r'''
int builderFlag=0,progressiveFlag=0;
void sub_6FCF8C70(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*,int,int){lastHand=sub_6FC7C7B0(p)==&rightWeapon?1:2;builderFlag=bool(p->dwFlags&64);saved.wResultFlags=1;allocs++;}
void sub_6FCF77E0(D2GameStrc*,D2UnitStrc*p,D2DamageStrc*){progressiveFlag=bool(p->dwFlags&64);p->dwFlags|=64;}
'''+function(texts['source/D2Game/src/SKILLS/SkillAss.cpp'],'SKILLS_SrvDo046_DragonClaw')+'\n'+function(texts['source/D2Game/src/SKILLS/SkillMonst.cpp'],'SKILLS_SrvSt25_64_DragonClaw_MonFrenzy')+r'''
void sub_6FD13410(D2GameStrc*g,D2UnitStrc*p){SKILLS_SrvDo046_DragonClaw(g,p,260,3);}
'''+cpp[end:]
 cpp+=r'''
int main(){printf("[");bool comma=false;for(int cls:{417,418})for(int speed:{38,128,175,256,333,448})for(int present:{0,1}){
owner={};owner.dwClassId=cls;selected=present;UNITS_InitializeSequence(&owner);owner.dwSeqSpeed=speed;int started=SKILLS_SrvSt25_64_DragonClaw_MonFrenzy(&game,&owner,260,3);allocs=drains=0;
printf("%s{\"Actor\":%d,\"Speed\":%d,\"Target\":%d,\"Started\":%d,\"Trace\":[",comma?",":"",cls,speed,present,started);comma=true;
for(int tick=1;tick<200;tick++){unsigned point=owner.dwSeqFrame;int remaining=owner.dwFrameCountPrecise,action=owner.nActionFrame,flag=bool(owner.dwFlags&64);lastHand=builderFlag=progressiveFlag=0;int result=sub_6FC64E90(&game,&owner);
printf("%s[%d,%u,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d,%d]",tick>1?",":"",tick,point,remaining,action,flag,result,owner.dwSeqFrame,owner.dwFrameCountPrecise,owner.nActionFrame,bool(owner.dwFlags&64),lastHand,builderFlag,progressiveFlag,allocs,drains);if(result==2)break;}printf("]}");}printf("]\n");}
'''
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(source),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stderr[-1800:]
 r=subprocess.run([str(binary)],capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr;cases=json.loads(r.stdout);assert len(cases)==24
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 handler=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp';execution=json.loads((ROOT/'tools/reference/skill-execution-source-evidence.json').read_text());assert hashlib.sha256(function(handler.read_text(),'D2GAME_SKILLS_Handler_6FD12BA0').encode()).hexdigest()==execution['completeRoutineHashes']['D2GAME_SKILLS_Handler_6FD12BA0']
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=24,reusedWholePacketRoutineHashes=review,genericExecutionRoutineHashes=execution['completeRoutineHashes'],artifacts={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__).resolve(),template,mp,source,binary,fixture,handler,ROOT/'tools/reference/skill-execution-source-evidence.json']},scope='Complete normal NPC mode14 initializer/callback/step/end/selector plus complete do46/start25. Successful packet builder/progressive release are declared trace services here;their existing1152whole-source composition is authenticated without repeating it. Raw point hand choice and flag clear at packet/progressive entry,progressive sets it before drain,parity only afterward;missing target preserves prior flag. Owned monster slot4selects the existing16row sequence. Modern exact NPC phase/charge/accuracy boundaries remain scoped;runtime reuses approved charged-hit/consume policy,not player payment/clock.')
 (ROOT/'tools/reference/shadow-dragon-claw-source-evidence.json').write_text(json.dumps(e,indent=2)+'\n');print('NPC Dragon Claw:24 new full callback/do46 controls;13 packet helpers authenticated;ASan/UBSan clean.')
if __name__=='__main__':main()
