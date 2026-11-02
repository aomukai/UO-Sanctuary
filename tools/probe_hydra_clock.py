#!/usr/bin/env python3
"""Reuse Wake's complete ordinary scheduler/callback/end composition for Hydra."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hydra-clock-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 subprocess.run([sys.executable,str(ROOT/'tools/probe_wake_of_fire_clock_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 priorpath=ROOT/'tools/reference/wake-of-fire-clock-source-evidence.json';prior=json.loads(priorpath.read_text());tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']
 original=ROOT/'.local/class-skills/wake-of-fire-clock-source-probe.cpp';cpp=original.read_text().split('int main()',1)[0]
 bodies={}
 for rel,digest in prior['sourceHashes'].items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert sha(p)==digest and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
  for name,expected in prior['routineHashes'].items():
   try:b=function(raw.decode(),name)
   except AssertionError:continue
   assert hashlib.sha256(b.encode()).hexdigest()==expected and function(cpp,name)==b;bodies[name]=b
 assert set(bodies)==set(prior['routineHashes'])
 def replace(old,new):
  nonlocal cpp
  assert old in cpp,old;cpp=cpp.replace(old,new)
 replace('dwClassId=411','dwClassId=351');replace('nBaseId=411','nBaseId=351');replace('assert(id==411)','assert(id>=351&&id<=353)');replace('assert(mode==9)','assert(mode==4||mode==9)')
 # Data/model/rate/start/do and event storage remain the same explicit
 # boundaries as the existing ordinary scheduler composition.
 cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int species:{351,352,353})for(int mode:{9,4})for(int speed:{0,128,180,256,341,512})for(int initial:{0,10,25,100})for(int stun:{0,1})for(int start:{0,1}){
 D2GameStrc game;Anim animation;if(mode==4)animation.pFrameFlags[8]=2;MonsterData data;D2UnitStrc actor;
 actor.dwClassId=species;record.nBaseId=species;actor.pAnimData=&animation;actor.pMonsterData=&data;actor.stunned=stun;actor.wAnimSpeed=speed;actor.dwFrameCountPrecise=12*256;
 D2ModeChangeStrc change{&actor,mode};nextAi=initial?100+initial:0;events.clear();used=mode==4;usedSkill.flags=0;starts=dispatches=stops=0;startResult=start;
 int entered=sub_6FC645E0(&game,&change);assert(entered==1&&actor.dwAnimMode==mode&&starts==int(mode==4));
 assert(UNITS_GetFrameBonus(&actor)==0);sub_6FCBCE70(&game,&actor);auto schedule=events;int shot=-1,end=-1;
 for(auto e:schedule){game.dwGameFrame=e.frame;
  if(e.type==EVENTTYPE_MODECHANGE){assert(mode==4&&e.arg==2);shot=e.frame-100;D2GAME_MONSTERS_AiFunction01_6FC65080(&game,&actor,e.arg,e.index);}
  else{assert(e.type==EVENTTYPE_ENDANIM);end=e.frame-100;D2GAME_MONSTERS_AiFunction02_6FC65150(&game,&actor,0,0);}
 }
 assert(actor.dwAnimMode==1&&dispatches==int(shot>=0)&&stops==dispatches);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",species,mode,speed,initial,stun,start,entered,shot,end,nextAi-100,dispatches,stops);first=false;
}printf("]\n");}
'''
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O2','-Wno-multichar',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==576
 clocks={0:(-1,1),128:(16,24),180:(12,18),256:(8,12),341:(7,10),512:(4,6)}
 for species,mode,speed,initial,stun,start,entered,shot,end,ai,dispatched,stops in cases:
  expected,endframe=clocks[speed];expected=expected if mode==4 else -1
  assert (entered,shot,end,ai,dispatched,stops)==(1,expected,endframe,initial if initial>endframe else endframe+(45 if stun else 15),int(expected>=0),int(expected>=0)),(species,mode,speed,initial,stun,start,shot,end,ai)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-Wno-multichar','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 animation=ROOT/'tools/reference/d2r-local-hydra-animation-evidence.json';data=json.loads(animation.read_text());assert data['referenceVersion']=='3.3.93847'
 pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ('monstats','monstats2'):
  q=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(q)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(q.relative_to(ROOT))]=sha(q);rows=json.loads(q.read_text())
  for i in range(1,4):
   r=rows['hydra'+str(i)]
   if name=='monstats':assert r['Sk1mode']=='A1' and r['aidel']==r['aidel(N)']==r['aidel(H)']==15 and not r.get('SplEndGeneric') and not r.get('SplGetModeChart')
   else:assert r['mA1']==r['mS2']==1 and not r.get('A1mv') and not r.get('S2mv') and r['BaseW']=='hth'
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=prior['sourceHashes'],routineHashes=prior['routineHashes'],currentTable=pins,sanitizers='ASan/UBSan clean',
  established=['Complete ordinary entry,frame-bonus,scheduler,direct event dispatch,used-skill callback,ENDANIM,generic end and neutral handoff are reused unchanged from Wake. Current three Hydra model records supply12frame S2and A1/frame8marker2. Emergence has no used skill/action;attack uses nonchannelled HydraMissile. Normal rate256gives emergence end+12and preserves initial AI+25;attack shot+8/end+12/next AI+27when no future AI deadline exists.',
  'All three classes,six declared rate controls,four existing AI deadlines,stun and start success/failure compose in576contexts. A future existing AI event remains unchanged even if it precedes end+15;otherwise end+15or stunned end+45is scheduled. Direct generic entry ignores the supplied skill start failure on this nonmoving route.'],
  declaredServices=['Older engine composition with current owned animation metadata,not a modern executable timing certificate. Rate computation,mode-change bridge,callback lookup,start/do/projectile creation,event list,states and StopSequence are declared services. Only ordinary nonchannelled used-skill actions and action-free emergence are exercised.',
  'AI86execution/target acquisition,full event-list interleaving before ENDANIM,global scheduling,death/retirement,and projectile contacts/owner lifetime/persistence/input remain separate. Future-deadline controls inspect end handoff without executing that earlier AI event. No C#/runtime/install/save change.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),priorpath,ROOT/'tools/probe_wake_of_fire_clock_reference.py',original,animation,p,binary,san,log,fixture)})
 target=ROOT/'tools/reference/hydra-clock-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra ordinary clock:576complete scheduler/action/end/neutral contexts;normal emergence12/AI25,attackshot8/end12/AI27;ASan/UBSan clean;AI/world services separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
