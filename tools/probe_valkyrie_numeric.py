#!/usr/bin/env python3
"""Replay full original Valkyrie items through the pinned older stat engine."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-numeric-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={};tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 def source(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);return read(p)
 prior=json.loads(read(ROOT/'tools/reference/generated-npc-stats-source-evidence.json'));p=BASE/'generated-npc-stats-source/source.cpp';assert sha(p)==prior['artifacts'][str(p.relative_to(ROOT))]
 cpp=read(p).split('int controls[][8]={',1)[0];hashes={}
 for name,h in prior['completeRoutineHashes'].items():
  assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
  hashes[name]=h
 stat=source('source/D2Common/src/D2StatList.cpp')
 for n in ['STATLIST_GetTotalStatValue','STATLIST_UnitGetStatValue','STATLIST_UnitGetItemStatOrSkillStatValue']:
  body=function(stat,n);hashes[n]=hashlib.sha256(body.encode()).hexdigest();cpp+=body+'\n'
 costs=json.loads(read(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json'));ids={r['Name']:r['Id']for r in costs}
 required={'STAT_DEXTERITY':'dexterity','STAT_ARMORCLASS':'armorclass','STAT_ITEM_ARMOR_PERCENT':'item_armor_percent','STAT_SKILL_ARMOR_PERCENT':'skill_armor_percent',
  'STAT_MODIFIERLIST_SKILL':'modifierlist_skill','STAT_MODIFIERLIST_LEVEL':'modifierlist_level','STAT_ARMOR_OVERRIDE_PERCENT':'armor_override_percent'}
 cpp+='\n#include <cstdlib>\n'+''.join(f'constexpr int {n}={ids[k]};\n'for n,k in required.items())
 cpp+='''
constexpr int STATE_HOLYSHIELD=100;
struct D2SkillsTxt{int dwCalc[1];};
// Holy Shield is absent in this prepared recipient scope. Every nested
// service of that branch refuses instead of inventing a shield/skill.
D2StatListStrc*STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){std::abort();}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){std::abort();}
D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int){std::abort();}
void INVENTORY_GetEquippedShield(void*,D2UnitStrc**){std::abort();}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){std::abort();}
'''
 defense=function(source('source/D2Common/src/Units/Units.cpp'),'UNITS_GetDefense');hashes['UNITS_GetDefense']=hashlib.sha256(defense.encode()).hexdigest();cpp+=defense+'\n'
 # Capture the complete older NPC accuracy routine as a source boundary;
 # its flat five-Dexterity branch is inspected, not a current-engine proof.
 attack=function(source('source/D2Game/src/UNIT/SUnitDmg.cpp'),'SUNITDMG_IsHitSuccessful')
 assert 'nAttackRate = nStatValue + 5 * STATLIST_UnitGetStatValue(pAttacker, STAT_DEXTERITY, 0) + STATLIST_UnitGetStatValue(pAttacker, STAT_TOHIT, 0);'in attack
 (STAGE/'inspected-accuracy.cpp').write_text(attack+'\n')
 records=json.loads(read(BASE/'valkyrie-loadout/valkyrie-equipment-fixtures.json'))['Bodies'];levels=json.loads(read(ROOT/'data/Sanctuary/Balance/summon-level-stats.json'))
 skills=json.loads(read(ROOT/'tools/reference/d2r-named-equipment/json/skills.json'));row=skills['32'];assert [row['Param'+str(i)]for i in range(1,9)]==[20,25,10,12,25,3,2,20]
 monsters=json.loads(read(ROOT/'tools/reference/d2r-named-equipment/json/monstats.json'));monster=next(r for r in monsters.values()if r.get('Id')=='valkyrie');assert monster['noRatio']==1
 cases=[]
 for held in records:
  s=json.loads(held['Text']);b=s['Birth'];rank=b['Rank'];d=s['Difficulty'];level=min(b['OwnerLevel'],max(1,rank+3*b['OwnerLevel']//4));assert b['Expansion']
  basis={'level':level,'maxhp':b['SpawnLife256']+b['SpawnLife256']*(20*(rank-1)+20*b['Decoy'])//100,
   'armorclass':monster[['AC','AC(N)','AC(H)'][d]]+levels[level]['expansionDefense'][d],'tohit':levels[level]['expansionAttack'][d],
   'strength':rank*25,'dexterity':rank*12,'item_armor_percent':10*(rank-1)}
  # Blank current skill ToHit fields are zero. All captured sunder fields
  # in these controlled originals are zero and remain body-gated otherwise.
  assert not row.get('ToHit')and not row.get('LevToHit')and not any(b[k]for k in ['ColdSunder','FireSunder','LightningSunder','PoisonSunder','PhysicalSunder'])
  for name,key in [('damageresist','ResDm'),('magicresist','ResMa'),('fireresist','ResFi'),('lightresist','ResLi'),('coldresist','ResCo'),('poisonresist','ResPo')]:
   basis[name]=monster.get(key+['','(N)','(H)'][d],0)+(min((rank+b['Decoy'])*2,85)if key in ['ResFi','ResLi','ResCo','ResPo']else 0)
  items=[]
  for text in held['Payloads']:
   item=json.loads(text);assert not item['Graph']['Ethereal'];items.append(item)
  cases.append(dict(Rank=rank,Difficulty=d,Basis=sorted([[ids[k]<<16,v]for k,v in basis.items()if v]),Items=items))
 def pairs(a):return '{'+','.join('{'+str(k)+','+str(v)+'}'for k,v in a)+'}'
 cpp+='''
struct ItemInput{int body,quality;bool weapon,has;std::vector<std::array<int64_t,2>>base,props;};
struct Case{std::vector<std::array<int64_t,2>>basis;std::vector<ItemInput>items;};
Case cases[]={
'''
 for c in cases:
  items=[]
  for i in c['Items']:
   g=i['Graph'];base=[[f['Packed'],f['Value']]for f in g['ItemBase']];props=[[f['Packed'],f['Value']]for f in g['Properties']]
   items.append('{'+f"{i['Equipment']['Body']},{i['Metadata']['Quality']},{int(i['Equipment']['Body']==4)},{int(g['HasProperties'])},"+pairs(base)+','+pairs(props)+'}')
  cpp+='{'+pairs(c['Basis'])+',{'+','.join(items)+'}},\n'
 cpp+='''};
D2StatListStrc root,nodes[10],props[10];D2UnitStrc actor,units[10];
void Raw(D2StatsArrayStrc&a,uint32_t packed,int value){if(value)STATLIST_GetOrInsertStat(nullptr,&a,packed)->nValue=value;}
void Array(D2StatsArrayStrc&a){printf("[");for(int i=0;i<a.nStatCount;i++)printf("%s[%u,%d]",i?",":"",a.pStat[i].nPackedValue,a.pStat[i].nValue);printf("]");}
int main(){Init();gate=false;playerBody=false;mainWeapon=nullptr;printf("[");bool comma=false;
for(auto c:cases){new(&root)D2StatListStrc;actor={};actor.id=1;actor.dwUnitType=1;actor.pStatListEx=&root;root.pOwner=&actor;root.dwOwnerType=1;root.pfOnValueChanged=Changed;
for(auto f:c.basis){Raw(root.Stats,f[0],f[1]);Raw(root.FullStats,f[0],f[1]);}
int count=c.items.size();for(int i=0;i<count;i++){auto input=c.items[i];new(&nodes[i])D2StatListStrc;new(&props[i])D2StatListStrc;units[i]={};auto&u=units[i];u.id=2+i;u.dwUnitType=4;u.pStatListEx=&nodes[i];u.body=input.body;u.quality=input.quality;u.weapon=input.weapon;nodes[i].pOwner=&u;nodes[i].pfOnValueChanged=Changed;props[i].dwFlags=0x40;
for(auto f:input.base){Raw(nodes[i].Stats,f[0],f[1]);Raw(nodes[i].FullStats,f[0],f[1]);}for(auto f:input.props)Raw(props[i].Stats,f[0],f[1]);if(input.has)D2COMMON_10475_PostStatToStatList(&u,&props[i],TRUE);
int maximum=STATLIST_GetTotalStat_6FDB63E0(&nodes[i],73u<<16,&rows[73]);STATLIST_SetBaseStat(&nodes[i],72,maximum,0,&u);D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&u,&actor,0);}
printf("%s[",comma?",":"");comma=true;Array(root.FullStats);printf(",%d,",UNITS_GetDefense(&actor));STATLIST_SetBaseStat(&root,STAT_SKILL_ARMOR_PERCENT,100,0,&actor);printf("%d,",UNITS_GetDefense(&actor));STATLIST_SetBaseStat(&root,STAT_SKILL_ARMOR_PERCENT,0,0,&actor);STATLIST_SetBaseStat(&root,STAT_ARMORCLASS,STATLIST_GetBaseStat_6FDB6340(&root,uint32_t(STAT_ARMORCLASS)<<16,&rows[STAT_ARMORCLASS])-90,0,&actor);printf("%d]",UNITS_GetDefense(&actor));
}printf("]\\n");}
'''
 sourcefile=STAGE/'source.cpp';sourcefile.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(sourcefile),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=json.loads(run.stdout);assert len(output)==57
 for c,o in zip(cases,output):c['Stats']=o[0];c['Defense']=o[1];c['Shout100']=o[2];c['InnerSight90']=o[3]
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=57,originalItems=sum(len(c['Items'])for c in cases),completeRoutineHashes=hashes,
  inspectedAccuracySha256=hashlib.sha256(attack.encode()).hexdigest(),inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [sourcefile,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log',STAGE/'inspected-accuracy.cpp',Path(__file__).resolve()]},
  scope='Complete retained older item/stat routines and complete defense reader replay every raw original layer with independently calculated pinned birth bases. Three defense contexts each. Holy Shield absent;its nested services abort. NPC five-Dexterity accuracy branch inspected only. Resource,skill,event,rate,world AI and current executable parity not admitted.')
 (ROOT/'tools/reference/valkyrie-numeric-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
 print('Valkyrie numeric source:57complete unfiltered loadouts/330original items;171defense contexts;ASan/UBSan clean.')
if __name__=='__main__':main()
