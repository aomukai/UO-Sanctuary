#!/usr/bin/env python3
"""Pin Mastery's existing table consumers without another source oracle run."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    reference=ROOT/'tools/reference/d2r-named-equipment';raw=reference/'json/skills.json'
    manifest=json.loads((reference/'sources.json').read_text());assert sha(raw)==manifest['files']['json/skills.json']['sha256']
    owned=json.loads(raw.read_text());rows=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())
    mastery=next(r for r in rows if r['sourceId']==374);source=owned['374']
    assert mastery['passive']and not mastery['left']and not mastery['right']
    assert {k:v for k,v in source.items()if not k.startswith('*')and k!='lineNumber'}==mastery['sourceFields']
    assert all(mastery['sourceParameters'][str(i)]==source.get('Param'+str(i),0)for i in range(1,21))
    assert source['passivestate']=='demonic_mastery'and source['passive']==1
    assert not any(k.startswith(('passivecalc','auraevent','srvstfunc','srvdofunc'))or k.startswith('passivestat')and k!='passivestate'for k in source)
    assert all(source[k]==0 for k in ('leftskill','rightskill','mana','lvlmana','minmana'))
    expected={373,376,377,382};consumers=[]
    for r in rows:
        fields={k:v for k,v in r['sourceFields'].items()if isinstance(v,str)and "skill('Demonic Mastery'."in v}
        if not fields:continue
        assert r['sourceId']in expected
        assert all(owned[str(r['sourceId'])][k]==v for k,v in fields.items())
        consumers.append(dict(skill=r['sourceId'],talent=r['talentId'],fields=fields))
    assert {r['skill']for r in consumers}==expected
    inputs={str(p.relative_to(ROOT)):sha(p)for p in [raw,reference/'sources.json',ROOT/'data/Sanctuary/Balance/class-skills.json',Path(__file__).resolve()]}
    result=dict(referenceVersion='3.3.93847',component='Demonic Mastery existing passive/table consumer review',skill=374,
        passiveState='demonic_mastery',parameters={str(i):source.get('Param'+str(i),0)for i in range(1,21)},consumers=consumers,
        existingFormulaAndBirthProofs=['tools/reference/warlock-foundation-native-evidence.json','tools/reference/goatman-native-evidence.json','tools/reference/tainted-foundation-native-evidence.json','tools/reference/defiler-body-native-evidence.json'],
        established=['Owned Mastery374 has no separate passive-stat/event/start/do payload;four summon/bind rows supply its actual bonuses through existing SkillValues and captured pet suppliers.',
            'Effective ranks supply capped attackrate,velocity,direct tohit and physical ED/Tainted fire mastery;hard ranks alone control the shared1/2/3summon limit at5/10.',
            'Goatman/Bind Demon tohit consumers install a flat bonus despite the older parameter-description percent wording;no invented percentage multiplier.',
            'Current learned3species already use these fields in original combat/movement/packet/clock systems;no parallel bonus writer or timer is needed.'],
        limits=['This is authenticated table/consumer review,not new current-executable or numeric oracle execution. Existing separate source/native proofs retain their original scope.',
            'Bind Demon itself remains gated for separate binding/channel/world mechanics;its numeric Mastery references are preserved,not claimed as a playable bound creature.',
            'Blood Oath reaction35 and BloodBoil do160 remain separate gaps;no behavior is inferred from their parameters.'],inputs=inputs)
    path=ROOT/'tools/reference/demonic-mastery-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text,'Mastery source review stale'
    else:path.write_text(text)
    print('Demonic Mastery374:owned passive metadata,20parameters and four unchanged consumer rows authenticated;no repeated oracle.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
