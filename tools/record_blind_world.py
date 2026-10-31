#!/usr/bin/env python3
"""Bind the bounded live Dim Vision bridge; do not certify full source AI parity."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/blind-world'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    smoke=ROOT/'.local/class-skills/blind-world-smoke'
    paths={'birth':smoke/'birth-inputs.json','audit':STAGE/'audit-inputs.json'}
    inputs={n:inventory.validate_inventory(p) for n,p in paths.items()}
    for n,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',paths[n])
        assert value['artifacts']==inputs['birth']['artifacts'] and value['copiedData']==inputs['birth']['copiedData']
    assert markers(smoke/'birth-native-checks.log')==Counter({'iron-golem-birth-check':3,'blind-policy-check':3,
        'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'blind-world-check':1,'blind-world-reload-check':2,'blind-world-live-result':3})
    smoke_log=(smoke/'birth-native-checks.log').read_text()
    assert ': failed' not in smoke_log and ' ERR]' not in smoke_log
    assert smoke_log.count('Shutting down')==smoke_log.count('Writing world save snapshot done')==3
    parent=ROOT/'tools/reference/blind-policy-native-evidence.json'
    prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/SourceBlind.cs','tests/Sanctuary.Integration/BlindWorldChecks.cs'}
    assert changed=={'src/Sanctuary.Content/NamedControl.cs','src/Sanctuary.Content/NecromancerCurseCasts.cs','src/Sanctuary.Content/NecromancerEffects.cs','tests/Sanctuary.Integration/ClassSkillChecks.cs'}
    assert set(before)<=set(after)
    data=inputs['birth']['copiedData'];previous=old['copiedData']
    assert set(data)-set(previous)=={'Data/Sanctuary/Balance/blind-ai-rules.json'}
    assert all(data[p]==h for p,h in previous.items())
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    cast_parent=ROOT/'tools/reference/dim-vision-cast-native-evidence.json'
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',852),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/blind-policy'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    audit=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit and ' ERR]' not in audit and audit.count('Shutting down')==1
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'named-readiness.log').read_text();assert '573 definitions accounted for' in text
    saved=json.loads((smoke/'blind-world-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Ai']['Fault'] is None
    assert saved['Ai']['Thinks']==1 and saved['Ai']['Draws']==5 and saved['Ai']['Goal'] is None
    assert not saved['Ai']['Combat'] and saved['Curse']['Ability']==83
    rules_path=ROOT/'tools/reference/blind-ai-rule-evidence.json';rules=json.loads(rules_path.read_text())
    assert rules['referenceVersion']=='3.3.93847' and rules['ruleCount']==23
    for p,h in [(rules['sourcePath'],rules['sourceSha256']),(rules['runtimePath'],rules['runtimeSha256']),('tools/build_blind_ai_rules.py',rules['producerSha256'])]:assert sha(ROOT/p)==h,p
    source_path=ROOT/'tools/reference/dim-vision-ai-source-evidence.json';source=json.loads(source_path.read_text())
    assert (source['contexts'],source['targetWrapperContexts'])==(12288,224)
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/blind-world-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/pet-adjustment-review.md']
    files=[parent,cast_parent,source_path,rules_path,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],
        ROOT/'data/Sanctuary/Balance/blind-ai-rules.json',ROOT/'tools/build_blind_ai_rules.py',ROOT/'tests/check_blind_world.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',
        ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/first-loop-affix-audit.json']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'audit-run-{run}.log','audit-inputs.json','audit-native-checks.log','python-tests.log','named-readiness.log']]
    files += [smoke/p for p in ['birth-inputs.json','birth-native-checks.log','run-2.log','blind-world-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Dim Vision bounded live blind controller',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=18,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[852,21],unchangedInstalledArtifacts=106,
            policyTickTacticsComparisonsPerBoot=12288,policyNoTargetComparisonsPerBoot=112,connectedNaturalClockChecks=3,currentTimingRows=23,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str(paths[n].relative_to(ROOT)),sha256=sha(paths[n])) for n in inputs},
        changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Dim Vision now invokes the tested policy for the Skeleton/Zombie/Megademon source families. Current pinned 23-row timing/range data selects neutral delays by configured facet. Actor-local source-algorithm RNG survives curse refresh, replacement and expiry; the historical D2 seed/global stream is not claimed.',
            'Explicit UTC decision and native-step deadlines prevent early polling/rerolls. Scoped target acquisition cannot grant ordinary distant harm. Controls exercise target-present/no-target idle, adjacent normal-weapon admission, recast/replacement/expiry continuity and actual native wander resumption across two reloads.',
            'Optional saved BlindAi state is outside the curse; existing version0 records remain readable. Capture native movement deadlines before an immutable worker serialization snapshot. Connected checks observe a target-present400ms idle, a no-target1000ms idle, no early decisions and subsequent same-actor adjacent reacquisition without editing the inspected deadlines.',
            'Focused18markers pass across3boots/2reloads/3saves with3connected target/no-target/return checks. Four shared Thorns/named/affix audit markers,71Python,573named,clean rebuilds and852/21fingerprints pass. Installed106artifacts unchanged;no deployment.'],
        remaining=[
            'No full521-marker acceptance is claimed. Extended attempts stopped on fixture faults;attempt5 passed its first full save and first blind-wander reload before an early timer-wheel observation failed. The repaired current fixture passes the focused18-marker cohort. Latest accepted full512cohort remains the prior Dim Vision cast checkpoint. The retained obstruction test timing changed350to650ms and advances only its saved step inspection clock;full retained regression remains pending.',
            'Full blind AI/skill/class is unfinished. Source acquisition/node ordering/LOS/ties and room/collision/path/mode services are native world adapters. Native reacquire gating, actor attack A1/action/recovery completion and initial deadline/lag behavior are not current-executable parity claims.',
            'Source dwAiState3/19 escape branches have no native mapping;the bridge supplies false. Distance5 collision displacement has policy coverage but no world acceptance. Unsupported AI families,ownership/conversion coexistence,facet transfers and failed path/projection cases retain flagged fallback behavior rather than invented source transitions.',
            'The40ms attack admission poll delegates to existing native weapon scheduling;it does not certify source attack-event cadence or a shared historical NPC RNG stream. Per-actor RNG initializes independently with the source algorithm.',
            'Pet upgrades and actual-safe-area parking remain documented design only. Existing wounds,normal effect expiry,explicit healer service and named mercenary exemption are approved parking constraints. Effects/audio and talent/hotkey icons remain separate placeholder ledgers.'])
    destination=ROOT/'tools/reference/blind-world-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==render
    else:destination.write_text(render)
    print('Bounded live blind controller:18focused markers,3boots/2reloads/3saves;full regression and AI/class parity remain unfinished.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
