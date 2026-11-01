#!/usr/bin/env python3
"""Record the combined mechanics/save diagnostic without claiming full input acceptance."""
import argparse
import json
from pathlib import Path

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
    snapshot(STAGE/'pre-build-4.json',MECHANICS/'birth-inputs.json')
    parent_path=ROOT/'tools/reference/confuse-resume-native-evidence.json'
    parent=json.loads(parent_path.read_text());ref=parent['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    prior=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    added={'tests/Sanctuary.Integration/NecromancerAcceptanceChecks.cs'}
    changed={'tests/Sanctuary.Integration/'+name for name in ('BoneBarrierBirthChecks.cs','BoneBarrierClockChecks.cs','FireGolemDeathChecks.cs','ClassSkillChecks.cs','PetChecks.cs','DamageRoundingChecks.cs')}
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
    assert 'pass across3boots/2reloads' in (STAGE/'mechanics-run-5.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-4.log')).read_text()
        assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot'])/name)==digest
    files=[parent_path,Path(__file__).resolve(),ROOT/'tests/check_necromancer_acceptance.py',ROOT/'tests/check_iron_golem_birth.py',*[ROOT/name for name in added|changed]]
    files += [p for p in MECHANICS.iterdir() if p.is_file()]
    files += [STAGE/name for name in ('pre-build-4.json','content-build-4.log','integration-build-4.log','mechanics-run-5.log')]
    result=dict(referenceVersion='3.3.93847',component='Combined Necromancer mechanics/save diagnostic',productionChanged=False,
        installed=False,goalComplete=False,completedClasses=[],build=4,mechanicsRun=5,
        verified=dict(nativeMarkers=467,boots=3,actualReloads=2,saves=3,fixtureFamilies=113,sharedChecks=21,unchangedInstalledArtifacts=106),
        inputInventory=dict(path=str((MECHANICS/'birth-inputs.json').relative_to(ROOT)),sha256=sha(MECHANICS/'birth-inputs.json')),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Combined preparation captures barrier clocks/maker/birth resources and pending Fire retirement before one save. Actual two reloads include original Revive,Corpse Explosion,Summon Resist,earlier blind/spell/curse and current pet/parking states.',
            'General class fixture reads saved armor,blindness motion,minion and binding state rather than recreating it on every boot. Legacy connected pet retains stat/progression/XP/friendly/guard/offline/facet/death assertions in explicit nonsafe fields. Clay requires the approved3–4rank1/Mastery1fractional baseline rather than olddamage2.',
            'Only fixture coordination,helper visibility,runner environment/order,inspection retention and stale expectations change. Production C#,runtime data,kernel and installed106are unchanged.'],
        remaining=[
            'This diagnostic omits the full configuration34live actions and46right/quick input actions per boot. It does not replace those checks or establish the full combined pass. The last accepted retained run remains Confuse resume411markers;full run3 and fresh selected-build audits are pending.',
            'Existing source/current-executable,barrier group/death/collision,unmapped species,legacy/control/credit/PvP/Sunder/client and asset limits remain. No class completion or deployment is claimed. Effects/audio and talent/hotkey icons remain separate.'])
    path=ROOT/'tools/reference/necromancer-mechanics-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Necromancer mechanics:467markers/3boots/2reloads/3saves;full live/input acceptance remains pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
