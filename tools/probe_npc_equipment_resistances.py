#!/usr/bin/env python3
"""Reuse verified unchanged stat graph for original-item resistance pools."""
import hashlib
import json
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-resistances-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def build():
 STAGE.mkdir(parents=True,exist_ok=True)
 prior_path=ROOT/'tools/reference/npc-equipment-defense-source-evidence.json';prior=json.loads(prior_path.read_text())
 cached=ROOT/'.local/class-skills/npc-equipment-defense-source/source.cpp'
 assert prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and sha(cached)==prior['artifacts'][str(cached.relative_to(ROOT))]
 data_path=ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json';data=json.loads(data_path.read_text())
 values_path=ROOT/'tools/reference/item-stat-values-source-evidence.json';assert sha(values_path)==prior['inputs'][str(values_path.relative_to(ROOT))]
 assert sha(data_path)==json.loads(values_path.read_text())['inputs'][str(data_path.relative_to(ROOT))]
 names=['damageresist','magicresist','fireresist','lightresist','coldresist','poisonresist'];ids=[data[n]['lineNumber'] for n in names]
 cpp=cached.read_text().split('int main(){')[0]
 cpp+='\nconst int resistanceIds[]={'+','.join(map(str,ids))+'};\n'
 cpp+=r'''int main(){Init();printf("[\n");bool first=true;
for(int kind:{0,1})for(int channel=0;channel<6;channel++)for(int base:{-100,0,75,95,100,150})for(int flat:{-50,0,20,35,75,100})for(int all:{0,25,75}){
new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;
actor={1,kind,0,&root,nullptr};weapon={2,4,0,&item,nullptr};root.pOwner=&actor;root.dwOwnerType=kind;item.pOwner=&weapon;item.dwOwnerType=4;bonus.dwFlags=0x40;
int stat=resistanceIds[channel];Raw(root.Stats,stat,base);Raw(root.FullStats,stat,base);
for(int c=0;c<6;c++){Raw(bonus.Stats,resistanceIds[c],(c==channel?flat:0)+(c>=2?all:0));}
D2COMMON_10475_PostStatToStatList(&weapon,&bonus,TRUE);STATLIST_MergeStatLists(&actor,&weapon,TRUE);
int local=STATLIST_UnitGetStatValue(&weapon,stat,0),total=STATLIST_UnitGetStatValue(&actor,stat,0);
printf("%s[[%d,%d,%d,%d,%d],[%d,%d]]",first?"":",\n",kind,channel,base,flat,all,local,total);first=false;
}printf("\n]\n");}
'''
 source=STAGE/'source.cpp';source.write_text(cpp)
 run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(STAGE/'probe')],capture_output=True,text=True)
 (STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
 cases=json.loads(run.stdout);assert len(cases)==1296
 for (kind,channel,base,flat,all),(local,total) in cases:
  assert local==flat+(all if channel>=2 else 0) and total==base+local
 (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],cases=len(cases),statIds=dict(zip(names,ids)),
  scope='Verified unchanged older item-stat merge/getter graph reused from the accepted defense probe. All-resistance is supplied as its four declared elemental stat contributions;this does not newly certify the property writer,modern executable,native UO adapter or final mitigation.',
  inputs={str(prior_path.relative_to(ROOT)):sha(prior_path),str(cached.relative_to(ROOT)):sha(cached),str(values_path.relative_to(ROOT)):sha(values_path),str(data_path.relative_to(ROOT)):sha(data_path)},
  reusedCompiledUnchangedRoutineHashes=prior['compiledUnchangedRoutineHashes'],artifacts={str(p.relative_to(ROOT)):sha(p) for p in [source,STAGE/'compiler.log',STAGE/'sanitizer.log',STAGE/'cases.json']})
 (ROOT/'tools/reference/npc-equipment-resistances-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n');print('Source original-item resistance pools:1296contexts pass ASan/UBSan.')
if __name__=='__main__':build()
