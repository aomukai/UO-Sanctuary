#!/usr/bin/env python3
"""Full NPC blind callback/direct helper and shared curse factory under declared services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
from probe_npc_damage_fill import enum
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-blind-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);parent=BASE/'necromancer-ai-curse-source/probe.cpp';old=json.loads((ROOT/'tools/reference/necromancer-ai-curse-source-evidence.json').read_text());assert sha(parent)==old['artifacts'][str(parent.relative_to(ROOT))]
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 def read(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 nec=read('source/D2Game/src/SKILLS/SkillNec.cpp');item=read('source/D2Game/src/SKILLS/SkillItem.cpp');skills=read('source/D2Game/src/SKILLS/Skills.cpp');unit=read('source/D2Game/src/UNIT/SUnit.cpp')
 for text,names in [(nec,['AiSpecialStateFromAuraState','sub_6FD0B2B0','sub_6FD0B450','sub_6FD0B9F0']),(skills,['sub_6FD10EC0']),(unit,['sub_6FCBD900']),(item,['SKILLITEM_EventFunc09_Stupidity'])]:
  for name in names:bodies[name]=function(text,name)
 specs=[('source/D2Common/include/D2StatList.h','D2C_ItemStats'),('source/D2Common/include/D2States.h','D2C_States'),('source/D2Common/include/Units/Units.h','D2C_UnitTypes'),('source/D2Common/include/Units/Units.h','D2C_UnitFlags'),('source/D2Common/include/Units/Units.h','D2C_UnitAlignment'),('source/D2Common/include/Units/Player.h','D2C_PlayerModes'),('source/D2Common/include/D2Monsters.h','D2C_MonModes'),('source/D2Game/include/AI/AiStates.h','D2C_AiSpecialState : uint32_t'),('source/D2Game/include/GAME/Event.h','D2C_EventTypes : uint8_t'),('source/D2Common/include/DataTbls/MonsterTbls.h','D2C_MonStatsTxtFlagIndices'),('source/D2Common/include/DataTbls/MonsterTbls.h','D2C_MonStats2TxtFlagIndices'),('source/D2Common/include/DataTbls/ItemsTbls.h','D2C_ItemStatCostTxtFlagIndices'),('source/D2Common/include/DataTbls/SkillsIds.h','D2C_Skills')]
 values={}
 for path,name in specs:values.update(enum(read(path),name))
 table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';data=ROOT/'data/Sanctuary/Balance/class-skills.json'
 row=json.loads(table.read_text())['71'];native=next(x for x in json.loads(data.read_text())if x['sourceId']==71)
 assert row['auralencalc']==native['sourceFields']['auralencalc']=='ln34' and row['auratargetstate']==native['sourceFields']['auratargetstate']=='dimvision'
 assert [row['Param3'],row['Param4']]==[native['sourceParameters']['3'],native['sourceParameters']['4']]==[175,50]
 assert not any(row.get('aurastat'+str(i))or native['sourceFields'].get('aurastat'+str(i))for i in range(1,7))
 for p in (table,data):inputs[str(p.relative_to(ROOT))]=sha(p)
 cpp=parent.read_text().split('int main(){')[0];bound={}
 # Rebind every declared integer constant, including inherited unused helpers.
 # Parent proof used synthetic ordinals; do not share those ABI assumptions.
 def bind(m):
  name=m[1];assert name in values,name;bound[name]=values[name];return name+'='+str(values[name])
 cpp=re.sub(r'\b([A-Z][A-Z0-9_]+)=(-?\d+)\b',bind,cpp)
 cpp=cpp.replace('constexpr uint32_t gdwBitMasks[]={1,2,4,8};','constexpr uint32_t gdwBitMasks[]={'+','.join(str(1<<i)+'u'for i in range(32))+'};')
 cpp=cpp.replace('nItemStatCostTxtRecordCount=64','nItemStatCostTxtRecordCount=512')
 cpp+='\nstruct D2DamageStrc{};constexpr int STAT_LEVEL='+str(values['STAT_LEVEL'])+',STAT_ITEM_STUPIDITY='+str(values['STAT_ITEM_STUPIDITY'])+',SKILL_DIMVISION='+str(values['SKILL_DIMVISION'])+';\n'
 bound.update({n:values[n]for n in ('STAT_LEVEL','STAT_ITEM_STUPIDITY','SKILL_DIMVISION')})
 cpp+='\nconstexpr int UNIT_ALIGNMENT_EVIL='+str(values['UNIT_ALIGNMENT_EVIL'])+';\n'
 bound['UNIT_ALIGNMENT_EVIL']=values['UNIT_ALIGNMENT_EVIL']
 cpp+='uint32_t raw=0;int draws=0;uint32_t ITEMS_RollRandomNumber(int*){draws++;return raw;}\n'+bodies['sub_6FD0B9F0']+'\n'+bodies['SKILLITEM_EventFunc09_Stupidity']
 main=r'''
D2UnitStrc actor,target;D2StatListStrc prior;
void reset(){actor={};target={};prior={};actor.dwUnitId=7;target.dwUnitId=42;actor.dwUnitType=target.dwUnitType=UNIT_MONSTER;target.alignment=UNIT_ALIGNMENT_EVIL;actor.room=target.room=&room;target.dwFlags=UNITFLAG_CANBEATTACKED|UNITFLAG_ISVALIDTARGET|UNITFLAG_TARGETABLE;row={};row.wAuraTargetState=STATE_DIMVISION;row.dwAuraLenCalc=1;for(int&i:row.wAuraStat)i=-1;validRow=validState=validMon=validModes=validDifficulty=true;row.dwAuraFilter=2;room={};mon={};modes={};modes.dwFlags=1u<<MONSTATS2FLAGINDEX_ISATT;modes.dwModeFlags=4;game={};game.dwGameFrame=100;difficulty={};difficulty.dwAiCurseDiv=1;removals=events=writer=updates=permissionCalls=aiCalls=traversals=draws=0;formulaUnit=-1;actor.stats[STAT_LEVEL]=target.stats[STAT_LEVEL]=20;actor.stats[STAT_ITEM_STUPIDITY]=1;raw=0;}
void result(bool&first,int success){auto*p=target.list;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",success,draws,p?p->rank:0,p?p->creator:0,p?p->expire:0,permissionCalls,aiCalls,formulaUnit,removals,events);first=false;}
int main(){printf("{\"callback\":[");bool first=true;
for(int a:{1,20,60,99})for(int t:{1,20,60,99})for(int bonus:{-1,0,1,2,5})for(int ranged:{0,1})for(int roll:{0,49,98,99})for(int kind:{-1,0,1,3}){
reset();actor.stats[STAT_LEVEL]=a;target.stats[STAT_LEVEL]=t;actor.stats[STAT_ITEM_STUPIDITY]=bonus;raw=roll;target.dwUnitType=kind<0?1:kind;printf("%s{\"input\":[%d,%d,%d,%d,%d,%d],\"output\":",first?"":",",a,t,bonus,ranged,roll,kind);bool f=true;int success=SKILLITEM_EventFunc09_Stupidity(&game,ranged?6:5,&actor,kind<0?nullptr:&target,nullptr,STAT_ITEM_STUPIDITY<<16,0);result(f,success);printf("}");first=false;}
printf("],\"composition\":[");first=true;
for(int variant:{0,1,2,3})for(int divisor:{-1,0,1,2,4})for(int allow:{0,1})for(int alignment:{0,1,2})for(int attract:{0,1})for(int resistance:{0,50,99,100})for(int old:{0,1,2,3,4}){
reset();actor.stats[STAT_ITEM_STUPIDITY]=variant==3?5:1;raw=variant==0?49:variant==1?25:0;int rank=variant==0?1:variant==1?6:variant==2?11:20;validDifficulty=divisor>=0;difficulty.dwAiCurseDiv=divisor;target.aiAllowed=allow;target.alignment=alignment;target.attract=attract;target.stats[STAT_CURSE_RESISTANCE]=resistance;
prior.skill=old==4?66:SKILL_DIMVISION;prior.state=old==4?19:STATE_DIMVISION;prior.rank=old==1?rank:old==2?rank+1:old==3?rank-1:9;prior.creator=999;prior.expire=777;target.list=old?&prior:nullptr;
printf("%s{\"input\":[%d,%d,%d,%d,%d,%d,%d],\"output\":",first?"":",",rank,divisor,allow,alignment,attract,resistance,old);bool f=true;int success=SKILLITEM_EventFunc09_Stupidity(&game,5,&actor,&target,nullptr,STAT_ITEM_STUPIDITY<<16,0);result(f,success);printf("}");first=false;}
printf("],\"guards\":[");first=true;
for(int gate=0;gate<12;gate++){
reset();if(gate==1)validRow=false;if(gate==2)validState=false;if(gate==3)validMon=false;if(gate==4)validModes=false;if(gate==5)mon.dwMonStatsFlags=1u<<MONSTATSFLAGINDEX_NPC;if(gate==6)modes.dwFlags=0;if(gate==7)modes.dwModeFlags=0;if(gate==8)target.monType=0x20;if(gate==9)target.dwFlags=0;if(gate==10)target.dead=true;if(gate==11)target.aligned=true;
printf("%s{\"input\":[%d],\"output\":",first?"":",",gate);bool f=true;int success=SKILLITEM_EventFunc09_Stupidity(&game,5,&actor,&target,nullptr,STAT_ITEM_STUPIDITY<<16,0);result(f,success);printf("}");first=false;}
printf("]}\n");}
'''
 cpp+='\n'+main
 for name,body in bodies.items():assert function(cpp,name)==body,name
 path=STAGE/'probe.cpp';path.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 cases=json.loads(run.stdout);assert {k:len(v)for k,v in cases.items()}==dict(callback=2560,composition=4800,guards=12)
 assert cases['guards'][0]['output']==[1,1,11,7,775,1,1,7,0,1],cases['guards'][0]
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n');inputs[str(parent.relative_to(ROOT))]=sha(parent);inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts={k:len(v)for k,v in cases.items()},completeRoutineHashes={k:hashlib.sha256(v.encode()).hexdigest()for k,v in bodies.items()},headerBindings=bound,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in (path,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log')},scope='Seven complete unchanged source routines:callback09,direct Dim Vision helper,AI-state mapping,curse receiver,eligibility,hostility and factory. All inherited integer constants independently rebound to headers. NPC/source stats,raw stream,formula/current no-stat Dim Vision row,metadata,empty subscribers,AI permission/dispatch,stat-list allocation and timers are declared services. This certifies the bounded callback/direct-target/factory contract,not live AI ticks or modern executable/world parity.')
 target=ROOT/'tools/reference/npc-item-blind-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'NPC item blind proof stale'
 else:target.write_text(encoded)
 print('NPC item blind:2560callback/4800composition/12guard contexts;seven unchanged routines;independent headers;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
