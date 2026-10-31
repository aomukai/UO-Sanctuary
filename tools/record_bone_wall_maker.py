#!/usr/bin/env python3
"""Seal independent maker acceptance, keeping world and death-phase gaps explicit."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bone-wall-maker';sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/bone-barrier-cast-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/BoneWallMakerBody.cs','src/Sanctuary.Content/BoneWallMakerUpdate.cs','tests/Sanctuary.Integration/BoneWallMakerChecks.cs'}
    assert changed=={'src/Sanctuary.Content/BoneBarriers.cs','tests/Sanctuary.Integration/BoneBarrierChecks.cs','tests/Sanctuary.Integration/BoneBarrierBirthChecks.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3,'bone-barriers-result':1,'bone-wall-maker-save':3,'bone-wall-maker-result':1,'bone-wall-maker-reload-result':2})
    for n in ['iron-golem-items','npc-resource-callbacks','bone-wall-maker','bone-barrier-cast','bone-barrier-birth','bone-barriers','spirit-wolf-casting']:
        expected[n+'-check']=1;expected[n+'-reload-check']=2
    for n in ['bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right']:expected[n+'-check']=expected[n+'-result']=3
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==49
    text=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for n,count in [('named-item-runtime-audit.json',860),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/bone-barrier-cast'/n).read_text())
        assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    saved=json.loads((STAGE/'bone-wall-maker-fixtures.json').read_text());assert saved['Growth']['Frames']==9 and saved['Growth']['Left']['Remaining']==7
    assert saved['Life']==saved['Birth']['Numbers']['Life256']-257
    source=ROOT/'tools/reference/bone-wall-maker-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['cases']==dict(maker=2592,blockedThenSuccessfulSequence=6,ai=96)
    for p,h in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/bone-wall-maker-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,source,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tests/check_bone_wall_maker.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','bone-wall-maker-fixtures.json','bone-barrier-birth-fixtures.json','bone-barrier-checks.json','spirit-wolf-casting-fixtures.json','bone-barrier-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Bone Wall independent maker growth',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=49,boots=3,actualReloads=2,saves=3,sourceMakerContexts=2592,sourceAiContexts=96,sourceSequenceFrames=6,nativeSourceMakerContexts=2592,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[860,21],unchangedInstalledArtifacts=106,connectedBarrierCasts=6,connectedWolfCasts=3,naturalTwentyFourSecondExpiries=6,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'New Wall roots use a saved subtype with independent Q12-vector/slow/formula/step/update arms. Maker allocation precedes movement/lifetime/contact. Only actual child creation spends the success budget; failed exact creation continues. Full maker source gates/order match2592 native contexts.',
            'Native blocked-then-successful allocation, duplicate rejection, birth-recipe inheritance, first-step ordering, natural maker lifetime and root deletion pass. Two live partial-growth reloads preserve actor/owner/vector/trace/budget/remaining frames/recipe/wounds/deadline/collision, then resume naturally. Persistence fixture holds only its growth clock until verified; normal24second barrier lifetime is never extended.',
            'Retained casting/allocation/payment/birth/collision/firebolt/Wolf/item/resource scopes and six real barrier/three Wolf casts pass. Historical plain/prepared layouts and data/kernel/installed106 remain unchanged. No skill or class completion.'],
        remaining=[
            'Native fit/town/source-cell-to-tile and LOS/contact candidate services remain adapters. Exact2x2 footprint/room allocation, initial factory/path flags, mode8 mask/finder ordering, source missile hit events and current executable/client equivalence are unverified. No exact wall length or shared-world geometry certificate.',
            'Root-shared native24second expiry remains; strict source AI deadline, per-child birth deadline, first-think/death/hidedead/minion scheduling, Prison raster geometry and actual old-build upgrade remain open. Held-clock reloads do not establish snapshot concurrency while natural growth adds a child. Historical types retain their prior growth rather than being silently migrated.',
            'Existing spawn-search/payment/acquisition gaps remain explicit. Effects/audio and talent/hotkey icons remain separate placeholders; approved pet upgrades and safe-area parking queued.'])
    out=ROOT/'tools/reference/bone-wall-maker-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Wall makers:49native markers/2592 source-matched contracts/2live reloads; world/death phase gaps retained.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
