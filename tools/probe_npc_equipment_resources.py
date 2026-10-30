#!/usr/bin/env python3
"""Compose real source item-stat attachment and resource maximum getters."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-resources-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def build():
 STAGE.mkdir(parents=True,exist_ok=True)
 proof=ROOT/'tools/reference/npc-equipment-defense-source-evidence.json';prior=json.loads(proof.read_text())
 source=ROOT/'.local/class-skills/npc-equipment-defense-source/source.cpp'
 assert prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and sha(source)==prior['artifacts'][str(source.relative_to(ROOT))]
 cpp=source.read_text().split('int main(){')[0]
 cpp+=r'''int main(){Init();printf("[\n");bool first=true;
const int resources[]={7,9,11},perlevels[]={216,217,242};
for(int kind:{0,1})for(int channel=0;channel<3;channel++)for(int level:{1,47,99})for(int base:{0,25603})for(int flat:{0,7680})for(int coefficient:{0,3})for(int itemPercent:{0,25})for(int skillPercent:{0,50})for(int attribute:{0,20}){
new(&root)D2StatListStrc;new(&item)D2StatListStrc;new(&bonus)D2StatListStrc;trace.clear();
actor={1,kind,0,&root,nullptr};weapon={2,4,0,&item,nullptr};root.pOwner=&actor;root.dwOwnerType=kind;item.pOwner=&weapon;item.dwOwnerType=4;bonus.dwFlags=0x40;
int stat=resources[channel],attr=channel==1?1:3,itemPct=channel==0?76:channel==1?77:162,skillPct=channel==2?163:itemPct;
Raw(root.Stats,stat,base);Raw(root.FullStats,stat,base);Raw(root.Stats,12,level);Raw(root.FullStats,12,level);
Raw(root.Stats,attr,30);Raw(root.FullStats,attr,30);Raw(root.Stats,skillPct,skillPercent);Raw(root.FullStats,skillPct,skillPercent);
Raw(bonus.Stats,stat,flat);Raw(bonus.Stats,perlevels[channel],coefficient<<8);if(channel==2){Raw(root.Stats,itemPct,itemPercent);Raw(root.FullStats,itemPct,itemPercent);}else{Raw(bonus.Stats,itemPct,itemPercent);}
Raw(bonus.Stats,attr,attribute);
sub_6FDB64A0(&root,D2SLayerStatIdStrc::MakeFromStatId(stat).nPackedValue,&rows[stat],nullptr);
D2COMMON_10475_PostStatToStatList(&weapon,&bonus,TRUE);STATLIST_MergeStatLists(&actor,&weapon,TRUE);
int value=STATLIST_UnitGetStatValue(&actor,stat,0);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],%d]",first?"":",\n",kind,channel,level,base,flat,coefficient,itemPercent,skillPercent,attribute,value);first=false;
}printf("\n]\n");}
'''
 p=STAGE/'source.cpp';p.write_text(cpp)
 run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],capture_output=True,text=True)
 (STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
 cases=json.loads(run.stdout);assert len(cases)==1152
 (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],cases=len(cases),
  scope='Reused verified unchanged full item stat graph,attachment and unit stat getter with declared base arrays. Both NPC and player control kinds exercise three resources,declared raw level coefficients,flat bonuses,life/mana item and skill percentages,stamina active/passive percentages and attributes. The stamina raw coefficient is declared in raw units,not certified item birth. Player class coefficients remain declared controls;not complete item birth,current executable or native live-resource acceptance.',
  inputs={str(proof.relative_to(ROOT)):sha(proof),str(source.relative_to(ROOT)):sha(source)},reusedCompiledUnchangedRoutineHashes=prior['compiledUnchangedRoutineHashes'],artifacts={str(x.relative_to(ROOT)):sha(x) for x in [p,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']})
 (ROOT/'tools/reference/npc-equipment-resources-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n')
 print('Source NPC equipment resource composition:1152contexts pass ASan/UBSan.')
 for i,o in cases:
  if i in [[1,0,47,25603,7680,3,25,50,20],[1,1,47,0,7680,3,25,50,20],[1,2,47,25603,7680,3,25,50,20]]:print(i,o)
if __name__=='__main__':build()
