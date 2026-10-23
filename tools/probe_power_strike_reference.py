#!/usr/bin/env python3
"""Compile pinned Power Strike scalar, start/commit and Amazon A1 contracts."""
from pathlib import Path
import argparse, hashlib, json, re, struct, subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';REF=ROOT/'tools/reference/d2r-named-equipment'

def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
  blocks[name]=text[m.start():e];return blocks[name]+'\n'
 common=source('source/D2Common/src/D2Skills.cpp');ama=source('source/D2Game/src/SKILLS/SkillAma.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');units=source('source/D2Game/src/UNIT/SUnit.cpp');commonunits=source('source/D2Common/src/Units/Units.cpp')
 catalogue=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text());shapes=[x for x in catalogue if x['sourceFields'].get('EMin',0)>0 or x['sourceFields'].get('EMax',0)>0];shapes.sort(key=lambda x:x['sourceId'])
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
using BOOL=int;
struct D2UnitStrc{} owner;
struct D2SkillsTxt{int dwEMin=0,dwEMax=0,dwEMinLev[5]={},dwEMaxLev[5]={},nToHitShift=8,dwEDmgSymPerCalc=0,nEType=3;} row;
int synergy=0,mastery=0;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &row;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return synergy;}
int SKILLS_CalculateMasteryBonus(D2UnitStrc*,int,int value){return int(int64_t(value)*mastery/100);}
'''
 for name in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage']:cpp+=routine(common,name)
 cpp+='struct Shape{int id;D2SkillsTxt value;};Shape shapes[]={\n'
 for shape in shapes:
  f=shape['sourceFields'];nums=[str(f.get('EMin',0)),str(f.get('EMax',0)),'{'+','.join(str(f.get('EMinLev'+str(i),0)) for i in range(1,6))+'}','{'+','.join(str(f.get('EMaxLev'+str(i),0)) for i in range(1,6))+'}',str(f.get('HitShift',8)),'0','3']
  cpp+='{'+str(shape['sourceId'])+',{'+','.join(nums)+'}},\n'
 cpp+=r'''};
int main(){bool first=true;std::printf("{\"endpoints\":[");for(auto shape:shapes)for(int rank:{0,1,8,9,16,17,22,23,28,29,40})for(int bonus:{0,14,840})for(int master:{0,25,100}){
row=shape.value;synergy=bonus;mastery=master;
int lo=SKILLS_GetMinElemDamage(&owner,shape.id,rank,1),hi=SKILLS_GetMaxElemDamage(&owner,shape.id,rank,1);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",shape.id,rank,bonus,master,lo,hi);
}std::printf("]}\n");}
'''
 p=STAGE/'power-strike-scalars.cpp';p.write_text(cpp);binary=STAGE/'power-strike-scalars';subprocess.run(['g++','-std=c++20','-O2','-fwrapv',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]))
 # Native port uses bounded FP256 values; omit source int32 overflow rows explicitly.
 bounded=[x for x in data['endpoints'] if 0<=x[4]<=2147483647 and 0<=x[5]<=2147483647];assert len(bounded)==len(data['endpoints']),'Source endpoint overflow needs explicit additional bounds'
 for sid,rank,bonus,master,lo,hi in bounded:
  if sid==14 and rank>0:assert lo==256+256*master//100,'Power Strike fixed minimum unexpectedly synergized'
 scalar_path=STAGE/'power-strike-scalar-probe.json';scalar_path.write_text(json.dumps(data,separators=(',',':'))+'\n')
 raw=(ROOT/'.local/research/AnimData.d2').read_bytes();anim_hash=hashlib.sha256(raw).hexdigest();assert anim_hash=='e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb';pos=0;animations=[]
 for bucket in range(256):
  count=struct.unpack_from('<i',raw,pos)[0];pos+=4
  for _ in range(count):
   rowdata=raw[pos:pos+160];pos+=160;code=rowdata[:8].rstrip(b'\0').decode()
   if code in ['AMA11HT','AMA12HT']:
    frames,speed=struct.unpack_from('<ii',rowdata,8);events=[i for i,value in enumerate(rowdata[16:]) if value==1];assert speed==256 and len(events)==1;animations.append(dict(code=code,frames=frames,event=events[0]))
 assert len(animations)==2
 cpp=r'''#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) if(!(x))std::abort()
