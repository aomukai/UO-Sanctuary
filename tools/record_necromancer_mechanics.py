#!/usr/bin/env python3
"""Record the combined mechanics/save diagnostic without claiming full input acceptance."""
import argparse
from datetime import datetime,timedelta,timezone
import json
from pathlib import Path
import re

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/necromancer-acceptance'
MECHANICS=ROOT/'.local/class-skills/necromancer-acceptance-mechanics'
sha=inventory.sha


def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(MECHANICS/'birth-inputs.json')
    snapshot(STAGE/'pre-build-10.json',MECHANICS/'birth-inputs.json')
    parent_path=ROOT/'tools/reference/confuse-resume-native-evidence.json'
    parent=json.loads(parent_path.read_text());ref=parent['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    prior=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    added={'tests/Sanctuary.Integration/NecromancerAcceptanceChecks.cs'}
    changed={'tests/Sanctuary.Integration/'+name for name in ('BoneBarrierBirthChecks.cs','BoneBarrierClockChecks.cs','FireGolemDeathChecks.cs','ClassSkillChecks.cs','PetChecks.cs','DamageRoundingChecks.cs','MageMissileChecks.cs','MagePetBuffChecks.cs','ConfuseCastChecks.cs','ConfuseResumeChecks.cs')}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed
    assert {p:h for p,h in before.items() if p.startswith('src/')}=={p:h for p,h in after.items() if p.startswith('src/')}
    assert tested['copiedData']==prior['copiedData']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert sha(STAGE/'native/Distribution'/name)==tested['artifacts'][name]==prior['artifacts'][name]
    counts=markers(MECHANICS/'birth-native-checks.log')
    text=(MECHANICS/'birth-native-checks.log').read_text()
    assert sum(counts.values())==467 and ' ERR]' not in text and ': failed' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    assert counts['class-skills-check']==1 and counts['class-skills-reload-check']==2
    assert counts['pet-connected-check']==counts['pet-connected-reload-check']==1
    for name in ('bone-barrier-clock','bone-wall-maker','bone-barrier-birth','fire-golem-death','summon-resist','damage-rounding','corpse-cast','revive-cast','confuse-resume','attract-cast'):
        assert counts[name+'-reload-check']==2
    assert counts['necromancer-acceptance-save']==3
    assert 'pass across3boots/2reloads' in (STAGE/'mechanics-run-9.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-9.log')).read_text()
        assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    observer=ROOT/'.local/class-skills/necromancer-acceptance-mage-observer'
    observed=inventory.validate_inventory(observer/'birth-inputs.json')
    snapshot(STAGE/'pre-build-10.json',observer/'birth-inputs.json')
    assert observed['artifacts']==tested['artifacts'] and observed['copiedData']==tested['copiedData']
    text=(observer/'birth-native-checks.log').read_text();observed_counts=markers(observer/'birth-native-checks.log')
    assert sum(observed_counts.values())==18 and observed_counts['mage-missile-live-result']==observed_counts['mage-missile-delayed-live-result']==3
    assert text.count('mage-missile-delayed-live-result: passed;sampled-flight=False')==3 and ' ERR]' not in text and ': failed' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    negative=(observer/'attempt-2-birth-native-checks.log').read_text()
    message=next(line for line in negative.splitlines() if 'Natural mage hit without traced collision/removal:launched=False' in line)
    packet=json.JSONDecoder().raw_decode(message.split('impact=',1)[1])[0]
    source,actor,damage=map(int,re.search(r'source=(\d+)/(\d+),damage=(\d+)',message).groups())
    release=re.search(r'release=([^,]+),delayed=True',message)[1]
    parse=lambda s:datetime.fromisoformat(s.replace('Z','+00:00'))
    assert source==actor==packet['Actor'] and damage>0 and packet['Packet']['Missile']=='firebolt'
    assert parse(packet['NextUtc'])-timedelta(milliseconds=(packet['Frame']+1)*40)==parse(release)
    negative_inputs=json.loads((observer/'attempt-2-birth-inputs.json').read_text())
    assert sha(observer/'negative-build-6-MageMissileChecks.cs')==negative_inputs['sourceInputs']['tests/Sanctuary.Integration/MageMissileChecks.cs']
    expiry=STAGE/'full-snapshot-4'
    expiry_log=(expiry/'birth-native-checks.log').read_text()
    assert sum(markers(expiry/'birth-native-checks.log').values())==641
    assert expiry_log.count('Writing world save snapshot done')==expiry_log.count('Shutting down')==2
    failed_at=re.search(r'\[(\d\d:\d\d:\d\d) ERR\] mage-pet-buff-reload-check: failed',expiry_log)[1]
    saved_buff=json.loads((expiry/'mage-pet-buff-fixtures.json').read_text())
    deadline=parse(saved_buff['Enchant']['UntilUtc']).astimezone(timezone(timedelta(hours=9)))
    assert saved_buff['Generation']==1 and deadline.strftime('%H:%M:%S')<failed_at
    expiry_inputs=json.loads((expiry/'birth-inputs.json').read_text())
    assert sha(expiry/'build-7-MagePetBuffChecks.cs')==expiry_inputs['sourceInputs']['tests/Sanctuary.Integration/MagePetBuffChecks.cs']
    combat=ROOT/'.local/class-skills/necromancer-acceptance-confuse-input'
    combat_inputs=inventory.validate_inventory(combat/'birth-inputs.json')
    snapshot(STAGE/'pre-build-10.json',combat/'birth-inputs.json')
    assert combat_inputs['artifacts']==tested['artifacts'] and combat_inputs['copiedData']==tested['copiedData']
    combat_log=(combat/'birth-native-checks.log').read_text();combat_counts=markers(combat/'birth-native-checks.log')
    assert sum(combat_counts.values())==24 and ' ERR]' not in combat_log and ': failed' not in combat_log
    assert combat_counts['confuse-cast-right-result']==combat_counts['confuse-cast-quick-result']==3
    assert combat_log.count('Shutting down')==combat_log.count('Writing world save snapshot done')==3
    assert combat_log.count('Confuse input combat: ')==6
    combat_times=[float(v) for v in re.findall(r'first damage after([0-9.]+)ms',combat_log)]
    assert len(combat_times)==6 and all(0<t<9900 for t in combat_times)
    late_log=(combat/'attempt-1-birth-native-checks.log').read_text()
    late=max(float(v) for v in re.findall(r'first damage after([0-9.]+)ms',late_log))
    assert 4000<late<9900
    late_inputs=json.loads((combat/'attempt-1-birth-inputs.json').read_text())
    assert late_inputs['sourceInputs']['tests/Sanctuary.Integration/ConfuseCastChecks.cs']==tested['sourceInputs']['tests/Sanctuary.Integration/ConfuseCastChecks.cs']
    assert {p:h for p,h in late_inputs['sourceInputs'].items() if p.startswith('src/')}=={p:h for p,h in tested['sourceInputs'].items() if p.startswith('src/')}
    prior_combat=STAGE/'full-snapshot-5'
    prior_log=(prior_combat/'birth-native-checks.log').read_text()
    assert sum(markers(prior_combat/'birth-native-checks.log').values())==843
    assert 'Real Confuse cast failed autonomous NPC combat/neutral attackability: hp=50000/50000' in prior_log
    prior_inputs=json.loads((prior_combat/'birth-inputs.json').read_text())
    assert sha(prior_combat/'build-8-ConfuseCastChecks.cs')==prior_inputs['sourceInputs']['tests/Sanctuary.Integration/ConfuseCastChecks.cs']
    resume=ROOT/'.local/class-skills/necromancer-acceptance-confuse-resume'
    resumed=inventory.validate_inventory(resume/'birth-inputs.json')
    snapshot(STAGE/'pre-build-10.json',resume/'birth-inputs.json')
    assert resumed['artifacts']==tested['artifacts'] and resumed['copiedData']==tested['copiedData']
    resumed_log=(resume/'birth-native-checks.log').read_text();resumed_counts=markers(resume/'birth-native-checks.log')
    assert sum(resumed_counts.values())==14 and resumed_counts['confuse-resume-reload-result']==2
    assert ' ERR]' not in resumed_log and ': failed' not in resumed_log
    assert resumed_log.count('Shutting down')==resumed_log.count('Writing world save snapshot done')==3
    failed_resume=MECHANICS/'failed-build-9'
    failed_resume_inputs=json.loads((failed_resume/'birth-inputs.json').read_text())
    assert sha(failed_resume/'build-9-ConfuseResumeChecks.cs')==failed_resume_inputs['sourceInputs']['tests/Sanctuary.Integration/ConfuseResumeChecks.cs']
    assert sum(markers(failed_resume/'birth-native-checks.log').values())==351
    assert 'Reload reset the curse/override or failed real acquisition draws' in (failed_resume/'birth-native-checks.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot'])/name)==digest
    files=[parent_path,Path(__file__).resolve(),ROOT/'tests/check_necromancer_acceptance.py',ROOT/'tests/check_iron_golem_birth.py',*[ROOT/name for name in added|changed]]
    files += [p for p in MECHANICS.iterdir() if p.is_file()]
    files += [observer/name for name in ('birth-inputs.json','birth-native-checks.log','mage-missile-fixtures.json','attempt-2-birth-inputs.json','attempt-2-birth-native-checks.log','negative-build-6-MageMissileChecks.cs')]
    files += [expiry/name for name in ('birth-inputs.json','birth-native-checks.log','mage-pet-buff-fixtures.json','build-7-MagePetBuffChecks.cs','run-4.log')]
    files += [p for p in combat.iterdir() if p.is_file()]
    files += [p for p in resume.iterdir() if p.is_file()]
    files += [failed_resume/name for name in ('birth-inputs.json','birth-native-checks.log','confuse-resume-fixtures.json','build-9-ConfuseResumeChecks.cs')]
    files += [prior_combat/name for name in ('birth-inputs.json','birth-native-checks.log','build-8-ConfuseCastChecks.cs','run-5.log')]
    files += [STAGE/name for name in ('pre-build-10.json','content-build-10.log','integration-build-10.log','mechanics-run-9.log')]
    result=dict(referenceVersion='3.3.93847',component='Combined Necromancer mechanics/save diagnostic',productionChanged=False,
        installed=False,goalComplete=False,completedClasses=[],build=10,mechanicsRun=9,
        verified=dict(nativeMarkers=467,observerMarkers=18,confuseInputMarkers=24,confuseInputActions=6,confuseResumeMarkers=14,confuseFirstDamageMs=combat_times,priorBuild9LateConfuseObservationMs=late,normalObserverRuns=3,delayedObserverRuns=3,negativeObserverControls=1,boots=3,actualReloads=2,saves=3,fixtureFamilies=113,sharedChecks=21,unchangedInstalledArtifacts=106),
        inputInventory=dict(path=str((MECHANICS/'birth-inputs.json').relative_to(ROOT)),sha256=sha(MECHANICS/'birth-inputs.json')),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Combined preparation captures barrier clocks/maker/birth resources and pending Fire retirement before one save. Actual two reloads include original Revive,Corpse Explosion,Summon Resist,earlier blind/spell/curse and current pet/parking states.',
            'General class fixture reads saved armor,blindness motion,minion and binding state rather than recreating it on every boot. Legacy connected pet retains stat/progression/XP/friendly/guard/offline/facet/death assertions in explicit nonsafe fields. Clay requires the approved3–4rank1/Mastery1fractional baseline rather than olddamage2.',
            'The original Mage live observer can miss launch or impact between polls. A preserved negative control reproduces failure despite an own-Mage firebolt damage callback with the exact expected source release timestamp. Current normal and deliberately delayed controls require that causal witness and actual flight removal;all three delayed runs miss the polling window. A fresh Mage remains frozen until the timed baseline,preventing authentication from activating an unmeasured early cast. Production combat and timings are unchanged.',
            'Mage buff reload now retains the exact raw Enchant payload/absolute deadline and requires the effective getter to obey that deadline. The previous full run reached its final reload after the real ten-minute Enchant expired;no temporary duration is extended or refreshed to suit the fixture.',
            'Connected Confuse input still requires real autonomous damage,neutral attackability,once-only payment and natural ten-second control expiry. Its observer now watches for damage during the actual lease rather than assuming native random decision/hit rolls guarantee a hit by four seconds. No hit is forced and no curse deadline is extended. Six focused right/quick casts report first-damage times;the prior843-marker failure remains preserved rather than counted as a full pass.',
            'Confuse resume now waits for both actual damage and a new acquisition draw within its original12second window before applying all existing exact saved-state/RNG assertions. It does not force a scan or rewrite a saved seed,target or deadline;the two independently scheduled observations need not occur in the same phase.',
            'Only fixture coordination,helper visibility,runner environment/order,inspection retention and stale expectations change. Production C#,runtime data,kernel and installed106are unchanged.'],
        remaining=[
            'This diagnostic omits the full configuration34live actions and46right/quick input actions per boot. It does not replace those checks or establish the full combined pass. Full845marker acceptance and fresh selected-build audits must be verified separately through necromancer-acceptance-native-evidence.json.',
            'Existing source/current-executable,barrier group/death/collision,unmapped species,legacy/control/credit/PvP/Sunder/client and asset limits remain. No class completion or deployment is claimed. Effects/audio and talent/hotkey icons remain separate.'])
    path=ROOT/'tools/reference/necromancer-mechanics-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Necromancer mechanics:467markers/3boots/2reloads/3saves;full live/input acceptance has a separate receipt.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
