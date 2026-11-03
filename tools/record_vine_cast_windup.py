#!/usr/bin/env python3
"""Bind shared prepared vine windups, retained public cast and explicit gates."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/vine-cast-windup'
PARENT='37256aed9edea4c205c2299c693078196d446d00'
sha=inventory.sha

def body(text,anchor):
    start=text.index('{',text.index(anchor));end=start+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]

def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==30
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ('vine-cast-windup-live','grizzly-upgrades-live'):
        assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==3
    for name in ('vine-cast-windup','creeper-casting','recycler-casting'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2
    saved=json.loads((STAGE/'vine-cast-windup-fixtures.json').read_text())
    assert len(saved['Actors'])==3 and len({x['Ability'] for x in saved['Actors']})==3 and saved['PendingOwner']>0 and saved['PendingMana']>0
    for actor in saved['Actors']:assert actor['Owner']>0 and actor['Body']>0 and actor['Life']==1111 and actor['Mana']>0 and actor['State']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',921),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-2.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    old=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert len(old['skills'])==len(status['skills'])==240 and {a['talentId'] for a,b in zip(old['skills'],status['skills']) if a!=b}=={'druid.poisoncreeper','druid.carrionvine','druid.solarcreeper'}
    previous=json.loads((ROOT/'.local/class-skills/grizzly-upgrades/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert {p for p,h in native['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        'src/Sanctuary.Content/'+name+'.cs' for name in ('SkillCombat','ClassCombat','DruidVineCasting')}
    path='src/Sanctuary.Content/SkillCombat.cs';old=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT,text=True);current=(ROOT/path).read_text()
    # Exact unchanged ordinary start body, apart from naming the same predicate.
    assert body(old,'public static bool Begin(').replace('!ClassCombat.Available(owner, ability)','!eligible(owner, ability)')==body(current,'private static bool BeginCore(')
    assert '=>BeginCore(owner,ability,selected,point,now,validTarget,ClassCombat.Available,requireMana,timing);' in current
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_vine_cast_windup.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'tools/reference'/name for name in ('druid-form-cast-clock-engine-evidence.json','vine-exposure-observation.json','recycler-world-source-review.json')),
        *(ROOT/'docs'/name for name in ('vine-cast-windup-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','content-build-1.log','pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-2.log','integration-build-2.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','vine-cast-windup-fixtures.json','creeper-casting-fixtures.json','recycler-casting-fixtures.json'))]
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Prepared shared do115 SC windup verified; all three learned handlers remain gated; no class certificate.',
        verified=dict(nativeMarkers=30,boots=3,reloads=2,saves=3,naturalPreparedVineCasts=9,retainedLearnedGrizzlyScenes=3,unchangedOrdinaryBeginBody=True,auditMarkers=2,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Prepared internal entry does not enable public availability/binding/input; incoming surface windows, poison transport/balance and Solar formula selection remain explicitly unresolved.',
            'Paid actor inspection clocks and the staged unpaid deadline are held; no overdue AI or exact mid-frame live-save acceptance is claimed.',
            'Shared human/form source cast clocks are reused; modern global input/cadence/world/geometry/client parity and final assets remain separate.',
            'No fresh full1180class run; the original ordinary Begin body is byte-for-byte preserved after substituting its passed predicate, and real learned Grizzly/source transaction regression is retained.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/vine-cast-windup-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Prepared vine windup:30native markers/3boots/2reloads,9natural prepared casts/3retained learned casts;71Python;191handlers/public vines gated;106installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
