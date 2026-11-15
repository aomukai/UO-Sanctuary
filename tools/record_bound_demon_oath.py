#!/usr/bin/env python3
"""Authenticate prepared bound Oath identity-scalar integration,not admission."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-oath'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');current=pin(STAGE/'source-snapshot-1.json')['inputs']
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    previous=pin(ROOT/'.local/class-skills/bind-demon-engorge/birth-inputs.json');parent=pin('tools/reference/bound-demon-engorge-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BoundDemonOathChecks.cs'};changed={'src/Sanctuary.Content/'+n+'.cs'for n in('WarlockDemonRecipients','PreparedMonsterNativeDamage','BloodOathDamageProposal')}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']and current['projectInputs']==pin(ROOT/'.local/class-skills/bind-demon-engorge/source-snapshot-3.json')['inputs']['projectInputs']
    for kind in('content','integration'):
        log=STAGE/f'{kind}-build-1.log';pin(log);assert all(s in log.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=STAGE/'native/Projects/UOContent/Mobiles/BaseCreature.cs';pin(source)
    assert all(s in source.read_text()for s in('public virtual double BonusPetDamageScalar => 1.0;','!Summoned && Controlled && Utility.RandomDouble() < 0.2','amount * BonusPetDamageScalar'))
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in('blood-oath-damage','bound-demon-engorge','bound-demon-recipient','bound-demon-oath'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log');assert text.count('Client: 127.0.0.1: Connected.')==12
    for phrase in('Bound Oath:actual mixed Goatman/Daemon/Tainted one25percent budget,whole and FP8 ordered shares,','identity native scalar retains original damage path while default/unregistered/modifier/fault guards refuse;',
        'Bound Oath:native middle Daemon dies to capped512FP8 share with actual enemy credit;',
        'Bound Engorge:real mixed corpse heal/payment/protected loot,','actual mixed three-body Blood Boil pays5percent per body once with one shared area hit and no self attack event'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound Oath:original bound body/weapon/birth/resources,owner and mixed summon wounds/order survive reload;')==2
    saved=pin(STAGE/'bound-demon-oath-fixtures.json');assert saved['Generation']==2 and saved['State']['RuntimeType']=='Server.Mobiles.Daemon'and len(saved['Summons'])==2
    assert saved['State']['Actor']==saved['Bound']and 0<saved['Life']<saved['State']['BaseMaximum256']and saved['OwnerLife']>0
    for name in('blood-oath-damage-fixtures.json','bound-demon-engorge-fixtures.json','bound-demon-recipient-fixtures.json'):assert pin(STAGE/name)['Generation']==2
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for name in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(name);assert e.sha(ROOT/name)==hashlib.sha256(subprocess.check_output(['git','show','bdd8c26773d7ee7dae615caada8c1e8676a95b21:'+name],cwd=ROOT)).hexdigest();frozen.append(name)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.BloodOath'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/BloodOathDamageProposal.cs').read_text()
    for name in(*sorted(new|changed),'tests/check_bound_demon_oath.py','tools/record_bound_demon_oath.py','docs/bind-demon-oath-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared bound Blood Oath with identity native controlled-pet scalar',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,learnedBloodOathEnabled=False,nativeMarkers=33,markers=dict(expected),actualInputs=0,authenticatedSessions=12,boots=3,reloads=2,saves=3,
        actualMixedNativeDamage=True,nativeControlledScalar=1.0,defaultModifierGuardRetained=True,selectedUnsupportedMemberRefuses=True,
        lethalBoundLifeCap=True,actualEnemyDeathCredit=True,originalBodySaved=True,leasePersisted=False,retainedOriginalOathRegression=True,retainedBoundEngorgeRegression=True,retainedMixedBloodBoilRegression=True,
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        newLiveReferenceReads=0,currentRuntimeBinderParity=False,frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Actual mixed Goatman/Daemon/Tainted global order applies one25percent budget:25601FP8 leaves19201owner and2134/2133/2133pets;1001FP8splits84/83/83.',
            'Actual native resisted/armor-ignore owner routes,whole/fractional native bound Damage,one owner attack event/no pet attack events,and ordinary native death credit pass.',
            'Explicit registered bound identity-scalar qualification retains native Damage path. Default/unregistered controlled guards,EvilOmen preflight and selected faulted original refusal pass before any shares.',
            'Other-facet/kind/unprepared/parked exclusions pass. Capped middle bound512FP8death leavesunabsorbedowner share without redistribution and retireshost.',
            'Same bound actor/weapon/birth/resources,owner and mixed original snapshots/RNG/wounds/order survive two reloads;fresh transient leases redirect exact whole/FP8 amounts once. Original Oath/bound Engorge/mixed Boil retain. First native run passed.'],
        limits=['Prepared adapter,not recovered current D2R event35/binder ordering. Ordinary native Daemon verified;nonidentity scalars/overrides/other modifiers/custom region and whole actor/RNG behavior remain unqualified. Native random branch is retained,not D2R RNG equivalence.',
            'Four prior proposals remain pending/frozen. No complete Bind proposal or learned Bind/BloodOath admission. Bound Consume payloads and DeathMark placement/commands/full Bind channel/chance/payment/mappings/modifiers/underlings/sunder/life-buff capture remain unfinished.',
            'No native/client build,deployment,new artwork or installed-save/config write. Installed D2R3.4.95032does not replace pinned3.3.93847. Property/PvP/client/globalworld/fullclass certification remains open.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-oath-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound Oath evidence stale'
    else:out.write_text(encoded)
    print('Bound Oath:C1/I1/S1/N1,33markers/12connections/2reloads;native identity scalar/mixed settled shares/guards/lethal credit;learned routes gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
