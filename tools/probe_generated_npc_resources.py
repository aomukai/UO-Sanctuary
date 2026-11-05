#!/usr/bin/env python3
"""Compose the three unchanged resource branches with original item stat graphs."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'generated-npc-resources-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 proof=ROOT/'tools/reference/generated-npc-stats-source-evidence.json';prior=json.loads(proof.read_text());inputs={str(proof.relative_to(ROOT)):sha(proof)}
 def inherit(rel,expected):
  p=ROOT/rel;assert sha(p)==expected,rel;inputs[rel]=sha(p);return p.read_text()
 rel='.local/class-skills/generated-npc-stats-source/source.cpp';cpp=inherit(rel,prior['artifacts'][rel])
 rel='.local/class-skills/generated-npc-stats-source/cases.json';data=json.loads(inherit(rel,prior['artifacts'][rel]))
 callbackproof=ROOT/'tools/reference/npc-resource-callbacks-source-evidence.json';callback=json.loads(callbackproof.read_text());inputs[str(callbackproof.relative_to(ROOT))]=sha(callbackproof)
 rel='.local/class-skills/d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp';text=inherit(rel,callback['inputs'][rel])
 branches=text[text.index('    case STAT_MAXHP:'):text.index('    case STAT_ITEM_NONCLASSSKILL:')];assert hashlib.sha256(branches.encode()).hexdigest()==callback['compiledUnchangedResourceBranchesSha256']
 # Keep all 26 original complete functions byte-for-byte. Only fixture main,
 # explicit bases, monster metadata and callback adapter change.
 hashes=prior['completeRoutineHashes']|prior['inheritedRoutineHashes']
 for name,h in hashes.items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
 old='trace.push_back({owner?owner->id:0,source?source->id:0,int(packed),old,value});}'
 assert cpp.count(old)==1;cpp=cpp.replace(old,old[:-1]+'if(owner&&owner->dwUnitType==1)ResourceChanged(owner,packed>>16,old,value);}')
 start=cpp.index('int main(){Init();');loop=cpp[cpp.index('for(auto c:numeric)',start):];cpp=cpp[:start]
 loop=loop.replace('root.pfOnValueChanged=Changed;minute=c.clock;','root.pfOnValueChanged=Changed;minute=c.clock;D2MonStatsTxt monster;monster.dwDamageRegen=variant==3?0:3;MonsterData md;md.pMonstatsTxt=&monster;actor.pMonsterData=&md;')
 old='std::array{7,256000},std::array{9,76800},std::array{11,25600}'
 assert old in loop;loop=loop.replace(old,'std::array{6,variant==1?1:variant==2?0:123457},std::array{7,256000},std::array{8,variant==1?1:variant==2?0:34567},std::array{9,76800},std::array{10,variant==1?1:variant==2?0:12799},std::array{11,25600},std::array{74,17}')
 # The final brace now closes both variant and main loops.
 assert loop.endswith('printf("]}\\n");}\n')
 loop=loop.removesuffix('printf("]}\\n");}\n')+'}printf("]}\\n");}\n'
 cpp+='constexpr int STAT_HITPOINTS=6,STAT_MANA=8,STAT_STAMINA=10,STAT_HPREGEN=74;\nvoid ResourceChanged(D2UnitStrc*pOwner,int nStatId,int nPreviousValue,int nNewValue){switch(nStatId){\n'+branches+'default:break;}}\n'
 cpp+='int main(){Init();bool comma=false;gate=false;playerBody=false;hasBase=hasGem=true;mainWeapon=nullptr;printf("{\\\"numeric\\\":[");for(int variant=0;variant<4;variant++){\n'+loop
 for name,h in hashes.items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
 rel='.local/class-skills/d2moo-pinned-source/source/D2Common/src/D2StatList.cpp'
 p=ROOT/rel;raw=p.read_bytes();tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Common/src/D2StatList.cpp');inputs[rel]=sha(p)
 added={n:function(raw.decode(),n)for n in ('STATLIST_GetTotalStatValue','STATLIST_UnitGetStatValue','STATLIST_SetStat','STATLIST_SetUnitStat')}
 helper='int D2Clamp(int value,int low,int high){return std::clamp(value,low,high);}'+'\n'+'\n'.join(added.values())+'\n'
 cpp=cpp.replace('constexpr int STAT_HITPOINTS=6',helper+'constexpr int STAT_HITPOINTS=6')
 hashes|={n:hashlib.sha256(b.encode()).hexdigest()for n,b in added.items()}
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True,timeout=120);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 result=json.loads(run.stdout);assert len(result['numeric'])==6264
 out=STAGE/'cases.json';out.write_text(json.dumps(dict(items=data['items'],numericInputs=data['numericInputs'],numericOutputs=result['numeric']),separators=(',',':'))+'\n')
 record=dict(referenceVersion='3.3.93847',currentExecutableParity=False,numericCompositions=6264,originalItemShapes=1566,resourceVariants=['fractional injured','one FP256 unit','empty pools','zero monster regeneration'],completeRoutineHashes=hashes,compiledUnchangedResourceBranchesSha256=callback['compiledUnchangedResourceBranchesSha256'],inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in(source,binary,out,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve())},scope='Three unchanged source resource switch branches compose with30complete original stat/item routines and current raw original item packets. Explicit source bases and monster coefficient;four stages:construction,repeat,level change,reverse removal. Other outer callback branches remain observation services,not admitted live skill/event consumers.')
 target=ROOT/'tools/reference/generated-npc-resources-source-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Generated NPC resource evidence stale'
 else:target.write_text(encoded)
 print('Generated NPC resources:6264 source compositions;30 unchanged complete routines plus three unchanged resource branches;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
