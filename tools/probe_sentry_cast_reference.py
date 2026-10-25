#!/usr/bin/env python3
"""Compose complete generic start/do wrappers with do45; world factory is a service.

Reuse the existing verified Strafe wrapper ABI. This probe establishes routing,
not current executable mana behavior, native input or complete placement.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_strafe_action_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
 def extract(rel,name):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
  while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
 ref=ROOT/'tools/reference/d2r-named-equipment';raw=(ref/'json/skills.json').read_bytes();meta=json.loads((ref/'sources.json').read_text());assert hashlib.sha256(raw).hexdigest()==meta['files']['json/skills.json']['sha256'];skill=next(x for x in json.loads(raw).values() if x.get('skill')=='Charged Bolt Sentry')
 assert skill['srvdofunc']==45 and skill['mana']==13 and skill['manashift']==8 and not any(skill.get(k) for k in ['srvstfunc','usemanaondo','decquant','periodic','srvmissile','delay'])
 base=(STAGE/'strafe-action-source-probe.cpp').read_text().split('int main()',1)[0];base='#include <cassert>\n'+base
 # These are the declared ABI/services, not source-body edits.
 base=base.replace('mana>=2816','mana>=3328').replace('mana<2816','mana<3328').replace('mana-=2816','mana-=3328')
 base=base.replace('int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 26;}','int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 261;}')
 base=base.replace('int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 20;}','int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 3;}')
 a='int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int level){return calc==0?std::min(4+level-1,10):calc==1?5*level+hardGuided*10+hardMulti*5:calc==2?2+level/4:35;}'
 assert a in base;base=base.replace(a,'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int id,int){assert(id==261 && calc==0);return 0;}')
 a='void D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=selected->x;*y=selected->y;}'
 assert a in base;base=base.replace(a,'int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int*,int*);')
 # Confirm inherited complete wrapper bodies are exactly the pinned source.
 for name in ['sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0']:
  body=extract('source/D2Game/src/SKILLS/Skills.cpp',name);assert body.strip() in base
 base+=r'''
int aimValid=1,aimX=108,aimY=208,targetCalls=0,placeCalls=0,makePet=1;D2UnitStrc pet;
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){targetCalls++;order+='T';*x=aimX;*y=aimY;return aimValid && aimX && aimY;}
D2UnitStrc* sub_6FCF8610(D2GameStrc*,D2UnitStrc*,int x,int y,D2SkillsTxt* p,int id,int rank){assert(x==aimX&&y==aimY&&p==&row&&id==261&&rank==3);placeCalls++;order+='P';if(makePet){created++;return &pet;}return nullptr;}
'''
 base+=extract('source/D2Game/src/SKILLS/SkillAss.cpp','SKILLS_SrvDo045_Sentry')
 base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int record:{0,1})for(int aim:{0,1})for(int make:{0,1})for(int before:{0,3328})for(int after:{0,3328})for(int onDo:{0,1})for(int point=0;point<3;point++){
 D2GameStrc game;D2UnitStrc owner;reset(owner,1,10,before);row.wSrvStartFunc=0;row.wSrvDoFunc=45;row.dwDelay=0;row.dwFlags[1]=onDo?gdwBitMasks[SKILLSFLAGINDEX2_USEMANAONDO]:0;hasRow=record;aimValid=aim;makePet=make;aimX=point==1?0:108;aimY=point==2?0:208;targetCalls=placeCalls=0;gpSkillSrvDoFnTable_6FD40A20[45]=SKILLS_SrvDo045_Sentry;
 int started=sub_6FD12950(&game,&owner,261,3,0);assert(mana==before&&manaCalls==0);mana=after;int result=started?D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,261,3,1,0,0):-1;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%s\"]",first?"":",",record,aim,make,before,after,onDo,point,started,result,created,manaCalls,manaPaid,mana,(owner.dwFlags&UNITFLAG_SKSRVDOFUNC)!=0,targetCalls,placeCalls,order.c_str());first=false;
}printf("]\n");}
'''
 p=STAGE/'sentry-cast-source-probe.cpp';p.write_text(base);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-cast-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:];rows=json.loads(run.stdout);assert len(rows)==192
 for record,aim,make,before,after,onDo,point,started,result,created,calls,paid,left,flag,gets,places,order in rows:
  start=bool(record and before>=3328);gate=start and (not onDo or after>=3328);place=gate and aim and point==0;emits=bool(place and make);payment=bool(emits and after>=3328)
  expected=(int(start),int(emits) if start else -1,int(emits),int(emits),int(payment),after-3328*payment,int(gate),int(gate),int(place),('T' if gate else '')+('P' if place else '')+('M' if emits else ''))
  assert(started,result,created,calls,paid,left,flag,gets,places,order)==expected
 out=STAGE/'sentry-cast-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',handlerEnabled=False,nativeCastImplemented=False,legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes=routines,sharedAbiEvidence='tools/reference/strafe-action-engine-evidence.json',sharedFactoryEvidence='tools/reference/assassin-trap-summon-engine-evidence.json',targetGetterEvidence='tools/reference/sentry-aim-source-evidence.json',pinnedSkill=skill,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=192),established=['Complete generic start/do wrappers and do45 compose with the current absent start function/usemanaondo flag. Start validates available mana without paying; successful do45 requests its explicit resolved ground point and triggers one post-placement payment. Failed target/zero coordinate/factory returns0 without debit.','The declared rank3 cost is13mana. With no usemanaondo bit, losing mana after successful start does not add a generic do-event eligibility check: a successful source do45 can create an actor, then attempt a debit that the declared insufficient-mana service refuses; handler result remains1. The flag-enabled control rejects before do45.','Do45 sets the server-do flag before target retrieval, unlike missing-row or pre-do resource rejection. It passes the resolved point to the separately verified common factory; it does not perform Blade do44 caster reposition or AI+1 override.'],limits=['Supplementary legacy wrapper behavior is not current3.3 executable confirmation. Mana eligibility/debit are explicit13mana/rank3 services; the full debit routine, rank1 legacy payment quirk, blood mana, charges and player mode/input/animation/cancellation are not executed. Do not silently adopt the lost-mana exception as current behavior.','The world summon factory is a checked explicit-point success/failure service here; its complete creation/pet/FIFO contracts have separate evidence. Actual spawn radius/room/collision geometry, placement limits and learned native casting remain pending.','The reused Strafe ABI retains unrelated compiled helpers, but only the complete generic wrappers and do45 route execute in this probe. No runtime assembly or learned handler is changed.'],sanitizers='ASan/UBSan; exit0 and empty runtime stderr required')
 target=ROOT/'tools/reference/sentry-cast-source-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('192 complete generic start/do45/payment route cases pass; current lost-mana/rank1/input acceptance remains open.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
