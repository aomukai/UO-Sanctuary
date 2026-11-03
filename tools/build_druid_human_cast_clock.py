#!/usr/bin/env python3
"""Project the already verified owned human/form SC records without recompiling an oracle."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    source=ROOT/'tools/reference/druid-form-cast-clock-engine-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['contexts']==2590 and not proof['currentExecutableParity']
    assert sha(ROOT/'.local/research/d2r-3.3.93847/AnimData.d2')==proof['animationSha256']
    fixture=ROOT/proof['fixture']['path'];assert sha(fixture)==proof['fixture']['sha256']
    cpp=ROOT/'.local/class-skills/druid-form-cast-clock-source-probe.cpp';assert sha(cpp)==proof['generatedSourceSha256']
    rows=[]
    for row in proof['animations']:
        if not row['code'].startswith('DZSC'):continue
        assert row['frames']==15 and row['speed']==208 and row['events']==[[8,1]]
        rows.append(dict(WeaponClass=row['code'][4:],Frames=15,Speed=208,Action=8,Flag=1))
    rows.sort(key=lambda r:r['WeaponClass']);assert len(rows)==8
    assert [r['WeaponClass'] for r in rows]==['1HS','1HT','2HS','2HT','BOW','HTH','STF','XBW']
    runtime=ROOT/'data/Sanctuary/Balance/druid-human-cast-clock.json';encoded=json.dumps(rows,indent=2)+'\n'
    if check:assert runtime.read_text()==encoded
    else:runtime.write_text(encoded)
    print('Druid human clock:eight owned DZSC records and existing2590case human/form source proof verified;no new oracle or form timing changes.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
