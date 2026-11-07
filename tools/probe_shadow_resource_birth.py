#!/usr/bin/env python3
"""Execute unchanged resource branches with Shadow birth HP and both regen rows."""
import hashlib,json,subprocess
from pathlib import Path
from probe_valkyrie_initializer import routine
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-resource-birth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True)
 proof=ROOT/'tools/reference/npc-resource-callbacks-source-evidence.json';parent=json.loads(proof.read_text())
 for group in ['inputs','artifacts']:
  for n,h in parent[group].items():assert sha(ROOT/n)==h,n
 source=BASE/'d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp';text=source.read_text()
 branch=text[text.index('    case STAT_MAXHP:'):text.index('    case STAT_ITEM_NONCLASSSKILL:')];assert hashlib.sha256(branch.encode()).hexdigest()==parent['compiledUnchangedResourceBranchesSha256']
 prefix=(BASE/'battle-orders-source-probe.cpp').read_text().split('int main(){')[0];assert branch in prefix
 monster_source=BASE/'d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterUnique.cpp';raw=monster_source.read_bytes();tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==parent['legacyCommit']
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']=='source/D2Game/src/MONSTER/MonsterUnique.cpp')
 percentage=routine(raw.decode(),'MONSTERUNIQUE_CalculatePercentage')
 prefix=prefix.replace('int MONSTERUNIQUE_CalculatePercentage(int v,int p,int b){return int(int64_t(v)*p/b);}',percentage)
 assert prefix.count('MONSTERUNIQUE_CalculatePercentage(')==2
 cpp=prefix+r'''
int main(){bool first=true;printf("[");for(int monster:{417,418})for(int rank=1;rank<=255;rank++){
D2UnitStrc actor;MonsterData md;actor.dwUnitType=UNIT_MONSTER;actor.pMonsterData=&md;actor.hp=96256;actor.maxhp=96256;
monrow.dwDamageRegen=monster==417?3:2;actor.regen=(96256*monrow.dwDamageRegen)>>12;writes=0;
int next=96256+MONSTERUNIQUE_CalculatePercentage(96256,15*(rank-1),100);Changed(&actor,STAT_MAXHP,96256,next);
printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",",monster,rank,next,actor.hp,actor.regen,writes);first=false;
}printf("]\n");}
'''
 code=STAGE/'source.cpp';code.write_text(cpp)
 with (STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(code),'-o',str(STAGE/'probe')],stdout=log,stderr=log,check=True)
 with (STAGE/'sanitizers.log').open('w')as log:run=subprocess.run([str(STAGE/'probe')],stdout=subprocess.PIPE,stderr=log,check=True)
 assert not(STAGE/'sanitizers.log').read_text();cases=json.loads(run.stdout);assert len(cases)==510
 for monster,rank,maximum,life,regen,writes in cases:
  rate=3 if monster==417 else 2;expected=96256+(96256*15*(rank-1))//100
  assert maximum==life==expected and regen==((96256*rate)>>12 if rank==1 else(rate*(expected>>8))>>4)and writes==(0 if rank==1 else 2)
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 fields=json.loads((ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json').read_text());assert next(r for r in fields if r['Id']==7)['Flags']&2048
 inputs=[Path(__file__).resolve(),proof,source,monster_source,BASE/'d2moo-pinned-source/source/D2Game/src/MONSTER/Monster.cpp',BASE/'d2moo-pinned-source/source/D2Common/src/D2StatList.cpp',ROOT/'tools/reference/shadow-initializer-source-evidence.json',BASE/'battle-orders-source-probe.cpp',ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json',ROOT/'tools/reference/shadow-initializer-data-evidence.json',ROOT/'tools/reference/shadow-current-birth-source-evidence.json']
 result=dict(referenceVersion='3.3.93847',legacyCommit=parent['legacyCommit'],sourceContexts=510,unchangedCallbackBranchesSha256=parent['compiledUnchangedResourceBranchesSha256'],unchangedPercentageSha256=hashlib.sha256(percentage.encode()).hexdigest(),allRanks=[1,255],sourceMaximumHasValueChangedCallback=True,currentSetterAndCallbackParityVerified=False,inputs={str(p.relative_to(ROOT)):sha(p)for p in inputs},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [code,out,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizers.log']},scope=['Complete unchanged older MAXHP/MAXMANA/MAXSTAMINA branch wrapper and percentage function execute with current Shadow376life,15%rank scaling and regen3/2. All255ranks for both species prove full-life rescale,unchanged rank1raw regen and changed maximum regeneration. ASan/UBSan clean.','The old initializer installs the server value callback before summon helpers;current maxhp metadata carries the notification flag. The new resource graph therefore enables the existing callback before current-helper birth writes and original item replay. Reconstruction keeps the saved cache without repeating native setters.'],limits=['No complete current unit-stat setter,outer initializer or callback ABI parity is certified. Declared actor/stat/game services are the same qualified wrapper used by existing resource branch controls. This is source composition and numeric evidence,not actual NPC skill/AI/casting admission.','The broader Valkyrie pre-equipment regeneration recipe should be audited against its own passive helper rather than assumed equivalent to Shadow. No existing Valkyrie birth/resource values are changed in this stage.'])
 (ROOT/'tools/reference/shadow-resource-birth-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
 print('Shadow resource birth:510unchanged whole-branch/percentage contexts;post-birth regeneration for ranks1..255,both species,ASan/UBSan clean;current setter parity remains separate.')
if __name__=='__main__':main()
