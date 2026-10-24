#!/usr/bin/env python3
"""Build the complete current Valkyrie type-eligible affix pool, without player implementation filtering."""
import argparse,csv,hashlib,io,json,urllib.request
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];REF=ROOT/'tools/reference/d2r-named-equipment'
EXCEL='1f16064e09b97e3e65abd6943662207cff00b07f'
def build(check=False,cross_check=False):
 meta=json.loads((REF/'sources.json').read_text());pins={};tables={}
 for name in ['magicprefix','magicsuffix','itemtypes','weapons','armor','misc','properties','monequip']:
  p=REF/'json'/f'{name}.json';raw=p.read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==meta['files']['json/'+name+'.json']['sha256'];tables[name]=json.loads(raw)
 evidencepath=ROOT/'tools/reference/summon-affix-table-evidence.json'
 if cross_check:
  evidence={}
  for name in ['magicprefix','magicsuffix']:
   url=f'https://raw.githubusercontent.com/pinkufairy/D2R-Excel/{EXCEL}/{name}.txt';raw=urllib.request.urlopen(url,timeout=30).read();rows=list(csv.DictReader(io.StringIO(raw.decode('utf-8-sig')),delimiter='\t'));clean=lambda r:{str(k).strip().casefold():str(v).strip().strip('"').strip() for k,v in r.items() if k is not None};checked=0;annotations={};differences=[]
   for idx,row in tables[name].items():
    src=clean(row);other=clean(rows[int(idx)])
    for k,v in src.items():
     if k=='linenumber' or k.startswith('*'):continue
     if k=='expansion' and k not in other:annotations[k]=annotations.get(k,0)+1;continue
     checked+=1
     if other.get(k)!=v:differences.append([idx,k,v,other.get(k)])
   assert not differences,differences[:10]
   evidence[name]=dict(jsonUrl=meta['files']['json/'+name+'.json']['url'],jsonSha256=pins[name],tsvUrl=url,tsvSha256=hashlib.sha256(raw).hexdigest(),jsonRows=len(tables[name]),tsvRows=len(rows),checkedFields=checked,extractionAnnotationsNotInTsv=annotations,differences=differences)
  rendered=json.dumps(dict(referenceVersion='3.3.93847',sourceCommit=meta['commit'],corroboratingCommit=EXCEL,comparison=evidence),indent=2)+'\n'
  if check:assert evidencepath.read_text()==rendered
  else:evidencepath.write_text(rendered)
 else:
  e=json.loads(evidencepath.read_text());assert e['referenceVersion']=='3.3.93847'
  for name in ['magicprefix','magicsuffix']:assert e['comparison'][name]['jsonSha256']==pins[name] and not e['comparison'][name]['differences']
 code=lambda value:str(value).strip()[:4]
 types={r['Code']:dict(code=r['Code'],parents=[code(r[k]) for k in ['Equiv1','Equiv2'] if r.get(k)],alwaysMagic=bool(r.get('Magic')),rare=bool(r.get('Rare')),characterClass=r.get('Class','')) for r in tables['itemtypes'].values() if r.get('Code')}
 def is_type(kind,ancestor):return kind==ancestor or any(is_type(p,ancestor) for p in types[kind]['parents'])
 for t in types.values():assert all(p in types for p in t['parents']),t
 bases=[];allbases={r['code']:r for name in ['weapons','armor','misc'] for r in tables[name].values() if r.get('code')}
 codes=sorted({r['item'+str(i)] for r in tables['monequip'].values() if r.get('monster')=='valkyrie' for i in range(1,4) if r.get('item'+str(i))})
 for c in codes:
  r=allbases[c];bases.append(dict(code=c,type=r['type'],qualityLevel=r.get('level',1),magicLevel=r.get('magic lvl',0),requiredClass=types[r['type']]['characterClass'],sourceFields=r))
 affixes=[];rejectedTypes=0;canonical={k.casefold():k for k in tables['properties']}
 def list_types(r,prefix,count):
  values=[]
  for i in range(1,count+1):
   if not r.get(prefix+str(i)):break
   v=code(r[prefix+str(i)]);assert v in types,(v,r);values.append(v)
  return values
 for table,kind in [('magicsuffix','Suffix'),('magicprefix','Prefix')]:
  for index,row in tables[table].items():
   if not row.get('spawnable') or row.get('frequency',0)<1 or not row.get('Name'):continue
   includes=list_types(row,'itype',7);excludes=list_types(row,'etype',5)
   if not any(any(is_type(b['type'],t) for t in includes) and not any(is_type(b['type'],t) for t in excludes) for b in bases):rejectedTypes+=1;continue
   modifiers=[]
   for i in range(1,4):
    if not row.get('mod'+str(i)+'code'):continue
    sourcecode=row['mod'+str(i)+'code'];prop=tables['properties'][canonical[sourcecode.casefold()]];funcs=[prop.get('func'+str(j),0) for j in range(1,8)];mode='Proc' if 11 in funcs else 'Charges' if 19 in funcs else 'DamageRange' if 15 in funcs else 'Parameter' if funcs[0]==17 else 'Numeric'
    modifiers.append(dict(code=sourcecode,parameter=str(row.get('mod'+str(i)+'param','')),min=row.get('mod'+str(i)+'min',0),max=row.get('mod'+str(i)+'max',0),mode=mode))
   first=tables['properties'][canonical[row['mod1code'].casefold()]]
   affixes.append(dict(row=int(index),version=row.get('version',0),firstPropertySockets=first.get('stat1')=='item_numsockets',definition=dict(id=('MagicPrefix' if kind=='Prefix' else 'MagicSuffix')+':'+index,name=row['Name'],kind=kind,rare=bool(row.get('rare')),level=row.get('level',0),maxLevel=row.get('maxlevel',0),requiredLevel=row.get('levelreq',0),frequency=row['frequency'],group=row['group'],classSpecific=row.get('classspecific',''),characterClass=row.get('class',''),classRequiredLevel=row.get('classlevelreq',0),include=includes,exclude=excludes,modifiers=modifiers),sourceFields=row))
 locations={'head':1,'neck':2,'tors':3,'rarm':4,'larm':5,'rrin':6,'lrin':7,'belt':8,'feet':9,'glov':10}
 equipment=[]
 for row in tables['monequip'].values():
  if row.get('monster')!='valkyrie':continue
  choices=[dict(slot=locations[row['loc'+str(i)]],code=row['item'+str(i)],quality=row['mod'+str(i)]) for i in range(1,4) if row.get('loc'+str(i))]
  assert len(choices)==1 and choices[0]['quality'] in (4,6)
  equipment.append(dict(rank=row.get('level',0),choices=choices))
 assert len(equipment)==19
 result=dict(referenceVersion='3.3.93847',affixes=affixes,bases=bases,types=list(types.values()),equipmentRows=equipment)
 path=ROOT/'data/Sanctuary/Balance/valkyrie-affix-catalogue.json';rendered=json.dumps(result,indent=2,ensure_ascii=False)+'\n'
 if check:assert path.read_text()==rendered
 else:path.write_text(rendered)
 print(f'Valkyrie: {len(bases)} current bases, {len(affixes)} type-eligible source affixes, {len(types)} current item types; {rejectedTypes} unrelated type rows excluded. No implementation/property/rarity/level filtering applied to catalogue.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--cross-check',action='store_true');a=p.parse_args();build(a.check,a.cross_check)