using BOOL=int;
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7,NUM_WEAPON_CLASSES=14,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=4,PLRMODE_ATTACK2=5,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16,EVENTTYPE_MODECHANGE=8,EVENTTYPE_ENDANIM=9,EVENTTYPE_REMOVESTATE=10,UNITFLAG_SKSRVDOFUNC=64;
struct D2AnimSeqTxt{};
struct Anim{int pFrameFlags[144]={};} anim;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,dwAnimMode=4,dwUnitId=1,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=15*256,nSeqCurrentFramePrecise=0,dwFlags=0;void* pAnimSeq=nullptr;void* pMemoryPool=nullptr;Anim* pAnimData=&anim;} owner,target;
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;} game;
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nEType=3,nSrcDam=128,wSrvOverlay=0,wAuraTargetState=-1,nAuraState=0,dwAuraLenCalc=0;} row;
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0,lightning=0;} saved;
struct D2StatListStrc{} statlist;
struct Tables{int nOverlayTxtRecordCount=100,nStatesTxtRecordCount=256;} tables;auto* sgptDataTables=&tables;
bool record=true,selected=true,hit=true;int allocs=0,drains=0,elements=0,explicitAr=-1,weaponClass=3;
std::vector<std::array<int,4>> scheduled;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){explicitAr=ar;return hit?1:0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 0;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc* p,int,int){elements++;p->lightning=12345;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int src){if(src!=128)std::abort();saved=*p;allocs++;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int n){if(n!=0)std::abort();drains++;}
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return &saved;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return &statlist;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return &statlist;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int arg,int ordinal){scheduled.push_back({type,frame,arg,ordinal});}
void STATES_ToggleState(D2UnitStrc*,int,int){}void STATLIST_SetState(D2StatListStrc*,int){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void sub_6FD10E50(){}void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return &target;}
int ITEMS_GetWeaponClassId(D2UnitStrc*){return weaponClass;}bool UNITS_CanDualWield(D2UnitStrc*){return false;}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){std::abort();}
'''
 for text,name in [(ama,'SKILLS_SrvSt06_PowerStrike_ChargedStrike'),(skills,'SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws'),(commonunits,'UNITS_GetFrameBonus'),(units,'sub_6FCBCE70')]:cpp+=routine(text,name)
 cpp+=r'''int main(){bool first=true;std::printf("{\"flow\":[");for(int present:{0,1})for(int targetPresent:{0,1})for(int success:{0,1}){
 record=present;selected=targetPresent;hit=success;allocs=drains=elements=0;explicitAr=-1;saved={};int start=SKILLS_SrvSt06_PowerStrike_ChargedStrike(&game,&owner,14,1);int commit=SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws(&game,&owner,14,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,targetPresent,success,start,commit,allocs,drains,elements,explicitAr,saved.lightning);
}std::printf("],\"timing\":[");first=true;
'''
 for animation in animations:
  code=animation['code'];family=1 if code.endswith('1HT') else 2;weapon_class=3 if family==1 else 6
  cpp+=f'''for(int advance:{{38,128,180,256,320,448}}){{weaponClass={weapon_class};owner.wAnimSpeed=advance;owner.dwFrameCountPrecise={animation['frames']}*256;anim={{}};anim.pFrameFlags[{animation['event']}]=1;scheduled.clear();sub_6FCBCE70(&game,&owner);int impact=-1,end=-1;for(auto e:scheduled){{if(e[0]==8)impact=e[1]-100;if(e[0]==9)end=e[1]-100;}}if(!first)std::printf(",");first=false;std::printf("[{family},%d,%d,%d,%d]",advance,UNITS_GetFrameBonus(&owner),impact,end);}}\n'''
 cpp+='std::printf("]}\\n");}\n'
 p=STAGE/'power-strike-flow.cpp';p.write_text(cpp);binary=STAGE/'power-strike-flow';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);flow=json.loads(subprocess.check_output([str(binary)]))
 for present,target_present,hit,start,commit,allocs,drains,elements,ar,lightning in flow['flow']:
  valid=bool(present and target_present);assert (start,commit,allocs,drains,elements,ar,lightning)==(int(valid),int(valid),int(valid),int(valid),int(valid and hit),0 if valid else -1,12345 if valid and hit else 0)
 for family,advance,bonus,event,end in flow['timing']:
  shape=next(x for x in animations if x['code'].endswith('1HT' if family==1 else '2HT'));assert bonus==2;assert (event,end)==(((shape['event']-2)*256+advance-1)//advance,((shape['frames']-2)*256+advance-1)//advance)
 (STAGE/'power-strike-flow-probe.json').write_text(json.dumps(flow,separators=(',',':'))+'\n')
 manifest=json.loads((REF/'sources.json').read_text());modern=(REF/'json/skills.json').read_bytes();assert hashlib.sha256(modern).hexdigest()==manifest['files']['json/skills.json']['sha256'];current=next(x for x in json.loads(modern).values() if x['skill']=='Power Strike');assert current['ToHit']==20 and current['LevToHit']==12 and current['Param8']==14
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},modernTableHash=hashlib.sha256(modern).hexdigest(),probe=dict(scalarShapes=len(shapes),scalarRows=len(data['endpoints']),flowRows=len(flow['flow']),timingRows=len(flow['timing']),scalarPath='.local/class-skills/power-strike-scalar-probe.json',flowPath='.local/class-skills/power-strike-flow-probe.json'),animations=animations,legacyAnimDataHash=anim_hash,modernCorrections='tools/reference/power-strike-modern-correction.json',established=[
 'Compiled shared elemental minimum helper skips synergy if raw minimum<=256 and first tier increment0; maximum always evaluates synergy. Rank0 returns0. Mastery adds a separately truncated percentage after synergy. Power Strike minimum remains1 before mastery even with hard synergy points.',
 'Compiled legacy start6 allocates a prepared full weapon plus success-only lightning packet; do2 performs the later ordinary drain/event commit. Pinned Power Strike has no physical calc1 bonus, conversion, aura state or server overlay.',
 'Legacy start6 explicit AR is0. Official released2.6 patch fixes the Power Strike AR bug; native uses the current20+12/rank bonus deliberately, not that obsolete0. This modern correction is separate from the unchanged legacy-flow fixture.',
 'Compiled source Amazon A1 frame bonus2 and ordinary scheduler agree at six declared speeds. Cached legacy AnimData gives1HT15/event9 and2HT18/event11; after bonus, base timing13/event7 or16/event9. Full current-build animation comparison remains open.'
 ],limits=[
 'Scalar services supply declared synergy/mastery percentages; native tests separately validate actual hard-only three-skill14% synergy, real gear and item/weapon event consumers. No complete modern executable/stat/seed certificate.',
 'Cached legacy AnimData SHA is recorded, but acquisition provenance remains incomplete; timing is a source-supported provisional adapter until current3.3 data/client acceptance.',
 'Source attack result/allocation/durability/elemental services are declared for control-flow isolation. Full RNG, avoided/blocked/PvP/special owner, proc/charged/foreign-class and target lifecycle need native/current-executable acceptance.',
 'No class completion certificate, item-skill gate or asset generation is established by this probe.'
 ])
 encoded=json.dumps(evidence,indent=2)+'\n';out=ROOT/'tools/reference/power-strike-engine-evidence.json'
 if check:assert out.read_text()==encoded,'Power Strike source evidence stale'
 else:out.write_text(encoded)
 print('Power Strike:',len(data['endpoints']),'shared elemental rows,',len(flow['flow']),'start/commit rows and',len(flow['timing']),'A1 schedules compiled.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
