#!/usr/bin/env python3
"""Review Defiler owned suppliers;keep raw Fog parsing failure distinct from controls."""
import argparse,csv,hashlib,json,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 foundation=json.loads(pin(ROOT/'tools/reference/warlock-foundation-preflight.json'));owned=ROOT/'.local/research/d2r-3.3.93847/skills.txt';assert sha(owned)==foundation['licensedSkillSha256']
 row=next(r for r in csv.DictReader(pin(owned).lstrip('\ufeff').splitlines(),delimiter='\t') if r['skill']=='Summon Defiler')
 exact="par2*((lvl - 1) + (skill('Demonic Mastery'.blvl))";assert row['passivecalc2']==exact and row['*Id']=='377' and row['srvdofunc']=='56' and row['summon']=='warputriddefiler' and row['summode']=='S3'
 assert row['sumskill1']=='Health Link' and row['sumskill2']=='Summon Defiler' and row['sumsk1calc']==row['sumsk2calc']=='lvl'
 goat=json.loads(pin(ROOT/'tools/reference/goatman-source-evidence.json'))
 for name in ('Calc.cpp','Calc.h','D2BasicTypes.h','Fog.h'):
  src=ROOT/'.local/class-skills/tainted-foundation-source'/name;assert sha(src)==goat['artifacts']['.local/class-skills/goatman-foundation/'+name];pin(src);shutil.copy2(src,STAGE/name)
 curves=ROOT/'.local/class-skills/warlock-foundation/skillcalc.txt';rows=list(csv.DictReader(pin(curves).splitlines(),delimiter='\t'))
 assert [(i,r['code']) for i,r in enumerate(rows) if r['code'] in ('par2','lvl','blvl')]==[(9,'par2'),(16,'lvl'),(41,'blvl')]
 cpp=r'''#include "Calc.h"
#include <cstdio>
int rank,hard,calls;
int keyword(char*name){assert(!strcmp(name,"skill"));return 3;}
int parameters(int key){assert(key==3);return 2;}
int link(char*name,BOOL*constant,int ast,int key){if(!strcmp(name,"Demonic Mastery")){assert(ast==1&&key==3);*constant=1;return 374;}*constant=0;if(!strcmp(name,"par2"))return 9;if(!strcmp(name,"lvl"))return 16;if(!strcmp(name,"blvl"))return 41;assert(false);return -1;}
int parent(int index,void*){assert(index==9||index==16);return index==9?10:rank;}
int skill(int id,int property,int extra,void*){assert(id==374&&property==41&&extra==0);calls++;return hard;}
int main(){D2CalcCallbackInfoStrc table[4]={};table[3]={skill,2};bool first=true;printf("[");
for(int variant=0;variant<2;variant++)for(rank=1;rank<=255;rank++)for(hard=0;hard<=20;hard++){
 calls=0;FOGASTNodeStrc buffer[1024]={};const char*text=variant?"(par2*((lvl - 1) + (skill('Demonic Mastery'.blvl))))":"(par2*((lvl - 1) + (skill('Demonic Mastery'.blvl)))";
 int size=DATATBLS_CompileExpression(text,buffer,1024,keyword,parameters,link);int result=size?DATATBLS_CalcEvaluateExpression(buffer,size,parent,table,4,nullptr):0;
 printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",",variant,rank,hard,size,result,calls);first=false;}printf("]\n");}
'''
 p=STAGE/'formula.cpp';p.write_text(cpp);binary=STAGE/'formula';compiler=STAGE/'compiler.log'
 result=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize=alignment','-fno-sanitize-recover=all','-I'+str(STAGE),str(p),str(STAGE/'Calc.cpp'),'-o',str(binary)],capture_output=True);compiler.write_bytes(result.stdout+result.stderr);assert result.returncode==0
 result=subprocess.run([str(binary)],capture_output=True);sanitizer=STAGE/'sanitizer.log';sanitizer.write_bytes(result.stderr);assert result.returncode==0 and not result.stderr
 cases=json.loads(result.stdout);assert len(cases)==10710
 for variant,rank,hard,size,value,calls in cases:assert size>0 and value==10*(rank-1+hard) and calls==1
 out=STAGE/'formula.json';out.write_bytes(result.stdout)
 models=json.loads(pin(ROOT/'tools/reference/d2r-local-sentry-model-evidence.json'));raw=ROOT/models['files']['monstats']['path'];assert sha(raw)==models['files']['monstats']['sha256'];monster=next(r for r in csv.DictReader(pin(raw).lstrip('\ufeff').splitlines(),delimiter='\t') if r['Id']=='warputriddefiler')
 manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'));cached=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';assert sha(cached)==manifest['files']['json/monstats.json']['sha256'];current=json.loads(pin(cached))['warputriddefiler'];compared=0
 for key,value in current.items():
  if key.startswith('*') or key in ('lineNumber','expansion'):continue
  assert monster[key].strip()==str(value).strip(),key;compared+=1
 assert monster['noRatio']=='1' and [monster[k] for k in ('minHP','MinHP(N)','MinHP(H)')]==['140','190','300']
 pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',component='Defiler owned creation suppliers and exact malformed damage formula',handlerEnabled=False,currentExecutableParity=False,wrappedControlFogCases=10710,rawExactFormulaVerified=False,rawDiagnostic='Old compiler reads pending-operations[-1] while linking the leading bare par2 symbol;ASan/UBSan refuses this expression before a result.',comparedOwnedMonsterFields=compared,inputs=inputs,
  established=['The complete reused Fog compiler/evaluator executes two explicitly WRAPPED controls for all255ranks and hard Mastery0..20. Their result is10*(summon rank-1+hard Mastery). These controls do not prove the raw expression or modern execution;no production compatibility repair or damage multiplier is enabled.',
   'Current do56 requests S3,Health Link and its own Summon Defiler ranks. NoRatio bases are140/190/300;ordinary A1 physical endpoints and armor/attack/resistances remain pinned data. Prepared suppliers do not execute Health Link or GenericPet AI.'],
  limitations=['Fog symbol/stat callbacks are declared fixtures. Actual current extended-passive writer,health-link execution,skill-list/mode/world/controller admission,ordinary physical release and learned cap/payment/lifecycle remain separate. Nonzero inherited sunder needs its own ordered consumer.',
   'This proves the narrow EOF compatibility case,not permission to repair arbitrary malformed formulas. Final Gazer appearance and generic effects remain placeholders;icons are separate.'],
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in (p,binary,compiler,sanitizer,out,*(STAGE/n for n in ('Calc.cpp','Calc.h','D2BasicTypes.h','Fog.h')),*(q for q in (STAGE/'raw-diagnostic-1').rglob('*') if q.is_file()))})
 target=ROOT/'tools/reference/defiler-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Defiler:10710 explicitly wrapped Fog controls;raw damage expression remains unverified;owned140/190/300life bases pinned;offense/Health Link stay disabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
