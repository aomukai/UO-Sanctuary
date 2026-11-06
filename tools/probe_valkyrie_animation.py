#!/usr/bin/env python3
"""Bind current Valkyrie transformation/animation data to complete older rate bodies."""
import hashlib,json,re,struct,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-animation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 def load(p):return json.loads(read(p))
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);return read(p)
 ref=ROOT/'tools/reference/d2r-named-equipment';manifest=load(ref/'sources.json')
 def table(name):
  p=ref/'json'/f'{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];return load(p)
 monsters=table('monstats');states=table('states');chars=table('charstats');weapons=table('weapons');skills=table('skills')
 assert monsters['valkyrie']['Code']=='VK'and monsters['valkyrie']['AI']=='NecroPet'
 assert {k:states['valkyrie'][k]for k in ['*ID','transform','gfxtype','gfxclass']}=={'*ID':93,'transform':1,'gfxtype':2,'gfxclass':0}
 skill=next(r for r in skills.values()if r.get('skill')=='Valkyrie');assert skill['srvdofunc']==16
 parent=load(ROOT/'tools/reference/d2r-local-animation-evidence.json');rawpath=ROOT/parent['localRawPath'];assert sha(rawpath)==parent['sha256'];inputs[str(rawpath.relative_to(ROOT))]=sha(rawpath);raw=rawpath.read_bytes();cursor=total=0;rows=[]
 for _ in range(256):
  count=struct.unpack_from('<I',raw,cursor)[0];cursor+=4
  for _ in range(count):
   r=raw[cursor:cursor+160];assert len(r)==160;cursor+=160;total+=1;code=r[:8].split(b'\0')[0].decode('ascii')
   if code.startswith('VK')or code in ['AMA12HT','AMWL2HT','AMRN2HT','AMNU2HT']:
    frames,speed=struct.unpack_from('<II',r,8);rows.append(dict(Code=code,Frames=frames,Speed=speed,Events=[[i,v]for i,v in enumerate(r[16:])if v]))
 assert cursor==len(raw)and total==3661 and len(rows)==11
 bycode={r['Code']:r for r in rows};assert bycode['VKA1HTH']['Events']==[]and bycode['VKA1HTH']['Frames']==2
 assert bycode['AMA12HT']==dict(Code='AMA12HT',Frames=18,Speed=256,Events=[[11,1]])
 current=dict(ReferenceVersion='3.3.93847',State=93,GraphicsType=2,GraphicsClass=0,WalkVelocity=chars['Amazon']['WalkVelocity'],RunVelocity=chars['Amazon']['RunVelocity'],Rows=[bycode[c]for c in ['AMA12HT','AMWL2HT','AMRN2HT','AMNU2HT']])
 assert current['WalkVelocity']==6 and current['RunVelocity']==9
 data=ROOT/'data/Sanctuary/Balance/valkyrie-animation.json';data.write_text(json.dumps(current,indent=2)+'\n')
 units=source('source/D2Common/src/Units/Units.cpp');dskills=source('source/D2Common/src/D2Skills.cpp');sunit=source('source/D2Game/src/UNIT/SUnit.cpp');ama=source('source/D2Game/src/SKILLS/SkillAma.cpp')
 summon=function(ama,'SKILLS_SrvDo016_Valkyrie');assert 'STATES_ToggleState(pPet, STATE_VALKYRIE, 1)'in summon
 convert=re.search(r'const int dword_6FDD2B98\[\]\s*=\s*\{.*?\};',dskills,re.S).group();assert [int(v)for v in re.findall(r'\d+',convert.split('{')[1])]==[0,1,2,4,7,8,9,10,13,14,15,16,17,19,18,3]
 def enum(rel,name):return re.search(r'enum '+name+r'\s*\{.*?\};',source(rel),re.S).group()
 bodies={n:function(s,n)for s,names in [(dskills,['D2COMMON_11013_ConvertMode']),(units,['UNITS_GetFrameBonus','UNITS_GetAnimStatBasePercentage','UNITS_GetBaseAnimSpeed','UNITS_GetBaseVelocity','UNITS_UpdateRunWalkAnimRateAndVelocity','UNITS_SetAnimationSpeed','UNITS_UpdateAttackAnimRateAndVelocity']),(sunit,['sub_6FCBCE70'])]for n in names}
 cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <limits>
