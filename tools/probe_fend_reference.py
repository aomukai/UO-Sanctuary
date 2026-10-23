#!/usr/bin/env python3
"""Compile pinned Fend count/start/do13 and ordinary Amazon A1 rollback contracts."""
import argparse, ast, hashlib, json, re, struct, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
  blocks[name]=text[m.start():e];return blocks[name]+'\n'
 ama=source('source/D2Game/src/SKILLS/SkillAma.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');units=source('source/D2Game/src/UNIT/SUnit.cpp');common=source('source/D2Common/src/Units/Units.cpp')
 # Reuse only the existing, explicitly declared Zeal fixture services; do not run
 # its probe or rewrite its evidence. Actual Fend/source bodies are added below.
 module=ast.parse((ROOT/'tools/probe_zeal_reference.py').read_text());header=next(n.value.value for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
 cpp=header.replace('#define __fastcall','#include <cstdlib>\n#define __stdcall\n#define D2_ASSERT(x) if(!(x))std::abort()\n#define __fastcall')
 cpp=cpp.replace('dwParam[8]={0,100}','dwParam[8]={1,30}')
 cpp=cpp.replace('int dwUnitType=1,dwUnitId=0,','int dwUnitType=1,dwClassId=0,dwAnimMode=4,dwUnitId=0,')
 cpp=cpp.replace('return calc==0?hitCount:336','return calc==0?12:260')
 cpp=cpp.replace('int UNITS_GetFrameBonus(D2UnitStrc*){return 0;} // Original Paladin A1 table: all weapon classes zero.','int UNITS_GetFrameBonus(D2UnitStrc*);')
 cpp+=r'''
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7,NUM_WEAPON_CLASSES=14,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16;
int weaponClass=3;
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc* u,int){return u;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){return weaponClass;}
bool UNITS_CanDualWield(D2UnitStrc*){return false;}
int sub_6FD107E0(D2AuraCallbackStrc*,D2UnitStrc*);
'''
 for text,name in [(skills,'sub_6FD107E0'),(skills,'sub_6FD10790'),(skills,'sub_6FD10880'),(skills,'sub_6FD107F0'),(ama,'SKILLS_SrvSt09_Fend'),(ama,'SKILLS_SrvDo013_Fend_Zeal_Fury'),(common,'UNITS_GetFrameBonus'),(units,'sub_6FCBCE70'),(units,'sub_6FCBCFD0')]:cpp+=routine(text,name)
 raw=(ROOT/'.local/research/AnimData.d2').read_bytes();anim_hash=hashlib.sha256(raw).hexdigest();assert anim_hash=='e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb';pos=0;animations=[]
 for bucket in range(256):
  count=struct.unpack_from('<i',raw,pos)[0];pos+=4
  for _ in range(count):
   data=raw[pos:pos+160];pos+=160;code=data[:8].rstrip(b'\0').decode()
   if code in ['AMA11HT','AMA12HT']:
    frames,speed=struct.unpack_from('<ii',data,8);events=[i for i,v in enumerate(data[16:]) if v==1];assert speed==256 and len(events)==1;animations.append(dict(code=code,frames=frames,event=events[0]))
 assert pos==len(raw) and len(animations)==2
 cpp+=r'''
int main(){D2GameStrc game;D2UnitStrc owner,targets[20];owner.dwUnitType=0;for(int i=0;i<20;i++)targets[i].dwUnitId=10*(i+1);
bool first=true;std::printf("{\"starts\":[");
for(int record:{0,1})for(int skill:{0,1})for(int hasTarget:{0,1})for(int count:{0,1,2,5,12,13,20}){
hasRow=record;hasSkill=skill;used={};selected=hasTarget?&targets[0]:nullptr;candidates.clear();for(int i=count-1;i>=0;i--)candidates.push_back(&targets[i]);
int result=SKILLS_SrvSt09_Fend(&game,&owner,30,20);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",record,skill,hasTarget,count,result,used.p[0],used.p[2]);
}
std::printf("],\"strikes\":[");first=true;
for(int record:{0,1})for(int skill:{0,1})for(int count:{0,1,2,5})for(int lost:{0,1,2})for(int hit:{0,1})for(int remaining:{1,2,12}){
hasRow=record;hasSkill=skill;row.nEType=0;row.nSrcDam=128;hitFlag=hit;used={};used.p[0]=remaining;used.p[1]=1;used.p[2]=10;candidates.clear();
for(int i=0;i<count;i++){targets[i].alive=!(i==0 && lost==1);targets[i].inRange=!(i==0 && lost==2);candidates.push_back(&targets[i]);}
allocations=drains=rollbacks=0;lastTarget=-1;lastDamage=-1;lastSource=-1;lastRollback=-1;
int result=SKILLS_SrvDo013_Fend_Zeal_Fury(&game,&owner,30,20);if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",record,skill,count,lost,hit,remaining,result,allocations,lastTarget,lastDamage,lastSource,drains,used.p[0],used.p[2],rollbacks,lastRollback);
}
std::printf("],\"timing\":[");first=true;
'''
 for animation in animations:
  family=1 if animation['code'].endswith('1HT') else 2
  cpp+=f'''for(int advance:{{38,128,180,256,320,448}})for(int count:{{1,2,3,5,12}}){{
Anim anim;anim.pFrameFlags[{animation['event']}]=1;D2UnitStrc unit;unit.dwUnitType=0;unit.dwClassId=0;unit.pAnimData=&anim;unit.wAnimSpeed=advance;unit.dwFrameCountPrecise={animation['frames']}*256;weaponClass={3 if family==1 else 6};game.dwGameFrame=100;events.clear();sub_6FCBCE70(&game,&unit);std::vector<int> hits;int end=0;
for(int i=0;i<count;i++){{auto it=std::find_if(events.begin(),events.end(),[](auto e){{return e.type==EVENTTYPE_MODECHANGE;}});for(auto e:events)if(e.type==EVENTTYPE_ENDANIM)end=e.frame-100;if(it==events.end())break;game.dwGameFrame=it->frame;hits.push_back(game.dwGameFrame-100);if(i+1<count)sub_6FCBCFD0(&game,&unit,30);}}
if(!first)std::printf(",");first=false;std::printf("[{family},%d,%d,%d,[",advance,count,UNITS_GetFrameBonus(&unit));for(size_t i=0;i<hits.size();i++)std::printf("%s%d",i?",":"",hits[i]);std::printf("],%d]",end);}}
'''
 cpp+='std::printf("]}\\n");}\n'
 p=STAGE/'fend-source-probe.cpp';p.write_text(cpp);binary=STAGE/'fend-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data['starts'])==56 and len(data['strikes'])==288 and len(data['timing'])==60
 for record,skill,selected,count,result,left,target in data['starts']:
  valid=record and skill
  assert result==int(valid)
  if valid:assert left==(min(12,count) if selected else 0) and target==(10 if selected or count else 0)
 for record,skill,count,lost,hit,remaining,result,alloc,target,damage,src,drain,left,nxt,rollbacks,rollback in data['strikes']:
  ids=[10*(i+1) for i in range(count) if i or not lost]
  valid=record and skill and ids
  assert result==0
  if not valid:assert alloc==drain==rollbacks==0 and left==remaining
  else:
   actual=ids[0];next_target=next((x for x in ids if x>actual),ids[0]);again=remaining>1
   assert (alloc,target,damage,src,drain,left,nxt,rollbacks,rollback)==(1,actual,260 if hit else 0,128,4,remaining-1,next_target if again else 10,int(again),30 if again else -1)
 for family,advance,count,bonus,hits,end in data['timing']:
  row=next(x for x in animations if x['code'].endswith('1HT' if family==1 else '2HT'))
  assert bonus==2 and 1<=len(hits)<=count and all(b>a for a,b in zip(hits,hits[1:])) and end>hits[-1]
  if advance==256:assert len(hits)==count
  assert hits[0]==((row['event']-2)*256+advance-1)//advance

 # Run the original current-family calculator rollback block unchanged. UI,
 # table formatting and weapon AnimData are declared adapters, not live DOM.
 calcdir=ROOT/'.local/research/ias-calculator-b7d7910'
 commit=json.loads((calcdir/'commit.json').read_text());ctree=json.loads((calcdir/'tree.json').read_text());assert commit['sha']=='b7d791031b73b0c6e56a3659e9198578aed21261' and ctree['sha']==commit['commit']['tree']['sha']
 modernhashes={}
 for name in ['README.md','calculator.js','constants.js']:
  content=(calcdir/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest()==next(x['sha'] for x in ctree['tree'] if x['path']==name);modernhashes[name]=hashlib.sha256(content).hexdigest()
 calc=(calcdir/'calculator.js').read_text();functions={}
 def jsfunction(name):
  m=re.search(r'\bfunction\s+'+name+r'\([^;\n]*\)\s*\{',calc);assert m,name;e=calc.index('{',m.start())+1;depth=1
  while depth:depth+=(calc[e]=='{')-(calc[e]=='}');e+=1
  functions[name]=calc[m.start():e];return functions[name]
 prefix_at=calc.index('let firstHitLength = 256 *');at=calc.index('if (skill == skills.FURY || skill == skills.STRAFE || skill == skills.FEND',prefix_at);e=calc.index('{',at)+1;depth=1
 while depth:depth+=(calc[e]=='{')-(calc[e]=='}');e+=1
 rollback=calc[prefix_at:e];assert rollback.endswith('}')
 js=r"""const skills=Object.fromEntries(['FEND','FURY','STRAFE','DRAGON_TALON','ZEAL','WHIRLWIND','STANDARD'].map(x=>[x,{}]));
const skill=skills.FEND,char={AMAZON:0,SORCERESS:1},character=0;
const wt=Object.fromEntries(['UNARMED','ONE_HANDED_SWINGING','TWO_HANDED_SWORD','ONE_HANDED_THRUSTING','TWO_HANDED_THRUSTING','TWO_HANDED'].map(x=>[x,{}]));
const trun=Math.trunc;function formatRollbackHitLength(x){return x;}function frameLengthsNotEqual(){return true;}
"""+jsfunction('rbf')+'\n'+jsfunction('rhits')+'\n'+jsfunction('getStartingFrame')+'\n'+r"""
let output=[];
for(let family of [1,2])for(let speedIncrease of [38,128,180,256,320,448]){
const weaponType=family==1?wt.ONE_HANDED_THRUSTING:wt.TWO_HANDED_THRUSTING;
const startingFrame=getStartingFrame(weaponType),framesPerDirection1=family==1?9:11,framesPerDirection2=family==1?15:18,offset=0,acceleration=0,EIAS=0;
const previousFrameLengths=[[],[]],results=[[],[]],accelerationTables=results.map(x=>({addBreakpoint:(_,v)=>x.push(v)}));
"""+rollback+r"""
output.push([family,speedIncrease,startingFrame,rbf(),results[0][0],results[1][0]]);
}
process.stdout.write(JSON.stringify(output)+'\n');
"""
 jsp=STAGE/'fend-modern-probe.js';jsp.write_text(js);data['modern']=json.loads(subprocess.check_output(['node',str(jsp)]));assert len(data['modern'])==12
 for family,rate,bonus,factor,even,odd in data['modern']:
  assert bonus==2 and factor==30 and len(even)==6 and len(odd)==5 and min(even+odd)>0
  action,end=(9,15) if family==1 else (11,18)
  lengths=[((action-2)*256+rate-1)//rate];start=2;starts=[2]
  for hit in range(4):
   start=((start*256+rate*lengths[-1])//256)*70//100;starts.append(start);lengths.append(((action-start)*256+rate-1)//rate)
  assert even==lengths+[((end-start)*256+rate-1)//rate-1]
  assert odd==lengths[:4]+[((end-starts[3])*256+rate-1)//rate-1]
 fixture=STAGE/'fend-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},probe=dict(startCases=56,strikeCases=288,timingCases=60,incompleteTimingCases=sum(len(row[4])<row[2] for row in data['timing']),fixture=str(fixture.relative_to(ROOT))),animations=animations,legacyAnimDataHash=anim_hash,modernOriginalAuthor=dict(repository='https://github.com/Warren1001/IAS_Calculator',commit=commit['sha'],fileHashes=modernhashes,functionHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in functions.items()},rollbackBlockHash=hashlib.sha256(rollback.encode()).hexdigest(),rows=12,scope='Original implementation with declared UI/table/weapon metadata, not pinned executable capture.'),established=[
 'Start9 clamps eligible nearby count to12 when a target is selected. No-target helper with last=-1 finds a target through fallback but does not fill its output count; start retains0. Generic do13 still attempts one strike before decrementing0. Outer current player/client target acquisition is separate.',
 'Do13 revalidates original target/reselects a cyclic available identity at each strike, applies declared AR/ED on successful result, allocates SrcDam128 and drains ordinary wear, then decrements count and requests rollback30 only if remaining positive and a next target exists. The selector can wrap to the same sole surviving target.',
 'Ordinary Amazon A1 frame bonus2 and unchanged rollback scheduler produce60 declared-rate/count observations. The isolated initial-scheduler/rollback call chain loses later attack events at some declared rates; full outer animation-anchor/event delivery is not supplied. Do not port those isolated clocks as current timing. Current anim=A1 is used deliberately; seqnum9 alone does not select SQ. Param2=30 is a rollback factor, not a global doubled attack rate.'],limits=[
 'Supplementary older engine bodies and cached AnimData with incomplete acquisition provenance; not current3.3.93847 executable equivalence or native handler proof.',
 'Shared Zeal fixture world eligibility/query/filter/geometry, hit/result, formulas/ED, combat allocation/drain, hand selection and event delivery are explicitly declared. Count wrapper/callback/selector bodies compile unchanged; full aura scan predicate remains outside this probe.',
 'Original current-family calculator rate-scales animation position before rollback; twelve unmodified block/function observations separately validate five-hit/four-hit oscillation and final recovery. It differs from the isolated older scheduler/rollback fixture; outer animation-anchor updates and exact current timing remain unresolved. Do not silently conflate the sources.',
 'Only ordinary human Amazon spear/javelin A1, frame bonus2, declared rate and one event per cycle are exercised. Modern moving-target/dodge/avoidance/first-frame, stat/RNG/owner/client acceptance and native lifecycle remain separate.'
 ])
 target=ROOT/'tools/reference/fend-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered,'Fend evidence changed'
 else:target.write_text(rendered)
 print('Fend:56 start/count,288 do13 and60 raw A1 rollback observations compiled; 12 original modern rate-scaled rollback rows verified separately.')

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
