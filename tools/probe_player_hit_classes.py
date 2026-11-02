#!/usr/bin/env python3
"""Pin ordinary player hit classes to owned tables and two unchanged legacy getters."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sacrifice-contact/source'
COMMIT='0c0bbd1e6bef9181a3da1c046523af622a925416'
CODES={'hth':'HandToHand','1hss':'OneHandSwingVsSmall','1hsl':'OneHandSwingVsLarge','2hss':'TwoHandSwingVsSmall','2hsl':'TwoHandSwingVsLarge','1ht':'OneHandThrust','2ht':'TwoHandThrust','club':'Club','staf':'Staff','bow':'Bow','xbow':'Crossbow'}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def block(text,name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
def build():
 tree=json.loads((STAGE/'tree.json').read_text());assert tree['sha']==COMMIT
 files={'Units.cpp':'source/D2Common/src/Units/Units.cpp','Items.cpp':'source/D2Common/src/Items/Items.cpp','headers/source/D2Common/include/D2DataTbls.h':'source/D2Common/include/D2DataTbls.h'}
 hashes={}
 for local,remote in files.items():
  p=STAGE/local;b=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==remote);hashes[remote]=sha(p)
 enum_text=(STAGE/'headers/source/D2Common/include/D2DataTbls.h').read_text();enum=enum_text[enum_text.rfind('enum ',0,enum_text.index('HITCLASS_None')):];enum=enum[:enum.index('};')+2]
 constants={name:int(value) for name,value in re.findall(r'HITCLASS_(\w+)\s*=\s*(\d+)',enum)}
 mapping={code:constants[name] for code,name in CODES.items()}
 current=ROOT/'tools/reference/d2r-named-equipment/json/weapons.json';rows=ROOT/'data/Sanctuary/Balance/creature-hit-classes.json';weapons=json.loads(current.read_text());native=json.loads(rows.read_text())
 assert len(native)==306 and all(weapons[r['code']]['hit class']==r['hitClass'] for r in native)
 units=block((STAGE/'Units.cpp').read_text(),'UNITS_GetHitClass');items=block((STAGE/'Items.cpp').read_text(),'ITEMS_GetHitClassFromItem')
 cpp='''#include <cstdint>
#include <cassert>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,FALSE=0;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0;};
struct D2ItemsTxt{uint8_t nHitClass=0;};struct D2MonStats2Txt{int nHitClass=0;};
D2UnitStrc item{UNIT_ITEM,0};D2ItemsTxt itemRow;D2MonStats2Txt mon;int present=0;
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){return present?&item:nullptr;}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return &itemRow;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int){return present?&mon:nullptr;}
'''+enum+'\n'+items+'\n'+units+'''
int main(){bool first=true;puts("[");for(int kind:{0,1,2,6})for(int has:{0,1})for(int hit=0;hit<=13;hit++){
 D2UnitStrc unit{kind,0};present=has;itemRow.nHitClass=hit;mon.nHitClass=hit;
 printf("%s[%d,%d,%d,%d]",first?"":",",kind,has,hit,UNITS_GetHitClass(&unit));first=false;
}puts("]");}
'''
 source=STAGE/'player-hit-class-probe.cpp';source.write_text(cpp);binary=STAGE/'player-hit-class-probe'
 subprocess.run(['g++','-std=c++20','-O2',str(source),'-o',str(binary)],check=True)
 result=json.loads(subprocess.check_output([str(binary)]));assert len(result)==112
 for kind,present,value,actual in result:assert actual==(value if present and kind in (0,1) else 1 if kind==0 else 0)
 return dict(referenceVersion='3.3.93847',supplementaryEngineCommit=COMMIT,sourceHashes=hashes,pinnedTableSha256=sha(current),existingClassRowsSha256=sha(rows),matchingPinnedWeaponRows=306,classes=mapping,compiledGetterCases=result,
  limits=['Unchanged legacy player/item getters run through declared inventory/table services;this is not a modern executable or native primary-selection certificate.','Unclassified UO recipient reactions remain native;these player weapon classes do not assign a D2 monster identity.'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');a=p.parse_args();out=ROOT/'tools/reference/player-hit-class-source-evidence.json';value=json.dumps(build(),indent=2)+'\n'
 if a.check:assert out.read_text()==value
 else:out.write_text(value)
 print('Player hit classes:306owned-table matches,112unchanged getter cases;unarmed1 and11weapon categories pinned.')
