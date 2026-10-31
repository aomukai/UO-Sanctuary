#!/usr/bin/env python3
"""Exercise existing complete source stat/resource routines above player level99."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-high-level-resources-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def build():
    STAGE.mkdir(parents=True,exist_ok=True)
    prior_path=ROOT/'tools/reference/npc-equipment-resources-source-evidence.json';prior=json.loads(prior_path.read_text())
    source=ROOT/'.local/class-skills/npc-equipment-resources-source/source.cpp'
    assert sha(source)==prior['artifacts'][str(source.relative_to(ROOT))]
    raw=source.read_text();prefix,main=raw.split('int main(){',1)
    assert 'for(int kind:{0,1})' in main and 'for(int level:{1,47,99})' in main
    main=main.replace('for(int kind:{0,1})','for(int kind:{1})').replace('for(int level:{1,47,99})','for(int level:{100,104,255,352})')
    cpp=prefix+'int main(){'+main;path=STAGE/'source.cpp';path.write_text(cpp)
    compiled=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(STAGE/'probe')],capture_output=True,text=True)
    (STAGE/'compiler.log').write_text(compiled.stdout+compiled.stderr);assert compiled.returncode==0,compiled.stderr
    result=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr
    cases=json.loads(result.stdout);assert len(cases)==768
    assert {r[0][0] for r in cases}=={1} and {r[0][2] for r in cases}=={100,104,255,352}
    (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
    receipt=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],cases=768,
        scope='Existing complete unchanged item-stat graph/attachment/resource getter routines with NPC kind1 and positive levels100/104/255/352. Three channels and declared base/flat/level/percentage/attribute inputs. Source prefix is byte-identical;only main input domains change. This does not extend player progression or certify current executable/live pet metadata.',
        parent=dict(path=str(prior_path.relative_to(ROOT)),sha256=sha(prior_path),source=str(source.relative_to(ROOT)),sourceSha256=sha(source)),
        unchangedSourcePrefixSha256=hashlib.sha256(prefix.encode()).hexdigest(),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),path,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']})
    (ROOT/'tools/reference/npc-high-level-resources-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('NPC resource source:768complete retained-routine contexts at levels100/104/255/352;sanitizers clean.')

if __name__=='__main__':build()
