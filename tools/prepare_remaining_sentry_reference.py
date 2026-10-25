#!/usr/bin/env python3
"""Pin the remaining stationary Assassin variants before sharing their backend."""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    pin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pin/'sources.json').read_text());tables,pins={},{}
    for name in ['skills','missiles','monstats','monstats2']:
        raw=(pin/'json'/f'{name}.json').read_bytes();pins[name]=hashlib.sha256(raw).hexdigest()
        assert pins[name]==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(raw)
    local=ROOT/'.local/research/d2r-3.3.93847/skills.txt';raw=local.read_bytes()
    skills={r['skill']:r for r in csv.DictReader(raw.decode('utf-8-sig').splitlines(),delimiter='\t') if r.get('skill')}
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    def inspected(relative,name):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        text=raw.decode();match=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
        end,depth=text.index('{',match.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        return dict(path=relative,symbol=name,sourceSha256=hashlib.sha256(raw).hexdigest(),routineSha256=hashlib.sha256(text[match.start():end].encode()).hexdigest())
    def corroborate(row):
        actual=skills[row['skill']];count=0
        for key,value in row.items():
            if key in actual and not key.startswith('*') and key!='lineNumber':
                assert actual[key]==str(value),(row['skill'],key,value,actual[key]);count+=1
        return count
    configurations=[
        (271,'lightning-sentry','assassin.lightningsentry',[313],['sentrylightningbolt'],
         ['Current player271 costs20mana and captures lightning sunder/pierce/mastery plus NPC313 effective rank and hard Shock Web/Charged Bolt Sentry/Death Sentry ranks.',
          'Fixed10shots: NPC313calc4=par8=10 mirrors player271Param1. The player has no calc4. Monster lightningsentry uses AI101, strict active distance25 and seq_chargesentry with the same lg model as Charged Bolt Sentry.',
          'Source-zero missile526 uses player Lightning Sentry damage, Size2, basic do1, LastCollide1, no NextHit or CollideKill, Vel30 and current Range10+2*rank. Do not copy weapon conversion/reset or the charged-bolt path/random fan.',
          'Current skill min1 has zero minimum increments; maximum starts20 with10/16/24/34/44 increments. Hard Shock Web and Charged Bolt Sentry contribute18percent each. Keep fixed minimum behavior and inherited mastery independently checked.'],
         ['Recompose the generic NPC313 missile dispatch, relative target/factory flags, damage getters and actual moving Size2 history-filtered world bolt. Existing CBS do17 multi-bolt emission is not this route.',
          'Reuse shared creation/sequence/AI/FIFO/offline/difficulty only after extending the lightning-specific skill capture and fixed-shot supplier. Resolve shared retirement/death/GUID ownership and verify real learned casting/gear/damage/reloads before enabling.'],
         [('source/D2Game/src/AI/AiThink.cpp','AITHINK_Fn101_AssassinSentry')]),
        (272,'wake-of-inferno','assassin.wakeofinferno',[311],['inferno sentry 1','inferno sentry 2'],
         ['Current player272 costs20mana, captures fire passives, effective NPC311 and hard Fire Blast/Wake of Fire/Death Sentry context. Fixed10shots mirror NPC311Param8 and player272Param1; no player calc4.',
          'Monster infernosentry uses AI101 with strict active distance13 and distinct seq_infernosentry. NPC311 uses start53/do95; do not substitute Wake direct S2 or the ordinary charge-sentry sequence.',
          'NPC311 missile duration calc is20+hard Wake of Fire, animation frame adder15+hard Wake of Fire and density3. These are separate controls requiring complete beam/sequence composition.',
          'Player fire damage has HitShift4, min20/max50 and hard Fire Blast+Wake of Fire synergies18percent each. Server missile523 has DamageRate82, Size1, LastCollide1 and no NextHit. DamageRate is flat-reduction scaling, not an extra DPS multiplier.'],
         ['Compile complete monster Inferno start/do, sequence rewind/state/shot ordering, missile duration/first-step and damage paths. The player Sorceress channel/payment path is not authority for this NPC beam.',
          'Resolve shared retirement/death/GUID ownership and verify learned placement, continuous beam contact, current ranks/gear, saved phase/projectiles and two world reloads before enabling.'],
         [('source/D2Game/src/SKILLS/SkillMonst.cpp','SKILLS_SrvSt53_MonInferno'),('source/D2Game/src/SKILLS/SkillMonst.cpp','SKILLS_SrvDo095_MonInferno')]),
        (276,'death-sentry','assassin.deathsentry',[312,324],['sentrylightningbolt2'],
         ['Current player276 costs20mana and captures lightning passives, effective NPC312/324 and hard Fire Blast/Lightning Sentry context. NPC312calc4 gives5+floor(hard Fire Blast/3); the player Param8=12 is lightning synergy, not the NPC shot base.',
          'Monster deathsentry uses its distinct AI104, not AI101. Its corpse/explosion-versus-lightning selection and remembered corpse identity need their own controller; both skills list seq_chargesentry.',
          'NPC312do55 explosion uses40..80percent base enemy life, half physical/half fire and radius10+(rank-1) source units. Do not copy Necromancer Corpse Explosion damage percentages or use already-scaled native maximum life as the corpse base.',
          'Lightning NPC324 uses missile641 with player276 lightning damage: minimum1, max50 plus8/14/22/28/34 increments and hard Lightning Sentry synergy12percent. Its Size2/LastCollide1/noNextHit path can reuse the corrected candidate filter; Pierce1 and no CollideKill remain distinct flags.'],
         ['Compile AI104 including initial exhaustion check, corpse selection/range/remembered identity, shot debit and fallback lightning chance. Inspect/compose source do55 corpse eligibility, base-life snapshot, consumption and area damage with shared corpse rules.',
          'Resolve full owned NPC retirement/death/GUID lifetime and test real corpse/lightning priorities, rank/gear, fractional resources, owner offline/facet changes, and saved actors/projectiles/corpses before enabling.'],
         [('source/D2Game/src/AI/AiThink.cpp','AITHINK_Fn104_DeathSentry')])]
    for source_id,stem,talent,npcs,missiles,facts,required,symbols in configurations:
        player=tables['skills'][str(source_id)];monster=tables['monstats'][player['summon']]
        assert player['srvdofunc']==45 and player['petmax']==5 and player['mana']==20
        actor_rows={str(i):tables['skills'][str(i)] for i in npcs}
        assert monster['Skill1']==actor_rows[str(npcs[0])]['skill']
        counts={r['skill']:corroborate(r) for r in [player,*actor_rows.values()]}
        if source_id in (271,272):assert player['Param1']==actor_rows[str(npcs[0])]['Param8']==10
        else:assert player['Param1']==actor_rows['312']['Param8']==5 and monster['AI']=='DeathSentry'
        e=dict(referenceVersion='3.3.93847',talentId=talent,sourceId=source_id,status='Current table routes pinned; native learned skill not implemented',installed=False,handlerEnabled=False,
            pinnedHashes=pins,localSkills=dict(path=str(local.relative_to(ROOT)),sha256=hashlib.sha256(raw).hexdigest(),comparedFields=counts),
            skill=player,actorSkills=actor_rows,monster=monster,model=tables['monstats2'][player['summon']],missiles={k:tables['missiles'][k] for k in missiles},
            inspectedLegacy=dict(commit=tree['sha'],routines=[inspected(*x) for x in symbols],executed=False,scope='Static full-body inspection only; no compiled differential or modern executable certificate'),
            established=facts,required=required,
            reuse=['Shared trap placement/creation, owner snapshot, five-slot FIFO, no-footprint body, old-facet retention, offline suppression and owner-difficulty scaling preserve approved Sanctuary rules.',
                   'Shared source-zero elemental packet and source missile history filtering are candidate reuse points; retain signed pierce and source NPC attribution. Nonzero inherited sunder remains explicitly unsupported.'],
            assets=dict(effects='Final trap emergence/firing/idle/collapse, projectile or beam/impact/explosion and audio remain separate; identified placeholders do not block mechanics.',icons='Separate talent/right/quick/hotkey icons remain in the240-row inventory.'),
            limits=['Current local skills are directly compared; monster/model rows are hash-pinned cached reference data and are not newly corroborated against local archives by this script.',
                    'No native actor, world projectile, learned input, mechanical completion or class certificate is claimed by this preflight. Keep unsupported/uncertain branches explicit.'],
            next='Begin Lightning Sentry with shared sequence/creation and generic NPC missile dispatch; handle Inferno and Death variants with their distinct controllers.')
        out=ROOT/'tools/reference'/f'{stem}-preflight.json';encoded=json.dumps(e,indent=2)+'\n'
        if check:assert out.read_text()==encoded,stem+' preflight stale'
        else:out.write_text(encoded)
        print(f'{stem}: current player/NPC tables locally corroborated ({sum(counts.values())} fields); distinct firing/AI/shot routes pinned; learned handler disabled.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
