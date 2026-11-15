#!/usr/bin/env python3
"""Authenticate prepared Bind input and existing combat-timer integration."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-input'
PARENT='488173f6747ad14cdca888cd1deb4db577818aa1'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bind-demon-channel-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-channel/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BindDemonInput.cs','tests/Sanctuary.Integration/BindDemonInputChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('BindDemonChannel','ClassCombat','ClassMovement','PlayerInfernoInput')}
    assert set(now)-set(old)==new and not set(old)-set(now) and {n for n in old if old[n]!=now[n]}==changed and born['copiedData']==previous['copiedData']
    for kind,number in (('content',2),('integration',3)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    built=pin(STAGE/'source-snapshot-3.json')['inputs']['sourceInputs'];assert {n:h for n,h in built.items() if not n.startswith('tests/')}=={n:h for n,h in now.items() if not n.startswith('tests/')}
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'inferno-held-wire-1-result':3})
    for family in ('bound-demon-life','bind-demon-channel','bind-demon-input'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,30);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log');assert text.count('Client: 127.0.0.1: Connected.')==12
    for phrase in ('Bind input:historical saved right/quick bind retained with forbidden left normalized;',
        'Bind input:real move/equip/mouse-quick rebind/shared Stop/recovery/pending/invalid aim and native disconnect/logout cancel;',
        'Bind input:expected callback failure after payment stops its reserved generation without refund/repeat;',
        'Bind input:actual existing40ms combat timer completes one native original bind after heartbeats stop before first deadline;'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bind input:saved native identity/wounds/mana and historical right/quick bindings survive reload;')==2
    for phase in ('pressed','burning'):assert text.count('inferno-held-wire-1: '+phase)==3
    for family in ('bound-demon-life','bind-demon-channel','bind-demon-input'):assert pin(STAGE/(family+'-fixtures.json'))['Generation']==2
    saved=pin(STAGE/'bind-demon-input-fixtures.json');assert saved['State']['Actor']==saved['Bound'] and saved['State']['Birth']['Rank']==20 and saved['Mana']==81*256
    archives=[]
    for number in (1,2):
        archive=STAGE/f'native-{number}';earlier=pin(archive/'birth-inputs.json');archives.append(earlier)
        log=archive/'birth-native-checks.log';pin(log);pin(archive/f'native-run-{number}.log')
        if number==1:e.cohort(archive,expected,30)
        else:assert 'Paid callback exception left active input/refunded/bound target' in log.read_text()
        for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    assert archives[1]['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    early={n:h for n,h in archives[1]['sourceInputs'].items() if n.endswith('.cs')};assert set(early)==set(now) and {n for n in now if now[n]!=early[n]}=={'tests/Sanctuary.Integration/BindDemonInputChecks.cs'}
    for i in range(1,5):pin(STAGE/f'source-snapshot-{i}.json')
    for kind,count in (('content',2),('integration',3)):
        for i in range(1,count+1):
            log=STAGE/f'{kind}-build-{i}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(new|changed),'tests/check_bind_demon_input.py','tests/check_inferno_held_input.py','tools/record_bind_demon_input.py','docs/bind-demon-input-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/bind-demon-channel-rule-review.json'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    result=dict(referenceVersion='3.3.93847',component='Prepared Bind held input and existing combat-timer updates',handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,
        nativeMarkers=30,markers=dict(expected),actualInputs=3,actualBindSocketInputs=0,retainedInfernoWireCasts=3,authenticatedSessions=12,boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=3,sourceSnapshot=4,nativeRun=3,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        suppliedPreparedBindInput=True,actualSavedBindings=True,actualSharedCombatTimer=True,existingCastPosePlaceholder=17,currentD2RSQ6InputParity=False,fullBindAdaptationApproved=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),'installed':False},
        established=['Actual historical version1 right/quick binding read through existing persistence;left normalized by pinned flags. Ordinary learning/assignment/production Bind input remains unavailable. Supplied preparation retains readiness,hard training,source and actual saved-binding checks.',
            'Shared packet/map-point/750mslease and40mscombat timer reused. Duplicate/heartbeat-before-press/wrong-source/target-lock/release controls are free. Real move/equip/mouse-quick rebind/Stop,recovery/pending/invalid aim and native disconnect/logout cancel;one-shot/held-cast bypass refuses.',
            'Real existing timer completes one Witch/original-Daemon binding after prepared heartbeats stop before deadline;one roll/nine mana,original wounds and target preserved. Real NetState detach cancels next partial channel. Native state/wounds/mana/right-quick bindings persist across two reloads.',
            'Paid unsupported callback cancels reserved generation without refund/repeat while preserving fresh callback input. Internal mechanical cleanup tested separately from world logging;no expected ERR injected into accepted cohort.',
            'Three original Inferno right-button wire casts retain actual packet replay/duplicate/natural flame/release behavior through shared dispatcher. Optional Python source selection preserves old default;earlier Bind chance/channel and life-buff/item/NPC controls retain.',
            'C1/I1/N1 passed30and archived. C2adds paid-error cleanup. I2/N2 fixture expired its750mslease before one-second attempt;I3 renews lease before injecting error. Exact archives retained;Content2 unchanged for final N3.'],
        limits=['No actual Bind socket/client admission or compiled Bind client. ClassicUO whitelist,fullSQ6/timing/payment/native quality/sunder/underling/species composite and complete Bind proposal remain unfinished.',
            'Previous proposed one-second/fixednine-all-ranks/grouped-health rule remains unapproved;input preparation does not establish currentD2Rprotected callback parity. Human pose17 is only an identified placeholder.',
            'Four earlier proposals pending/frozen;226handlers/Warlock17/no class certified. No general PvP/item/global-world/full-class certificate.',
            'Reference3.3.93847 unchanged despite newer installedclient. Installed artifacts/saves/config/clientpatches unchanged;no artwork and separate final effect/icon registers.'],inputs=inputs)
    target=ROOT/'tools/reference/bind-demon-input-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bind input evidence stale'
    else:target.write_text(encoded)
    print('Bind input:C2/I3/S4/N3,30markers/12connections/2reloads;shared world timer/saved input/cancellation verified;three original Inferno wire casts;learned Bind gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