#include <vector>
#define __stdcall
#define __fastcall
#define __forceinline inline
#define D2_ASSERT assert
using BOOL=int;
constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6,NUMBER_OF_PLAYERCLASSES=7;
constexpr int MONMODE_RUN=15,UNITFLAGEX_ISSHAPESHIFTED=1,STAT_ITEM_FASTERATTACKRATE=93,STAT_ATTACKRATE=68,STAT_VELOCITYPERCENT=67,STAT_ITEM_FASTERMOVEVELOCITY=96,BODYLOC_RARM=4,BODYLOC_LARM=5,ITEMTYPE_WEAPON=45,STATEMASK_MELEEONLY=0,EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;
struct Anim{int dwAnimSpeed=256;int pFrameFlags[144]={};}anim;
struct D2DynamicPathStrc{int velocity=0;};struct D2AnimSeqTxt{};
struct D2UnitStrc{int dwUnitType=1,dwClassId=357,dwAnimMode=4,dwFlagEx=1,wAnimSpeed=0,dwSeqSpeed=0,dwSeqFrameCount=0,dwFrameCountPrecise=18*256,nSeqCurrentFramePrecise=0;void*pInventory=nullptr;void*pStatListEx=this;D2DynamicPathStrc*pDynamicPath=nullptr;Anim*pAnimData=&anim;D2AnimSeqTxt*pAnimSeq=nullptr;};
struct D2GameStrc{int dwGameFrame=0;};struct D2StatesTxt{int nGfxType=2,wGfxClass=0;}state;
int transformStates[]={93};struct Tables{int nTransformStates=1,*pTransformStates=transformStates,nStatesTxtRecordCount=94;D2StatesTxt*pStatesTxt=nullptr;}tables;Tables*sgptDataTables=&tables;
bool STATES_CheckState(D2UnitStrc*,int id){assert(id==93);return true;}
int D2Common_11014_ConvertShapeShiftedMode(int,int){assert(false);return 0;}
int ias=0,attack=100,move=100,fasterMove=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int id,int){assert(id==93||id==96);return id==93?ias:fasterMove;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int id,int){assert(id==67||id==68);return id==67?move:attack;}
struct D2MonStatsTxt{int nRunAnimSpeed=256,nWalkAnimSpeed=256,nVelocity=11;};
const D2MonStatsTxt*DATATBLS_GetMonStatsTxtRecord(int){assert(false);return nullptr;}
struct D2CharStatsTxt{int nWalkSpeed=6;};D2CharStatsTxt character;
D2CharStatsTxt*UNITS_GetCharStatsTxtRecord(int cls){assert(cls==0);return &character;}
void FOG_Trace(const char*,...){assert(false);}
void UNITS_SetAnimationSpeed(D2UnitStrc*,int);
void PATH_SetVelocity(D2DynamicPathStrc*p,int velocity,const char*,int){p->velocity=velocity;}
void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int one){assert(one==1);}
bool UNITS_CanDualWield(D2UnitStrc*){return false;}
D2UnitStrc*INVENTORY_GetItemFromBodyLoc(void*,int){assert(false);return nullptr;}
bool ITEMS_CanBeEquipped(D2UnitStrc*){assert(false);return false;}bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){assert(false);return false;}
bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){return false;}int D2Common_11043(D2UnitStrc*){assert(false);return 0;}
template<class T>T D2Clamp(T x,T a,T b){return std::clamp(x,a,b);}
D2UnitStrc weapon;
D2UnitStrc*D2Common_10434(D2UnitStrc*,int one){assert(one==1);return &weapon;}
int ITEMS_GetWeaponClassId(D2UnitStrc*);
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}
struct Event{int type,frame,kind,param;};std::vector<Event>events;
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int param){events.push_back({type,frame,kind,param});}
'''+enum('source/D2Common/include/D2Composit.h','D2WeaponClasses')+'\n'+enum('source/D2Common/include/Units/Player.h','D2C_PlayerModes')+'\n'+convert+r'''
int ITEMS_GetWeaponClassId(D2UnitStrc*){return WEAPONCLASS_2HT;}
enum D2C_UnitAnimModeStats{ANIMMODESTAT_MOVEVELOCITY};
struct Percentage{bool bHasBasePercentage;int nBasePercentage,nStatId;};Percentage gaAnimModeStatBasePercentage[]={{true,150,96}};
'''
 # Conversion indexes its full state table by ID93.
 cpp+='\n'.join(bodies.values())+r'''
