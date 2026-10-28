#!/usr/bin/env python3
"""Compose complete uninterruptible removal with full NPC death return."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'monster-death-delay'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);priorpath=ROOT/'tools/reference/monster-return-source-evidence.json';prior=json.loads(priorpath.read_text());inputs={str(priorpath.relative_to(ROOT)):sha(priorpath)}
 for group in ['inputs','artifacts']:
  for n,h in prior[group].items():assert sha(ROOT/n)==h,n;inputs[n]=h
 cpp=(BASE/'monster-return/source.cpp').read_text().split('bool first=true;',1)[0]
 # Delimit the inherited wrapper and HP helper for exact function extraction.
 cpp=cpp.replace('}uint8_t __fastcall sub_6FC62F50','}\nuint8_t __fastcall sub_6FC62F50')
 for n,h in {**prior['compiledUnchangedRoutineHashes'],**prior['retainedRoutineHashes']}.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());name='source/D2Game/src/UNIT/SUnit.cpp';path=BASE/'d2moo-pinned-source'/name;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name);inputs[str(path.relative_to(ROOT))]=sha(path)
 body=function(raw.decode(),'sub_6FCBDE90');unchanged=dict(prior['compiledUnchangedRoutineHashes']);unchanged['sub_6FCBDE90']=hashlib.sha256(body.encode()).hexdigest()
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a[:120];cpp=cpp.replace(a,b,1)
 replace('int requested=-1;', 'bool gamePresent=true,attackerPresent=true;int returnCalls=0,deletedThink=0;int requested=-1;')
 replace('state==STATE_FREEZE?frozen:false','state==STATE_FREEZE?frozen:state==STATE_DEATH_DELAY?delay:false')
 replace('void STATES_ToggleState(D2UnitStrc* p,int state,int on){assert(p==&target&&state==STATE_DEATH_DELAY&&on==1);delay=true;trace.push_back({6,state,on});}', 'void STATES_ToggleState(D2UnitStrc* p,int state,int on){assert(p==&target&&(on==0||on==1));if(state==STATE_UNINTERRUPTABLE){uninterruptible=on;trace.push_back({19,state,on});}else{assert(state==STATE_DEATH_DELAY);delay=on;trace.push_back({6,state,on});}}')
 replace('assert(p==&target&&attacker==&actor&&petKill==1);trace.push_back({11,0,0});', 'assert(p==&target&&(attacker==&actor||attacker==nullptr)&&petKill==1);trace.push_back({11,0,0});')
 replace('UNUSED_VOID(D2GAME_EVENTS_Delete_6FC34840)', 'constexpr int EVENTTYPE_AITHINK=1002;void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc* p,int type,int zero){assert(p==&target&&type==EVENTTYPE_AITHINK&&zero==0);deletedThink++;trace.push_back({22,type,0});}')
 original=function(cpp,'SUNITDMG_ExecuteMissileDamage');replace(original,'namespace OriginalReturn{\n'+original+'\n}\nvoid SUNITDMG_ExecuteMissileDamage(D2GameStrc*g,D2UnitStrc*a,D2UnitStrc*u,D2DamageStrc*d){assert(d->wResultFlags==2&&d->dwDmgTotal==0&&d->dwPhysDamage==0&&d->dwFireDamage==0&&d->dwColdDamage==0&&d->dwLtngDamage==0&&d->dwMagDamage==0&&d->dwPoisDamage==0&&d->dwHitClass==0&&d->dwHitFlags==0&&d->dwStunLen==0);returnCalls++;trace.push_back({23,d->wResultFlags,d->dwDmgTotal});OriginalReturn::SUNITDMG_ExecuteMissileDamage(g,a,u,d);}')
 cpp+='''
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*p){assert(p==&target);trace.push_back({20,gamePresent,0});return gamePresent?&game:nullptr;}
D2UnitStrc* SUNIT_GetLastAttacker(D2GameStrc*,D2UnitStrc*p){assert(p==&target);trace.push_back({21,attackerPresent,0});return attackerPresent?&actor:nullptr;}
'''+body+'\n'
 for n,h in unchanged.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 cpp+=r'''
void State(){printf("[%d,%d,%d,%d,%d,%d,%d]",uninterruptible,delay,target.dwAnimMode,kills,returnCalls,deletedThink,target.stats[STAT_HITPOINTS]);}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int set:{0,1})for(int old:{0,1})for(int pending:{0,1})for(int hasGame:{0,1})for(int attacker:{0,1})for(int kind:{0,1,2})for(int roomKind:{-1,0,1})for(int monrow:{0,1})for(int townFlag:{0,1})for(int mode:{0,1,12,14})for(int life:{0,12800}){
actor={};target={};game={};row={};target.dwUnitType=UNIT_MONSTER;target.dwAnimMode=mode;target.stats[STAT_HITPOINTS]=life;actor.dwUnitType=kind;actor.pGame=target.pGame=&game;hasRoom=roomKind>=0;inTown=roomKind==1;hasRow=monrow;row.dwMonStatsFlags=townFlag?gdwBitMasks[MONSTATSFLAGINDEX_INTOWN]:0;uninterruptible=old;delay=pending;gamePresent=hasGame;attackerPresent=attacker;kills=returnCalls=deletedThink=0;trace.clear();
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",set,old,pending,hasGame,attacker,kind,roomKind,monrow,townFlag,mode,life);first=false;
sub_6FCBDE90(&target,set);State();printf(",");sub_6FCBDE90(&target,set);State();printf(",[");for(size_t i=0;i<trace.size();i++)printf("%s[%d,%d,%d]",i?",":"",trace[i][0],trace[i][1],trace[i][2]);printf("]]");
}printf("]\n");}
'''
 source=STAGE/'source.cpp';source.write_text(cpp);compiler=STAGE/'compiler.log';san=STAGE/'sanitizer.log'
 with compiler.open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g','-DD2_VERSION_HAS_UBERS','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(STAGE/'probe')],capture_output=True);san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:];cases=json.loads(run.stdout);assert len(cases)==9216
 stats=dict(contexts=len(cases),returnRequests=0,directKillRequests=0,deathRequestsAtPositiveLife=0,townSuppressedReturn=0,missingGameRetainsDelay=0)
 for p,first,second,trace in cases:
  set,old,pending,game,attacker,kind,room,row,town,mode,life=p;active=not set and pending and game;returned=int(active and attacker);direct=int(active and not attacker);refused=room==1 and (kind!=1 or not row or not town)
  kill=direct+int(returned and not refused and mode not in [0,12])
  expected=[set,0 if active else pending,0 if kill else mode,kill,returned,game,life]
  assert first==expected and second==[set,expected[1],expected[2],kill,returned,game*2,life],(p,first,second)
  assert trace[0]==[19,37,set] and trace[1]==[20,game,0]
  if active:
   clear=next(i for i,v in enumerate(trace) if v==[6,30,0]);lookup=next(i for i,v in enumerate(trace) if v[0]==21);assert clear<lookup
  stats['returnRequests']+=returned;stats['directKillRequests']+=direct;stats['deathRequestsAtPositiveLife']+=int(kill and life>0);stats['townSuppressedReturn']+=int(returned and refused);stats['missingGameRetainsDelay']+=int(not game and pending)
 out=BASE/'monster-death-delay-source-probe.json';encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 inputs[str(Path(__file__).relative_to(ROOT))]=sha(Path(__file__))
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],component='Uninterruptible removal and deferred NPC death completion source',currentExecutableParity=False,nativeConsumerAccepted=False,handlerEnabled=False,processMemoryRead=False,compiledUnchangedRoutineHashes=unchanged,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in [source,compiler,san,out]},verified=stats,
 established=['Complete sub_6FCBDE90 composes with the unchanged full NPC return,typed class,recovery and source seed routines. Uninterruptible toggles before game lookup. Missing game returns without clearing pending death. Clearing with a game clears death delay before last-attacker resolution,then sends a fresh zeroed death-only packet through return or directly requests kill without an attacker.', 'Queued AI-think deletion follows death processing on each call. Repeated clearing does not dispatch another death. Cached delayed death can request kill at positive HP. A present attacker follows return town/dead-mode guards;missing attacker uses the declared direct-kill service. These requests are not the complete kill routine.'],
 services=['NPC targets;room/row/state/game/last-attacker registry,kill/mode/deleted-think services are explicit. Full return is namespace-linked only to observe the fresh packet without editing its body. No actual actor mode scheduler,timer,persistence,owner lifecycle or current executable parity is certified. The inherited seed literal platform shim remains unchanged.'])
 target=ROOT/'tools/reference/monster-death-delay-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Deferred NPC death:9216complete-source contexts/two calls each;fresh return or missing-attacker kill,clear-before-lookup and delete-think order;native pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
