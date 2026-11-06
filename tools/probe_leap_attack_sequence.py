#!/usr/bin/env python3
"""Current SQ14 plus complete legacy initial/absolute-reset scheduler controls."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from player_weapon_sequences import ROOT,BASE,read,sha
from review_weapon_event_order import function
STAGE=BASE/'leap-attack-sequence-source'
def build(check=False):
 STAGE.mkdir(exist_ok=True);profiles,inputs,exe=read(14)
 lengths=[22,25,28,25,27,0,0,28,22,22,22,22,22,22]
 contacts=[16,16,18,16,17,None,None,18,16,16,16,16,16,16]
 assert len(profiles)==14
 for i,p in enumerate(profiles):
  n=lengths[i];expected=[[0,13 if k<15 else 7,k if k<15 else k-10 if n==22 else k-9,0,int(k in [5,11,contacts[i]])]for k in range(n)]
  assert (p['Points'],p['Frames'],p['RawAuxiliary'],p['Rows'])==(n,n,256,expected),(p['Code'],p)
 reference=ROOT/'tools/reference/dragon-flight-source-evidence.json';prior=json.loads(reference.read_text());source=BASE/'dragon-flight-source/source.cpp'
 assert sha(source)==prior['artifacts'][str(source.relative_to(ROOT))]
 inputs.update({str(p.relative_to(ROOT)):sha(p)for p in [reference,source]})
 cpp=source.read_text().split('int main(){')[0]
 old='void sub_6FD13410(D2GameStrc* g,D2UnitStrc* p){phases.push_back(p->dwSeqFrame>>8);callbackResult=SKILLS_SrvDo052_DragonFlight(g,p,275,rankNow);results.push_back(callbackResult);}'
 assert old in cpp;cpp=cpp.replace(old,'void sub_6FD13410(D2GameStrc*,D2UnitStrc* p){phases.push_back(p->dwSeqFrame>>8);results.push_back(p->dwSeqFrame&255);}')
 assert 'D2GAME_EVENTS_Delete_6FC34840('not in cpp
 cpp+='\nvoid D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){events.erase(std::remove_if(events.begin(),events.end(),[&](auto e){return e.type==type;}),events.end());}\n'
 cpp=cpp.replace('#include <cassert>','#include <cassert>\n#include <algorithm>',1)
 path=BASE/'d2moo-pinned-source/source/D2Game/src/UNIT/SUnit.cpp';raw=path.read_bytes();tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(v['sha']for v in tree['tree']if v['path']==str(path.relative_to(BASE/'d2moo-pinned-source')))
 absolute=function(raw.decode(),'D2GAME_SKILLS_RewindSkillEx_6FCBD260');cpp+='\n'+absolute+'\n';inputs[str(path.relative_to(ROOT))]=sha(path)
 active=[p for p in profiles if p['Points']]
 for i,p in enumerate(active):cpp+='D2AnimSeqTxt leap'+str(i)+'['+str(p['Points'])+']={'+','.join('{'+','.join(map(str,r))+'}'for r in p['Rows'])+'};\n'
 cpp+='D2AnimSeqTxt* leapRows[]={'+','.join('leap'+str(i)for i in range(12))+'};\n'
 cpp+='int leapLengths[]={'+','.join(str(p['Points'])for p in active)+'};\n'
 cpp+=r'''
int main(){printf("{\"initial\":[");
for(int profile=0;profile<12;profile++)for(int speed:{0,1,15,64,128,255,256,257,384,448,512,768,1024,4352,65536})for(int entry:{0,100,4127})for(int fraction:{0,1,255}){
 reset();owner.dwClassId=4;game.dwGameFrame=entry;owner.pAnimSeq=leapRows[profile];owner.dwSeqFrameCount=leapLengths[profile]*256;owner.dwSeqSpeed=speed;owner.dwFlags=0;owner.dwSeqFrame=fraction;
 sub_6FCBCE70(&game,&owner);auto clock=events;
 for(auto e:clock)if(e.type==EVENTTYPE_MODECHANGE){game.dwGameFrame=e.frame;sub_6FC80EE0(&game,&owner,e.event,e.param);}
 if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d],[",profile,speed,entry,fraction);
 for(size_t i=0;i<clock.size();i++){auto e=clock[i];if(i)printf(",");list({e.type,e.frame,e.event,e.param});}printf("],");list(phases);printf(",");list(results);printf(",%d,%d]\n",owner.dwSeqFrame,owner.nSeqCurrentFramePrecise);
}printf("],\"absolute\":[");comma=false;
for(int profile=0;profile<12;profile++)for(int speed:{0,1,15,64,128,255,256,257,384,448,512,768,1024,4352,65536})for(int entry:{20,100,4127})for(int point:{10,16}){
 reset();owner.dwClassId=4;game.dwGameFrame=entry;owner.pAnimSeq=leapRows[profile];owner.dwSeqFrameCount=leapLengths[profile]*256;owner.dwSeqSpeed=speed;owner.dwFlags=0;owner.dwSeqFrame=123;
 events.push_back({1,entry+1,1,99});events.push_back({2,entry+2,0,0});events.push_back({3,entry+3,0,17});
 D2GAME_SKILLS_RewindSkillEx_6FCBD260(&game,&owner,point);
 if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d],[",profile,speed,entry,point);
 for(size_t i=0;i<events.size();i++){auto e=events[i];if(i)printf(",");list({e.type,e.frame,e.event,e.param});}printf("],%d,%d]\n",owner.nSeqCurrentFramePrecise,owner.dwSeqFrame);
}printf("]}\n");}
'''
 bodies={name:function(source.read_text(),name)for name in ['DATATBLS_GetSequenceEvent','UNITS_SetAnimationFrame','UNITS_GetFrameBonus','sub_6FCBCE70','sub_6FC80EE0']}
 bodies['D2GAME_SKILLS_RewindSkillEx_6FCBD260']=absolute
 for name,body in bodies.items():assert function(cpp,name)==body,name
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wformat=2','-Werror=format','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-3000:]
 cases=json.loads(result.stdout);assert len(cases['initial'])==1620 and len(cases['absolute'])==1080
 for args,events,phases,fractions,word,epoch in cases['initial']:
  profile,speed,entry,fraction=args;assert epoch==(entry*256 if speed else 0);assert phases==[e[3]for e in events if e[0]==1]and fractions==[fraction]*len(phases)
  assert word==((phases[-1]<<8)|fraction if phases else fraction)
  if speed==256:assert [e[1]-entry for e in events if e[0]==1]==[5,11,next(i for i,r in enumerate(active[profile]['Rows'][15:],15)if r[4])]
 for args,events,epoch,word in cases['absolute']:
  profile,speed,entry,point=args;assert epoch==(entry-point)*256 and word==123
  assert events[0]==[3,entry+3,0,17]and all(e[3]==0 for e in events[1:])
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 data=ROOT/'data/Sanctuary/Balance/leap-attack-sequence.json';encoded=json.dumps(profiles,indent=2)+'\n'
 if check:assert data.read_text()==encoded
 else:data.write_text(encoded)
 for p in [Path(__file__).resolve(),ROOT/'tools/player_weapon_sequences.py']:inputs[str(p.relative_to(ROOT))]=sha(p)
 evidence=dict(referenceVersion='3.3.93847',executableSha256=exe,readOnlyFileInspection=True,processMemoryRead=False,currentExecutableCodeParity=False,
  classicDescriptorsMatched=322,sequence=14,profiles=14,activeProfiles=12,emptyProfiles=['BOW','XBW'],initialContexts=1620,absoluteContexts=1080,
  completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,data]},
  established=['Current SQ14 retains twelve22/25/27/28point profiles and two empty ranged profiles;raw auxiliary256. Three source action markers occur at5,11and the weapon-dependent16/17/18contact point. This file data does not select actual attack rate or flight duration.',
   'Six complete older scheduler/reader/frame/player-callback/absolute-reset bodies execute unchanged with explicit table/queue/unit/dispatcher services. Initial callbacks preserve ordinal and low fractional bits. Absolute resets cancel action/end events,preserve other event types,set epoch directly from point10or16,and leave dwSeqFrame unchanged. Future callbacks from the reset carry parameter0 rather than fresh initial ordinals.'],
  remaining=['Modern start41/do78,actual rate/flight/target/payment/death/input and landing wave emission/order remain unverified. Legacy absolute phase reset is distinct from relative rewind;neither is a new learned cast or a modern code certificate.'])
 target=ROOT/'tools/reference/leap-attack-sequence-source-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('Leap Attack SQ14:14profiles/12active,322classic anchors;1620initial+1080absolute-phase controls;ASan/UBSan clean;landing emission remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
