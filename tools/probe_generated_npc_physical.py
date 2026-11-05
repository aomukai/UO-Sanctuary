#!/usr/bin/env python3
"""Selected-hand original item graph, mastery and full source physical roll."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'generated-npc-physical-stats-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);inputs={}
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 prior=load('tools/reference/generated-npc-weapons-source-evidence.json');tree=load('.local/class-skills/d2moo-source-tree.json');assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 path=BASE/'generated-npc-weapons-source/source.cpp';assert sha(path)==prior['artifacts'][str(path.relative_to(ROOT))];inputs[str(path.relative_to(ROOT))]=sha(path);cpp=path.read_text();hashes=prior['completeRoutineHashes'].copy()
 cases=load('.local/class-skills/generated-npc-weapons-source/cases.json');old=load('.local/class-skills/generated-npc-stats-source/cases.json');raw=load('.local/class-skills/item-quality-current-source/prepared-cases.json');catalog=load('data/Sanctuary/Balance/npc-item-affix-catalogue.json');typeRows=load('data/Sanctuary/Balance/item-initialization-types.json')['types']
 types={r['code']:r['ordinal']for r in typeRows};parents={r['code']:r['parents']for r in catalog['types']};bases={r['code']:r for r in catalog['bases']};rare={r['code']:r for r in catalog['rareBases']}
 def isa(code,parent):return code==parent or any(isa(p,parent)for p in parents[code])
 facts=[]
 for item in old['items']:
  code=raw['inputs'][item['Case']][0];b=bases[code];r=b['sourceFields'];facts.append(dict(Str=r.get('StrBonus',0),Dex=r.get('DexBonus',0),Two=r.get('2handed',0),Either=r.get('1or2handed',0),Throwable=rare[code]['throwable'],Types=[i for c,i in types.items()if isa(b['type'],c)]))
 units=source('source/D2Common/src/D2StatList.cpp');skills=source('source/D2Common/src/D2Skills.cpp');damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp');items=source('source/D2Common/src/Items/Items.cpp');inventory=source('source/D2Common/src/D2Inventory.cpp')
 header=source('source/D2Common/include/D2StatList.h')
 enum=re.search(r'enum D2C_ItemStats\s*\{(.*?)\};',header,re.S).group(1)
 enum=re.sub(r'/\*.*?\*/|//[^\n]*','',enum,flags=re.S);statIds={};ordinal=-1
 for entry in enum.split(','):
  if not entry.strip():continue
  name,sep,value=entry.strip().partition('=');ordinal=int(value.strip(),0)if sep else ordinal+1;statIds[name.strip()]=ordinal
 costs=load('data/Sanctuary/Balance/equipment-stat-costs.json');costsById={r['Id']:r for r in costs}
 assert statIds['STAT_ITEM_MINDAMAGE_PERCENT']==18 and costsById[18]['Name']=='item_mindamage_percent'
 assert statIds['STAT_ITEM_MAXDAMAGE_PERCENT']==17 and costsById[17]['Name']=='item_maxdamage_percent'
 added={}
 for text,names in[(units,['STATLIST_GetTotalStatValue','STATLIST_UnitGetStatValue','STATLIST_UnitGetItemStatOrSkillStatValue','D2Common_CopyStats_6FDB9C50','STATLIST_CopyStats']),(skills,['D2Common_11024','SKILLS_GetWeaponMasteryBonus']),(items,['ITEMS_GetStrengthBonus','ITEMS_GetDexBonus']),(inventory,['INVENTORY_GetWieldType']),(unique,['MONSTERUNIQUE_CalculatePercentage']),(damage,['SUNITDMG_ApplyDamageBonuses'])]:
  for n in names:added[n]=function(text,n)
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 cpp=cpp.replace('struct D2UnitStrc;','struct D2UnitStrc;struct D2InventoryGridStrc{D2UnitStrc*ppItems[13]{};};struct D2InventoryStrc{bool signature=true;D2InventoryGridStrc*grid=nullptr;};')
 cpp=cpp.replace('void*pInventory=nullptr;','D2InventoryStrc*pInventory=nullptr;')
 cpp=cpp.replace('unsigned dwSeqFrame=0;','unsigned dwSeqFrame=0;std::array<bool,256>types{};bool throwable=false;int pSeed=0;')
 cpp=cpp.replace('*found=i<a->nStatCount&&a->pStat[i].nPackedValue==packed;','if(found)*found=i<a->nStatCount&&a->pStat[i].nPackedValue==packed;')
 cpp=cpp.replace('struct D2ItemsTxt{int dwGemOffset=0;}basis;','struct D2ItemsTxt{int dwGemOffset=0;short nStrBonus=0,nDexBonus=0;bool n2Handed=false,n1or2Handed=false;}basis;')
 cpp=cpp.replace('struct D2SkillsTxt{int nWeapSel=0;}usedRow;struct D2SkillStrc{}usedSkill;','struct D2SkillsTxt{int nWeapSel=0,nITypeA[1]={},nRange=0;}usedRow;struct D2SkillStrc{D2SkillsTxt*pSkillsTxt=nullptr;}usedSkill;')
 cpp=cpp.replace('ITEMTYPE_GEM=96,ITEMTYPE_RUNE=97','ITEMTYPE_GEM=10096,ITEMTYPE_RUNE=10097')
 oldtype='bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int type){return p&&(type==ITEMTYPE_GEM?p->gem:type==ITEMTYPE_RUNE?p->rune:type==ITEMTYPE_WEAPON?p->weapon:false);}'
 assert oldtype in cpp;cpp=cpp.replace(oldtype,'bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int type){return p&&(type==ITEMTYPE_GEM?p->gem:type==ITEMTYPE_RUNE?p->rune:type>=0&&type<256?p->types[type]:false);}')
 cpp=cpp.replace('D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int){return hasBase?&basis:nullptr;}','D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int);')
 # Keep all source bodies unchanged; the extended fixture supplies pinned
 # type ancestry, raw short coefficients and valid native inventory grids.
 before,main=cpp.split('int main(){Init();',1);cpp=before
 cpp=cpp.replace('void SelectedSnapshot(){','void PhysicalSnapshot();void SelectedSnapshot(){').replace('Snapshot(itemCount);printf("]");}','Snapshot(itemCount);printf(",");PhysicalSnapshot();printf("]");}')
 cpp+='struct Facts{short str,dex;bool two,either,throwable;std::vector<int>types;};\nFacts facts[]={\n'+''.join('{'+','.join(str(int(f[k]))for k in('Str','Dex','Two','Either','Throwable'))+',{'+','.join(map(str,f['Types']))+'}},\n'for f in facts)+'};\n'
 cpp+='bool typeParents[256][256]={};void InitTypes(){'+''.join('typeParents['+str(i)+']['+str(j)+']=true;'for c,i in types.items()for p,j in types.items()if isa(c,p))+'}\n'
 cpp+=r'''
constexpr int BODYLOC_LARM=5,BODYLOC_RARM=4,INVGRID_BODYLOC=0,ITEMTYPE_THROWN_WEAPON=48;
constexpr int STAT_MINDAMAGE=21,STAT_MAXDAMAGE=22,STAT_SECONDARY_MINDAMAGE=23,STAT_SECONDARY_MAXDAMAGE=24,STAT_ITEM_NORMALDAMAGE=111,STAT_DAMAGEPERCENT=25,STAT_STRENGTH=0,STAT_DEXTERITY=2,STAT_ITEM_MINDAMAGE_PERCENT=18,STAT_ITEM_MAXDAMAGE_PERCENT=17;
constexpr int STAT_PASSIVE_MASTERY_MELEE_TH=342,STAT_PASSIVE_MASTERY_MELEE_DMG=343,STAT_PASSIVE_MASTERY_MELEE_CRIT=344,STAT_PASSIVE_MASTERY_THROW_TH=345,STAT_PASSIVE_MASTERY_THROW_DMG=346,STAT_PASSIVE_MASTERY_THROW_CRIT=347;
D2InventoryStrc inv;D2InventoryGridStrc body;int gBodyLocInventoryGridInfo=0,edge=0,lastBound=0,draws=0;
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int id){if(id<0||id>=int(ARRAY_SIZE(facts)))return nullptr;auto f=facts[id];basis.nStrBonus=f.str;basis.nDexBonus=f.dex;basis.n2Handed=f.two;basis.n1or2Handed=f.either;return&basis;}
bool ITEMS_CheckIfThrowable(D2UnitStrc*p){return p&&p->throwable;}
bool ITEMS_CheckType(int type,int ancestor){return type>=0&&type<256&&ancestor>=0&&ancestor<256&&typeParents[type][ancestor];}
BOOL INVENTORY_CheckSignature(D2InventoryStrc*p){return p->signature;}
D2InventoryGridStrc*INVENTORY_GetGrid(D2InventoryStrc*p,int,void*){return p->grid;}
uint32_t ITEMS_CheckWeaponIfTwoHanded(D2UnitStrc*p){return p&&p->dwUnitType==4?facts[p->dwClassId].two:0;}
int ITEMS_Is1Or2Handed(D2UnitStrc*p,D2UnitStrc*w){return p->dwUnitType==0&&p->dwClassId==4&&facts[w->dwClassId].either;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){assert(bound>0);lastBound=bound;draws++;return edge?bound-1:0;}
'''+ '\n'.join(added.values())+r'''
void PhysicalSnapshot(){auto w=sub_6FC7C7B0(&actor);usedSkill.pSkillsTxt=hasRow?&usedRow:nullptr;printf("[%d,[",INVENTORY_GetWieldType(&actor,actor.pInventory));for(int type=0;type<3;type++)printf("%s%d",type?",":"",SKILLS_GetWeaponMasteryBonus(&actor,w,nullptr,type));printf("],[");bool comma=false;for(int added:{-150,0,125})for(int source:{64,128,255})for(int choice:{0,1}){edge=choice;lastBound=draws=0;int result=SUNITDMG_ApplyDamageBonuses(&actor,1,nullptr,0,0,added,0,source);printf("%s[%d,%d,%d]",comma?",":"",result,lastBound,draws);comma=true;}printf("]]");}
void MasteryControls(){printf("[");bool comma=false;for(int mode=0;mode<4;mode++)for(int throwing=0;throwing<2;throwing++)for(int kind=-1;kind<4;kind++)for(int pattern=0;pattern<4;pattern++){
 new(&root)D2StatListStrc;actor={};actor.dwUnitType=1;actor.pStatListEx=&root;root.dwOwnerType=1;root.pOwner=&actor;if(mode==3)root.dwFlags=0;
 D2UnitStrc item;item.dwUnitType=4;item.throwable=throwing;item.types[12]=true;item.types[40]=true;hasSkill=hasRow=true;usedRow.nITypeA[0]=48;usedRow.nRange=2;usedSkill.pSkillsTxt=&usedRow;
 for(int stat=342;stat<=347;stat++)for(int layer=1;layer<=40;layer++){int value=pattern==0?layer:pattern==1?-layer:pattern==2?(layer==40?999:-1):(layer==12?55:layer);Raw(root.FullStats,uint32_t(stat)<<16|layer,value);}
 int result=SKILLS_GetWeaponMasteryBonus(mode==1?nullptr:&actor,mode==2?nullptr:&item,nullptr,kind);
 printf("%s[%d,%d,%d,%d,%d]",comma?",":"",mode,throwing,kind,pattern,result);comma=true;
}printf("]");}
'''
 main=main.replace('bool comma=false;printf("{\\\"equippable\\\":[");','InitTypes();printf("{\\\"mastery\\\":");MasteryControls();bool comma=false;printf(",\\\"equippable\\\":[");')
 assert 'MasteryControls();'in main
 main=main.replace('std::array{31,80}', 'std::array{31,80},std::array{18,37},std::array{17,51},std::array{185,89},std::array{186,60},std::array{187,300}')
 main=main.replace('actor.pInventory=&actor;','actor.pInventory=&inv;inv.signature=true;inv.grid=&body;for(auto&v:body.ppItems)v=nullptr;')
 main=main.replace('trace.clear();calls.clear();itemCount=', 'for(int stat=342;stat<=347;stat++){int value=stat==342?25:stat==343?50:stat==344?15:stat==345?35:stat==346?65:30;Raw(root.Stats,uint32_t(stat)<<16|45,value);Raw(root.FullStats,uint32_t(stat)<<16|45,value);}for(int stat=342;stat<=344;stat++){int value=stat==342?40:stat==343?80:25;Raw(root.Stats,uint32_t(stat)<<16|69,value);Raw(root.FullStats,uint32_t(stat)<<16|69,value);}\ntrace.clear();calls.clear();itemCount=')
 needle='u.weapon=input.weapon;u.pItemData=&flags[i];';assert needle in main;main=main.replace(needle,'u.weapon=input.weapon;u.dwClassId=c.items[i];u.throwable=facts[c.items[i]].throwable;for(int type:facts[c.items[i]].types)u.types[type]=true;body.ppItems[u.body]=&u;u.pItemData=&flags[i];')
 main=main.replace('usedRow.nWeapSel=h.select;hasSkill=h.used;hasRow=h.row;skillFlags=h.flags;','usedRow.nWeapSel=h.select;hasSkill=h.used;hasRow=h.row;skillFlags=h.flags;usedRow.nITypeA[0]=48;usedRow.nRange=h.index%3==0?2:1;')
 cpp+='int main(){Init();'+main
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 for n,b in added.items():assert function(cpp,n)==b,n
 # ABI constants are validated independently against the hash-pinned enum,
 # not merely made to agree with a production reader or a duplicated oracle.
 bindings={n:int(v,0)for n,v in re.findall(r'\b(STAT_[A-Z0-9_]+)\s*=\s*(-?(?:0x[\da-fA-F]+|\d+))',cpp)if not n.startswith('STAT_OP_')}
 assert len(bindings)==19 and all(statIds.get(n)==v for n,v in bindings.items()),bindings
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2500:]
 result=json.loads(run.stdout);assert len(result['numeric'])==3276 and len(result['mastery'])==160
 out=STAGE/'cases.json';out.write_text(json.dumps(dict(numericInputs=cases['numericInputs'],hands=cases['hands'],numericOutputs=result['numeric'],mastery=result['mastery']),separators=(',',':'))+'\n')
 hashes|={n:hashlib.sha256(b.encode()).hexdigest()for n,b in added.items()};record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,statBindings=bindings,validatedStatBindings=19,statIdHeader='source/D2Common/include/D2StatList.h',regressionBasis={'18':37,'17':51,'185':89,'186':60,'187':300},supersedes='de7a3b17 physical binding proof:wrong186/187fixture constants and native reads;old cached source remains frozen',compositions=3276,stages=5,rollsPerStage=18,physicalRollComparisons=294840,masteryControls=160,completeRoutineHashes=hashes,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in(generated,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve())},scope='Complete physical getter/roll,layered mastery readers,stat copies,wield caller,coefficient getters and percentage function compose with30retained original item/stat/hand routines. Current item-type ancestry,throwability,valid raw records and body grid are declared data/services. Ordinary getStats1/initialDamage0;explicit skill range/type and added percent/source share. No elemental/poison/critical/HP/event or learned-actor admission.')
 target=ROOT/'tools/reference/generated-npc-physical-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Generated NPC physical proof stale'
 else:target.write_text(encoded)
 print('Generated NPC physical:3276compositions/294840rolls/160mastery controls;',len(hashes),'complete source routines;19header-verified stat IDs;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
