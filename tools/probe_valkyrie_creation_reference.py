#!/usr/bin/env python3
"""Compile full pinned summon base/passive helpers; declare nested world/formula services."""
import argparse, hashlib, json, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; STAGE=ROOT/'.local/class-skills'; REF=ROOT/'tools/reference/d2r-named-equipment'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text()); assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 hashes={}; blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes(); assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest(); return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M); assert m,name
  end=text.index('{',m.start())+1; depth=1
  while depth: depth+=(text[end]=='{')-(text[end]=='}'); end+=1
  block=text[m.start():end]; blocks[name]=hashlib.sha256(block.encode()).hexdigest(); return block+'\n'
 nec=source('source/D2Game/src/SKILLS/SkillNec.cpp'); base=routine(nec,'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10'); passive=routine(nec,'D2GAME_SetSummonPassiveStats_6FD0C530')
 percent=routine(source('source/D2Game/src/MONSTER/MonsterUnique.cpp'),'MONSTERUNIQUE_CalculatePercentage')
 pins={}; tables={}; manifest=json.loads((REF/'sources.json').read_text())['files']
 for name in ('skills','monlvl','itemstatcost'):
  p=REF/'json'/f'{name}.json'; pins[name]=hashlib.sha256(p.read_bytes()).hexdigest(); assert pins[name]==manifest['json/'+name+'.json']['sha256']; tables[name]=json.loads(p.read_text())
 row=tables['skills']['32']; assert row['skill']=='Valkyrie' and row['calc1']=="par1 * (lvl - 1) + skill('Dopplezon'.blvl) * par8" and row['calc2']=='ln56'
 assert not row.get('ToHit') and not row.get('LevToHit')
 assert [row['sumsk'+str(i)+'calc'] for i in range(1,5)]==["skill('Dodge'.blvl)","skill('Avoid'.blvl)","skill('Evade'.blvl)","skill('Critical Strike'.blvl)"]
 assert [row['Param'+str(i)] for i in range(1,9)]==[20,25,10,12,25,3,2,20]
 assert row['passivecalc1']=='par3 * (lvl - 1)' and row['passivecalc2']=='toht'
 for i in range(3,8): assert row['passivecalc'+str(i)]==f"stat('{row['passivestat'+str(i)]}'.accr)"
 for i in range(1,7):
  assert row['aurastatcalc'+str(i)] in ('lvl * par2','lvl * par4','"min((lvl+skill(\'Dopplezon\'.blvl))*par7,85)"')
 for name in [row['passivestat'+str(i)] for i in range(1,8)]+[row['aurastat'+str(i)] for i in range(1,7)]:
  assert not any(v for k,v in tables['itemstatcost'][name].items() if k.lower().startswith('itemevent'))
 levels=[]
 for level in range(111):
  r=tables['monlvl'][str(level)]; assert r['Level']==level
  values=lambda name:[r[name+suffix] for suffix in ('','(N)','(H)')]
  levels.append(dict(level=level,defense=values('AC'),attack=values('TH'),expansionDefense=values('L-AC'),expansionAttack=values('L-TH')))
 datafile=ROOT/'data/Sanctuary/Balance/summon-level-stats.json'; rendered=json.dumps(levels,indent=2)+'\n'
 if check: assert datafile.read_text()==rendered
 else: datafile.write_text(rendered)
 cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int STAT_LEVEL=0,STAT_ARMORCLASS=1,STAT_TOHIT=2,STAT_MAXHP=3,STAT_HITPOINTS=4,SKILLSFLAGINDEX_AURA=0;
