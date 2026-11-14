#!/usr/bin/env python3
"""Authenticate the complete prepared Blood Oath proposal and native bridge."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/blood-oath-damage'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    a=pin(STAGE/'source-snapshot-1.json')['inputs'];b=pin(STAGE/'source-snapshot-2.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(a)==production(b)and a['projectInputs']==b['projectInputs']and a['copiedData']==b['copiedData']
    for kind,number in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/demonic-mastery-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/blood-oath-source-review.json');assert source['skill']==378 and source['sourceCandidate']['readableCodeBytes']==0 and not source['sourceCandidate']['identityVerified']
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'blood-oath-damage-check':1,'blood-oath-damage-check-result':1,
        'blood-oath-damage-reload-check':2,'blood-oath-damage-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('Prepared Blood Oath:original owner/three recipients retain fractional wounds,birth,RNG/host through reload,then react again without offline replay;lease remains transient')==2
    assert 'Witch/Tarnhelm current effective4to5redirect12to14percent' in text
    saved=pin(STAGE/'blood-oath-damage-fixtures.json');assert saved['Generation']==2 and len(saved['Pets'])==3
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.BloodOath'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/BloodOathDamageProposal.cs').read_text()
    for name in ('src/Sanctuary.Content/BloodOathDamageProposal.cs','src/Sanctuary.Content/SkillHealth.cs','src/Sanctuary.Content/SkillCombat.cs',
        'tests/Sanctuary.Integration/BloodOathDamageChecks.cs','tests/check_blood_oath_damage.py','tools/review_blood_oath.py','tools/record_blood_oath_damage.py',
        'docs/blood-oath-damage-proposal.md','docs/blood-oath-damage-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    pin(STAGE/'native-1/birth-inputs.json');pin(STAGE/'native-1/birth-native-checks.log');pin(STAGE/'native-1/native-run-1.log')
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared Blood Oath settled incoming damage rule',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBloodOathEnabled=False,reactionApprovalPending=True,preparedOwnerLeasesOnly=True,nativeMarkers=15,markers=dict(expected),actualInputs=0,
        authenticatedSessions=3,boots=3,reloads=2,saves=3,originalRecipients=3,originalOwners=1,witchGearControl=True,
        contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,
        serializationVersionChanged=False,newLiveReferenceReads=0,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Internal prepared leases drive one effective-rank pinned budget,FP8 creation-order division,life-capped shares/no redistribution and actual original-attacker death credit through existing SkillHealth.',
            'Actual native resisted35res-all Goldskin,armor-ignore and fully-absorbed PsychicWard routes pass;pet elemental immunity is bypassed only for already-settled transfer.',
            'Unprepared/no-pet/unsupported legacy/facet/park and periodic/return/selfcost/environment controls preserve original owner damage with no guessed protection or recursive direct-hit events.',
            'Witch current effective4to5gear changes redirect12to14percent without recapturing pet life/resistance birth context.',
            'Original owner/3recipients retain fractional wounds,birth,RNG/controllers across both reloads and react again;prepared lease is transient and no offline damage replays.'],
        diagnostics=['Native1 C1/I1 stopped at a fixture expecting an arbitrary UO resistance mod to change the D2 class pool;actual19200FP8owner loss correctly reflected25percent of an unresisted100hit.',
            'C1/I2/S2 changes only integration fixture:real Goldskin35res-all and explicit unsupported legacy preflight. Native2 passes;failed source/assemblies/logs retained. Production Content1 unchanged.'],
        limits=['Current start66/event35,candidate identity/formula/rank/recipient/splitting/order/low-life behavior remain unavailable;complete tested rule requires explicit user adaptation approval.',
            'Pinned table and older unspecified-version tester notes conflict;no old bug silently adopted and no protected/live capture retried.',
            'Bind Demon and broader PvP/property/recipient/global-world/client/class certification remain unfinished. No new socket-input proof or final artwork claimed.'],inputs=inputs)
    path=ROOT/'tools/reference/blood-oath-damage-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Blood Oath damage evidence stale'
    else:path.write_text(encoded)
    print('Prepared Blood Oath:C1/I2/S2/N2,15markers/3original recipients/2reloads;226handlers;decision pending;installed build/saves protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
