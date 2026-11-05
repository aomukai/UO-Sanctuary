#!/usr/bin/env python3
"""Bind source cast contracts, actual Blood delivery and protected native checks."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
from record_item_proc_callbacks import expected as prior_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/item-effect-casts'
WIDE=ROOT/'.local/class-skills/item-effect-casts-broader'
PARENT='a48409b0801a5e4a8b21e198a6afd2cbb8f88f6e'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def old(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=prior_expected(broader);c['item-effect-cast-check']=1;c['item-effect-cast-reload-check']=2;return c

def build(check=False):
    born=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert born['artifacts']==wide['artifacts']==audit['artifacts'] and born['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-6.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/item-proc-callbacks/pre-build-1.json')['inputs']
    now=load(STAGE/'pre-build-6.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('ItemEffectCasts','NpcItemSpellCasts','NpcHitSkills','NecromancerCurseCasts','NecromancerEffects','SkeletonPets')}|{'tests/Sanctuary.Integration/'+n+'.cs' for n in ('ItemEffectCastChecks','BloodLifeTapChecks')}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,122),(WIDE,True,338)):
        log=(stage/'birth-native-checks.log').read_text()
        assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('Item effect casts:18912complete wrapper/handler')==3
        assert log.count('Item proc callbacks:25920complete gate/decode/LCG')==3
        assert log.count('Item skill properties:72558 complete source controls')==3
        assert log.count('Fresh property graphs:24original native items')==3
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        blood=load(stage/'blood-life-tap-fixtures.json')
        assert blood['Generation']==2 and blood['Curse']['Source']==blood['Source'] and blood['Curse']['Rank']==5
        assert (stage/'item-effect-cast-fixtures.json').read_text()=='source-cast-contracts-and-actual-blood-life-tap:other-npc-spells-not-admitted'
    for name in ('content-build-3.log','integration-build-6.log'):
        assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):
        assert sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    source=load(ROOT/'tools/reference/item-effect-casts-source-evidence.json')
    assert source['cases']==18912 and len(source['completeRoutineHashes'])==3 and not source['currentExecutableParity']
    for group in ('inputs','artifacts'):
        for p,h in source[group].items():assert sha(ROOT/p)==h,p
    fingerprints={}
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        current=load(STAGE/name);assert current==load(ROOT/'tools/reference'/name)
        baseline=old('tools/reference/'+name);omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit}
        fingerprints[name]=len(current['InputHashes'])
        for p,h in current['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses'] and sum(r['implementation']=='handler-present' for r in status['skills'])==194
    assert sum(r['characterClass']=='Assassin' and r['implementation']=='handler-present' for r in status['skills'])==24
    assert not load(ROOT/'tools/reference/shadow-summons-preflight.json')['handlerEnabled']
    for name,needle in (('source-check.log','Item effect casts:18912complete source'),('callback-source-check.log','Item proc callbacks:25920complete source'),('python-tests.log','Ran 71 tests'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert needle in (STAGE/name).read_text(),name
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(c['exitCode']==0 for c in load(STAGE/'verification-commands.json'))
    installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    protection=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installation['runtimeRoot'])/'.local/server'/relative
        assert len(protection[kind])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protection[kind]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for kind in ('patchHashes','sharedInputs'):
        for p,h in client[kind].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),ROOT/'tools/probe_item_effect_casts.py',ROOT/'tests/check_item_effect_casts.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/item-effect-casts-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json']
    files.extend(ROOT/p for p in changed('sourceInputs'))
    files.extend(ROOT/p for p in source['artifacts'])
    files.extend(ROOT/'docs'/n for n in ('item-effect-casts-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))
    for stage in (STAGE,WIDE):files.extend(p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    for failure in ('failed-focused-1','failed-focused-2','failed-focused-3','failed-focused-4','failed-focused-5'):files.extend(p for p in (STAGE/failure).rglob('*') if p.is_file())
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=194,assassinHandlers=24,completedClasses=[],generatedNpcItems=False,genericNpcProcAdmission=False,
        scope='Complete older item-effect wrapper/handler contracts plus actual Blood Life Tap adapter and native targeting repair;other spells/searches/start services remain explicit.',
        artifacts=born['artifacts'],copiedData=born['copiedData'],focusedRun=6,broaderRun=1,contentBuild=3,integrationBuild=6,focusedMarkers=122,broaderMarkers=338,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        sourceCasesPerBoot=18912,completeSourceRoutines=3,managedExceptionContextsPerBoot=4,retainedCallbackControlsPerBoot=25920,actualConnectedBloodChecksPerCohort=3,
        pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Five failed focused cohorts are preserved with inputs/logs/binaries and producer snapshots. Runs1/2caught native direct aggression retargeting to a curse recipient. Run3showed the full melee hit legitimately selects its struck target,so runs4/5isolated the assertion around the actual item cast and corrected baseline capture after setup. Run5proved native retaliation feedback also retargeted the caster despite indirect aggression. The final repair preserves native aggression and guards ordinary Combatant reassignment only during the synchronous item-cast scope;normal targeting resumes afterward.'],
        limits=['Source proof executes complete older handler/wrappers through their actual paired-output call shapes. Random/corpse/last-attacker/start/execution/path/notifications are declared services;current executable and optional direct output-pointer contexts remain unverified.',
            'Actual Blood uses a synchronous local item-skill frame and existing NPC Life Tap area/owner/rank/expiry service;no player mana/action. The frame is separate from ordinary saved AI/native combat targeting,not a persisted D2 path implementation.',
            'Generic event subscriptions and all other NPC spells/search/start services remain gated. Full generated NPC item initialization/fallback/charge use and Shadow/Valk actor/AI/casting/lifecycle remain unfinished. No learned handler or class completion added.',
            'Separate effects/audio and talent/hotkey artwork remain identified placeholders. No deployment,native/client/protocol/persistence-schema change.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/item-effect-casts-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Item effect casting receipt stale'
    else:out.write_text(encoded)
    print('Item effect casts:122focused/338broader markers,18912source controls and actual Blood target/controller/aggression/no-mana delivery;71Python/audits/protected pass.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
