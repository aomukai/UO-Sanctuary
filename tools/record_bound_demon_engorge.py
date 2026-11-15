#!/usr/bin/env python3
"""Authenticate prepared bound Engorge storage,stats,recovery and native leech."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-engorge'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-3.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert{n for n in production(first)if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'src/Sanctuary.Content/EngorgeEffects.cs'}
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    previous=pin(ROOT/'.local/class-skills/bind-demon-recipients/birth-inputs.json');parent=pin('tools/reference/bound-demon-recipients-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BoundDemonEngorgeChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in('BoundDemonHosts','EngorgeCasting','EngorgeEffects','PreparedWeaponEvents','GearEffects')}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    assert first['projectInputs']==pin(ROOT/'.local/class-skills/bind-demon-recipients/source-snapshot-4.json')['inputs']['projectInputs']
    for kind,number in(('content',2),('integration',3)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in('engorge-world','bound-demon-typed','bound-demon-recipient','bound-demon-engorge'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log');assert text.count('Client: 127.0.0.1: Connected.')==12
    for phrase in('Bound Engorge:real mixed corpse heal/payment/protected loot,','actual Dagger leeches fractional clipped physical once with no owner/ranged/spell/immune/expired healing;',
        '35raw frame recovery/parking/no catch-up/expiry pass','actual Dagger.OnHit keeps0physical50fire50poison and adds captured75physical once before290percentED',
        'actual mixed three-body Blood Boil pays5percent per body once with one shared area hit and no self attack event'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound Engorge:same native Daemon/items/birth/wounds/resources and original mixed buff deadlines survive reload without recovery;')==2
    saved=pin(STAGE/'bound-demon-engorge-fixtures.json');assert saved['Generation']==2 and saved['State']['RuntimeType']=='Server.Mobiles.Daemon'
    effect=saved['State']['Engorge'];assert effect['Owner']==saved['Owner']and effect['Suppliers']['Rank']==20 and effect['Suppliers']['HardBloodOath']==7
    assert effect['Suppliers']['RegenerationStat']==35 and effect['Suppliers']['MinimumDrainStat']==53 and saved['GoatEffect']==effect
    assert 0<saved['Life']<=saved['State']['BaseMaximum256']
    for name in('engorge-world-fixtures.json','bound-demon-recipient-fixtures.json','bound-demon-typed-fixtures.json'):assert pin(STAGE/name)['Generation']==2
    archive=STAGE/'native-1';failed=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/'native-run-1.log')
    assert 'Expiry granted extra source frames'in(archive/'birth-native-checks.log').read_text()
    passed=pin(STAGE/'native-2/birth-inputs.json');pin(STAGE/'native-2/birth-native-checks.log');pin(STAGE/'native-2/native-run-2.log');e.cohort(STAGE/'native-2',expected,33)
    assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==passed['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name,h in failed['sourceInputs'].items():assert e.sha(archive/'source'/name)==h,name
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for name in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(name);assert e.sha(ROOT/name)==hashlib.sha256(subprocess.check_output(['git','show','ec956bf384b6573fea43eb65401b453d27ab0a3f:'+name],cwd=ROOT)).hexdigest();frozen.append(name)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in code and 'CombatAbility.Engorge'not in code
    assert 'internal static bool Approved=>false;'in(ROOT/'src/Sanctuary.Content/EngorgeEffects.cs').read_text()
    for name in(*sorted(new|changed),'tests/check_bound_demon_engorge.py','tools/record_bound_demon_engorge.py','docs/bind-demon-engorge-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared Engorge on original bound native creatures',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,learnedEngorgeEnabled=False,nativeMarkers=33,markers=dict(expected),actualInputs=0,authenticatedSessions=12,boots=3,reloads=2,saves=3,
        actualMixedCorpseRelease=True,actualNativeDaggerLeech=True,lethalVictimFractionalClip=True,originalBoundBuffSaved=True,oldMissingOptionalFieldAccepted=True,
        retainedOriginalEngorgeRegression=True,retainedTypedNativeRegression=True,retainedMixedBloodBoilRegression=True,
        contentBuild=2,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        optionalSavedFieldsAdded=['BoundDemonState.Engorge'],transientRecoveryCursorSaved=False,newLiveReferenceReads=0,currentRuntimeBinderParity=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Actual mixed original/bound corpse release reserves/consumes once,preserves protected loot,charges once and applies captured49percent healing.',
            'Bound optional buff feeds actual armor175,physical resistance and native attack rate34. Refresh replaces;gear/training do not rewrite birth/buff. Old missing field loads and malformed owner refuses atomically.',
            'Actual native Dagger heals bound attacker once through shared clipped FP8 contact phase/WeaponLeech. Lethal one-HP victim permits only fractional gain;owner/ranged/spell/immune/expired/friendly controls give none.',
            'Actual bound-host tick and additive35raw frame recovery,no same-frame replay,parked no recovery,no resume catch-up,exclusive expiry and maximum-life clamp pass. Original UO underlying regeneration is retained.',
            'Original native bound actor/items/birth/wounds/resources/buff deadline and original mixed summon state retain through two reloads;native contacts still leech once. Original Engorge/typed Imp/mixed Boil families retain.',
            'Native1fixture expected raw recovery above maximum;archive preserves all tested sources/DLLs/logs. Content1/Integration2/Native2passes33and is archived. FinalContent2addsfriendshipguard and Integration3testsactualhosttick;Native3isacceptance.'],
        limits=['Prepared world/recipient/recovery/leech mapping,not recovered current D2R binder/event ordering. Active19tile leech qualification matches existing proposed original path;exact D2R scope remains unverified.',
            'Four frozen complete prior proposals remain pending;this separate bound extension is not their approval. No complete Bind proposal or learned Bind/Engorge admission.',
            'Bound BloodOath incoming/native modifiers,Consume payloads and DeathMark placement/commands,full Bind channel/chance/payment/mappings/modifiers/underlings/sunder/life-buff capture remain unfinished.',
            'No native/client build,deployment,new artwork or installed-save/config write. Installed D2R3.4.95032does not replace pinned3.3.93847. Whole-native-ability/property/PvP/client/globalworld/fullclass certification remains open.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-engorge-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound Engorge evidence stale'
    else:out.write_text(encoded)
    print('Bound Engorge:C2/I3/S3/N3,33markers/12connections/2reloads;real corpse/native stat/leech/recovery/save integration;learned routes gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
