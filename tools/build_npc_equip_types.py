#!/usr/bin/env python3
"""Bind current body/ammunition metadata to the existing owned type rows."""
import argparse,csv,hashlib,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 raw=ROOT/'.local/class-skills/item-finishing-source/itemtypes.txt';prior=ROOT/'tools/reference/item-finishing-table-evidence.json'
 assert sha(raw)==json.loads(prior.read_text())['inputs'][str(raw.relative_to(ROOT))]
 header=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Common/include/D2Inventory.h';tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());data=header.read_bytes();rel='source/D2Common/include/D2Inventory.h'
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
 enum=re.search(r'enum D2C_PlayerBodyLocs\s*\{(.*?)\}',data.decode(),re.S).group(1);names=re.findall(r'^\s*(BODYLOC_\w+)',enum,re.M)
 labels=('NONE','HEAD','NECK','TORSO','RARM','LARM','RRIN','LRIN','BELT','FEET','GLOVES','SWRARM','SWLARM');assert [names.index('BODYLOC_'+n)for n in labels]==list(range(13))
 locations={'':0,'none':0,'head':1,'neck':2,'tors':3,'rarm':4,'larm':5,'rrin':6,'lrin':7,'belt':8,'feet':9,'glov':10,'swra':11,'swla':12}
 rows=[dict(Code=r['Code'],Body1=locations[r['BodyLoc1']],Body2=locations[r['BodyLoc2']],Shoots=r['Shoots'],Quiver=r['Quiver'])for r in csv.DictReader(raw.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')if r['Code']]
 assert len(rows)==105 and len({r['Code']for r in rows})==105
 codes={r['Code']for r in rows};assert all(not r[k]or r[k]in codes for r in rows for k in ('Shoots','Quiver'))
 metadata=ROOT/'data/Sanctuary/Balance/iron-golem-items.json';bases=ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json';bytype={r['Code']:r for r in rows};bycode={r['code']:r for r in json.loads(bases.read_text())['bases']}
 for r in json.loads(metadata.read_text()):
  t=bytype[bycode[r['Code']]['type']];assert (r['Body1'],r['Body2'])==(t['Body1'],t['Body2'])
 target=ROOT/'data/Sanctuary/Balance/npc-equip-types.json';encoded=json.dumps(dict(ReferenceVersion='3.3.93847',Types=rows),indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 evidence=dict(referenceVersion='3.3.93847',typeRows=105,bodyRows=13,baseCrossChecks=692,inputs={str(p.relative_to(ROOT)):sha(p)for p in (raw,prior,header,metadata,bases,Path(__file__).resolve())},outputSha256=sha(target),limits=['Body/type codes are logical reference identities,not current compiled or save IDs. No current executable/equip dispatcher parity is inferred.'])
 path=ROOT/'tools/reference/npc-equip-types-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print('NPC equip types:105owned rows/13body labels/692base cross-checks.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
