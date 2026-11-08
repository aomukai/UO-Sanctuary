#!/usr/bin/env python3
"""Compose complete NPC Talon start/do and ordinary initial/rewind clocks."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-talon-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};bodies={}
 for rel,names in [('source/D2Game/src/SKILLS/SkillAss.cpp',['SKILLS_SrvSt24_DragonTalon','SKILLS_SrvDo042_DragonTalon']),('source/D2Game/src/UNIT/SUnit.cpp',['sub_6FCBCE70','sub_6FCBCFD0'])]:
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 prior=ROOT/'tools/reference/shadow-kick-rewind-source-evidence.json';proof=json.loads(prior.read_text());assert len(proof['ownedRows'])==3 and all(r['precedingFlag']==0 for r in proof['ownedRows'])
 for rel,h in proof['artifacts'].items():assert sha(ROOT/rel)==h
 template=ROOT/'.local/class-skills/shadow-kick-rewind-source/source.cpp';cpp=template.read_text().split('void __fastcall sub_6FCBCFD0',1)[0]
 cpp=cpp.replace('#include <array>','#include <array>\n#include <algorithm>')
 cpp=cpp.replace('struct D2UnitStrc{','struct D2SeedStrc{unsigned value=0;};\nstruct D2UnitStrc{int dwUnitType=1;unsigned dwFlags=0;D2SeedStrc pSeed;')
 cpp=cpp.replace('deletes.push_back(type);','deletes.push_back(type);events.erase(std::remove_if(events.begin(),events.end(),[&](auto e){return e[0]==type;}),events.end());')
 cpp+='\nint UNITS_GetFrameBonus(D2UnitStrc*){return 0;}\n'+bodies['sub_6FCBCE70']+'\n'+bodies['sub_6FCBCFD0']+r'''
constexpr int UNIT_PLAYER=0,STAT_LEVEL=12,MONTYPEFLAG_UNIQUE=1,UNITFLAG_SKSRVDOFUNC=64;
struct D2SkillStrc{int remaining;};struct D2SkillsTxt{int dwCalc[4]={0,1,2,3};};struct D2DamageStrc{};
D2UnitStrc actor,target;D2GameStrc game;D2SkillStrc skill;D2SkillsTxt row;D2DamageStrc damage;
bool cached=false;int allocations=0,deliveries=0,rewinds=0;
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int){return &row;}
D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){return &skill;}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return &target;}
bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return true;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){assert(calc==0);return rank/6+1;}
void SKILLS_SetParam1(D2SkillStrc*s,int p){s->remaining=p;}int SKILLS_GetParam1(D2SkillStrc*s){return s->remaining;}
void sub_6FCF7BC0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2SkillsTxt*,int,int,int){assert(!cached);cached=true;++allocations;}
D2DamageStrc*SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return cached?&damage:nullptr;}
void sub_6FCF77E0(D2GameStrc*,D2UnitStrc*u,D2DamageStrc*d){if(d)u->dwFlags|=64;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){if(cached){++deliveries;cached=false;}}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 99;}
bool MONSTERS_GetHirelingTypeId(D2UnitStrc*){return false;}bool MONSTERS_IsBoss(void*,D2UnitStrc*){return false;}bool MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc*,int){return false;}
unsigned ITEMS_RollRandomNumber(D2SeedStrc*){return 0;}
void sub_6FD15080(D2UnitStrc*u,int percent){++rewinds;sub_6FCBCFD0(&game,u,percent);}
'''+bodies['SKILLS_SrvSt24_DragonTalon']+'\n'+bodies['SKILLS_SrvDo042_DragonTalon']+r'''
int main(){printf("[");bool comma=false;for(int rank:{1,6,12,18,24})for(int speed:{64,170,256,448}){
 Anim anim;anim.pFrameFlags[4]=1;actor={};actor.wAnimSpeed=speed;actor.dwFrameCountPrecise=13*256;actor.pAnimData=&anim;game.dwGameFrame=0;allocations=deliveries=rewinds=0;cached=false;events.clear();deletes.clear();
 sub_6FCBCE70(&game,&actor);assert(SKILLS_SrvSt24_DragonTalon(&game,&actor,255,rank)==1&&allocations==1&&deliveries==0);
 printf("%s{\"Rank\":%d,\"Speed\":%d,\"Trace\":[",comma?",":"",rank,speed);comma=true;bool sep=false;int end=0;
 for(int frame=1;frame<2000&&!end;frame++){
  game.dwGameFrame=frame;auto scheduled=events;events.erase(std::remove_if(events.begin(),events.end(),[&](auto e){return e[1]==frame;}),events.end());
  for(auto e:scheduled)if(e[1]==frame){if(e[0]==2){end=frame;break;}if(e[0]==1){SKILLS_SrvDo042_DragonTalon(&game,&actor,255,rank);
   printf("%s[%d,%d,%d,%d,%d]",sep?",":"",frame,skill.remaining,allocations,deliveries,rewinds);sep=true;}}
 }assert(end&&allocations==rank/6+1&&deliveries==allocations);printf("],\"End\":%d}",end);
 }printf("]\n");}
'''
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe';run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 run=subprocess.run([str(binary)],capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
 data=json.loads(run.stdout);assert len(data)==20;fixture=STAGE/'cases.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 out=ROOT/'tools/reference/shadow-talon-source-evidence.json';inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=20,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},inputs=inputs,
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log']},
  scope='Four complete unchanged routines composed:ordinary initial/percent-rewind event calendars plus own NPC start24/do42. Current owned kick flags/base/header reused. Observed allocations/drains retain one cached packet,first callback may deliver two,then one per rewound callback;all final recovery ends retain source timing. Packet damage,hit/block,progressive/emission/charge rules are explicit services,not compiled here. Ordinary target/formula/RNG controls supplied;no sequence branch.',
  limits=['Approved per-successful-kick charge policy is a separate runtime adapter;old do42s progressive callback placement is not a modern consume-one proof.','Current NPC input/rate/mutation/event/geometry/PvP/removal boundaries remain unverified. Existing packet/contact proofs are reused;no old individual matrices rerun.'])
 out.write_text(json.dumps(receipt,indent=2)+'\n');print('Own NPC Talon:20 new composed start24/do42/initial/rewind controls,ASan/UBSan clean;packet/modern charge services declared.')
if __name__=='__main__':main()
