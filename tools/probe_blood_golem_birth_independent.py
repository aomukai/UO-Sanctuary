#!/usr/bin/env python3
"""Execute the reviewed summon helpers with independently varied Blood synergies."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/blood-golem-birth-independent-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent=ROOT/'tools/reference/blood-golem-birth-source-evidence.json';prior=json.loads(parent.read_text())
    for group in ['inputs','artifacts']:
        for path,digest in prior[group].items():assert sha(ROOT/path)==digest,path
    original=ROOT/'.local/class-skills/blood-golem-birth-source/probe.cpp';cpp=original.read_text()
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
    replace('clay=0,blood=0,fire=0','clay=0,iron=0,fire=0')
    replace('case 3:return 35*clay;','case 3:return 35*iron;')
    replace('pattern<5','pattern<7')
    replace('gm=pattern==0?0:pattern==1?1:pattern==2?20:pattern==3?28:255;clay=blood=fire=pattern==0?0:pattern==1?1:20;',
        'gm=pattern==0?0:pattern==1?1:pattern==2?20:pattern==3?28:pattern==4?255:pattern==5?8:2;clay=pattern==0?0:pattern==1?1:pattern<5?20:pattern==5?4:0;iron=pattern<5?clay:pattern==5?3:7;fire=pattern<5?clay:pattern==5?4:11;')
    replace('level,r,diff,gm,blood,clay,fire,','level,r,diff,gm,iron,clay,fire,')
    from review_weapon_event_order import function
    for name,digest in prior['functions'].items():
        assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest,name
    STAGE.mkdir(parents=True,exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fwrapv','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    rows=json.loads(run.stdout);assert len(rows)==16065
    old=json.loads((ROOT/'.local/class-skills/blood-golem-birth-source/cases.json').read_text());assert [r for r in rows if r[3] not in (8,2)]==old
    independent=[r for r in rows if r[3] in (8,2)];assert len(independent)==4590 and any(r[4]!=r[5]!=r[6] for r in independent)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',cases=len(rows),retainedOriginalCases=len(old),independentSynergyCases=len(independent),unsafePercentageCases=sum(r[8] for r in rows),nonpositiveWrappedLifeCases=sum(r[10]<=0 for r in rows),
        columns=['ownerLevel','effectiveRank','difficulty','effectiveMastery','hardIron','hardClay','hardFire','summonResistance','unsafe','level','maxLife256','life256','armor','attack','regeneration256','velocityPercent','damagePercent','unusedSlow','equipmentLevel','events','fireResistance','lightningResistance','coldResistance','poisonResistance'],
        functions=prior['functions'],localTables=prior['localTables'],inputs={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__),parent,original]},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [source,fixture,compiler,san]},
        established=['Retains all11475original contexts and executes4590additional contexts separating hard Clay/Iron/Fire ranks. Complete shared helper bodies are unchanged;formula/stat services remain explicit adapters.',
            'The independent rows distinguish Clay attack-rating,Iron armor and Fire damage synergies. Current owned-table corroboration remains148fields. Event23/26registrations are counted,not executed.'],
        remaining=['Native casting/resources/facets/buffs/save acceptance and full event/initializer/equipment/AI/current-executable/client parity remain separate.'])
    path=ROOT/'tools/reference/blood-golem-birth-independent-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print(f'Blood birth:{len(rows)}complete source contexts,{len(independent)}independent synergy rows,{result["unsafePercentageCases"]}explicit refusals.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
