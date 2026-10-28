#!/usr/bin/env python3
"""Compose pinned NPC416/418 rows with complete unchanged shared melee bodies."""
import argparse,csv,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'goatman-npc-melee'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={};bodies={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 pin(BASE/'d2moo-source-tree.json')
 def source(rel,names):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=pin(p)
  for n in names:bodies[n]=function(text,n)
 source('source/D2Game/src/SKILLS/SkillBar.cpp',['SKILLS_SrvSt39_Berserk','SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite'])
 source('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD11E40','D2GAME_RollElementalDamage_6FD14DD0','SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws'])
 source('source/D2Common/include/D2Combat.h',[])
 parent=BASE/'bear-smite-source-probe.cpp';receipt=json.loads(pin(ROOT/'tools/reference/bear-smite-engine-evidence.json'));assert sha(parent)==receipt['fixture']['generatedSourceSha256'];cpp=pin(parent).split('bool first=true;',1)[0]
 for rel,h in receipt['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/rel)==h;inputs[str((BASE/'d2moo-pinned-source'/rel).relative_to(ROOT))]=h
 for n,h in receipt['unchangedRoutineHashes'].items():
  found=[]
  for rel in receipt['sourceHashes']:
   try:found.append(function((BASE/'d2moo-pinned-source'/rel).read_text(),n))
   except AssertionError:pass
  assert len(found)==1 and hashlib.sha256(found[0].encode()).hexdigest()==h and found[0] in cpp,n
 manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'));tables={}
 for n in ['skills','monstats','states']:
  p=ROOT/f'tools/reference/d2r-named-equipment/json/{n}.json';assert sha(p)==manifest['files'][f'json/{n}.json']['sha256'];tables[n]=json.loads(pin(p))
 owned=ROOT/'.local/research/d2r-3.3.93847/skills.txt';pin(owned)
 with owned.open(encoding='utf-8-sig',newline='') as f:ownedrows={r['skill']:r for r in csv.DictReader(f,delimiter='\t') if r.get('skill')}
 selected={str(i):tables['skills'][str(i)] for i in [416,417,418,419]}
 for row in selected.values():
  for k,v in row.items():
   if not k.startswith('*') and k!='lineNumber':assert str(v).strip(chr(34))==ownedrows[row['skill']].get(k,'').strip(chr(34)),(row['skill'],k,v)
 assert tables['states']['berserk']['*ID']==95 and tables['monstats']['wargoatman']['Crit']==5
 assert selected['416'].get('calc1',0)==0 and 'ELenSymPerCalc' not in selected['416'] and selected['418']['calc1']=='ln12' and selected['418']['Param1']==105 and selected['418']['Param2']==1
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a[:100];cpp=cpp.replace(a,b,1)
 replace('enum{ELEMTYPE_FIRE=1,ELEMTYPE_LTNG,ELEMTYPE_MAGIC,ELEMTYPE_COLD,ELEMTYPE_POIS,ELEMTYPE_BURN,ELEMTYPE_FREEZE,ELEMTYPE_RAND};','enum{ELEMTYPE_NONE,ELEMTYPE_FIRE,ELEMTYPE_LTNG,ELEMTYPE_MAGIC,ELEMTYPE_COLD,ELEMTYPE_POIS,ELEMTYPE_LIFE,ELEMTYPE_MANA,ELEMTYPE_STAMINA,ELEMTYPE_STUN,ELEMTYPE_RAND,ELEMTYPE_BURN,ELEMTYPE_FREEZE};')
 replace('struct D2StatListStrc{};','struct D2StatListStrc{int flags=0,until=0,rank=0;};D2StatListStrc stateList;bool statePresent=false,allowState=true;int stateAllocs=0,stateToggles=0,stateRefreshes=0,stateEvents=0;')
 replace('D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return hasRow&&id==326?&skill:nullptr;}','int currentId=416;D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return hasRow&&id==currentId?&skill:nullptr;}')
 replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int rank){return calc==0?15*rank:calc==1?std::max(250,15+5*(rank-1)):0;}', 'int Duration(int rank){return 75-std::min(110*rank/(rank+6)*50/100,50);}\nint SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int id,int rank){if(id==416)return 0;return calc==0?104+rank:calc==1?Duration(rank):calc==3?100:0;}')
 replace('int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 0;}','int SKILLS_GetToHitFactor(D2UnitStrc*,int id,int rank){return id==416?10+5*rank:100+15*(rank-1);}')
 replace('int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){assert(ar==0);trace.push_back({2,resultFlags});return resultFlags;}','int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){assert(ar==(currentId==416?10+5*npcRank:100+15*(npcRank-1)));trace.push_back({2,resultFlags});return resultFlags;}')
 replace('int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}','int StunLength(int rank){return 30+std::min(rank-1,15)*5+std::max(rank-16,0)*2;}\nint SKILLS_GetElementalLength(D2UnitStrc*,int id,int rank,int){return id==416?StunLength(rank):0;}')
 replace('void sub_6FD11E40(D2UnitStrc*,D2DamageStrc*,int type,int damage,int length,int,int){assert(type==0&&damage==0&&length==0);}',bodies['sub_6FD11E40'])
 replace('D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}','D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc* u,int){return u==&attacker&&statePresent?&stateList:nullptr;}')
 replace('D2StatListStrc*STATLIST_AllocStatList(void*,unsigned,int,int,int){__builtin_trap();}','D2StatListStrc*STATLIST_AllocStatList(void*,unsigned flags,int until,int type,int id){assert(type==UNIT_MONSTER&&id==17);stateAllocs++;if(!allowState)return nullptr;stateList={int(flags),until,0};return &stateList;}')
 replace('void STATLIST_SetState(D2StatListStrc*,int){__builtin_trap();}','void STATLIST_SetState(D2StatListStrc*,int state){assert(state==95);}')
 replace('void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){__builtin_trap();}','void D2COMMON_10475_PostStatToStatList(D2UnitStrc* u,D2StatListStrc*,int){assert(u==&attacker);statePresent=true;}')
 replace('void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){__builtin_trap();}','void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}')
 replace('void STATES_ToggleState(D2UnitStrc*,int,int){__builtin_trap();}','void STATES_ToggleState(D2UnitStrc*,int state,int on){assert(state==95&&on==1);stateToggles++;}')
 replace('void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){__builtin_trap();}','void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc* s,D2SkillsTxt*,int id,int rank){assert(id==418);s->rank=rank;stateRefreshes++;}\nvoid D2COMMON_10476(D2StatListStrc* s,int until){s->until=until;}')
 replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){__builtin_trap();}','void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int event,int until,int,int){assert(event==EVENTTYPE_REMOVESTATE&&until==stateList.until);stateEvents++;}')
 cpp+='\n'+bodies['SKILLS_SrvSt39_Berserk']+r'''
void Reset(int id,int rank){game={};attacker={};defender={};allocated={};skill={};currentId=id;npcRank=rank;stateList={};statePresent=false;allowState=true;stateAllocs=stateToggles=stateRefreshes=stateEvents=0;
attacker.dwUnitType=defender.dwUnitType=UNIT_MONSTER;attacker.dwClassId=742;defender.dwClassId=28;attacker.dwUnitId=17;defender.dwUnitId=29;attacker.pGame=defender.pGame=&game;attacker.stats[STAT_HITPOINTS]=defender.stats[STAT_HITPOINTS]=10000000;
skill.wResultFlags=0;skill.nEType=id==416?ELEMTYPE_STUN:ELEMTYPE_MAGIC;skill.dwHitClass=id==416?96:0;skill.nAuraState=id==418?95:-1;
high=0;criticalRoll=99;draws=events=overlays=0;bounds.clear();trace.clear();hasRow=hasTarget=hasRoom=interact=inRange=true;town=false;
}
int Start(int rank){return currentId==416?SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite(&game,&attacker,currentId,rank):SKILLS_SrvSt39_Berserk(&game,&attacker,currentId,rank);}
void State(){printf("[%d,%d,%d,%d,%d,%d,%d,%d]",statePresent,statePresent?stateList.flags:0,statePresent?stateList.until:0,statePresent?stateList.rank:0,stateAllocs,stateToggles,stateRefreshes,stateEvents);}
bool first=true;
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("{\"packets\":[");int ends[5][2]={{3,4},{30,50},{230,275},{0,0},{4200,4200}};
for(int id:{416,418})for(int rank:{1,2,8,9,16,17,20,21,28,255})for(auto&end:ends)for(int ed:{-100,0,210})for(int typed:{0,1})for(int hi:{0,1})for(int crit:{4,5})for(int resist:{0,50})for(int flags:{0,1,16,129,32769}){
Reset(id,rank);attacker.stats[STAT_MINDAMAGE]=end[0];attacker.stats[STAT_MAXDAMAGE]=end[1];attacker.stats[STAT_DAMAGEPERCENT]=ed;
if(typed){attacker.stats[STAT_FIREMINDAM]=2;attacker.stats[STAT_FIREMAXDAM]=6;attacker.stats[STAT_LIGHTMINDAM]=3;attacker.stats[STAT_LIGHTMAXDAM]=7;attacker.stats[STAT_COLDMINDAM]=5;attacker.stats[STAT_COLDMAXDAM]=8;attacker.stats[STAT_COLDLENGTH]=31;}
for(int stat:{STAT_DAMAGERESIST,STAT_MAGICRESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST})defender.stats[stat]=resist;
high=hi;criticalRoll=crit;resultFlags=flags;int ok=Start(rank);D2DamageStrc d=attacker.pCombat?attacker.pCombat->tDamage:D2DamageStrc{};
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",id,rank,end[0],end[1],ed,typed,hi,crit,resist,flags,ok,d.dwPhysDamage,d.dwMagDamage,d.dwFireDamage,d.dwLtngDamage,d.dwColdDamage,d.dwColdLen,d.dwStunLen,d.dwDmgTotal,d.wResultFlags,d.dwHitClass,overlays,draws);first=false;
State();printf(",[");for(size_t i=0;i<bounds.size();i++)printf("%s%d",i?",":"",bounds[i]);printf("]]");
}
printf("],\"states\":[");first=true;
for(int rank:{1,20,255})for(int existing:{0,2,4})for(int allocated:{0,1})for(int hit:{0,1})for(int expired:{0,1})for(int impact:{1,20,255}){
Reset(418,rank);statePresent=existing!=0;stateList={existing,1234,7};allowState=allocated;resultFlags=hit;int start=Start(rank);
printf("%s[[%d,%d,%d,%d,%d,%d],%d,",first?"":",",rank,existing,allocated,hit,expired,impact,start);first=false;State();
if(expired)statePresent=false;hasTarget=false;int action=SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws(&game,&attacker,418,impact);
printf(",%d,",action);State();printf(",%u]",attacker.dwFlags);
}
printf("],\"guards\":[");first=true;
for(int id:{416,418})for(int row:{0,1})for(int target:{0,1})for(int townFlag:{0,1}){Reset(id,1);hasRow=row;hasTarget=target;town=townFlag;resultFlags=0;int ok=Start(1);printf("%s[%d,%d,%d,%d,%d]",first?"":",",id,row,target,townFlag,ok);first=false;}
printf("]}\n");}
'''
 p=STAGE/'source.cpp';p.write_text(cpp);compiler=STAGE/'compiler.log';san=STAGE/'sanitizer.log'
 with compiler.open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=f,stderr=subprocess.STDOUT,check=True)
 r=subprocess.run([str(p.with_suffix(''))],capture_output=True);san.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-3000:];data=json.loads(r.stdout);assert len(data['packets'])==24000 and len(data['states'])==216 and len(data['guards'])==16
 for id,row,target,town,ok in data['guards']:assert ok==int(bool(row and target and not(id==416 and town)))
 out=BASE/'goatman-npc-melee-source-probe.json';encoded=json.dumps(data,separators=(',',':'))+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 artifacts=[p,compiler,san,out,Path(__file__).resolve(),ROOT/'tools/build_class_skills.py',ROOT/'data/Sanctuary/Balance/warlock-npc-skills.json',ROOT/'data/Sanctuary/Balance/necromancer-summons.json']
 result=dict(referenceVersion='3.3.93847',component='Goatman NPC Stun/Berserk packet and self-state preparation',legacyCommit=tree['sha'],packetCases=24000,stateCases=216,guardCases=16,currentExecutableParity=False,handlerEnabled=False,processMemoryRead=False,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},unchangedRoutines={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},retainedFullPacketRoutines=receipt['unchangedRoutineHashes'],
  established=['Owned current NPC416/418rows differ from player Stun/Berserk. Source start32/39,elemental assignment/roll,allocation,fill,percentage,monster critical,mitigation/total and full do2remain unchanged. NPCBerserk converts before intrinsic monster critical;large-value early division can leave a small physical remainder even at100percentconversion.', 'Berserk start creates/refreshes timed flag2state even on a miss. Existing flags remain unchanged;do2refreshes an existing state without extending expiry and can recreate an absent temporary flag4state. State tests make the release target absent to isolate the complete do2self-state path;no health/action acceptance is claimed.'],
  services=['NPCformula/AR/element-length values are supplied from pinned row arithmetic. Hit flags,stat access,random endpoints,allocation/statlist/event/refresh and other shared scaffold services remain declared. No weapon/equipment/strength,nonzero mastery/extra item-critical,poison or modern Goatman event16callbacks are certified.', 'All original functions are compiled unchanged with ASan+UBSan;this is supplementary older engine evidence,not a current executable certificate. Current raw Frenzy/Cleave rows are retained but their new callback bodies remain gated.'])
 target=ROOT/'tools/reference/goatman-npc-melee-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Goatman NPC melee:24000complete-source packets,216Berserk state phases,16guards;current callbacks/world actor remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
