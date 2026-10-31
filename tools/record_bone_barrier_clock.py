#!/usr/bin/env python3
"""Seal barrier AI scheduling without certifying unresolved death/mode phases."""
import argparse,json,re
from collections import Counter
from datetime import datetime,timedelta
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bone-barrier-clock';sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/bone-wall-maker-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/BoneBarrierThinkClock.cs','tests/Sanctuary.Integration/BoneBarrierClockChecks.cs'}
    assert changed=={'src/Sanctuary.Content/BoneBarriers.cs','tests/Sanctuary.Integration/BoneBarrierBirthChecks.cs','tests/Sanctuary.Integration/BoneWallMakerChecks.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3,'bone-barriers-result':1,'bone-barrier-clock-save':3,'bone-wall-maker-result':1,'bone-wall-maker-reload-result':2,'bone-barrier-clock-result':1,'bone-barrier-clock-reload-result':2})
    for n in ['iron-golem-items','npc-resource-callbacks','bone-barrier-clock','bone-wall-maker','bone-barrier-cast','bone-barrier-birth','bone-barriers','spirit-wolf-casting']:
        expected[n+'-check']=1;expected[n+'-reload-check']=2
    for n in ['bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right']:expected[n+'-check']=expected[n+'-result']=3
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==55
    text=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    observed=re.findall(r'\b(bone-wall-target-right|bone-prison-self-quick): death request frame=(\d+) thinks=(\d+)',text)
    assert Counter(row[0] for row in observed)==Counter({'bone-wall-target-right':3,'bone-prison-self-quick':3})
    assert all(int(frame)>600 and int(thinks)>0 for _,frame,thinks in observed)
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for n,count in [('named-item-runtime-audit.json',861),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/bone-wall-maker'/n).read_text())
        assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    saved=json.loads((STAGE/'bone-barrier-clock-fixtures.json').read_text());assert len(saved)==5
    clocks=[r for r in saved if r['Clock'] is not None];assert len(clocks)==3
    assert {r['Type'] for r in saved}=={'BoneWallThinkingBody','BoneBarrierThinkingBody','BoneBarrierSegment','BoneBarrierBody'}
    for row in clocks:
        s=row['Clock'];assert (s['Deadline'],s['Next'],s['Last'],s['Thinks'],s['DeathRequested'])==(600,610,595,39,False)
        assert datetime.fromisoformat(row['Until'])==datetime.fromisoformat(s['BornUtc'])+timedelta(seconds=24)
    source=ROOT/'tools/reference/bone-barrier-retirement-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['cases']==128 and len(proof['executed'])==7 and len(proof['staticallyReviewed'])==13
    for p,h in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/bone-barrier-clock-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,source,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tests/check_bone_barrier_clock.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','bone-barrier-clock-fixtures.json','bone-wall-maker-fixtures.json','bone-barrier-birth-fixtures.json','bone-barrier-checks.json','spirit-wolf-casting-fixtures.json','bone-barrier-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Bone Wall/Prison AI first-think and expiry clocks',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=55,boots=3,actualReloads=2,saves=3,sourceClockContexts=128,nativeSourceClockContexts=128,retainedMakerSourceContexts=2592,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[861,21],unchangedInstalledArtifacts=106,connectedBarrierCasts=6,connectedWolfCasts=3,handlers=186,necromancerHandlers=30),naturalDeathRequests=[dict(family=n,frame=int(f),thinks=int(t)) for n,f,t in observed],
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'New saved barrier subtypes observe first AI event25/deadline600/strict comparison/idle15. One actual observed frame preserves delayed phases; no synthetic catch-up.128 complete source schedules match native clock results, including equality600->615. New bodies reuse existing UsesNativeSummonTimer=false ownership hook.',
            'Later-created children capture their own birth-relative deadline and preserve recipe/root links. Historical plain/prepared/maker types keep their layouts and expiry/timer ownership. Two reloads preserve three inspected partial AI clocks and two historical typed actors, exact wounds/owner/deadline/collision; observed next610 resumes native removal. This is not an actual old-build byte upgrade.',
            'Six real barrier casts pay once and retire through DeathRequested frames>600; actual observations are listed. Retained maker/casting/birth/collision/firebolt/Wolf/item/resource scopes pass. Data/kernel/installed106 unchanged; no deployment or class completion.'],
        remaining=[
            'A death request still maps to the prior native deletion/root-cleanup adapter. Source DEATH mode entry/DT15/hidedead/minion graph/collision release and full current-executable/room/stun/leash/inactivity routing remain unverified. This acceptance covers the AI schedule, not the complete death phase.',
            'Save fixtures inspect clock through595 without moving BornUtc/deadline, then inspect610 after reload; separate real-input checks use natural observations. No lifetime extension, unheld snapshot concurrency or full elapsed-time retirement-save proof. Exact footprint/Prison geometry/acquisition/payment/search and actual old-build upgrade remain open.',
            'Effects/audio and talent/hotkey icons remain separate placeholders; approved pet upgrades and safe-area parking queued.'])
    out=ROOT/'tools/reference/bone-barrier-clock-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Barrier AI clocks:55native markers/128source schedules/2reloads/6natural AI-expiry casts; full death phase remains open.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
