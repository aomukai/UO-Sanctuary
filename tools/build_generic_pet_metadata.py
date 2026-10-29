#!/usr/bin/env python3
"""Bind owned compiled pet metadata to pinned tables; optional strict extraction."""
import argparse,csv,hashlib,json,struct,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-metadata'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False,extract=False):
 installation=ROOT/'tools/reference/d2r-local-installation-evidence.json';pin=json.loads(installation.read_text());assert pin['referenceVersion']==pin['buildInfo']['version']=='3.3.93847'
 game=Path(pin['installationPath']);reader=ROOT/'.local/class-skills/warlock-foundation/casc-reference-reader';readerSource=reader.with_suffix('.cpp')
 meta=json.loads((ROOT/'tools/reference/warlock-foundation-preflight.json').read_text())['localExtraction'];assert sha(reader)==meta['readerSha256'] and sha(readerSource)==meta['readerSourceSha256']
 assert sha(game/'.build.info')==pin['buildInfo']['sha256'] and sha(game/pin['executable']['filename'])==pin['executable']['sha256']
 STAGE.mkdir(exist_ok=True)
 for name in ['skills.bin','states.bin','skills.txt','states.txt']:
  if extract:
   result=subprocess.run([str(reader),str(game),'data:data/global/excel/'+name,str(STAGE/name)],capture_output=True,text=True)
   (STAGE/(name+'.extract.log')).write_text(result.stdout+result.stderr);assert result.returncode==0,(name,result.stderr)
 assert sha(game/'.build.info')==pin['buildInfo']['sha256']
 artifacts=[installation,reader,readerSource,Path(__file__).resolve()];tables={};ownedTables={};matches={};representations=[]
 manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
 for name in ['skills','states']:
  p=ROOT/'tools/reference/d2r-named-equipment/json'/(name+'.json');assert sha(p)==manifest['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(p.read_text());artifacts.append(p)
  rows=list(csv.DictReader((STAGE/(name+'.txt')).read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'));key='skill' if name=='skills' else 'state';idKey='*Id' if name=='skills' else '*ID';owned={int(r[idKey]):r for r in rows}
  ownedTables[name]=owned
  matches[name]=0
  for r in tables[name].values():
   row=owned[r[idKey]]
   if key not in r:
    assert name=='skills' and r[idKey]==362 and row[key]=='Map'
    representations.append(dict(table=name,id=362,field=key,pinned=None,owned='Map'))
   for field,value in r.items():
    if field in row and not field.startswith('*') and field!='lineNumber':
     actual=row[field];expected=str(value)
     if actual!=expected:
      normalized=expected[1:-1] if expected.startswith('"') and expected.endswith('"') else expected
      assert actual.strip()==normalized.strip(),(name,r.get(key),field,value,actual)
      representations.append(dict(table=name,id=r[idKey],field=field,pinned=expected,owned=actual))
     matches[name]+=1
  assert len(owned)==len(tables[name]);artifacts += [STAGE/(name+'.txt'),STAGE/(name+'.bin')]
 skillsBin=(STAGE/'skills.bin').read_bytes();statesBin=(STAGE/'states.bin').read_bytes();skillCount=struct.unpack_from('<I',skillsBin)[0];stateCount=struct.unpack_from('<I',statesBin)[0]
 assert skillCount==len(tables['skills'])==429 and stateCount==len(tables['states'])==232
 assert len(skillsBin)==4+skillCount*748 and len(statesBin)==4+stateCount*68
 names={r['state']:r['*ID'] for r in tables['states'].values()};skills=[];states=[]
 for i in range(skillCount):
  r=tables['skills'][str(i)];assert r['*Id']==i;offset=4+i*748;assert struct.unpack_from('<h',skillsBin,offset)[0]==i
  aura,target=struct.unpack_from('<hh',skillsBin,offset+160);ai=skillsBin[offset+728]
  assert aura==names.get(r.get('aurastate'),-1) and target==names.get(r.get('auratargetstate'),-1) and ai==r.get('aitype',0),(i,aura,target,ai)
  skills.append(dict(id=i,name=ownedTables['skills'][i]['skill'],flags36=skillsBin[offset+36],flags39=skillsBin[offset+39],aiType=ai,auraState=aura,auraTargetState=target))
 byId={r['*ID']:r for r in tables['states'].values()}
 for i in range(stateCount):
  r=byId[i];group=struct.unpack_from('<h',statesBin,4+i*68+30)[0];assert group==r.get('group',0),(i,group)
  states.append(dict(id=i,name=r['state'],group=group))
 for name,value in [('generic-pet-skills',skills),('generic-pet-states',states)]:
  p=ROOT/'data/Sanctuary/Balance'/(name+'.json');encoded=json.dumps(value,indent=2)+'\n'
  if check:assert p.read_text()==encoded
  else:p.write_text(encoded)
  artifacts.append(p)
 result=dict(referenceVersion='3.3.93847',component='Owned compiled skill/state metadata for explicit GenericPet table views',executableSha256=pin['executable']['sha256'],buildInfoSha256=pin['buildInfo']['sha256'],readerSha256=sha(reader),readerSourceSha256=sha(readerSource),processMemoryRead=False,processInput=False,handlerEnabled=False,worldSelectorBindingVerified=False,
  verified=dict(skillRows=429,stateRows=232,skillStride=748,stateStride=68,skillsBinaryBytes=len(skillsBin),statesBinaryBytes=len(statesBin),compiledFieldMatches=429*4+232,ownedTextFieldMatches=matches),
  fields=dict(skillId=0,flags36=36,flags39=39,auraState=160,auraTargetState=162,aiType=728,stateGroup=30),textRepresentationDifferences=representations,publicUnnamedSkillRows=[r['id'] for r in skills if 'skill' not in tables['skills'][str(r['id'])]],
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},
  established=['Strict read-only owned CASC extraction is guarded by pinned executable/build metadata and the existing strict reader hashes. Binary headers/counts/strides match429skills and232states;all skill IDs,self states,target states,AI types and signed state groups match pinned public fields and owned text.',
   'Raw skill bytes36/39 are retained as consumed by captured GenericPet helpers;no new interpretation of unrelated bits is introduced. Missing aura states compile to signed-1;groups retain their source sign and exact IDs. Output ordering is numeric source ID,not JSON name ordering. Owned row362 is named Map although the public JSON omits its name;the actual slot/name is retained,with the annotation difference recorded alongside quote/whitespace representations.'],
  remaining=['Native catalog loading,adapter comparison and composition with current search preparation still require acceptance. Actual world table-selector/byte445 binding is unverified;do not map facets or difficulty to selector values or silently alias all selectors.',
   'Actual search callback consumption,world state membership/iterators,ordering and actor lifecycle remain separate. A compiled-table/text match proves data and layout for these fields,not full compiler/runtime dispatch or a functioning demon summon.'])
 p=ROOT/'tools/reference/generic-pet-metadata-data-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Owned pet metadata:429skills/232states;1948compiled field matches;explicit selector binding only.')
 return skills,states
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--extract',action='store_true');a=p.parse_args();build(a.check,a.extract)