using D2C_UnitEventTypes=int;
constexpr uint32_t gdwBitMasks[]={1};
struct D2UnitStrc{int dwUnitId=17,dwUnitType=1,stats[32]={};}owner,pet;
struct D2GameStrc{void* pMemoryPool=nullptr;uint8_t nDifficulty=0,nGameType=0;int dwGameType=0;}game;
struct D2MonLvlTxt{int dwLAC[3],dwAC[3],dwLTH[3],dwTH[3];};
struct Tables{int nMonLvlTxtRecordCount=111,nStatesTxtRecordCount=100,nOverlayTxtRecordCount=500;D2MonLvlTxt pMonLvlTxt[111];}tables;auto* sgptDataTables=&tables;
struct D2SkillsTxt{int nPassiveStat[5]={5,6,7,8,9},dwPassiveCalc[5]={0,1,2,3,4},wAuraStat[6]={12,13,14,15,16,17},dwAuraStatCalc[6]={5,6,7,8,9,10};int nAuraState=0,dwCalc[1]={11},wSumSkill[5]={100,101,102,103,-1},dwSumSkCalc[5]={12,13,14,15,16},dwFlags[1]={0},wAuraEvent[3]={-1,-1,-1},wAuraEventFunc[3]={},wSumUMod=0,wSumOverlay=0;}skill;
struct D2ItemStatCostTxt{int wItemEvent[2]={},wItemEventFunc[2]={};}cost;
struct D2StatListStrc{}list;
int rank=1,hard=0,pierce=0,inherit=0,equipmentLevel=0,skills[4]={};
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]=n;}
void STATLIST_AddUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]+=n;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*p){return p->stats[STAT_MAXHP];}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id>=0?&skill:nullptr;}
D2ItemStatCostTxt* SKILLS_GetItemStatCostTxtRecord(int){return &cost;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int){switch(f){case 0:return 10*(rank-1);case 1:return 0;case 2:case 3:case 4:return pierce;case 5:return rank*25;case 6:return rank*12;case 7:case 8:case 9:case 10:return std::min(85,2*(rank+hard));case 11:return 20*(rank-1)+20*hard;default:return inherit? (f==12?1:f==13?3:f==14?6:f==15?20:0):0;}}
bool SUNITEVENT_GetEvent(D2GameStrc*,D2UnitStrc*,int,int,int){return false;}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){}
D2StatListStrc* STATLIST_AllocStatList(void*,int,int,int,int){return &list;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int s,int n,int){pet.stats[s]=n;}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void STATLIST_SetState(D2StatListStrc*,int){}
void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){skills[s-100]=n;}
void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int,int){}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){}
void D2GAME_BOSSES_AssignUMod_6FC6FF10(D2GameStrc*,D2UnitStrc*,int,int){}
void UNITS_SetOverlay(D2UnitStrc*,int,int){}
void sub_6FCF9580(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int level,int){equipmentLevel=level;}
int D2Clamp(int value,int minimum,int maximum){return std::clamp(value,minimum,maximum);}
'''+percent+base+passive+'\nint main(){\n'
 for r in levels:
  for field,member in [('defense','dwAC'),('attack','dwTH'),('expansionDefense','dwLAC'),('expansionAttack','dwLTH')]:
   for i,n in enumerate(r[field]): cpp+=f'tables.pMonLvlTxt[{r["level"]}].{member}[{i}]={n};\n'
 cpp+=r'''bool first=true;std::printf("[");
