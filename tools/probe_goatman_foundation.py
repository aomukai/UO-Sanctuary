#!/usr/bin/env python3
"""Run exact Goatman Fog expression and shared creation/base-stat bodies."""
import argparse,csv,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'goatman-foundation'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};routines={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 pin(BASE/'d2moo-source-tree.json')
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);return pin(p)
 fog=source('source/Fog/src/Calc.cpp');header=source('source/Fog/include/Calc.h');source('source/D2Common/src/DataTbls/SkillsTbls.cpp');source('source/D2Common/include/Units/Units.h')
 damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');damagebody=function(damage,'SUNITDMG_ApplyDamageBonuses')
 nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');basebody=function(nec,'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10');dobody=function(nec,'SKILLS_SrvDo056_Golem')
 for n,b in [('D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10',basebody),('SKILLS_SrvDo056_Golem',dobody)]:routines[n]=hashlib.sha256(b.encode()).hexdigest()
 directory=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads(pin(directory/'sources.json'));tables={}
 for name in ['skills','skilldesc','monstats','monstats2','monlvl','pettype','itemstatcost']:
  p=directory/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(pin(p))
 goat=tables['skills']['373'];master=tables['skills']['374'];oath=tables['skills']['378'];monster=tables['monstats']['wargoatman'];model=tables['monstats2']['wargoatman'];pet=tables['pettype']['demon'];desc=next(r for r in tables['skilldesc'].values() if r.get('skilldesc')=='summon goatman')
 assert monster['AI']=='GenericPet' and monster['noRatio']==1 and goat['srvdofunc']==56 and goat['passivecalc3']=="(ln56 + (skill('Demonic Mastery'.ln21))"
 owned=ROOT/'.local/research/d2r-3.3.93847/skills.txt';pin(owned)
 with owned.open(encoding='utf-8-sig',newline='') as f:row=next(r for r in csv.DictReader(f,delimiter='\t') if r.get('skill')=='Summon Goatman')
 assert row['passivecalc3']==goat['passivecalc3']
 curve=BASE/'warlock-foundation/skillcalc.txt';pin(curve)
 with curve.open(newline='') as f:curves=list(csv.DictReader(f,delimiter='\t'))
 ln56=next(i for i,r in enumerate(curves) if r['code']=='ln56');ln21=next(i for i,r in enumerate(curves) if r['code']=='ln21')
 (STAGE/'Calc.cpp').write_text(fog);(STAGE/'Calc.h').write_text(header)
 (STAGE/'D2BasicTypes.h').write_text('''#pragma once
#include <cstdint>
#include <cstring>
#include <cstdlib>
#include <cctype>
#include <cmath>
#include <cassert>
#define __fastcall
#define __stdcall
#define FOG_DLL_DECL
#define D2_ASSERT assert
#define D2_UNREACHABLE __builtin_unreachable()
using BOOL=int;
#define TRUE 1
#define FALSE 0
''');(STAGE/'Fog.h').write_text('#pragma once\n#include "D2BasicTypes.h"\n')
 cpp=r'''#include "Calc.h"
#include <cstdio>
int rank=0,mastery=0,calls=0;
int keyword(char* name){assert(!strcmp(name,"skill"));return 3;}
int parameters(int key){assert(key==3);return 2;}
int link(char* name,BOOL* constant,int ast,int key){
 if(!strcmp(name,"Demonic Mastery")){assert(ast==1&&key==3);*constant=1;return 374;}
 *constant=0;if(!strcmp(name,"ln56"))return LN56;if(!strcmp(name,"ln21"))return LN21;assert(false);return -1;
}
int parent(int index,void*){assert(index==LN56);return 10*rank;}
int skill(int id,int property,int extra,void*){assert(id==374&&property==LN21&&extra==0);calls++;return mastery>0?10*mastery:0;}
int main(){D2CalcCallbackInfoStrc table[4]={};table[3]={skill,2};bool first=true;printf("[");
'''.replace('LN56',str(ln56)).replace('LN21',str(ln21))
 for variant,expression in enumerate([goat['passivecalc3'],goat['passivecalc3']+')']):
  cpp+=f'''for(rank=1;rank<=255;rank++)for(int m:{{0,1,7,17,20,21,255}}){{mastery=m;calls=0;FOGASTNodeStrc buffer[1024]={{}};int size=DATATBLS_CompileExpression({json.dumps(expression)},buffer,1024,keyword,parameters,link);int result=size?DATATBLS_CalcEvaluateExpression(buffer,size,parent,table,4,nullptr):0;printf("%s[{variant},%d,%d,%d,%d,%d]",first?"":",",rank,m,size,result,calls);first=false;}}\n'''
 cpp+='printf("]\\n");}\n';cpp=cpp.replace('#include <cstdio>','#include <cstdio>\n#include <initializer_list>')
 p=STAGE/'formula.cpp';p.write_text(cpp)
 def run(p):
  log=p.with_suffix('.compiler.log');san=p.with_suffix('.sanitizer.log')
  with log.open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',*(['-fno-sanitize=alignment'] if p.stem=='formula' else []),'-I'+str(STAGE),str(p),*([str(STAGE/'Calc.cpp')] if p.stem=='formula' else []),'-o',str(p.with_suffix(''))],stdout=f,stderr=subprocess.STDOUT,check=True)
  r=subprocess.run([str(p.with_suffix(''))],capture_output=True);san.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-2000:];return json.loads(r.stdout),[p,log,san]
 formula,artifacts=run(p);assert len(formula)==3570
 for variant,rank,mastery,size,result,calls in formula:assert size>0 and result==10*(rank+mastery) and calls==1,(variant,rank,mastery,size,result,calls)
 formulaout=BASE/'goatman-formula-source-probe.json';formulaout.write_text(json.dumps(formula,separators=(',',':'))+'\n');artifacts += [formulaout,STAGE/'Calc.cpp',STAGE/'Calc.h',STAGE/'D2BasicTypes.h',STAGE/'Fog.h']
 # Complete unchanged shared base routine and do56; allocation/passive/resist/AI
 # services are deliberately declared. This does not compile GenericPet.
 cpp=r'''#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <string>
using std::int32_t;
#define __fastcall
constexpr int UNITFLAG_SKSRVDOFUNC=0x40,AISPECIALSTATE_NONE=0,STAT_LEVEL=1,STAT_ARMORCLASS=2,STAT_TOHIT=3;
struct D2UnitStrc{int dwFlags=0,dwNodeIndex=7,level=0,armor=0,attack=0;};
struct D2SkillsTxt{int wSummon=742,nPetType=19;uint8_t nSumMode=12;int dwPetMax=1;};
struct D2MonLvlTxt{int dwLAC[3],dwAC[3],dwLTH[3],dwTH[3];};
struct Tables{int nMonStatsTxtRecordCount=751,nPetTypeTxtRecordCount=20,nMonLvlTxtRecordCount=111;D2MonLvlTxt* pMonLvlTxt;};
struct D2GameStrc{uint8_t nDifficulty=0;int nGameType=1,dwGameType=0;};
struct D2SummonArgStrc{int dwFlags;D2UnitStrc* pOwner;int nAiSpecialState,nHcIdx,nPetType,nMonMode,nPetMax;};
D2MonLvlTxt levels[111]={};Tables data;Tables* sgptDataTables=&data;D2SkillsTxt rec;D2UnitStrc pet;D2SummonArgStrc captured;bool valid=true,spawn=true;int hardMastery=0,ownerLevel=99;std::string trace;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return valid?&rec:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return hardMastery>=10?3:hardMastery>=5?2:1;}
D2UnitStrc* D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc* a){trace+="S";captured=*a;return spawn?&pet:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* u,int stat,int){assert(stat==STAT_LEVEL);return u?u->level:ownerLevel;}
void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){assert(stat==STAT_LEVEL);u->level=value;trace+="L";}
void STATLIST_AddUnitStat(D2UnitStrc* u,int stat,int value,int){if(stat==STAT_ARMORCLASS){u->armor+=value;trace+="A";}else{assert(stat==STAT_TOHIT);u->attack+=value;trace+="T";}}
void D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int){trace+="P";}
void D2GAME_SetSummonResistance_6FD0C2E0(D2UnitStrc*,D2UnitStrc*){trace+="R";}
int SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return 8;}
void sub_6FC3E200(int,D2UnitStrc*){trace+="C";}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*,int,int){trace+="I";}
'''
 cpp+=basebody+'\n'+dobody+'\nint main(){data.pMonLvlTxt=levels;\n'
 for k,row in tables['monlvl'].items():
  i=int(k);assert 0<=i<111
  for field,target in [('L-AC','dwLAC'),('AC','dwAC'),('L-TH','dwLTH'),('TH','dwTH')]:
   for diff,suffix in enumerate(['','(N)','(H)']):cpp+=f'levels[{i}].{target}[{diff}]={row[field+suffix]};\n'
 cpp+=r'''bool first=true;printf("{\"base\":[");D2GameStrc game;D2UnitStrc owner;
for(int level:{1,10,47,50,99})for(int rank=1;rank<=255;rank++)for(int diff=0;diff<3;diff++)for(int expansion:{0,1}){
 owner.level=level;game.nDifficulty=diff;game.nGameType=expansion;pet={};int ok=D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,rank,diff,expansion,ok,pet.level,pet.armor,pet.attack);first=false;
}
printf("],\"cast\":[");first=true;game={};owner.level=99;
for(int present:{1})for(int record:{0,1})for(int allocated:{0,1})for(int id:{-1,742,751})for(int type:{0,19,20,255})for(int mode:{0,8,16,255})for(int rank:{1,20,255})for(int mastery:{0,4,5,9,10,20}){
 valid=record;spawn=allocated;rec.wSummon=id;rec.nPetType=type;rec.nSumMode=mode;hardMastery=mastery;owner.dwFlags=0x100;pet={};trace.clear();captured={};
 int ok=SKILLS_SrvDo056_Golem(&game,present?&owner:nullptr,373,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,\"%s\"]",first?"":",",present,record,allocated,id,type,mode,rank,mastery,ok,present?owner.dwFlags:0,captured.nHcIdx,captured.nPetType,captured.nMonMode,captured.nPetMax,captured.pOwner==&owner,trace.c_str());first=false;
}printf("]}\n");}'''
 p=STAGE/'creation.cpp';p.write_text(cpp);creation,extra=run(p);artifacts+=extra
 assert len(creation['base'])==7650 and len(creation['cast'])==3456
 for level,rank,diff,expansion,ok,petlevel,armor,attack in creation['base']:
  expected=min(level,rank+3*level//4);row=tables['monlvl'][str(expected)];suffix=['','(N)','(H)'][diff]
  assert (ok,petlevel,armor,attack)==(1,expected,row[('L-' if expansion else '')+'AC'+suffix],row[('L-' if expansion else '')+'TH'+suffix])
 for present,valid,allocated,id,type,mode,rank,mastery,ok,flags,summon,pettype,summode,cap,owner,trace in creation['cast']:
  accepted=valid and 0<=id<751;assert ok==int(bool(accepted and allocated)) and flags==(0x140 if present else 0)
  assert trace==('SLATPRCI' if accepted and allocated else 'S' if accepted else '')
  if accepted:assert (summon,pettype,summode,cap,owner)==(id,type if type<20 else 0,mode if mode<16 else 1,3 if mastery>=10 else 2 if mastery>=5 else 1,present)
 out=BASE/'goatman-creation-source-probe.json';out.write_text(json.dumps(creation,separators=(',',':'))+'\n');artifacts.append(out)
 observation=ROOT/'tools/reference/goatman-tooltip-observation.json';observed=json.loads(pin(observation));assert observed['tooltip']['life']==752 and observed['ownerLevel']==47
 data=[]
 for difficulty,suffix,hpfield in [('Normal','','minHP'),('Nightmare','(N)','MinHP(N)'),('Hell','(H)','MinHP(H)')]:
  data.append(dict(Difficulty=difficulty,Life=monster[hpfield],MinimumDamage=monster['A1MinD'+suffix],MaximumDamage=monster['A1MaxD'+suffix],Armor=monster['AC'+suffix],Attack=monster['A1TH'+suffix]))
 target=ROOT/'data/Sanctuary/Balance/goatman-base.json';body=json.dumps(data,indent=2)+'\n'
 if check:assert target.read_text()==body
 else:target.write_text(body)
 artifacts += [target,Path(__file__).resolve(),*sorted((STAGE/'alignment-diagnostic').iterdir()),*sorted((STAGE/'null-owner-diagnostic').iterdir())]
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,currentExecutableParity=False,formulaCases=3570,baseStatCases=7650,creationCases=3456,reviewedPhysicalRoutine=dict(name='SUNITDMG_ApplyDamageBonuses',sha256=hashlib.sha256(damagebody.encode()).hexdigest(),executedHere=False,scope='No-item/no-strength ordinary NPC endpoints add item_normaldamage before damagepercent;source review only,not execution of the whole combat routine.'),sanitizers='ASan/UBSan;alignment is excluded only for unchanged Fog packed bytecode casts. The initial alignment diagnostic is retained,not a mechanical formula failure.',formulaSymbolServices='Current ln56/ln21 linker rows,skill keyword3and skill373/374IDs;parent and nested curve values are declared context services. Complete Fog compiler/evaluator is unchanged. Full current symbol/stat acquisition is not executed.',inputs=inputs,routines=routines,skill=goat,mastery=master,oath=oath,monster=monster,model=model,pettype=pet,description=desc,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
  established=['Exact unclosed Goatman passivecalc3 and separately balanced control execute3570complete Fog compiler/evaluator contexts with one nested skill callback and additive10*(Goatman rank+Demonic Mastery rank). This supports a narrowly matched outer-group adapter,not general auto-closing. Current user rank4/mastery17damage independently corroborates210percent ED at one observed point.',
   'Complete unchanged do56Golem executes3456guard/allocation/pettype/mode/cap/owner contexts with declared allocator/passive/resist/client/AI services. Source call order is allocation,base stats,passives,resistance,client notification,AI setup. Complete base routine executes7650rank/owner-level/difficulty/expansion contexts using pinned monlvl values;pet level min(owner,rank+3*owner/4)and additive level AR/AC are established for the admitted source context.',
   'User Nightmare level47with Goatman effective4/hard1,Mastery17/16and BloodOath6/5reports752life,114..176damage,34speed,585displayAR,160defensebonus and16.5mana. The exact current tooltip AR expression differs by100from passivecalc2;do not substitute the display for the server stat.'],
  remaining=['GenericPet is a new AI with no matching older body. Goatman Stun/Berserk/Frenzy/Cleave unlock at effective2/3/4/5;new start67Frenzy and start68/do164Cleave remain separate. No learned cast,world pet or AI parity is claimed.',
   'Initial null-owner fixture reached an unsafe legacy node-index dereference because its allocator stub admitted an invalid caller. It is retained as a diagnostic;final do56contexts require a nonnull owner and do not claim a null-owner guard. Older passive writer has5slots;current Goatman uses12and repeats physical-sunder inheritance in5and7. Modern aggregation/event/helper behavior,passive BloodOath/resistance sharing,regen/base creation/randomization and complete mode/target/owner/persistence/current-client acceptance remain unverified. Complete do56does not certify declared services.',
   'The primary site marks Goatman descriptions as AI-interpreted and potentially erroneous;the owned tables and user observations are authoritative for this preparation. Missing effects/audio and talent/hotkey icons remain separate.'])
 p=ROOT/'tools/reference/goatman-source-evidence.json';body=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==body
 else:p.write_text(body)
 print('Goatman:3570complete Fog formula,7650base-stat and3456shared do56contexts;new AI/passive extension/learned summon remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
