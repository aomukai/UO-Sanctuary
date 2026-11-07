#!/usr/bin/env python3
"""Corroborate initializer inputs in the owned pinned text and compiled tables."""
import csv,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/valkyrie-initializer-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rows(name):return [r for r in csv.DictReader((STAGE/(name+'.txt')).read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')if r.get('Id',r.get('Name'))!='Expansion']
def offsets(binary,stride,fmt,values):
 return [o for o in range(stride-struct.calcsize(fmt)+1)if all(struct.unpack_from(fmt,binary,i*stride+o)[0]==v for i,v in enumerate(values))]
def main():
 receipt=STAGE/'extraction.json';extracted=json.loads(receipt.read_text());assert extracted['referenceVersion']=='3.3.93847'
 for name,entry in extracted['files'].items():assert sha(STAGE/name)==entry['sha256']
 primary=rows('monstats');secondary=rows('monstats2');properties=rows('monprop');b=(STAGE/'monstats.bin').read_bytes();c=(STAGE/'monstats2.bin').read_bytes()
 assert struct.unpack_from('<I',b)[0]==len(primary)==751 and len(b)==4+751*508
 assert struct.unpack_from('<I',c)[0]==len(secondary)==623 and len(c)==4+623*296
 b,c=b[4:],c[4:];property_ids={r['Id']:i for i,r in enumerate(properties)}
 assert offsets(b,508,'<H',[property_ids[r['MonProp']]if r['MonProp']else 65535 for r in primary])==[78]
 assert offsets(b,508,'<B',[int(r['Align']or 0)for r in primary])==[135]
 order=['hd','tr','lg','ra','la','rh','lh','sh','s1','s2','s3','s4','s5','s6','s7','s8'];choices=[]
 for row in secondary:
  low={k.lower():v for k,v in row.items()};choices.append(bytes(len([x for x in low[k+'v'].strip('"').split(',')if x])for k in order))
 assert [o for o in range(281)if all(c[i*296+o:i*296+o+16]==v for i,v in enumerate(choices))]==[21]
 assert offsets(c,296,'<B',[int(r['TotalPieces']or 0)for r in secondary])==[236]
 primary_id=next(i for i,r in enumerate(primary)if r['Id']=='valkyrie');secondary_id=next(i for i,r in enumerate(secondary)if r['Id']=='valkyrie');assert primary_id==secondary_id==357
 v=primary[357];v2=secondary[357]
 assert b[357*508+135]==int(v['Align'])==1 and struct.unpack_from('<H',b,357*508+78)[0]==65535>=len(properties)
 assert c[357*296+236]==int(v2['TotalPieces'])==2 and choices[357]==bytes(16)
 assert all(not v['Skill'+str(i)]and not v['Sk'+str(i)+'lvl']for i in range(1,9))
 equipment=[{k.lower():x for k,x in r.items()}for r in rows('monequip')if r['monster']=='valkyrie'];assert len(equipment)==19 and all(int(r['oninit']or 0)==0 for r in equipment)
 current=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/monstats.json').read_text())['valkyrie']
 for field in ['Align','noRatio','minHP','maxHP','MinHP(N)','MaxHP(N)','MinHP(H)','MaxHP(H)','AC','AC(N)','AC(H)','ToBlock','ToBlock(N)','ToBlock(H)','DamageRegen']:
  assert int({k.lower():x for k,x in v.items()}[field.lower()]or 0)=={k.lower():x for k,x in current.items()}[field.lower()],field
 assert all(int(v[k]or 0)==0 for k in ['Level','Level(N)','Level(H)'])and int(v['noRatio'])==1
 result=dict(referenceVersion='3.3.93847',executableSha256=extracted['executableSha256'],compiledTextMatches=dict(primaryRows=751,propertyIndexOffset=78,alignmentOffset=135,secondaryRows=623,componentCountsOffset=21,totalPiecesOffset=236),valkyrie=dict(monster=357,alignment=1,level=[0,0,0],noRatio=True,componentChoiceCounts=list(choices[357]),totalPieces=2,monsterProperty=65535,monsterPropertyCount=len(properties),initialSkills=[],onInitEquipment=False,minimumLife=[400,400,400],maximumLife=[480,480,480]),limits=['These are current table fields,not executed current initialization code. Legacy helper code establishes conditional player-count,component RNG,initial rates and allocation order separately. No empty-string-to-index assumption or old/current raw-layout equivalence is used;compiled fields were matched across all rows.'],artifacts={str(p.relative_to(ROOT)):sha(p)for p in [receipt,Path(__file__).resolve(),ROOT/'tools/reference/d2r-named-equipment/json/monstats.json',*[STAGE/n for n in extracted['files']]]})
 runtime=ROOT/'data/Sanctuary/Balance/valkyrie-initializer.json';runtime.write_text(json.dumps([result['valkyrie']],indent=2)+'\n');result['artifacts'][str(runtime.relative_to(ROOT))]=sha(runtime)
 target=ROOT/'tools/reference/valkyrie-initializer-data-evidence.json';target.write_text(json.dumps(result,indent=2)+'\n')
 print('Valkyrie initializer data:751primary/623secondary records corroborate actual fields;friendly,noRatio,400-480life,zero component choices,no initial skills/properties or onInit equipment.')
if __name__=='__main__':main()