for(int level:{1,30,99})for(int r:{1,2,8,17,20,27,40,255})for(int difficulty:{0,1,2})for(int expansion:{0,1})for(int hp:{102437,122880})for(int h:{0,1,20})for(int pi:{0,17,100})for(int inh:{0,1}){
rank=r;hard=h;pierce=pi;inherit=inh;owner={};pet={};owner.stats[STAT_LEVEL]=level;pet.stats[STAT_MAXHP]=hp;pet.stats[STAT_HITPOINTS]=hp/2;pet.stats[STAT_ARMORCLASS]=141;pet.stats[STAT_TOHIT]=250;game.nDifficulty=difficulty;game.nGameType=expansion;game.dwGameType=0;for(int&i:skills)i=0;equipmentLevel=0;
int ok=D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,rank);int result=D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,32,rank,25+3*(rank-1));
if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",level,rank,difficulty,expansion,hp,hard,pierce,inherit,ok,result,pet.stats[STAT_LEVEL],pet.stats[STAT_MAXHP],pet.stats[STAT_HITPOINTS],equipmentLevel);
for(int i=0;i<32;i++)std::printf(i?",%d":"%d",pet.stats[i]);std::printf("],[%d,%d,%d,%d]]",skills[0],skills[1],skills[2],skills[3]);
}std::printf("]\n");}
'''
 p=STAGE/'valkyrie-creation-source-probe.cpp'; p.write_text(cpp); binary=p.with_suffix(''); subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 fixture=STAGE/'valkyrie-creation-source-probe.json'; rows=json.loads(subprocess.check_output([str(binary)])); assert len(rows)==5184
 for level,rank,difficulty,expansion,hp,hard,pierce,inherit,ok,result,petlevel,maxhp,life,itemlevel,stats,skills in rows:
  l=min(level,max(1,rank+3*level//4)); r=levels[l]
  assert (ok,result,petlevel,maxhp,life,itemlevel)==(1,1,l,hp+hp*(20*(rank-1)+20*hard)//100,hp+hp*(20*(rank-1)+20*hard)//100,25+3*(rank-1))
  assert stats[1]==141+r['expansionDefense' if expansion else 'defense'][difficulty] and stats[2]==250+r['expansionAttack' if expansion else 'attack'][difficulty]
  assert stats[5:10]==[10*(rank-1),0,pierce,pierce,pierce] and stats[12:18]==[25*rank,12*rank]+[min(85,2*(rank+hard))]*4
  assert skills==([1,3,6,20] if inherit else [0]*4)
 fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,pinnedTableHashes=pins,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),data=dict(path=str(datafile.relative_to(ROOT)),sha256=hashlib.sha256(datafile.read_bytes()).hexdigest()),established=[
 'Complete base helper overwrites level with min(casterLevel,max(1,rank+3*casterLevel/4)), then adds the difficulty/game-context monlvl AC and TH to existing spawn stats. All111 pinned rows and all three difficulty columns are exported without choosing a new summon HP baseline.',
 'Complete old passive helper applies five additive passive stats and six nonzero aura stats, fills both maximum/current life with existing maximum plus source percentage(calc1), grants positive hard-point summon skills and dispatches equipment with the supplied item level.',
 'Pinned current Valkyrie row has seven passive stats. First two are armor percent and zero toht; five accrued immunity-piercing stats follow. Current source data, rather than the old five-slot loop, defines the prepared seven-slot native snapshot.',
 'Hard Decoy points supply life and elemental resistance synergies; resistance caps85. Dodge/Avoid/Evade/Critical Strike use hard points only. Equipment item level is retained as the raw calc2 value; downstream creation clamping remains separate.'
 ],services=[
 'Formula service supplies the explicitly asserted pinned row expressions. It does not compile the original formula VM. Caller stats/spawn baseline, stat list allocation/storage, nested item-event/state/overlay and equipment routines are declared services; equipment is only a recorded request.',
 'All tested passive/aura itemstatcost rows have no item events. Full old helpers are compiled unchanged; this is not execution of the modern seven-slot loop or the full source stat dependency graph.',
 'All source cases use positive bounded owner levels and HP values, valid records and successful allocation. The old percentage helper executes unchanged. Original spawn HP RNG, difficulty/player-count scaling and requested-quality gear are not inferred from these inputs.'
 ],limits=[
 'No learned Valkyrie handler or NPC is enabled by this component. Stat inheritance consumers, gear affixes, moving AI/attacks, HP regeneration, resource/delay/placement/lifecycle/saves and actual client remain pending.',
 'No current-executable equivalence certificate. Exact modern accrued-stat layers and the two additional passive slots require later consumer acceptance.'
 ])
 out=ROOT/'tools/reference/valkyrie-creation-engine-evidence.json'; rendered=json.dumps(evidence,indent=2)+'\n'
 if check: assert out.read_text()==rendered
 else: out.write_text(rendered)
 print('Valkyrie creation:5184 full legacy base/passive helper cases;111 pinned level rows. Current seven-slot data extension is separately bounded.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
