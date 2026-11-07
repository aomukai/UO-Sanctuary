#!/usr/bin/env python3
"""Execute full source proc callbacks over actual original stat/event frames."""
import hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-original-procs-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={}
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 parent=json.loads(read(ROOT/'tools/reference/item-proc-callbacks-source-evidence.json'));p=BASE/'item-proc-callbacks-source/source.cpp';assert sha(p)==parent['artifacts'][str(p.relative_to(ROOT))];cpp=read(p).split('int main(){for(int i=0;i<32;i++)',1)[0]
 def seed(low):return(666<<32)|low
 def roll(s):return ((s&0xffffffff)*0x6ac690c5+(s>>32))&0xffffffffffffffff
 lows=[next(i for i in range(1,10000)if (roll(seed(i))&0xffffffff)%100==wanted)for wanted in [0,5,50,99]]
 events=json.loads(read(BASE/'npc-original-events-source/cases.json'));numeric=json.loads(read(BASE/'valkyrie-resources-source/cases.json'));numeric=[r for r in numeric if r['Variant']==0];cases=[]
 cpp+='int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool comma=false;D2GameStrc g;D2DynamicPathStrc path;D2UnitStrc caster,other;caster.tag=1;caster.pDynamicPath=&path;other.tag=2;exists=true;record.dwFlags[0]=0;tables.nStuff=6;tables.nShiftedStuff=63;D2DamageStrc damage{};\n'
 for index,(ev,num)in enumerate(zip(events,numeric)):
  values=dict(num['Stages'][0][2]);nodes=ev['States'][0][1]
  for phase in sorted({n[1]for n in nodes if n[4]in[20,21]}):
   active=[n for n in nodes if n[1]==phase];assert all(n[4]in[20,21]for n in active)
   for variant in range(3):
    for low in lows:
     for allocation in [0,1]:
      inp=dict(Case=index,Phase=phase,Variant=variant,Seed=str(seed(low)),Allocate=allocation,Nodes=active)
      cases.append(inp);cpp+='if(comma)printf(",");comma=true;caster.pSeed.lSeed='+str(seed(low))+'ULL;damage=D2DamageStrc{'+('32,4'if variant==0 else'0,0')+'};castResult='+str(allocation)+';printf("[");\n'
      for ordinal,n in enumerate(active):
       fn=n[4];key=n[2];cpp+='chance='+str(values[key])+';trace.clear();{int result='+('SKILLITEM_EventFunc20_SkillOnAttackHitKill'if fn==20 else'SKILLITEM_EventFunc21_SkillOnGetHit')+'(&g,0,&caster,&other,'+('nullptr'if variant==2 else'&damage')+','+str(key)+',0);'
       cpp+=('printf(",");'if ordinal else'')+'printf("[%d,\\\"%llu\\\",[",result,(unsigned long long)caster.pSeed.lSeed);for(size_t j=0;j<trace.size();j++){printf("%s[",j?",":"");for(size_t k=0;k<trace[j].size();k++)printf("%s%lld",k?",":"",(long long)trace[j][k]);printf("]");}printf("]]");}\n'
      cpp+='printf("]");\n'
 cpp+='printf("]\\n");}\n'
 for n,h in parent['completeRoutineHashes'].items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wno-literal-suffix','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 r=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-1500:];outputs=json.loads(r.stdout);assert len(outputs)==len(cases)
 for c,o in zip(cases,outputs):c['Results']=o
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n');receipt=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),loadouts=len({c['Case']for c in cases}),sourceRoutines=parent['completeRoutineHashes'],inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',out,Path(__file__).resolve()]},scope='Actual original graph phase/head/layer/chance frames feed complete older callbacks and LCG. Three packet contexts,four boundary-roll seeds and success/failure cast services preserve result,per-node seed and ordered chance/RNG/lookup/cast services. World emission and allocation remain separate native checks.')
 (ROOT/'tools/reference/npc-original-procs-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print(f'Original NPC proc source:{len(cases)}contexts/{receipt["loadouts"]}unfiltered proc loadouts;complete callbacks/seed routines;ASan/UBSan clean.')
if __name__=='__main__':main()
