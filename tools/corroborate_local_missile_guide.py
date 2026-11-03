#!/usr/bin/env python3
"""Pin current shipped missile documentation; it is not executable equivalence."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
FILE=ROOT/'.local/research/d2r-3.3.93847/dataguide/data/files/missiles.js'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False,extract=False):
    pin=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text())
    install=Path(pin['installationPath']);reader=ROOT/'.local/class-skills/casc-reference-reader'
    expected=json.loads((ROOT/'tools/reference/d2r-local-dataguide-evidence.json').read_text())['reader']
    assert sha(reader)==expected['binarySha256'] and sha(reader.with_suffix('.cpp'))==expected['sourceSha256']
    for name,digest in [('.build.info',pin['buildInfo']['sha256']),('D2R.exe',pin['executable']['sha256'])]:assert sha(install/name)==digest
    if extract:subprocess.run([str(reader),str(install),'data:data/global/dataguide/data/files/missiles.js',str(FILE)],check=True)
    text=FILE.read_text();model=json.loads(text[text.index('=')+1:].strip().removesuffix(';'))
    names=('LastCollide','NextHit','NextDelay','CollideType','pSrvDoFunc')
    fields={f['name']:f['description'] for f in model['fields'] if f['name'] in names}
    assert len(fields)==5 and 'same skill cast' in fields['NextDelay']
    for name,digest in [('.build.info',pin['buildInfo']['sha256']),('D2R.exe',pin['executable']['sha256'])]:assert sha(install/name)==digest
    data=dict(referenceVersion='3.3.93847',installationEvidence='tools/reference/d2r-local-installation-evidence.json',
        buildInfoSha256=pin['buildInfo']['sha256'],executableSha256=pin['executable']['sha256'],reader=expected,
        file=dict(path=str(FILE.relative_to(ROOT)),archivePath='data:data/global/dataguide/data/files/missiles.js',sha256=sha(FILE),bytes=FILE.stat().st_size),fields=fields,
        established=['The pinned shipped NextDelay documentation explicitly describes sharing among missiles from the same skill cast. NextHit must be enabled. LastCollide is a distinct last-recipient identity.'],
        limits=['Documentation does not establish the precise NPC cast identity: individual sentry placement versus each NPC skill execution and descendants. The prepared Wake backend explicitly supplies a do125 execution scope; learned dispatch remains gated.',
                'No current executable scheduling, collision enumeration, retirement or full skill certificate follows from this read-only extraction. Raw shipped files remain local and ignored.'])
    out=ROOT/'tools/reference/d2r-local-missile-guide-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Owned missile guide:5fields pinned;same-skill-cast NextDelay;NPC execution identity remains explicit acceptance work.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--extract',action='store_true');a=p.parse_args();record(a.check,a.extract)
