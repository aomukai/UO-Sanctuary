#!/usr/bin/env python3
"""Compose original hand selectors/entry/restore with actual generated item packets."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'generated-npc-weapons-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);inputs={}
 def load(rel):
  p=ROOT/rel;inputs[rel]=sha(p);return json.loads(p.read_text())
 prior=load('tools/reference/generated-npc-stats-source-evidence.json');old=load('tools/reference/dragon-talon-hands-source-evidence.json');tree=load('.local/class-skills/d2moo-source-tree.json')
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 p=BASE/'generated-npc-stats-source/source.cpp';assert sha(p)==prior['artifacts'][str(p.relative_to(ROOT))];inputs[str(p.relative_to(ROOT))]=sha(p)
 cpp=p.read_text().split('int main(){Init();',1)[0];data=load('.local/class-skills/generated-npc-stats-source/cases.json');assert sha(BASE/'generated-npc-stats-source/cases.json')==prior['artifacts']['.local/class-skills/generated-npc-stats-source/cases.json']
 hashes=prior['completeRoutineHashes']|prior['inheritedRoutineHashes']
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 player=source('source/D2Game/src/PLAYER/Player.cpp');items=source('source/D2Common/src/Items/Items.cpp');header=source('source/D2Common/include/D2Items.h')
 assert 'IFLAG_NOEQUIP = 0x00004000'in header
 added={n:function(player,n)for n in('sub_6FC7C7B0','sub_6FC7C900','sub_6FC7CA10')};added['ITEMS_CanBeEquipped']=function(items,'ITEMS_CanBeEquipped')
 for n in('sub_6FC7C7B0','sub_6FC7C900'):assert hashlib.sha256(added[n].encode()).hexdigest()==old['compiledRoutines'][n]
 cpp=cpp.replace('D2UnitStrc*INVENTORY_GetLeftHandWeapon(void*){C(5);return mainWeapon;}','D2UnitStrc*INVENTORY_GetLeftHandWeapon(void*);')
 cpp=cpp.replace('struct D2UnitStrc;','struct D2ItemDataStrc{unsigned dwItemFlags=0;};\nstruct D2UnitStrc;')
 cpp=cpp.replace('bool gem=false,rune=false,weapon=false;','bool gem=false,rune=false,weapon=false;D2ItemDataStrc*pItemData=nullptr;unsigned dwSeqFrame=0;')
 original='bool ITEMS_CheckItemTypeId(D2UnitStrc*p,int type){return type==ITEMTYPE_GEM?p->gem:type==ITEMTYPE_RUNE?p->rune:type==ITEMTYPE_WEAPON?p->weapon:false;}'
 assert original in cpp;cpp=cpp.replace(original,original.replace('return type==','return p&&(type==').replace(':false;}',':false);}'))
 cpp+=r'''
constexpr int IFLAG_NOEQUIP=0x4000;
BOOL ITEMS_CanBeEquipped(D2UnitStrc*);
struct D2SkillsTxt{int nWeapSel=0;}usedRow;struct D2SkillStrc{}usedSkill;
bool hasSkill=true,hasRow=true;unsigned skillFlags=0;D2UnitStrc*primary=nullptr,*secondary=nullptr;int itemCount=0;
D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&usedSkill:nullptr;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return hasRow?&usedRow:nullptr;}
unsigned SKILLS_GetFlags(D2SkillStrc*){return skillFlags;}
void INVENTORY_GetEquippedWeapon(void*,D2UnitStrc**weapon,int*body,int*left){*weapon=primary;*body=4;*left=0;}
D2UnitStrc*INVENTORY_GetLeftHandWeapon(void*){return primary;}D2UnitStrc*INVENTORY_GetSecondaryWeapon(void*){return secondary;}
D2UnitStrc*INVENTORY_GetCompositItem(void*,int part){for(int i=0;i<itemCount;i++)if(units[i].body==(part==5?4:5))return&units[i];return nullptr;}
'''+ '\n'.join(added.values())+'\n'
 cases=[]
 for n,c in enumerate(data['numericInputs']):
  weapons=[i+1 for i,k in enumerate(c['Items'])if data['items'][k]['Weapon']and data['items'][k]['Body']in(4,5)]
  def row(select,frame,flags,reset,flip,used=True,valid=True):
   pair=list(reversed(weapons))if flip else weapons
   return dict(Case=n,Primary=pair[0]if pair else 0,Secondary=pair[1]if len(pair)>1 else 0,Selection=select,Frame=frame,Flags=flags,Reset=reset,Skill=used,Row=valid)
  if n<1548:cases.append(row([0,1,2,3,4,7][n%6],(n%2)*256,0x2000 if n%4==0 else 0,n%2,False,n%11!=0,n%13!=0))
  else:
   for select in(0,1,2,3,4,7):
    for frame in(0,256):
     for flags in(0,0x2000):
      for reset in(0,1):
       for flip in(0,1):cases.append(row(select,frame,flags,reset,flip))
 assert len(cases)==3276
 cpp+='struct HandCase{int index,primary,secondary,select,frame,flags,reset;bool used,row;};\nHandCase hands[]={\n'+''.join('{'+','.join(str(int(a[k]))for k in('Case','Primary','Secondary','Selection','Frame','Flags','Reset','Skill','Row'))+'},\n'for a in cases)+'};\n'
 cpp+=r'''
void SelectedSnapshot(){auto p=sub_6FC7C7B0(&actor);printf("[%d,",p?p->id:0);Snapshot(itemCount);printf("]");}
int main(){Init();bool comma=false;printf("{\"equippable\":[");
for(int present:{0,1})for(int kind:{0,1,4})for(int data:{0,1})for(int flags:{0,0x100,0x4000,0x4100,0x1010}){D2UnitStrc u;D2ItemDataStrc d;u.dwUnitType=kind;d.dwItemFlags=flags;u.pItemData=data?&d:nullptr;printf("%s[%d,%d,%d,%d,%d]",comma?",":"",present,kind,data,flags,ITEMS_CanBeEquipped(present?&u:nullptr));comma=true;}
printf("],\"numeric\":[");comma=false;gate=false;playerBody=false;hasBase=hasGem=true;mainWeapon=nullptr;
for(auto h:hands){auto c=numeric[h.index];new(&root)D2StatListStrc;actor={};actor.id=1;actor.dwUnitType=1;actor.dwClassId=c.kind;actor.pStatListEx=&root;actor.pDrlgAct=&minute;actor.pDynamicPath=&actor;actor.pInventory=&actor;actor.dwSeqFrame=h.frame;root.pOwner=&actor;root.dwOwnerType=1;root.pfOnValueChanged=Changed;minute=c.clock;
for(auto a:{std::array{0,100},std::array{1,20},std::array{2,50},std::array{3,30},std::array{7,256000},std::array{9,76800},std::array{11,25600},std::array{12,c.level},std::array{21,7},std::array{22,14},std::array{31,80}}){Raw(root.Stats,uint32_t(a[0])<<16,a[1]);Raw(root.FullStats,uint32_t(a[0])<<16,a[1]);}
trace.clear();calls.clear();itemCount=c.items.size();D2ItemDataStrc flags[10];for(int i=0;i<itemCount;i++){auto input=inputs[c.items[i]];new(&nodes[i])D2StatListStrc;new(&props[i])D2StatListStrc;units[i]={};auto&u=units[i];u.id=2+i;u.dwUnitType=4;u.pStatListEx=&nodes[i];u.pDrlgAct=&minute;u.body=input.body;u.quality=input.quality;u.weapon=input.weapon;u.pItemData=&flags[i];flags[i].dwItemFlags=0x1010;nodes[i].pOwner=&u;nodes[i].pfOnValueChanged=Changed;props[i].dwFlags=0x40;for(auto p:input.base){Raw(nodes[i].Stats,p[0],p[1]);Raw(nodes[i].FullStats,p[0],p[1]);}for(auto p:input.props)Raw(props[i].Stats,p[0],p[1]);if(input.has)D2COMMON_10475_PostStatToStatList(&u,&props[i],TRUE);int maximum=STATLIST_GetTotalStat_6FDB63E0(&nodes[i],73u<<16,&rows[73]);STATLIST_SetBaseStat(&nodes[i],72,maximum,0,&u);D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&u,&actor,0);}
primary=h.primary?&units[h.primary-1]:nullptr;secondary=h.secondary?&units[h.secondary-1]:nullptr;usedRow.nWeapSel=h.select;hasSkill=h.used;hasRow=h.row;skillFlags=h.flags;
printf("%s[[",comma?",":"");comma=true;SelectedSnapshot();sub_6FC7C900(&actor,h.reset);printf(",");SelectedSnapshot();sub_6FC7C900(&actor,h.reset);printf(",");SelectedSnapshot();sub_6FC7CA10(&actor,h.reset);printf(",");SelectedSnapshot();usedRow.nWeapSel=h.select==0?1:0;actor.dwSeqFrame+=256;sub_6FC7C900(&actor,h.reset);printf(",");SelectedSnapshot();printf("],");Trace();printf("]");}printf("]}\n");}
'''
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 for n,b in added.items():assert function(cpp,n)==b,n
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 result=json.loads(run.stdout);assert len(result['numeric'])==3276 and len(result['equippable'])==60
 out=STAGE/'cases.json';out.write_text(json.dumps(dict(numericInputs=data['numericInputs'],hands=cases,numericOutputs=result['numeric'],equippable=result['equippable']),separators=(',',':'))+'\n')
 hashes|={n:hashlib.sha256(b.encode()).hexdigest()for n,b in added.items()}
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,numericCompositions=3276,multiItemCompositions=1728,completeRoutineHashes=hashes,equippableControls=60,reusedSelectorControls=12288,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in(generated,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve())},scope='Complete older selector,damage-list entry/primary restore and item equip predicate compose with26complete retained item/stat routines. Physical primary/secondary identity,used-skill/row,flags and phase are explicit services. Four transitions compare raw arrays,topology,flags and callback order. No selected damage roll,live combat consumer,learned actor or modern Warlock player hand policy is claimed.')
 target=ROOT/'tools/reference/generated-npc-weapons-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Generated NPC weapons source evidence stale'
 else:target.write_text(encoded)
 print('Generated NPC weapons:3276source compositions/1728multi-item;30complete routines;60equip controls;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
