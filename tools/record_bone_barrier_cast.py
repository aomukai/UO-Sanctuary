#!/usr/bin/env python3
"""Bind bounded barrier cast/allocation acceptance without certifying native spawn search."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/bone-barrier-cast'
sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/bone-barrier-birth-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit'];assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text());before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/BoneBarrierCasts.cs','src/Sanctuary.Content/GroundSummonPlacement.cs','tests/Sanctuary.Integration/BoneBarrierCastChecks.cs'}
    assert changed=={'src/Sanctuary.Content/BoneBarriers.cs','src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/DruidSpiritWolfCasting.cs','src/Sanctuary.Content/NecromancerCombat.cs','tests/Sanctuary.Integration/BoneBarrierBirthChecks.cs','shared/ClassAbilities.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3,'bone-barriers-result':1,'bone-barrier-birth-save':3})
    for n in ['iron-golem-items','npc-resource-callbacks','bone-barrier-cast','bone-barrier-birth','bone-barriers','spirit-wolf-casting']:expected[n+'-check']=1;expected[n+'-reload-check']=2
    for n in ['bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right']:expected[n+'-check']=expected[n+'-result']=3
    log=STAGE/'birth-native-checks.log';assert markers(log)==expected and sum(expected.values())==43
    text=log.read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for n,count in [('named-item-runtime-audit.json',858),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/bone-barrier-birth'/n).read_text());assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert 'Eight classes, 240 source skills, stable ability IDs and asset requirements verified.' in (STAGE/'catalogue-check.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    s=json.loads((STAGE/'bone-barrier-birth-fixtures.json').read_text());assert s['Generation']==2 and s['Life']==s['State']['Numbers']['Life256']-257
    cast=json.loads((STAGE/'bone-barrier-cast-fixtures.json').read_text());assert cast['Right']==79 and cast['Mana']>0
    source=ROOT/'tools/reference/bone-barrier-cast-source-evidence.json';proof=json.loads(source.read_text());assert proof['referenceVersion']=='3.3.93847' and proof['cases']==192 and len(proof['reviewedNotExecuted'])==7 and len(proof['executed'])==4
    for p,h in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/bone-barrier-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,source,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tools/probe_bone_barrier_cast.py',ROOT/'tools/build_class_skills.py',ROOT/'patches/ClassicUO/0035-allied-class-skill-targeting.patch',ROOT/'patches/ClassicUO/0036-ally-targeted-left-and-held-skills.patch',ROOT/'tests/check_bone_barrier_cast.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/first-loop-affix-audit.json']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','bone-barrier-birth-fixtures.json','bone-barrier-checks.json','spirit-wolf-casting-fixtures.json','bone-barrier-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Bone Wall/Prison targets, allocation outcomes and shared native placement',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=43,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[858,21],unchangedInstalledArtifacts=106,connectedBarrierCasts=6,connectedWolfCasts=3,naturalTwentyFourSecondExpiries=6,sourceContexts=192,nativeSourceOutcomeContexts=8,reviewedAdditionalBodies=7,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Wall occupied-target and failed-exact allocation retry through the shared GroundSummonPlacement projection extracted from Wolf without changing its predicate/order. New roots grow from the successful actual position. Wall fails without a root; Prison reports accepted completion and pays once even with no segments.',
            'Prison selected living-mobile admission includes self and friendly units, with native caster/target town and range/LOS gates. Held input uses the same admission. Allocation follows original source anchor order for bottom/left pairs, and first successful root deletion removes its children; raster connectors remain the existing adapter.',
            'Eight native even-failure allocation/order/payment controls match the complete callback fixture. Six real occupied-Wall/right and self-Prison/quick casts pay once and retire at the native24second deadline. Existing barrier birth/live-save/collision/firebolt and Wolf casting/three real Wolf inputs pass.43markers/3boots/2reloads/3saves,4audits/71Python/573unchanged named decisions,858/21fingerprints and clean builds;data/kernel/installed106unchanged.'],
        remaining=[
            'Pinned SpawnNormalMonster is a null stub plus commented decompile. Native two-tile perimeter/height search remains an adapter for exact/parameter4 spawn calls; source room/footprint/RNG/candidate ordering and full current-executable world behavior are unverified.',
            'Native payment remains the shared release-settlement framework. Legacy Prison start19 phase and rank1 consume quirk differ from current-table values; exact current cancellation/charged/Blood Mana is unresolved. Empty-ground SearchEnemyNear acquisition and item/corpse/targetable flag mapping remain unsupported/uncertain.',
            'Wall-maker growth/collision, prison raster connectors, source AI/death/minion phase and actual old-build upgrade remain unfinished. No whole skill/class or extended cohort certificate. Effects/audio and talent/hotkey icons remain separate placeholders; approved pet upgrades/safe-area parking queued.'])
    dest=ROOT/'tools/reference/bone-barrier-cast-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Barrier casts:43 checks/6 barrier and3 Wolf real casts/2reloads/192 source contexts; spawn/payment gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
