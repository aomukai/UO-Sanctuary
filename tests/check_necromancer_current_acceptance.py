#!/usr/bin/env python3
"""Reconcile the full Necromancer cohort with the current production build."""
import argparse
import os
from pathlib import Path
from check_necromancer_acceptance import combined
from check_iron_golem_birth import ROOT,check
from check_necromancer_army import configuration as army_configuration

def configuration(mechanics=False):
    c=combined()
    c['stage']=ROOT/'.local/class-skills'/('necromancer-current-mechanics' if mechanics else 'necromancer-current-acceptance')
    c['extra_inputs']+=(Path(__file__).resolve(),)
    army=army_configuration()
    for key in('extra_families','extra_live_commands','extra_inputs'):
        c[key]=tuple(dict.fromkeys((*c.get(key,()),*army.get(key,()))))
    for key in('extra_files','extra_manifest_names'):
        for name,value in army.get(key,{}).items():
            assert name not in c[key] or c[key][name]==value,(key,name)
            c[key][name]=value
    if mechanics:
        c.update(extra_live_commands=(),extra_target_input_commands=(),extra_ground_input_commands=())
    return c

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--mechanics',action='store_true');parser.add_argument('--parking',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    c=configuration(args.mechanics)
    if args.parking:
        c.update(stage=ROOT/'.local/class-skills/necromancer-current-parking',
            extra_families=('parking-boundary','necromancer-army'),extra_checks=(),
            extra_live_commands=('parking-boundary-live','necromancer-army-live'),
            extra_target_input_commands=(),extra_ground_input_commands=(),extra_connected_checks=(),
            extra_one_reload_families=(),extra_early_reload_families=(),save_command='save',save_result=None)
    check(**c)
