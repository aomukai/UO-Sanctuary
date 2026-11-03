#!/usr/bin/env python3
"""Verify owned passive slots and expose old/current decoder incompatibility."""
import argparse,hashlib,json,struct,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-compiled-formula'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_bytes()
 extraction=json.loads(pin(STAGE/'extraction.json'));installation=json.loads(pin(ROOT/'tools/reference/d2r-local-installation-evidence.json'))
 assert extraction['referenceVersion']==installation['referenceVersion']=='3.3.93847' and extraction['executableSha256']==installation['executable']['sha256'] and extraction['buildInfoSha256']==installation['buildInfo']['sha256']
 assert not extraction['processMemoryRead'] and not extraction['gameFilesWritten']
 for name,meta in extraction['files'].items():assert sha(STAGE/name)==meta['sha256'];pin(STAGE/name);pin(STAGE/(name+'.extract.log'))
 for name,key in (('casc-reference-reader','readerSha256'),('casc-reference-reader.cpp','readerSourceSha256')):assert sha(STAGE/name)==extraction[key];pin(STAGE/name)
 data=(STAGE/'skills.bin').read_bytes();code=(STAGE/'skillscode.bin').read_bytes();assert struct.unpack_from('<I',data)[0]==429 and len(data)==4+429*748 and len(code)==10324
 manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'));path=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';assert sha(path)==manifest['files']['json/skills.json']['sha256'];skills=json.loads(pin(path))
 costs=json.loads(pin(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json'));stats={r['Name']:r['Id'] for r in costs};matches=0;presence=0
 for id in range(429):
  row=skills[str(id)];base=4+id*748;assert struct.unpack_from('<h',data,base)[0]==id
  for slot in range(12):
   stat=struct.unpack_from('<h',data,base+186+slot*2)[0];offset=struct.unpack_from('<i',data,base+216+slot*4)[0]
   assert stat==stats.get(row.get('passivestat'+str(slot+1)),-1),(id,slot,stat)
   text=row.get('passivecalc'+str(slot+1));assert (offset>=0)==(text is not None) and (offset==-1 or 0<=offset<len(code)),(id,slot,text,offset)
   matches+=1;presence+=1
 base=4+377*748;offset=struct.unpack_from('<i',data,base+220)[0];following=struct.unpack_from('<i',data,base+224)[0]
 actual=code[offset:following];assert offset==8152 and following==8171
 # Record an explicitly legacy token view;modern marker/callback grammar is not certified.
 tokens=[];at=0
 while at<len(actual):
  op=actual[at];at+=1
  if op in (4,7,1):value=actual[at];at+=1;tokens.append([op,value])
  elif op==8:value=struct.unpack_from('<h',actual,at)[0];at+=2;tokens.append([op,value])
  else:assert op in (0,2,16,17,18);tokens.append([op])
 assert tokens==[[4,9],[4,16],[7,1],[17],[8,374],[7,41],[1,3],[2],[16],[2],[18],[0]]
 curves=ROOT/'.local/class-skills/warlock-foundation/skillcalc.txt';pin(curves)
 import csv
 names=list(csv.DictReader(curves.read_text().splitlines(),delimiter='\t'));assert [(i,r['code']) for i,r in enumerate(names) if r['code'] in ('par2','lvl','blvl')]==[(9,'par2'),(16,'lvl'),(41,'blvl')]
 prior=json.loads(pin(ROOT/'tools/reference/defiler-source-evidence.json'));assert not prior['rawExactFormulaVerified'] and prior['wrappedControlFogCases']==10710
 tree=json.loads(pin(ROOT/'.local/class-skills/d2moo-source-tree.json'));relative='source/D2Common/src/DataTbls/SkillsTbls.cpp';loader=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=pin(loader)
 assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==relative)
 callers={name:function(raw.decode(),name) for name in ('DATATBLS_SkillCalcLinker','DATATBLS_SkillDescCalcLinker')}
 assert all('DATATBLS_CompileExpression(pSrc, pBuffer' in body for body in callers.values())
 # Execute the complete unchanged existing evaluator against actual CASC bytes;
 # the old text compiler and the altered/wrapped strings are not called.
 for name in ('Calc.cpp','Calc.h','D2BasicTypes.h','Fog.h'):
  src=ROOT/'.local/class-skills/defiler-source'/name;assert sha(src)==prior['artifacts'][str(src.relative_to(ROOT))];pin(src)
 cpp='''#include "Calc.h"
#include <cstdio>
int rank,hard,calls;
int parent(int index,void*){assert(index==9||index==16);return index==9?10:rank;}
int skill(int id,int property,int extra,void*){assert(id==374&&property==41&&extra==0);calls++;return hard;}
int main(){FOGASTNodeStrc bytes[]={'''+','.join('{static_cast<FOGASTType>('+str(x)+')}' for x in actual)+'''};
D2CalcCallbackInfoStrc table[4]={};table[3]={skill,2};printf("[");bool first=true;
for(rank=1;rank<=255;rank++)for(hard=0;hard<=20;hard++){calls=0;int value=DATATBLS_CalcEvaluateExpression(bytes,sizeof(bytes),parent,table,4,nullptr);printf("%s[%d,%d,%d,%d]",first?"":",",rank,hard,value,calls);first=false;}printf("]\\n");}
'''
 path=STAGE/'evaluate.cpp';path.write_text(cpp);binary=STAGE/'evaluate';headers=ROOT/'.local/class-skills/defiler-source'
 r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize=alignment','-fno-sanitize-recover=all','-I'+str(headers),str(path),str(headers/'Calc.cpp'),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(r.stdout+r.stderr);assert r.returncode==0
 r=subprocess.run([str(binary)],capture_output=True);sanitizer=STAGE/'sanitizer.log';sanitizer.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr
 rows=json.loads(r.stdout);assert len(rows)==5355
 for rank,hard,value,calls in rows:assert value==hard and calls==1
 assert sum(value!=10*(rank-1+hard) for rank,hard,value,calls in rows)==5354
 out=STAGE/'cases.json';out.write_bytes(r.stdout);pin(Path(__file__).resolve())
 result=dict(referenceVersion='3.3.93847',component='Owned Defiler bytecode layout and legacy-decoder incompatibility',rawTextCompilerUsed=False,currentCompiledDataVerified=True,currentExecutableParity=False,handlerEnabled=False,
  legacyCallerHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in callers.items()},passiveStatMatches=matches,passiveCalcPresenceMatches=presence,skillRows=429,skillStride=748,passiveStatsOffset=186,passiveCalcsOffset=216,skill=377,slot=2,stat=25,codeOffset=offset,codeBytes=actual.hex(),legacyTokenView=tokens,rawLegacyEvaluatorCases=5355,currentFormulaExecutionVerified=False,tenfoldCandidateMismatchCases=5354,inputs=inputs,
  established=['All429owned skill records and12passive slots agree with pinned stat names and calc presence. Skill377slot2is damagepercent25 and points to actual skillscode.bin bytes8152..8170.',
   'The complete unchanged older evaluator consumes actual19bytes for all255ranks/hard Mastery0..20 with clean ASan/UBSan,but returns hard Mastery alone. It terminates on byte02 after the callback instead of reaching later arithmetic.5354cases differ from the proposed10*(rank-1+hard) curve. This is a decoder incompatibility/ambiguity result,not current formula execution evidence.',
   'This establishes current field/stat/bytecode identity but does NOT resolve the damage multiplier. Byte02 also occurs before the final multiplication;its current interpretation must be established before any production rule. Offense and Health Link remain disabled.'],
  limitations=['Current callback/marker encoding is not assumed equivalent to the old interpreter. No callback-arity stripping,marker removal or compiler normalization is used to manufacture a passing multiplier. The current formula-context helper is observed unreadable;no inaccessible code was read.',
   'The prior raw-compiler failure remains valid and preserved. No production parser or damage changes are made. The earlier failed tenfold comparison is preserved separately;this check certifies the discrepancy rather than changing gameplay to fit it.'],
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in (path,binary,compiler,sanitizer,out,*(q for q in (STAGE/'diagnostic-1').rglob('*') if q.is_file()),STAGE/'formula-context-observation.json')})
 target=ROOT/'tools/reference/defiler-compiled-formula-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Defiler compiled boundary:5148stat/5148presence matches;5355old-decoder cases and5354tenfold mismatches;current multiplier remains unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
