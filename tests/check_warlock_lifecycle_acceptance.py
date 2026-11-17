#!/usr/bin/env python3
"""All thirty Warlock/Witch talents with current direct/item/native/save contracts."""
import argparse,json,os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_warlock_demons_acceptance import configuration as demons
from check_warlock_weapons_acceptance import configuration as weapons
from check_warlock_chaos_acceptance import configuration as chaos
from check_warlock_final_talents import configuration as final
from check_warlock_direct_link import configuration as direct
from check_gear_strike_link import configuration as gear
STAGE=ROOT/'.local/class-skills/warlock-lifecycle-acceptance'

def configuration():
    c=demons();c['stage']=STAGE
    for source in (weapons,chaos,final,direct,gear):
        other=source()
        for key in ('extra_families','extra_checks','extra_connected_checks','extra_target_input_commands',
                    'extra_ground_input_commands','extra_pre_save_commands','extra_inputs'):
            c[key]=tuple(dict.fromkeys((*c.get(key,()),*other.get(key,()))))
        for key in ('extra_files','extra_wait_results','extra_manifest_names','extra_input_timeouts','extra_check_timeouts'):
            c.setdefault(key,{}).update(other.get(key,{}))
    c['extra_families']+=('psychic-ward','player-melee-return','cold-corpse-proposal')
    # Consume's pre-save refresh releases its authenticated socket in finally.
    # Defiler's existing save command can then use that same unclaimed socket
    # for its original mid-windup save;both dedicated assertions retain.
    assert c['extra_pre_save_commands']==('cleave-cast-presave-check','consume-aura-save-check')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),*helpers,
        ROOT/'tools/reference/warlock-final-talents-approval.json',ROOT/'tools/reference/ordinary-cold-corpse-approval.json')))
    rows=[r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())['skills']if r['characterClass']=='Warlock']
    assert len(rows)==30 and all(r['implementation']=='handler-present'for r in rows)
    for key in ('extra_families','extra_checks','extra_target_input_commands'):
        assert len(c[key])==len(set(c[key])),key
    for p in (*c['extra_inputs'],*c['extra_files'].values()):assert p.is_file(),p
    return c

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',action='store_true');args=p.parse_args();c=configuration()
    if args.plan:print({k:len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files','extra_pre_save_commands')})
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        with paced_clients():check(**c)
