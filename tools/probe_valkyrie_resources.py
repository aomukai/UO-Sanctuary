#!/usr/bin/env python3
"""Full original Valkyrie resource composition with unchanged source callbacks."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-resources-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={}
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 proof=json.loads(read(ROOT/'tools/reference/valkyrie-numeric-source-evidence.json'));p=BASE/'valkyrie-numeric-source/source.cpp';assert sha(p)==proof['artifacts'][str(p.relative_to(ROOT))]
 cpp=read(p).split('int main(){Init();',1)[0];numeric=json.loads(read(BASE/'valkyrie-numeric-source/cases.json'))
 parent=json.loads(read(ROOT/'tools/reference/generated-npc-stats-source-evidence.json'));hashes=parent['completeRoutineHashes']|parent['inheritedRoutineHashes']
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 callback=json.loads(read(ROOT/'tools/reference/npc-resource-callbacks-source-evidence.json'));p=BASE/'d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp';assert sha(p)==callback['inputs'][str(p.relative_to(ROOT))];text=read(p)
 branches=text[text.index('    case STAT_MAXHP:'):text.index('    case STAT_ITEM_NONCLASSSKILL:')];assert hashlib.sha256(branches.encode()).hexdigest()==callback['compiledUnchangedResourceBranchesSha256']
 original='trace.push_back({owner?owner->id:0,source?source->id:0,int(packed),old,value});}'
 assert cpp.count(original)==1;cpp=cpp.replace(original,original[:-1]+'if(owner&&owner->dwUnitType==1)ResourceChanged(owner,packed>>16,old,value);}')
 p=BASE/'d2moo-pinned-source/source/D2Common/src/D2StatList.cpp';tree=json.loads((BASE/'d2moo-source-tree.json').read_text());raw=p.read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Common/src/D2StatList.cpp');stat=read(p)
 for n in ['STATLIST_GetTotalStatValue','STATLIST_UnitGetStatValue','STATLIST_SetStat','STATLIST_SetUnitStat']:
  body=function(stat,n);hashes[n]=hashlib.sha256(body.encode()).hexdigest()
  if n in ['STATLIST_SetStat','STATLIST_SetUnitStat']:cpp+=body+'\n'
 cpp+='int D2Clamp(int v,int low,int high){return std::clamp(v,low,high);}\nconstexpr int STAT_HITPOINTS=6,STAT_MANA=8,STAT_STAMINA=10,STAT_HPREGEN=74;\n'
 cpp+='void ResourceChanged(D2UnitStrc*pOwner,int nStatId,int nPreviousValue,int nNewValue){switch(nStatId){\n'+branches+'default:break;}}\n'
 skills=json.loads(read(ROOT/'tools/reference/d2r-named-equipment/json/skills.json'));orders=next(r for r in skills.values()if r.get('skill')=='Battle Orders');assert orders['Param3']==35 and orders['aurastatcalc1']=='ln34'
 monsters=json.loads(read(ROOT/'tools/reference/d2r-named-equipment/json/monstats.json'));valk=next(r for r in monsters.values()if r.get('Id')=='valkyrie');assert valk['DamageRegen']==12
 toggle=[]
 for c in numeric:
  toggle.append(next((i for i,item in enumerate(c['Items'])if any(f['Packed']>>16 in [7,9,11,76,77,162,216,217,242]and f['Value']for f in item['Graph']['Properties'])),0))
 cpp+='int toggles[]={'+','.join(map(str,toggle))+'};\n'
 by={(c['Rank'],c['Difficulty']):dict(c['Basis'])for c in numeric}
 cpp+='int areas[][3][2]={'+','.join('{'+','.join('{'+str(by[(c['Rank'],d)][31<<16])+','+str(by[(c['Rank'],d)][19<<16])+'}'for d in [1,2,0])+'}'for c in numeric)+'};\n'
 cpp+='''
void Snapshot(){printf("[");Array(root.FullStats);printf(",%d,[",STATLIST_GetBaseStat_6FDB6340(&root,74u<<16,&rows[74]));
std::vector<uint32_t>keys;for(int i=0;i<368;i++)keys.push_back(uint32_t(i)<<16);for(int i=0;i<root.FullStats.nStatCount;i++)keys.push_back(root.FullStats.pStat[i].nPackedValue);std::sort(keys.begin(),keys.end());keys.erase(std::unique(keys.begin(),keys.end()),keys.end());bool comma=false;
for(auto key:keys){int value=STATLIST_GetTotalStat_6FDB63E0(&root,key,&rows[key>>16]);if(value){printf("%s[%u,%d]",comma?",":"",key,value);comma=true;}}printf("]]");}
int main(){Init();gate=false;playerBody=false;hasBase=hasGem=true;mainWeapon=nullptr;bool comma=false;printf("[");int index=0;
for(auto c:cases){for(int variant=0;variant<4;variant++){
new(&root)D2StatListStrc;actor={};actor.id=1;actor.dwUnitType=1;actor.pStatListEx=&root;root.pOwner=&actor;root.dwOwnerType=1;
D2MonStatsTxt monster;monster.dwDamageRegen=12;MonsterData md;md.pMonstatsTxt=&monster;actor.pMonsterData=&md;
for(auto f:c.basis){Raw(root.Stats,f[0],f[1]);Raw(root.FullStats,f[0],f[1]);}
int maximum=STATLIST_UnitGetStatValue(&actor,7,0),life=variant==0?maximum:variant==1?maximum/3+17:variant==2?1:maximum-257;
int mana=variant==1?34567:variant==2?1:0,stamina=variant==1?12799:variant==2?1:0,mm=(variant==1||variant==2)?76800:0,ms=(variant==1||variant==2)?25600:0,regen=variant==1?17:variant==2?0:300;
for(auto f:{std::array{6,life},std::array{8,mana},std::array{9,mm},std::array{10,stamina},std::array{11,ms},std::array{74,regen}}){Raw(root.Stats,uint32_t(f[0])<<16,f[1]);Raw(root.FullStats,uint32_t(f[0])<<16,f[1]);}
root.pfOnValueChanged=Changed;int count=c.items.size();for(int i=0;i<count;i++){
auto input=c.items[i];new(&nodes[i])D2StatListStrc;new(&props[i])D2StatListStrc;units[i]={};auto&u=units[i];u.id=2+i;u.dwUnitType=4;u.pStatListEx=&nodes[i];u.body=input.body;u.quality=input.quality;u.weapon=input.weapon;nodes[i].pOwner=&u;nodes[i].pfOnValueChanged=Changed;props[i].dwFlags=0x40;
for(auto f:input.base){Raw(nodes[i].Stats,f[0],f[1]);Raw(nodes[i].FullStats,f[0],f[1]);}for(auto f:input.props)Raw(props[i].Stats,f[0],f[1]);if(input.has)D2COMMON_10475_PostStatToStatList(&u,&props[i],TRUE);
int durability=STATLIST_GetTotalStat_6FDB63E0(&nodes[i],73u<<16,&rows[73]);STATLIST_SetBaseStat(&nodes[i],72,durability,0,&u);D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&u,&actor,0);}
printf("%s[",comma?",":"");comma=true;Snapshot();
for(int s:{77,76,162})STATLIST_SetBaseStat(&root,s,35,0,&actor);printf(",");Snapshot();
for(int s:{77,76,162})STATLIST_SetBaseStat(&root,s,0,0,&actor);printf(",");Snapshot();
int chosen=toggles[index];D2GAME_ITEMS_UpdateTransferredProperties_6FC424E0(nullptr,&units[chosen],&actor,1,1);printf(",");Snapshot();
D2GAME_ITEMS_UpdateItemStatlist_6FC42310(nullptr,&units[chosen],&actor,0);printf(",");Snapshot();
for(auto area:areas[index]){STATLIST_SetBaseStat(&root,31,area[0],0,&actor);STATLIST_SetBaseStat(&root,19,area[1],0,&actor);printf(",");Snapshot();}printf("]");
}index++;}printf("]\\n");}
'''
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 outputs=json.loads(run.stdout);assert len(outputs)==228;result=[]
 for i,c in enumerate(numeric):
  maximum=dict(c['Basis'])[7<<16]
  for variant in range(4):
   life=maximum if variant==0 else maximum//3+17 if variant==1 else 1 if variant==2 else maximum-257
   result.append(dict(Case=i,Variant=variant,Rank=c['Rank'],Difficulty=c['Difficulty'],Life=life,LifeMaximum=maximum,
    Mana=34567 if variant==1 else 1 if variant==2 else 0,ManaMaximum=76800 if variant in [1,2]else 0,
    Stamina=12799 if variant==1 else 1 if variant==2 else 0,StaminaMaximum=25600 if variant in [1,2]else 0,
    RegenerationBase=17 if variant==1 else 0 if variant==2 else 300,Toggle=toggle[i],Stages=outputs[i*4+variant]))
 out=STAGE/'cases.json';out.write_text(json.dumps(result,separators=(',',':'))+'\n')
 for n,h in hashes.items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 receipt=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=228,stagesPerContext=8,originalItemReplays=1320,completeRoutineHashes=hashes,
  compiledUnchangedResourceBranchesSha256=callback['compiledUnchangedResourceBranchesSha256'],inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [source,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]},
  scope='57full original loadouts x4mandatory supplied resource contexts. Complete older numeric/update routines and three unchanged callback branches cover all368scalar readers plus occupied layers at birth,Orders35,clear,source item removal/reattachment and three captured-base area transitions. Zero/fractional/tiny pools and regeneration bases are explicit fixtures,not modern initializer defaults. Other skill/event/rate/world consumers remain unadmitted.')
 (ROOT/'tools/reference/valkyrie-resources-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n')
 print('Valkyrie resource source:228unfiltered compositions/1824stages,1320original item replays;30complete routines and3unchanged callback branches;ASan/UBSan clean.')
if __name__=='__main__':main()
