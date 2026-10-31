#!/usr/bin/env python3
"""Bind exact Bone Armor absorption, learned inputs and legacy-compatible saves."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bone-armor';sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/skeleton-roster-native-evidence.json';prev=json.loads(previous.read_text());ref=prev['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'tests/Sanctuary.Integration/BoneArmorChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['NecromancerEffects','DefensiveArmors','WardrobeTransaction','SkillBindings']}
    assert inputs['birth']['copiedData']==old['copiedData']
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/skeleton-roster/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'bone-armor-check':1,'bone-armor-reload-check':2,'psychic-ward-check':1,'psychic-ward-reload-check':2,
        'bone-armor-right-check':3,'bone-armor-right-result':3,'bone-armor-quick-check':3,'bone-armor-quick-result':3,'class-skills-check':3})
    assert sum(expected.values())==172 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    from probe_bone_armor import build as probe
    probe(True)
    saved=json.loads((STAGE/'bone-armor-fixtures.json').read_text());assert len(saved)==2 and all(s['Generation']==2 for s in saved)
    assert saved[0]['Remaining256']==16637 and saved[0]['Pool']==dict(Rank=2,Remaining256=16637,Maximum256=16640)
    assert saved[1]['Remaining256']==5118 and saved[1]['Pool']==dict(Rank=None,Remaining256=5118,Maximum256=None) and saved[1]['ManaFraction']==128
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','5197dc4:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        audits.append(a)
    assert [len(a['InputHashes']) for a in audits]==[825,21] and len(audits[0]['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous,baseline,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/probe_bone_armor.py','tools/reference/bone-armor-source-evidence.json',
        'tests/check_bone_armor.py','tests/check_bone_armor_audits.py','docs/bone-armor-checkpoint.md','docs/necromancer-mechanical-audit.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log',
        'birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','bone-armor-fixtures.json',
        'failed-birth-run-1.log','failed-birth-inputs-1.json','failed-birth-run-2.log','failed-birth-inputs-2.json']]
    result=dict(referenceVersion='3.3.93847',component='Bone Armor exact shield and learned input',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=172,boots=3,actualReloads=2,saves=3,sourceEventContexts=262,matchedWithinPoolContexts=45,capacityContexts=2295,retainedPredecessorMarkers=151,
            actualRightQuickCasts=6,finalSavedGeneration=2,legacySavedRankMaximum='unknown',auditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[825,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Physical shield consumption now retains FP256 through the existing SourceAbsorptionPool rather than rounding every hit up to a whole point. Optional saved pool captures rank and original capacity for new casts;legacy whole-HP payloads remain readable with unknown original rank/maximum.',
            'All2295current rank/hard-synergy formula contexts pass. The complete older event22 probe has262numeric/guard contexts;45within-pool native comparisons match. Its exhaustion discrepancy is explicitly excluded from modern equivalence.',
            'Learned right/quick casts use ordinary unpaid windup and once-only release payment. The pinned right-only flag now has an actual binding gate. Six connected native input casts pass,with retained previous input checks.',
            'Actual fractional physical and native AOS hits consume the shield;nonphysical damage does not. Real Shaftstop checks pre-physical-reduction absorption. Recast refills,gear/talent changes preserve active capture,unpaid/interrupted casts retain old armor,and armor-group/death/respec/wardrobe transaction cleanup clears both representations.',
            'Two real reloads retain wounded new and legacy shields,mana fraction and unknown legacy metadata;both resume exact1/256absorption. All151predecessor markers,Psychic Ward group/reload and three older general class checks pass.'],
        remaining=['Older event22 zeroes the pool before subtracting a breaking hit and therefore passes that full hit through. Sanctuary retains its existing min(pool,damage) overflow while modern behavior remains uncertain;this is not a claim that the two branches match.',
            'Full modern do18/stat-list/event registration,Energy Shield/other incoming priority,source gfx parameter,all special incoming producers and graphical-client acceptance remain open. Wardrobe cleanup tests its actual transaction,not the public ceremony/combat-wait dispatcher.',
            'The first run caught the genuine missing right-only gate. The second test used a restricted history helper during cast recovery;the corrected fixture exercises the actual wardrobe transaction. Failed logs remain recorded.',
            'No old-content binary migration,deployment,new full-class run,complete skill/class or final assets are claimed. The30-skill audit identifies provisional bone/poison flight/field paths as the next implementation priority.'])
    path=ROOT/'tools/reference/bone-armor-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Bone Armor:172markers/3boots/2reloads,6real inputs,45source absorption/2295capacity contexts,4audits/71Python;installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
