#!/usr/bin/env python3
"""Shared read-only accounting for completed native class-input cohorts."""
from collections import Counter
import hashlib,json,re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def collect(character,stages,negative_prefixes=(),held=None,legacy=None,basic_controls=()):
    registry=ROOT/'data/Sanctuary/Balance/class-skills.json';enum=ROOT/'shared/DiabloClass.cs'
    rows=[s for s in json.loads(registry.read_text())if s['characterClass']==character]
    active={s['ability']:s for s in rows if not s['passive']};passive=[s for s in rows if s['passive']]
    assert len(rows)==30 and len(active)+len(passive)==30
    cls=int(re.search(r'\b'+re.escape(character)+r'\s*=\s*(\d+)',enum.read_text()).group(1))
    found={ability:{}for ability in active};files=[Path(__file__).resolve(),registry,enum];artifacts=None;data=None
    ordinary=foreign=negative=held_count=legacy_count=basic_count=0
    for stage in stages:
        path=stage/'birth-native-checks.log';log=path.read_text();born_path=stage/'birth-inputs.json';born=json.loads(born_path.read_text())
        assert ' ERR]'not in log and ': failed'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3,stage
        if artifacts is None:artifacts=born['artifacts'];data=born['copiedData']
        assert born['artifacts']==artifacts and born['copiedData']==data
        for source,digest in born['sourceInputs'].items():
            if source.endswith('.cs'):assert sha(ROOT/source)==digest,source
        passed=Counter(re.findall(r'\b([\w-]+): passed(?:\s|$)',log))
        ready=Counter((label,int(a),int(c))for label,a,c in re.findall(r' ([\w-]+): ready ability=(\d+) class=(\d+)',log))
        for (label,ability,owner_class),count in ready.items():
            assert count==3 and passed[label+'-result']==count and passed[label+'-check']==count,(stage,label,count)
            if owner_class!=cls:foreign+=count;continue
            if label in basic_controls:
                assert ability==1,(label,ability)
                basic_count+=count;continue
            assert ability in active,(label,ability)
            ordinary+=count
            if label.startswith(negative_prefixes):negative+=count;continue
            found[ability][label]=dict(stage=stage.name,successfulInputs=count,evidence='actual ready class/ability plus successful check/result')
        for spec in legacy or ():
            # Legacy Cast packets omit the class byte. Verify the compiled
            # fixture's owner/default and observed ability instead of inventing
            # a class field or counting repeated phases as new scenarios.
            source=ROOT/spec['ownerSource'];assert born['sourceInputs'][spec['ownerSource']]==sha(source)
            for fragment in spec['ownerFragments']:assert fragment in source.read_text(),(source,fragment)
            ability=next(a for a,row in active.items()if row['talentId']==spec['talentId'])
            label=spec['label'];starts=re.findall(r' '+re.escape(label)+r': ready ability=(\d+) x=',log)
            assert starts==[str(ability)]*3 and passed[label+'-check']==passed[label+'-result']==3,(label,starts)
            found[ability][label]=dict(stage=stage.name,successfulLegacySequences=3,evidence='actual legacy Cast ability/check/result;class from compiled fixture ownership/default')
            legacy_count+=3;files.append(source)
        if held:
            # Held packets carry an ability and input source,not a class byte.
            # Bind their class explicitly to the compiled fixture owner and
            # normal availability guard;do not invent a logged class field.
            owner_source=ROOT/held['ownerSource'];guard=ROOT/held['guardSource']
            assert born['sourceInputs'][held['ownerSource']]==sha(owner_source)
            assert born['sourceInputs'][held['guardSource']]==sha(guard)
            source=owner_source.read_text();assert held['ownerAssignment']in source and held['wireOwner']in source
            assert held['availabilityGuard']in guard.read_text()
            ability=next(a for a,s in active.items()if s['talentId']==held['talentId'])
            for label in held['labels']:
                starts=re.findall(r' '+re.escape(label)+held.get('readyPattern',r': ready ability=(\d+) x='),log)
                assert starts==[str(ability)]*3 and passed[label+'-result']==3
                if 'phases' in held:
                    for phase in held['phases']:
                        assert len(re.findall(r' '+re.escape(label+': '+phase)+r'(?:\s|$)',log))==3,(label,phase)
                else:assert log.count(label+': pressed')==log.count(label+': burning')==3
                found[ability][label]=dict(stage=stage.name,successfulHeldSequences=3,evidence=held.get('evidence','actual held ability/press/burning/result;class from compiled owner and availability guard'))
                held_count+=3
            files.extend((owner_source,guard))
        files.extend((path,born_path))
    assert all(found.values()),[active[a]['talentId']for a,routes in found.items()if not routes]
    assert all(not s['left']and not s['right']for s in passive)
    for p,d in data.items():assert sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    result=dict(referenceVersion='3.3.93847',characterClass=character,classId=cls,talents=30,activeTalents=len(active),passiveTalents=len(passive),verifiedActiveInputRoutes=len(found),ordinaryInputCases=ordinary,negativeInputCasesSeparatelyExcluded=negative,heldInputSequences=held_count,foreignInputCasesSeparatelyExcluded=foreign,artifacts=artifacts,
        skills=[dict(talentId=active[a]['talentId'],ability=a,routes=dict(sorted(found[a].items())))for a in sorted(active)],
        passiveSkills=[dict(talentId=s['talentId'],ability=s['ability'])for s in passive],classCertified=False,
        limits='Every admitted route has successful input results on three boots. Negative controls do not establish a positive cast. Held class context comes from compiled owner/guard evidence rather than an invented log field. Passive mechanics remain separate birth/stat evidence. This is input accounting,not whole-class mechanics or exact engine/client/PvP certification.',
        files={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))})

    if legacy is not None:result['legacyInputSequences']=legacy_count
    if basic_controls:result['basicAttackControlsSeparatelyExcluded']=basic_count
    return result
