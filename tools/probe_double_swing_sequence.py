#!/usr/bin/env python3
"""Pin current SQ11 and execute the original scheduler/player callback unchanged."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from player_weapon_sequences import ROOT,BASE,read,sha
from review_weapon_event_order import function
STAGE=BASE/'double-swing-sequence-source'
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);profiles,inputs,exe=read(11)
 poses=[1,2,4,6,7,9,11,13,14,2,4,5,6,8,9,10,11];expected=[[0,7 if i<9 else 15,poses[i],0,int(i in [4,9])]for i in range(17)]
 assert len(profiles)==14 and all((p['Points'],p['Frames'],p['RawAuxiliary'],p['Rows'])==(17,17,256,expected)for p in profiles)
 priorPath=ROOT/'tools/reference/dragon-flight-source-evidence.json';prior=json.loads(priorPath.read_text());inputs[str(priorPath.relative_to(ROOT))]=sha(priorPath)
 source=BASE/'dragon-flight-source/source.cpp';assert sha(source)==prior['artifacts'][str(source.relative_to(ROOT))];inputs[str(source.relative_to(ROOT))]=sha(source)
 cpp=source.read_text().split('int main(){')[0];old='void sub_6FD13410(D2GameStrc* g,D2UnitStrc* p){phases.push_back(p->dwSeqFrame>>8);callbackResult=SKILLS_SrvDo052_DragonFlight(g,p,275,rankNow);results.push_back(callbackResult);}'
 new='void sub_6FD13410(D2GameStrc*,D2UnitStrc* p){phases.push_back(p->dwSeqFrame>>8);results.push_back(p->dwSeqFrame&255);}'
 assert old in cpp;cpp=cpp.replace(old,new);seq='D2AnimSeqTxt sequence[17] = {'+','.join('{'+','.join(map(str,row))+'}'for row in expected)+'};'
 cpp,n=re.subn(r'D2AnimSeqTxt sequence\[23\] = \{[^\n]*\};',lambda _:seq,cpp);assert n==1
 cpp+=r'''
int main(){printf("{\"cases\":[");
for(int speed:{0,1,15,64,128,255,256,257,384,448,512,768,1024,4352,65536})for(int entry:{0,100,4127})for(int fraction:{0,1,255}){
 reset();owner.dwClassId=4;game.dwGameFrame=entry;owner.pAnimSeq=sequence;owner.dwSeqFrameCount=17*256;owner.dwSeqSpeed=speed;owner.dwFlags=0;owner.dwSeqFrame=fraction;
 sub_6FCBCE70(&game,&owner);auto clock=events;
 for(auto e:clock)if(e.type==EVENTTYPE_MODECHANGE){game.dwGameFrame=e.frame;sub_6FC80EE0(&game,&owner,e.event,e.param);}
 if(comma)printf(",");comma=true;printf("[[%d,%d,%d],[",speed,entry,fraction);
 for(size_t i=0;i<clock.size();i++){auto e=clock[i];if(i)printf(",");list({e.type,e.frame,e.event,e.param});}printf("],");list(phases);printf(",");list(results);printf(",%d]\n",owner.dwSeqFrame);
}printf("]}\n");}
'''
 names=['DATATBLS_GetSequenceEvent','UNITS_SetAnimationFrame','UNITS_GetFrameBonus','sub_6FCBCE70','sub_6FC80EE0'];bodies={name:function(source.read_text(),name)for name in names}
 for name,body in bodies.items():assert function(cpp,name)==body,name
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wformat=2','-Werror=format','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 cases=json.loads(run.stdout);assert len(cases['cases'])==135
 for args,events,phases,fractions,word in cases['cases']:
  speed,entry,fraction=args
  assert phases==[e[3]for e in events if e[0]==1] and fractions==[fraction]*len(phases) and word==((phases[-1]<<8)|fraction if phases else fraction)
  if speed==256:assert events==[[1,entry+4,1,0],[1,entry+9,1,1],[2,entry+17,0,0]]
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n');data=ROOT/'data/Sanctuary/Balance/double-swing-sequence.json';encoded=json.dumps(profiles,indent=2)+'\n'
 if check:assert data.read_text()==encoded
 else:data.write_text(encoded)
 for p in [Path(__file__).resolve(),ROOT/'tools/player_weapon_sequences.py']:inputs[str(p.relative_to(ROOT))]=sha(p)
 result=dict(referenceVersion='3.3.93847',executableSha256=exe,readOnlyFileInspection=True,processMemoryRead=False,processInput=False,currentExecutableCodeParity=False,handlerEnabledByThisProbe=False,sequence=11,classicDescriptorsMatched=322,profiles=14,points=17,actionIndices=[4,9],sourceContexts=135,sourcePlayerClass=4,nativeComparisons=1890,inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,data]},
  established=['All322classic descriptor/profile pairs and exact pose rows anchor read-only current SQ11. All14current profiles retain17rows:ATTACK1mode7thenSPECIAL3mode15;action flags are atindices4/9. Raw auxiliary256is preserved without inventing its role.',
               'Five original scheduler/sequence-reader/player-callback/frame-setter/frame-bonus bodies remain unchanged.135supplied speed/entry/fraction contexts establish event ordinals0/1and retained FP8low bits;135x14native comparisons are required separately. At supplied advance256events are entry+4,+9 and end+17;these are supplied-rate facts,not a chosen real attack rate.'],
  services=['Existing pinned Flight oracle scaffold supplies unit/table/event queue services. The ordinary skill dispatcher is replaced by an explicit ordinal/fraction recorder;it does not run Double Swing,start68,damage,payment or target/hand selection. Barbarian class4and a present used-skill record are explicit. Frame bonus is0in source contexts.'],
  remaining=['Current startup68,mana cadence,paired attack rate,world hand/combat/target dispatch,input and client acceptance remain unverified. Legacy do70/search evidence remains separate and does not fill start68. The playable startup uses a separately approved Sanctuary rule;this source probe does not certify it or complete a class.'])
 target=ROOT/'tools/reference/double-swing-sequence-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('Double Swing SQ11:14current profiles/322classic anchors;17rows/action indices4,9;135complete scheduler/player-callback contexts;ASan/UBSan clean;startup68 remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
