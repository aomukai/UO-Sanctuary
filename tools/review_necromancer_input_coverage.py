#!/usr/bin/env python3
"""Account for actual Necromancer input routes, separate from handler counts."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUTPUT=ROOT/'tools/reference/necromancer-current-input-coverage.json'
STAGES=('necromancer-lifecycle-acceptance','necromancer-terror-input')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def review(check=False):
    skills=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())
    rows=[s for s in skills if s['characterClass']=='Necromancer'];active={s['ability']:s for s in rows if not s['passive']};passive=[s for s in rows if s['passive']]
    assert len(rows)==30 and len(active)==27 and len(passive)==3
    protocol=ROOT/'shared/DiabloClass.cs';class_id=int(re.search(r'\bNecromancer\s*=\s*(\d+)',protocol.read_text()).group(1))
    found={ability:{}for ability in active};files=[Path(__file__).resolve(),ROOT/'data/Sanctuary/Balance/class-skills.json',protocol];artifacts=None;data=None;observed=0;foreign=0
    for name in STAGES:
        stage=ROOT/'.local/class-skills'/name;path=stage/'birth-native-checks.log';log=path.read_text();born_path=stage/'birth-inputs.json';born=json.loads(born_path.read_text())
        assert ' ERR]'not in log and ': failed'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3,name
        assert 'pass across3boots/2reloads'in (stage/'native-run-1.log').read_text(),name
        if artifacts is None:artifacts=born['artifacts'];data=born['copiedData']
        assert born['artifacts']==artifacts and born['copiedData']==data,'Input cohorts use different binaries/data'
        for source,digest in born['sourceInputs'].items():
            if source.endswith('.cs'):assert sha(ROOT/source)==digest,source
        passed=Counter(re.findall(r'\b([\w-]+): passed(?:\s|$)',log))
        ready=Counter((label,int(ability),int(cls))for label,ability,cls in re.findall(r' ([\w-]+): ready ability=(\d+) class=(\d+)',log))
        for (label,ability,cls),count in ready.items():
            assert count==3 and passed[label+'-result']==count and passed[label+'-check']==count,(name,label,count,passed[label+'-result'])
            if cls!=class_id:foreign+=count;continue
            assert ability in active,(name,label,ability)
            found[ability][label]=dict(stage=name,successfulInputs=count);observed+=count
        files.extend((path,born_path,stage/'native-run-1.log'))
    assert all(found.values()),[active[a]['talentId']for a,routes in found.items()if not routes]
    for p,d in data.items():assert sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p,d in artifacts.items():
        current=ROOT/'.local/class-skills/cold-corpse-rule-adoption'
        target=current/'content/Sanctuary.Content.dll'if p=='Assemblies/Sanctuary.Content.dll'else current/'integration/Sanctuary.Integration.dll'if p=='Assemblies/Sanctuary.Integration.dll'else current/'native/Distribution'/p
        assert sha(target)==d,p
    result=dict(referenceVersion='3.3.93847',classId=class_id,talents=30,activeTalents=27,passiveTalents=3,verifiedActiveInputRoutes=27,successfulNecromancerInputs=observed,foreignInputsSeparatelyExcluded=foreign,bootsPerCohort=3,reloadsPerCohort=2,artifacts=artifacts,
        skills=[dict(talentId=active[a]['talentId'],ability=a,routes=dict(sorted(found[a].items())))for a in sorted(active)],
        passiveSkills=[dict(talentId=s['talentId'],ability=s['ability'],left=s['left'],right=s['right'])for s in passive],
        classCertified=False,limits='Readiness fields are matched to successful real input-result markers on all three boots and to the pinned catalogue ability/class IDs. Cohorts must share exact binaries/data. Passive mechanics require their separate birth/recipient evidence;they receive no invented cast. This accounts for active input routes,not all mechanics,current-engine/PvP/client parity or whole-class completion.',files={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))})
    assert all(not s['left']and not s['right']for s in passive)
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Necromancer input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Necromancer inputs:27 active talents,{observed} successful class inputs;3 passive talents stay separate.")
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