int main(){D2StatesTxt states[94];tables.pStatesTxt=states;int count;assert(scanf("%d",&count)==1);printf("[");
for(int i=0;i<count;i++){int mode;assert(scanf("%d%d%d%d%d",&mode,&ias,&attack,&move,&fasterMove)==5);D2UnitStrc actor;actor.dwAnimMode=mode;D2DynamicPathStrc path;actor.pDynamicPath=&path;int kind=1,cls=357,converted=mode;D2COMMON_11013_ConvertMode(&actor,&kind,&cls,&converted,__FILE__,__LINE__);events.clear();D2GameStrc game;anim={};anim.pFrameFlags[11]=1;int bonus=UNITS_GetFrameBonus(&actor);
if(mode==4){UNITS_UpdateAttackAnimRateAndVelocity(&actor);sub_6FCBCE70(&game,&actor);}else{UNITS_UpdateRunWalkAnimRateAndVelocity(&actor,converted,kind,cls,__FILE__,__LINE__);}
printf("%s[%d,%d,%d,%d,%d,%d,[",i?",":"",kind,cls,converted,bonus,actor.wAnimSpeed,path.velocity);bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame,e.kind,e.param);sep=true;}printf("]]");}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 original=load(BASE/'valkyrie-numeric-source/cases.json');reader=load(BASE/'valkyrie-resources-source/cases.json');cases=[];numbers=[]
 for i,c in enumerate(original):
  fields=dict(next(r for r in reader if r['Case']==i and r['Variant']==0)['Stages'][0][2]);stats=[fields.get(k<<16,0)for k in [93,68,67,96]]
  weapon=next(item for item in c['Items']if item['Equipment']['Body']==4)['Metadata']['Code'];assert weapons[weapon]['wclass']=='2ht'and weapons[weapon]['2handedwclass']=='2ht'
  for mode in [2,4,15]:
   for baseAttack,baseMove in [(100,100),(100,150),(50,50),(175,200),(0,0),(-200,-200),(300,300),(125,150)]:
    row=dict(Case=i,Rank=c['Rank'],Difficulty=c['Difficulty'],Weapon=weapon,Mode=mode,BaseAttack=baseAttack,BaseMove=baseMove,Stats=stats);cases.append(row)
    numbers.extend([mode,stats[0],baseAttack+stats[1],baseMove+stats[2],stats[3]])
 request=STAGE/'input.txt';request.write_text(' '.join(map(str,[len(cases)]+numbers))+'\n');path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=STAGE/'output.json';output.write_bytes(run.stdout);values=json.loads(run.stdout);assert len(values)==len(cases)==1368
 for c,value in zip(cases,values):c['Expected']=value
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 proof=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),originalLoadouts=len(original),parsedAnimationRecords=total,records=rows,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()},summonRoutineHash=hashlib.sha256(summon.encode()).hexdigest(),inputs=inputs,
  established=['Pinned state93 transforms Valkyrie to player graphics/class Amazon0. Full older converter retains actual unit kind1 while converting animation lookup;monsterA1mode4 becomes playerA1mode7.','All57original loadouts hold two-handed 2HTspears. AMA12HT has18frames/marker1at11/speed256. Full older frame-bonus getter returns2,not0. Bare VKA1HTH has2frames and no marker;it is not used as an attack clock.','Full older run/walk rate path uses converted player bases:Amazon walk velocity6,walk animation213/run101. The supplied velocity-percent input must already include run-state and any external stat lists;no initializer or live run-state ownership is inferred.','Full original graph values from the already-qualified independent source reader feed IAS93/attackrate68/velocity67/fastermove96. All1368contexts compare conversion,frame bonus,attack/walk/run speed,FP8velocity and complete initial attack event plan.'],
  services=['Original weapon identity/type and current tables are validated;composite class is2HT. Source inventory merge,non-dual-wield,no melee-only state,valid transformed state93,charstats and event queue insertion are explicit services.','The complete older routines are supplementary evidence. Current executable transformation/rate/scheduler parity,initializer bases,external aura/stat-list collection,world controller/contact and learned cast remain separate. No guessed source rate bases are persisted.'],
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,request,output,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log',data,Path(__file__).resolve()]})
 (ROOT/'tools/reference/valkyrie-animation-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n');print('Valkyrie animation:1368original-loadout contexts,nine complete source bodies;transformed Amazon2HT/frame bonus2;ASan/UBSan clean.')
if __name__=='__main__':main()
