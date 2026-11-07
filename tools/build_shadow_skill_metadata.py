#!/usr/bin/env python3
"""Bind owned current NPC skill fields needed by the Shadow skill list."""
import argparse,csv,json,struct,hashlib
from pathlib import Path
from corroborate_valkyrie_initializer import offsets
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_bytes()
 extraction=BASE/'defiler-compiled-formula/extraction.json';e=json.loads(pin(extraction));assert e['referenceVersion']=='3.3.93847'
 binary=BASE/'defiler-compiled-formula/skills.bin';assert sha(binary)==e['files']['skills.bin']['sha256'];b=pin(binary)
 assert struct.unpack_from('<I',b)[0]==429 and len(b)==4+429*748;b=b[4:]
 manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'));tables={}
 for n in ['skills','states','monstats','pettype']:
  p=ROOT/'tools/reference/d2r-named-equipment/json'/(n+'.json');assert sha(p)==manifest['files']['json/'+n+'.json']['sha256'];tables[n]=json.loads(pin(p))
 rows=[tables['skills'][str(i)]for i in range(429)]
 modes=json.loads(pin(ROOT/'tools/reference/d2r-local-monmode-evidence.json'));mode={r['code']:r['index']for r in modes['rows']}
 cls={k:i for i,k in enumerate(['ama','sor','nec','pal','bar','dru','ass','war'])};element={'fire':1,'ltng':2,'mag':3,'cold':4,'pois':5,'stun':9}
 states={r['state']:r['*ID']for r in tables['states'].values()};mon={k.lower():r['*hcIdx']for k,r in tables['monstats'].items()}
 typesPath=BASE/'warlock-hands-reference/itemtypes.txt';typeProof=json.loads(pin(ROOT/'tools/reference/warlock-hands-reference-evidence.json'));assert sha(typesPath)==typeProof['inputs'][str(typesPath.relative_to(ROOT))]
 itemrows=[r for r in csv.DictReader(pin(typesPath).decode('utf-8-sig').splitlines(),delimiter='\t')if r['ItemType']!='Expansion'];itype={r['Code']:i for i,r in enumerate(itemrows)}
 expected=[('class','<b',44,[cls.get(r.get('charclass'),-1)for r in rows]),('monmode','<B',49,[mode.get(r.get('monanim'),255)for r in rows]),
  ('element','<B',644,[element.get(r.get('EType'),0)for r in rows]),('passiveState','<h',180,[states.get(r.get('passivestate'),-1)for r in rows]),
  ('passiveType','<h',182,[itype.get(r.get('passiveitype'),0)for r in rows]),('summon','<H',272,[mon[r['summon'].lower()]if r.get('summon')else 65535 for r in rows]),
  ('petType','<b',274,[tables['pettype'][r['pettype']]['lineNumber']if r.get('pettype')else 0 if i in [78,88,199]else -1 for i,r in enumerate(rows)])]
 for n,fmt,offset,values in expected:assert offsets(b,748,fmt,values)==[offset],n
 ids=[i for i,r in enumerate(rows)if r.get('charclass')=='ass'];assert ids==list(range(251,281))and itype['h2h']==68
 records=[]
 for i in [0,*ids]:
  r=rows[i];records.append(dict(Id=i,Name=r['skill'],Class=cls.get(r.get('charclass'),-1),Mode=mode[r['monanim']],Element=element.get(r.get('EType'),0),
    PassiveState=states.get(r.get('passivestate'),-1),PassiveType=itype.get(r.get('passiveitype'),0),Summon=mon[r['summon'].lower()]if r.get('summon')else 65535,
    PetType=tables['pettype'][r['pettype']]['lineNumber']if r.get('pettype')else -1))
 path=ROOT/'data/Sanctuary/Balance/shadow-skill-metadata.json';encoded=json.dumps(records,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 pin(path);pin(Path(__file__).resolve());loader=BASE/'d2moo-pinned-source/source/D2Common/src/DataTbls/SkillsTbls.cpp';pin(loader)
 result=dict(referenceVersion='3.3.93847',executableSha256=e['executableSha256'],compiledFieldMatches=429*7,compiledRows=429,shadowMetadataRows=31,assassinClassOrder=ids,passiveClawType=68,
  fields={n:offset for n,fmt,offset,values in expected},petTypeMissingZeroRows=[78,88,199],currentClassOrderExecuted=False,inputs=inputs,
  scope=['All429compiled skills match owned/public fields for class,monster animation,element,passive state/type,summon and pet type. Absent monster mode compiles255;three absent-pet-type rows compile0rather than-1,and remain explicitly identified. Only31actual Attack/Assassin rows enter this NPC adapter.','Item type IDs follow the owned linker row order with the Expansion marker omitted;claw passive layer68 is not guessed from public line numbers. Both Shadow summon skills use pet type16.','The old class-list loader appends physical skill IDs in ascending row order;current compiled class membership is251..280. Exact current class-list execution/order is not certified.'],limits=['Metadata is not a skill-list allocator,passive refresh,AI or skill execution certificate. Current executable getter/assignment/init parity and world selector binding remain separate. No handler or class certificate is added.'])
 out=ROOT/'tools/reference/shadow-skill-metadata-data-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Shadow NPC skill metadata:3003compiled field matches,31actual rows,claw layer68;current class-list execution remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
