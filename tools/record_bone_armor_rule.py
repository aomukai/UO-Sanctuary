#!/usr/bin/env python3
"""Bind current Bone Armor policy and the native armor-ignore producer repair."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/bone-armor-rule';P='761527d84a54389f79db67c144d089a8160fea87'

def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-2.json',S/'birth-inputs.json');pin(S/'source-snapshot-2.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/bone-barrier-world-cast/birth-inputs.json')
    a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/BoneArmorRuleChecks.cs'}and not a.keys()-b.keys()
    assert{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/SkillCombat.cs','tests/Sanctuary.Integration/EncounterShieldChecks.cs'}
    assert born['copiedData'].keys()==old['copiedData'].keys()
    assert{n for n in old['copiedData']if old['copiedData'][n]!=born['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    previous=json.loads(subprocess.check_output(['git','show',P+':data/Sanctuary/Balance/class-skill-status.json'],cwd=R));current=pin('data/Sanctuary/Balance/class-skill-status.json')
    for x,y in zip(previous['skills'],current['skills']):
        if x['talentId']in('necromancer.bonewall','necromancer.boneprison'):x['remaining']=y['remaining']
    assert previous==current
    for kind in('content','integration'):
        p=S/f'{kind}-build-2.log';pin(p);assert all(x in p.read_text()for x in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    base=pin('tools/reference/bone-barrier-world-cast-native-evidence.json')
    expected=Counter({k:v for k,v in base['markers'].items()if k.startswith(('iron-golem-birth-','iron-golem-items-','npc-resource-callbacks-'))})
    for f in('bone-armor','psychic-ward','cyclone-armor','bone-armor-rule'):expected.update({f+'-check':1,f+'-reload-check':2})
    expected.update({'encounter-shields-check':3})
    for mode in('right','quick'):expected.update({f'bone-armor-{mode}-check':3,f'bone-armor-{mode}-result':3})
    log=e.cohort(S,expected,36);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==6
    for mode in('right','quick'):assert log.count(f'bone-armor-{mode}: windup')==3
    overlap=pin(S/'bone-armor-rule-fixtures.json');assert overlap['Generation']==2 and overlap['Armor']['Remaining256']==704 and overlap['Energy']['Rank']==0 and overlap['Mana']==25617 and overlap['Life']%256
    assert all(x['Generation']==2 for x in pin(S/'bone-armor-fixtures.json'))
    for n in('psychic-ward-fixtures.json','cyclone-armor-fixtures.json'):pin(S/n)
    before=pin(S/'before-armor-ignore/birth-inputs.json');pin(S/'before-armor-ignore/birth-native-checks.log')
    assert before['sourceInputs']['src/Sanctuary.Content/SkillCombat.cs']==old['sourceInputs']['src/Sanctuary.Content/SkillCombat.cs']
    for n in('tools/reference/bone-armor-source-evidence.json','tools/reference/bone-armor-current-followup.json'):pin(n)
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in('src/Sanctuary.Content/SkillCombat.cs','tests/Sanctuary.Integration/BoneArmorRuleChecks.cs','tests/Sanctuary.Integration/EncounterShieldChecks.cs',
        'tests/check_bone_armor_rule.py','tools/record_bone_armor_rule.py','tools/build_skill_roadmap.py','docs/bone-armor-rule-proposal.md','docs/bone-armor-rule-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/necromancer-coverage-matrix.md',
        'docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Bone Armor current absorption policy and native armor-ignore correction',policyApproved=False,
        existingBoneArmorAvailable=True,nativeArmorIgnorePhysicalFix=True,handlers=236,completedClasses=[],contentBuild=2,integrationBuild=2,contentSourceSnapshot=2,sourceSnapshot=2,nativeRun=2,
        nativeMarkers=36,markers=dict(expected),socketSessions=6,actualInputs=6,inputCases=dict(right=3,quick=3),accountHandshakeVerified=False,boots=3,reloads=2,saves=3,
        artifacts=born['artifacts'],preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Native armor-ignore now consumes Bone Armor physical capacity once before Ward while bypassing resistance and preserving nonphysical remainder;actual AOS/Shaftstop controls pass.',
            'Conventional fractional/equal/breaking pool boundaries,typed Energy-before-Bone-before-resistance order,poison exclusion and partial-mana settlement pass.',
            'Original owner,injured BoneArmor/legacy-rank-zero EnergyShield metadata,fractional life/mana and exact deadlines retain through two actual reloads;live mitigation then restores original inspected state.',
            'Existing learned BoneArmor right/quick casts,capacity/source,AOS/encounter damage,groups/lifecycle and new/legacy saved pools plus Ward/Cyclone component controls pass.',
            'Only two generated barrier remaining descriptions changed;mechanics/handler/verification flags remain unchanged.'],
        limits=['Pinned modern event22 breaking-hit and global priority remain unverified;explicit policy adoption is pending,not a current D2R observation.',
            'Legacy native armor-ignore API lacks full typed lanes;EnergyShield/Cyclone handling of those unqualified contexts is not established by the physical-pool repair.',
            'Prepared stored overlap does not grant new cross-class item skills;no full global event/PvP/special-producer/class/graphical certificate.',
            'Four older Warlock proposals stay frozen;effects/audio and talent/hotkey icons remain separate placeholders.'],inputs=inputs)
    p=R/'tools/reference/bone-armor-rule-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Bone Armor C2/I2/S2/N2:36markers/6real casts/2reloads;native armor-ignore fixed,policy adoption pending.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
