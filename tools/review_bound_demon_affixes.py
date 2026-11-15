#!/usr/bin/env python3
"""Qualify retained modifier IDs without reading the updated D2R installation."""
import argparse,csv,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    receipt=ROOT/'tools/reference/bone-barrier-hidedead-reference-evidence.json'
    parent=json.loads(receipt.read_text());assert parent['referenceVersion']=='3.3.93847'
    table=ROOT/'.local/class-skills/bone-barrier-death-phase-source/monumod.txt'
    assert sha(table)==parent['evidence'][str(table.relative_to(ROOT))]
    rows=list(csv.DictReader(table.open(),delimiter='\t'));assert len(rows)==45
    skills=ROOT/'data/Sanctuary/Balance/class-skills.json'
    skill=next(r for r in json.loads(skills.read_text()) if r['talentId']=='warlock.binddemon')
    assert skill['sourceId']==382
    expected=[(5,5,'strong','ExtraStrong'),(10,6,'fast','ExtraFast'),(15,27,'spectralhit','SpectralHit'),(20,30,'aura','AuraEnchanted')]
    mappings=[]
    for field,(rank,identity,name,native) in enumerate(expected,2):
        row=next(r for r in rows if r['id']==str(identity))
        assert row['uniquemod']==name and row['enabled']=='1'
        assert skill['sourceFields'][f'calc{field}']==f'(lvl >= {rank})?{identity}:0'
        mappings.append(dict(effectiveRank=rank,sourceId=identity,sourceName=name,nativeName=native,enabled=True))
    inputs=[Path(__file__),receipt,table,skills,ROOT/'tools/reference/iron-affix-reuse-review.json']
    out=dict(referenceVersion='3.3.93847',component='Pinned Bind Demon modifier identities and existing-affix reuse',
        sourceTableRows=45,modifiers=mappings,currentD2RModifierCallbackParity=False,newGameFilesRead=False,newProcessMemoryRead=False,
        existingAffixPolicy='Use existing Sanctuary direct Extra Strong, movement-only Extra Fast and Spectral Hit services. Current initializer/stat-order/RNG equivalence is not established.',
        officialCrossCheck=dict(url='https://news.blizzard.com/en-gb/article/24261478/diablo-ii-resurrected-ladder-season-14-has-concluded',
            checkedUtc='2026-11-15',publishedScope='Patch 3.2 final notes;corroboration only,not a repin or3.3.93847binary certificate',
            softPointsGrantModifiers=True,replacements={'Might':'Concentration','Holy Fire':'Vigor','Blessed Aim':'Thorns','Holy Freeze':'Holy Freeze','Conviction':'Fanaticism','Fanaticism':'Fanaticism','Holy Shock':'Vigor'},
            bindingNotes=['Hard points gate Champion10,Unique15,SuperUnique20.','Binding costs nine mana per second and checks about once per second.','Aura selection/rank/seed and underling contexts are not specified.']),
        unresolved=['Bound aura30selection/rank/RNG/native delivery remains unfinished;replacement table is qualified independently from its unavailable producer.',
            'Official cadence/cost guidance does not define SQ6progress or convert the raw shifted per-update cost into a proven channel clock. Raw chained equality remains unchanged.',
            'Source modifier IDs are identities,not proof that current D2R callbacks match native Sanctuary affixes. Full Bind still requires a tested composite adaptation before learned admission.'],
        inputs={str(p.relative_to(ROOT)):sha(p) for p in inputs})
    target=ROOT/'tools/reference/bound-demon-affix-source-review.json';encoded=json.dumps(out,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bound modifier source review stale'
    else:target.write_text(encoded)
    print('Pinned modifiers5Strong/6Fast/27Spectral/30Aura qualified;official bound-aura remapping recorded;no repin/new capture.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
