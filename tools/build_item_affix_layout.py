#!/usr/bin/env python3
"""Pin current global affix/name ID offsets,including omitted separator rows."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 inputs={};tables={}
 for folder,names in [('d2r-named-equipment',('magicprefix','magicsuffix')),('item-generation',('rareprefix','raresuffix'))]:
  ref=ROOT/'tools/reference'/folder;meta=json.loads((ref/'sources.json').read_text());inputs[str((ref/'sources.json').relative_to(ROOT))]=sha(ref/'sources.json')
  for name in names:
   p=ref/('json/'+name+'.json'if folder=='d2r-named-equipment'else name+'.json');digest=sha(p)
   assert digest==meta['files'][('json/'if folder=='d2r-named-equipment'else'')+name+'.json']['sha256']
   inputs[str(p.relative_to(ROOT))]=digest;tables[name]=json.loads(p.read_text())
 counts={k:max(map(int,v))+1 for k,v in tables.items()}
 assert counts==dict(magicprefix=722,magicsuffix=786,rareprefix=46,raresuffix=155)
 data=dict(ReferenceVersion='3.3.93847',MagicSuffixRows=786,MagicPrefixRows=722,RareSuffixRows=155,RarePrefixRows=46)
 target=ROOT/'data/Sanctuary/Balance/item-affix-layout.json';encoded=json.dumps(data,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Affix layout stale'
 else:target.write_text(encoded)
 catalogue=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';inputs[str(catalogue.relative_to(ROOT))]=sha(catalogue);c=json.loads(catalogue.read_text())
 ids=[r['row']+1+(786 if r['definition']['kind']=='Prefix'else 0)for r in c['affixes']];assert len(ids)==len(set(ids))==991
 selectable=[a for a in c['affixes']if not a['firstPropertySockets']];assert len(selectable)==988
 codes={m['code']for a in selectable for m in a['definition']['modifiers']};assert len(codes)==89
 inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
 record=dict(referenceVersion='3.3.93847',inputs=inputs,tableSpans=counts,magicAffixIds=991,selectableAffixRows=988,selectablePropertyCodes=89,
  omittedRows={k:sorted(set(range(counts[k]))-set(map(int,v)))for k,v in tables.items()},
  scope='Global IDs are suffix-first then prefix,each with source +1. Table spans use largest retained row index+1,not JSON row count;omitted separator rows remain part of this explicit reference layout. Rare-name offsets use the same suffix-first convention.',
  outputSha256=sha(target),limits=['Reference ID offsets are internal catalogue identities;the actual current binary/compiled/save ID layout is not proved. The three socket-first affixes retain the existing complete older eligibility rejection;this is not an implementation-based property filter or proof of current socket parity.'])
 target=ROOT/'tools/reference/item-affix-layout-evidence.json';encoded=json.dumps(record,indent=2)+'\n'
 if check:assert target.read_text()==encoded,'Affix layout evidence stale'
 else:target.write_text(encoded)
 print('Current layout:786suffix/722prefix spans;155/46rare names;991IDs/988selectable rows/89property codes.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
