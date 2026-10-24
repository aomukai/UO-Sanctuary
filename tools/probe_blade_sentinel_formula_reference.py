#!/usr/bin/env python3
"""Compile exact current Blade Sentinel passive formulas with unchanged Fog Calc.

Symbol IDs and stat callback are declared services, not modern executable proof.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';CACHE=STAGE/'blade-sentinel-formula-probe'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw
 cpp=source('source/Fog/src/Calc.cpp');header=source('source/Fog/include/Calc.h');source('source/D2Common/src/DataTbls/SkillsTbls.cpp')
 pinroot=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pinroot/'sources.json').read_text());pins={}
 for n in ['skills','itemstatcost']:
  p=pinroot/f'json/{n}.json';h=hashlib.sha256(p.read_bytes()).hexdigest();assert h==manifest['files'][f'json/{n}.json']['sha256'];pins[n]=h
 row=json.loads((pinroot/'json/skills.json').read_text())['257'];stats={v['Stat']:v['*ID'] for v in json.loads((pinroot/'json/itemstatcost.json').read_text()).values() if 'Stat' in v and '*ID' in v}
 assert row['passivecalc6']=="stat('item_pierce_cold_immunity'.accr"
 expressions=[(i,row[f'passivecalc{i}'],stats[row[f'passivestat{i}']]) for i in range(2,15)]+[(106,row['passivecalc6']+')',stats[row['passivestat6']])]
 CACHE.mkdir(exist_ok=True);(CACHE/'Calc.cpp').write_bytes(cpp);(CACHE/'Calc.h').write_bytes(header)
 (CACHE/'D2BasicTypes.h').write_text('''#pragma once
#include <cstdint>
#include <cstring>
#include <cstdlib>
#include <cctype>
#include <cmath>
#include <cassert>
#define __fastcall
#define __stdcall
#define FOG_DLL_DECL
#define D2_ASSERT assert
#define D2_UNREACHABLE __builtin_unreachable()
using BOOL=int;
#define TRUE 1
#define FALSE 0
''');(CACHE/'Fog.h').write_text('#pragma once\n#include "D2BasicTypes.h"\n')
 text=r'''#include "Calc.h"
#include <cstdio>
#include <array>
int wanted=0,sentinel=0,calls=0;
int keyword(char* name){assert(!strcmp(name,"stat"));return 5;}
int parameters(int id){assert(id==5);return 2;}
int link(char* name,BOOL* constant,int ast,int key){assert(ast==1 && key==5);if(!strcmp(name,"accr")){*constant=0;return 0;}*constant=1;return wanted;}
int accrued(int id,int kind,int extra,void*){assert(id==wanted && kind==0 && extra==0);calls++;return sentinel;}
int main(){D2CalcCallbackInfoStrc table[6]={};table[5]={accrued,2};bool first=true;printf("[");
'''
 for slot,expression,stat in expressions:
  text+=f'for(int value:{{-100,0,1,50,200}}){{wanted={stat};sentinel=value;calls=0;FOGASTNodeStrc buffer[1024]={{}};int size=DATATBLS_CompileExpression({json.dumps(expression)},buffer,1024,keyword,parameters,link);int result=size?DATATBLS_CalcEvaluateExpression(buffer,size,nullptr,table,6,nullptr):0;printf("%s[{slot},%d,%d,%d,%d,[",first?"":",",value,size,result,calls);first=false;for(int i=0;i<size;i++)printf("%s%d",i?",":"",buffer[i].nRawValue);printf("]]");}}\n'
 text+='printf("]\\n");}\n';(CACHE/'main.cpp').write_text(text)
 subprocess.run(['g++','-std=c++20','-O2','-I'+str(CACHE),str(CACHE/'Calc.cpp'),str(CACHE/'main.cpp'),'-o',str(CACHE/'probe')],check=True)
 data=json.loads(subprocess.check_output([str(CACHE/'probe')]));assert len(data)==70
 for slot,value,size,result,calls,code in data:
  assert size>0 and result==(0 if slot==6 else value) and calls==(0 if slot==6 else 1),(slot,value,size,result,calls,code)
 out=STAGE/'blade-sentinel-formula-source-probe.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,pinnedHashes=pins,fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=70),compiled='Complete unchanged Fog Calc.cpp and Calc.h; only type/calling-convention shim headers. Exact pinned passivecalc2..14 and separately labeled balanced control for slot6.',symbolServices='Reviewed SkillsTbls stat keyword5/two arguments and accrued property0. Stat name linker resolves each supplied pinned itemstatcost ID; callback returns explicit -100/0/1/50/200 sentinels. Full SkillsTbls linker and real stat acquisition are not compiled.',established=['The exact unclosed passivecalc6 compiles but does not emit/call the stat function. Across all five supplied accrued values it evaluates to zero. Adding a closing parenthesis executes the callback and returns the sentinel; that is a separate control, not an authorized correction of source data.','All twelve other pinned stat formulas execute exactly one callback and return supplied accrued value. A generic auto-close adapter would change slot6 behavior and is not justified by the older compiler.'],limits=['This is legacy compiler behavior against current pinned source text, not proof of current3.3.93847 binary behavior or actual cold-sunder inheritance. Current cold-sunder inheritance remains explicitly uncertain.','No native formula repair, handler enablement, current inherited14-slot helper equivalence or class certificate is claimed.'])
 p=ROOT/'tools/reference/blade-sentinel-formula-engine-evidence.json';render=json.dumps(evidence,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade Sentinel full compiler checks passed:70 cases; exact unclosed cold-sunder formula returns0 without a stat callback, current executable inheritance uncertain.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
