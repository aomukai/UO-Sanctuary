#!/usr/bin/env python3
"""Replay native original-item full-fill contexts through the six pinned routines."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(stage,generation=2):
 source=ROOT/'.local/class-skills/npc-damage-fill-source/source.cpp';text=source.read_text().split('int main(){printf("[");')[0]
 # Replace only declared metadata/copy services. All six source bodies must remain exact.
 text=text.replace('bool demon=false,undead=false;', 'std::vector<D2StatStrc> typed;bool demon=false,undead=false;')
 text=text.replace('for(int i=0;i<128;i++)out[i]={i+1,i%3==0?-5:10};return 128;', 'int n=std::min<int>(typed.size(),cap);for(int i=0;i<n;i++)out[i]=typed[i];return n;')
 main=r'''
int main(){int cases=0;assert(scanf("%d",&cases)==1);printf("[");for(int index=0;index<cases;index++){
actor={};target={};weapon={};weapon.dwUnitType=4;actor.dwUnitType=1;target.dwClassId=10000;target.dwUnitType=1;actor.pInventory=&actor;
int args[10],physical[11],critical=0;for(int&n:args)assert(scanf("%d",&n)==1);for(int&n:physical)assert(scanf("%d",&n)==1);assert(scanf("%d",&critical)==1);
int count=0;assert(scanf("%d",&count)==1);for(int i=0;i<count;i++){int stat,value;assert(scanf("%d%d",&stat,&value)==2);actor.stats[stat]=value;}
int originalED=actor.stats[STAT_DAMAGEPERCENT];armed=physical[6];// Selected endpoints use an equivalent declared wield service below.
two=0;actor.stats[STAT_MINDAMAGE]=physical[0];actor.stats[STAT_MAXDAMAGE]=physical[1];
strength=physical[7];dexterity=physical[8];masteryDamage=physical[3]-originalED;masteryCrit=critical;
alignment=args[2]?UNIT_ALIGNMENT_GOOD:0;hire=args[1];edge=args[5];blunt=args[6];mon={args[7],0};targetMon={0,7};demon=args[8];undead=args[9];target.stats[STAT_ARMORCLASS]=20;style=1;draws=0;mutate=0;eventChange=1;overlay=0;trace.clear();
assert(scanf("%d",&count)==1);typed.clear();for(int i=0;i<count;i++){int layer,value;assert(scanf("%d%d",&layer,&value)==2);typed.push_back({layer,value});}
int a[21];for(int&n:a)assert(scanf("%d",&n)==1);D2DamageStrc p;
p.dwPhysDamage=a[0];p.dwFireDamage=a[1];p.dwLtngDamage=a[2];p.dwColdDamage=a[3];p.dwMagDamage=a[4];p.dwPoisDamage=a[5];p.dwColdLen=a[6];p.dwPoisLen=a[7];p.dwStunLen=a[8];p.dwLifeLeech=a[9];p.dwManaLeech=a[10];p.dwStamLeech=a[11];p.dwEnDmgPct=a[12];p.dwHitFlags=a[13];p.wResultFlags=a[14];p.nConvType=a[15];p.dwConvPct=a[16];p.dwFrzLen=a[17];p.dwBurnLen=a[18];p.dwHitClass=a[19];overlay=a[20];
SUNITDMG_FillDamageValues(nullptr,&actor,&target,&p,args[3],args[4]);
printf("%s{\"output\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%u,%d],\"armor\":%d,\"trace\":[",index?",":"",p.dwPhysDamage,p.dwFireDamage,p.dwLtngDamage,p.dwColdDamage,p.dwMagDamage,p.dwPoisDamage,p.dwColdLen,p.dwPoisLen,p.dwStunLen,p.dwLifeLeech,p.dwManaLeech,p.dwStamLeech,p.dwEnDmgPct,p.dwHitFlags,p.wResultFlags,p.nConvType,p.dwConvPct,p.dwFrzLen,p.dwBurnLen,p.dwHitClass,overlay,target.stats[STAT_ARMORCLASS]);
bool comma=false;for(auto t:trace)if(t[0]!=2&&t[0]!=3&&t[0]!=6){printf("%s[%d,%d,%d]",comma?",":"",t[0],t[1],t[2]);comma=true;}printf("]}");}printf("]\n");}
'''
 # The bridge supplies selected physical endpoints and coefficient/mastery metadata
 # already established by the separate 42-routine selected-hand oracle.
 text+=main
 from probe_current_item_affixes import function
 proof=json.loads((ROOT/'tools/reference/npc-damage-fill-source-evidence.json').read_text())
 for n,h in proof['completeRoutineHashes'].items():assert hashlib.sha256(function(text,n).encode()).hexdigest()==h,n
 path=stage/'bridge-source.cpp';path.write_text(text);binary=stage/'bridge-probe'
 with(stage/'bridge-compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(path),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 observed=stage/'npc-damage-fill-fixtures.json';data=json.loads(observed.read_text());rows=data['Bridge'];assert len(rows)==96 and data['Original']['Generation']==generation
 numbers=[len(rows)]
 for r in rows:
  numbers+=r['args']+r['physical']+[r['mastery'],len(r['stats'])]+[v for pair in r['stats']for v in pair]+[len(r['typed'])]+[v for pair in r['typed']for v in pair]+r['initial']
 request=stage/'bridge-input.txt';request.write_text(' '.join(map(str,numbers))+'\n')
 run=subprocess.run([str(binary)],input=request.read_bytes(),capture_output=True,timeout=120);(stage/'bridge-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 output=stage/'bridge-output.json';output.write_bytes(run.stdout);expected=json.loads(run.stdout);assert len(expected)==96
 for i,(r,e)in enumerate(zip(rows,expected)):
  for field in('output','armor','trace'):assert r[field]==e[field],(i,field,r[field],e[field])
 receipt=dict(scope='96native original-item contexts at the recorded native generation;full source caller plus six unchanged routines;selected endpoints,coefficients,mastery and copied typed stats are explicit services from the already separately verified graph. RNG/armor/event/packet comparisons;entry/restore are validated by retained graph checks,overlay metadata is compared but no visual overlay delivered.',contexts=96,generation=generation,currentExecutableParity=False,completeRoutineHashes=proof['completeRoutineHashes'],artifacts={str(p.relative_to(ROOT)):sha(p)for p in(source,path,binary,observed,request,output,stage/'bridge-compiler.log',stage/'bridge-sanitizer.log')})
 (stage/'bridge-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Full fill original-item bridge:96contexts/21packet fields,armor,event and RNG match six full source routines;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('stage',type=Path);p.add_argument('--generation',type=int,default=2);a=p.parse_args();build(a.stage.resolve(),a.generation)
