#!/usr/bin/env python3
"""Execute complete stack getters against all pinned weapon-table stack rows."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
rel='source/D2Common/src/Items/Items.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);text=raw.decode();blocks={}
def routine(name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 body=text[m.start():end];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
pin=ROOT/'tools/reference/d2r-named-equipment/json/weapons.json';manifest=json.loads((pin.parent.parent/'sources.json').read_text());digest=hashlib.sha256(pin.read_bytes()).hexdigest();assert digest==manifest['files']['json/weapons.json']['sha256'];rows=sorted([r for r in json.loads(pin.read_text()).values() if r.get('maxstack',0)>0],key=lambda r:r['code']);assert len(rows)==36
cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr int UNIT_ITEM=4,STAT_ITEM_EXTRA_STACK=0;
struct D2ItemsTxt{int dwMaxStack=0,dwMinStack=0,dwSpawnStack=0;} row;
struct D2UnitStrc{int dwUnitType=4,dwClassId=0;} unit;
int extra=0;
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return &row;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return extra;}
'''
for name in ['ITEMS_GetMaxStack','ITEMS_GetTotalMaxStack','ITEMS_GetSpawnStackFromItemId','ITEMS_GetSpawnStack','ITEMS_GetMinStackFromItemId','ITEMS_GetMinStack']:cpp+=routine(name)+'\n'
cpp+='int main(){bool first=true;std::printf("[");\n'
for r in rows:
 cpp+=f'row={{{r["maxstack"]},{r["minstack"]},{r["spawnstack"]}}};\n'
 cpp+='for(int bonus:{0,1,80,200,512,1000}){extra=bonus;if(!first)std::printf(",");first=false;'
 cpp+=f'std::printf("[\\"{r["code"]}\\",%d,%d,%d,%d,%d]",ITEMS_GetMinStack(&unit),ITEMS_GetMaxStack(0),ITEMS_GetSpawnStack(&unit),bonus,ITEMS_GetTotalMaxStack(&unit));}}\n'
cpp+='std::printf("]\\n");}\n';path=STAGE/'throwing-quantity-source-probe.cpp';path.write_text(cpp);binary=STAGE/'throwing-quantity-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==216
by_code={r['code']:r for r in rows}
for code,minimum,maximum,spawn,bonus,total in data:
 r=by_code[code];assert (minimum,maximum,spawn,total)==(r['minstack'],r['maxstack'],r['spawnstack'],min(511,r['maxstack']+bonus))
fixture=STAGE/'throwing-quantity-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines=list(blocks),blockHashes=blocks,pinnedWeaponHash=digest,probe=dict(rows=36,cases=216,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Full getters return the current base minimum, maximum and spawn metadata independent of named-item definitions. Tax is min24/max200/spawn48; all30 throwing weapon and6 potion rows are covered.','The complete older total-max helper adds extra-stack to base maximum then caps at511. Large bonus controls distinguish that cap from uncapped addition.'],limits=['Item lookup, item type and extra-stack stat access are declared services. This proves complete older getters with current pinned rows, not modern executable equivalence or item creation/roll/stat preparation.','Current native lookup migration preserves its existing base-plus-named-stack behavior and saved counts. Complete modern upper-cap/ordinary-affix/initial-quantity handling remains separate; no data-only support is taken as native throwing-potion implementation.'])
out=ROOT/'tools/reference/throwing-quantity-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Throwing quantity source evidence stale'
else:out.write_text(encoded)
print('Throwing quantities:216 full getter cases over36 current stack rows; initialization and modern upper-cap remain separate.')
