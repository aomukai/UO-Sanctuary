#!/usr/bin/env python3
"""Pin regular skeleton/Mage current table and SK animation evidence for world integration."""
import argparse,csv,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(local_root,check=False):
    refs=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((refs/'sources.json').read_text());assert manifest['commit']=='fa1286949266498b294c40c2942521a7c0d7207a'
    tables={};pins={};compared=0
    for table,filename in [('monstats','monstats-sentries-verified.txt'),('monstats2','monstats2-sentries-verified.txt')]:
        cached=refs/'json'/(table+'.json');assert sha(cached)==manifest['files']['json/'+table+'.json']['sha256']
        raw=json.loads(cached.read_text());local={x['Id']:x for x in csv.DictReader((local_root/filename).read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')}
        tables[table]={name:raw[name] for name in ['necroskeleton','necromage']}
        for name,row in tables[table].items():
            for key,value in row.items():
                if key!='lineNumber' and key in local[name]:
                    expected=str(value).strip(chr(34)) if key.endswith('v') else str(value)
                    actual=local[name][key].strip(chr(34)) if key.endswith('v') else local[name][key]
                    assert expected==actual,(table,name,key);compared+=1
        pins[table]=dict(cachedSha256=sha(cached),localSha256=sha(local_root/filename),localFilename=filename)
    animation=local_root/'AnimData.d2';pin=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert sha(animation)==pin['sha256'] and animation.stat().st_size==pin['size']
    raw=animation.read_bytes();offset=0;records=[]
    for _ in range(256):
        count=struct.unpack_from('<I',raw,offset)[0];offset+=4
        for _ in range(count):
            code=raw[offset:offset+8].split(b'\0')[0].decode('ascii');frames,speed=struct.unpack_from('<II',raw,offset+8)
            if code.startswith('SK'):records.append(dict(code=code,frames=frames,speed=speed,events=[[i,v] for i,v in enumerate(raw[offset+16:offset+160]) if v]))
            offset+=160
    assert offset==len(raw) and len(records)==25
    by={x['code']:x for x in records}
    assert by['SKA11HS']==dict(code='SKA11HS',frames=16,speed=224,events=[[10,1]])
    assert by['SKA1HTH']==dict(code='SKA1HTH',frames=16,speed=208,events=[[8,2]])
    assert [tables['monstats']['necroskeleton'][k] for k in ['AI','Code','Velocity','Run']]==['NecroPet','SK',13,15]
    assert [tables['monstats']['necromage'][k] for k in ['AI','Code','Velocity','Run']]==['NecroPet','SK',12,14]
    review=ROOT/'tools/reference/skeleton-components-review.json';ai=ROOT/'tools/reference/necromancer-pet-ai-source-evidence.json'
    result=dict(referenceVersion='3.3.93847',primaryDataCommit=manifest['commit'],localTables=pins,corroboratedFields=compared,componentQuoteNormalization='Component variant text strips outer CSV quotation marks before comparing;variant order and contents remain exact.',currentRows=tables,
        animation=dict(sha256=pin['sha256'],archivePath=pin['archivePath'],extractionEvidence='tools/reference/d2r-local-animation-evidence.json',records=records),
        sourceReviews={str(p.relative_to(ROOT)):sha(p) for p in [review,ai]},
        established=['Current SKA11HS uses16frames,speed224 and action1 at index10. SKA1HTH uses16frames,speed208 and skill action2 at index8. Mage and regular skeleton must not share the old fixed half-cycle action time.',
            'Current regular skeleton velocity13/run15 and Mage12/run14 share AI NecroPet. Their model sizes are2x2,raw reach0;the melee base weapon class is1hs and Magehth. All25owned SK rows are retained rather than substituting G4 or player animations.',
            'Previously verified complete AI67 policy can be reused;the complete-body component review separately records the unresolved final shield distribution.'],
        remaining=['Animation speed/frame/event records alone do not execute a world action clock. Source mode/default stat/speed/scheduler logic,initial S1 overlap,hit/skill release and movement still require native integration and save acceptance.',
            'Complete current initializer,regional pools,full-set cosmetic level,seed ownership,ordinary attack/critical/item and incoming events,geometry/global phase/client parity remain unverified. No learned actor or data profile is changed by this preflight.'])
    p=ROOT/'tools/reference/skeleton-world-preflight.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print(f'Skeleton/Mage world reference:{compared}corroborated table fields and25current SK animation rows;no world acceptance claimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--local-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();build(a.local_root,a.check)
