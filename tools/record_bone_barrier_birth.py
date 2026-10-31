#!/usr/bin/env python3
"""Bind bounded barrier birth/resource acceptance without certifying growth geometry."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/bone-barrier-birth'
sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/attract-cast-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit'];assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text());before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/BoneBarrierBirth.cs','src/Sanctuary.Content/BoneBarrierBody.cs','tests/Sanctuary.Integration/BoneBarrierBirthChecks.cs'}
    assert changed=={'src/Sanctuary.Content/BoneBarriers.cs','src/Sanctuary.Content/NpcEquipmentRegeneration.cs','src/Sanctuary.Content/SummonDifficulties.cs','tests/Sanctuary.Integration/BoneBarrierChecks.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3,'bone-barriers-result':1,'bone-barrier-birth-save':3})
    for n in ['iron-golem-items','npc-resource-callbacks','bone-barrier-birth','bone-barriers','npc-equipment-regeneration']:expected[n+'-check']=1;expected[n+'-reload-check']=2
    for n in ['bone-wall-birth-right','bone-prison-birth-quick']:expected[n+'-check']=expected[n+'-result']=3
    log=STAGE/'birth-native-checks.log';assert markers(log)==expected and sum(expected.values())==34
    text=log.read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for n,count in [('named-item-runtime-audit.json',856),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/attract-cast'/n).read_text());assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    s=json.loads((STAGE/'bone-barrier-birth-fixtures.json').read_text());assert s['Generation']==2 and s['State']['Rank']==1 and s['Life']==s['State']['Numbers']['Life256']-257
    source=ROOT/'tools/reference/bone-barrier-birth-source-evidence.json';proof=json.loads(source.read_text());assert proof['referenceVersion']=='3.3.93847' and proof['cases']==21420 and len(proof['reviewedNotExecuted'])==8
    for p,h in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/bone-barrier-birth-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,source,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tools/probe_bone_barrier_birth.py',ROOT/'tests/check_bone_barrier_birth.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/first-loop-affix-audit.json']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','bone-barrier-birth-fixtures.json','bone-barrier-checks.json','npc-equipment-regeneration-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Bone Wall/Prison birth, FP health/recovery and owner facet resources',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=34,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[856,21],unchangedInstalledArtifacts=106,connectedCasts=6,naturalTwentyFourSecondExpiries=6,sourceContexts=21420,reviewedAdditionalBodies=8,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'New learned barriers capture fixed noRatio difficulty stats and exact passive/MAXHP callback results through the shared NecromancerMageBirth.Resources helper. FP256 life, native ceiling and callback recovery are delivered through SkillHealth and the existing NPC recovery clock. Barriers do not receive the skeleton base/level adjustment or Summon Resist.',
            'Effective cast rank and hard Armor/other-barrier ranks are captured independently. Owner difficulty transitions rescale existing wounds with the common life-percentage helper while bodies remain in the old map and keep their expiry. Native defense/resistance/level delivery and recovery/PMH/cancellation controls pass.',
            'New bodies have an immutable saved recipe in a new subtype. Each save stages a fresh normal-lifetime cohort; both reloads verify their actual prior actor, wounds, owner, recipe, deadline and collision link. No lifetime extension or same-actor-through-entire-suite claim. Historical plain type/layout/resource code is retained; actual old-build upgrade remains unverified.',
            'Six real rightWall/quickPrison casts pay once and retire bodies/collision after 24 seconds. Existing barrier collision/firebolt/destruction and NPC equipment recovery regressions pass. 34 native markers, four audits,71Python,573unchanged named decisions,856/21fingerprints and clean builds pass; runtime data/kernel/installed106 unchanged.'],
        remaining=[
            'Full spawn/player-count/room/RNG/equipment/event/AI/current-executable parity is outside the supplied fixed one-player noRatio services. Eight complete bodies are statically reviewed rather than executed. Source passive/percentage/MAXHP comparisons do not certify the complete engine.',
            'Occupied-target spawn fallback, Prison non-hostile eligibility and allocation-failure payment, growth placement/cadence, prison raster/order, terrain/projectile exceptions, root/death animation/minion timing, owner-death behavior and actual old-build migration remain unfinished. No whole skill/class or extended cohort completion claim.',
            'Bodies/effects/audio and talent/hotkey icons remain separate identified placeholders. Approved pet upgrades and safe-area parking remain queued.'])
    dest=ROOT/'tools/reference/bone-barrier-birth-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Bone barriers:34 relevant checks/6 real casts/2 reloads/21420 source contexts; growth/world gaps explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
