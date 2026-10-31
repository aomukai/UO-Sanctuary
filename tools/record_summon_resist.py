#!/usr/bin/env python3
"""Bind existing shared Summon Resist behavior; do not hide curve uncertainty."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/summon-resist';sha=inventory.sha
def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/bone-barrier-clock-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(after)-set(before)=={'tests/Sanctuary.Integration/SummonResistChecks.cs'}
    assert all(after[p]==h for p,h in before.items()),'Existing production/integration source changed'
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3})
    for n in ['iron-golem-items','npc-resource-callbacks','summon-resist']:expected[n+'-check']=1;expected[n+'-reload-check']=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==12
    text=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for n,count in [('named-item-runtime-audit.json',861),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/bone-barrier-clock'/n).read_text())
        assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    saved=json.loads((STAGE/'summon-resist-fixtures.json').read_text());assert saved['Rank']==19 and len(saved['Pets'])==19
    assert Counter(r['Captured'] for r in saved['Pets'])==Counter({52:6,61:6,65:6,66:1})
    assert {r['Type'] for r in saved['Pets']}=={'RaisedSkeleton','NecromancerMinion','ClayGolemBody','BloodGolemBody','FireGolemDeathBody','IronGolemBody'}
    assert all(r['Life']==r['Maximum']-257 for r in saved['Pets'])
    source=ROOT/'tools/reference/summon-resist-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['cases']==13824 and proof['acceptedAllocationContexts']==6912
    assert len(proof['executed'])==3 and len(proof['staticallyReviewed'])==6
    for p,h in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/summon-resist-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,source,Path(__file__),ROOT/'tools/probe_summon_resist.py',ROOT/'tests/Sanctuary.Integration/SummonResistChecks.cs',ROOT/'tests/check_summon_resist.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',*[ROOT/p for p in docs]]
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','summon-resist-fixtures.json','iron-golem-items-fixtures.json','npc-resource-callbacks-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Existing Summon Resist passive and captured recipient bonuses',installed=False,gameplayChanged=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=12,boots=3,actualReloads=2,saves=3,sourceReceiverContexts=13824,nativeAcceptedReceiverContexts=6912,curveRanks=256,liveRecipients=19,liveKinds=6,currentTooltipRanks=[7,13,19],auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[861,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Existing SkillValues/NecromancerSummons/IronGolemBirth and prepared recipient factories are reused unchanged. All256 retained curve ranks and6912 complete accepted-allocation source channel outcomes match. Source failure allocation contexts remain source-only; native planner does not claim to simulate allocator failure.',
            'Actual warrior/Mage/Clay/Blood/Fire/Iron recipients at effective7/13/19 inherit52/61/65. Fire absorption excludes fire SR; Iron original Crown contributes33fire plus intrinsic species channels. Physical/magic unchanged. Actual equipped Wormskull gives20/66 to a fresh skeleton; existing pets retain captured bonuses through training and equip/removal. Passive rejects active/right/quick binding without mana change.',
            'Both reloads retain all19 actual actors,types/owners/raw resistances/exact wounds/maxima and effective owner rank. Direct component creation does not re-certify learned corpse/mana/golem-cap transactions. Existing item/resource checks pass; no production code/data/kernel change or deployment.'],
        remaining=[
            'Specific Summon Resist current-executable rounding at distinguishing ranks3/21 remains unverified. Supplied7/13/19tooltips agree with both models. Cross-skill Dodge3/Avoid3 calibrate shared retained dm12 but are not a direct SR-specific proof; legacy and retained outputs stay separate.',
            'Full modern passive refresh/item/oskill/Sunder/event/allocator/dynamic recipient/AI/current-client parity and Revive/boundary exceptions remain explicit. Reviewed missing direct helper calls are not a full current-engine proof. Effects/audio and tree/hotkey icons separately pending; approved pet upgrades/safe-area parking queued. No complete skill/class certificate.'])
    out=ROOT/'tools/reference/summon-resist-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Summon Resist:12native markers/19actual recipients/6912source matches/2reloads; gameplay reused unchanged and rounding gap retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
