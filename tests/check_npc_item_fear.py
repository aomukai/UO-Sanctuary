#!/usr/bin/env python3
"""Original NPC fear items, separate curse/AI ownership and affected shared consumers."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_lightning_return_retained import settings as previous
from check_blood_life_tap import configuration as capture


def configuration(retained=False):
    c=dict(stage=ROOT/'.local/class-skills/npc-item-fear',extra_families=('npc-item-fear',),
        extra_connected_checks=('npc-item-fear-check','npc-item-fear-reload-check'),extra_live_commands=('npc-item-fear-live',),
        extra_files={'npc-item-fear-cases.json':ROOT/'.local/class-skills/npc-item-fear-source/cases.json'},extra_inputs=(Path(__file__).resolve(),))
    if not retained:return c
    base=previous();other=capture('check_attract_cast.py')
    for key in ('extra_families','extra_checks','extra_live_commands','extra_target_input_commands','extra_connected_checks','extra_inputs'):
        base[key]=tuple(dict.fromkeys((*base.get(key,()),*other.get(key,()),*c.get(key,()))))
    for key in ('extra_files','extra_wait_results','extra_manifest_names','extra_input_timeouts'):
        for part in (other,c):
            base.setdefault(key,{})
            for name,value in part.get(key,{}).items():
                assert name not in base[key]or base[key][name]==value,(key,name)
                base[key][name]=value
    for family in ('npc-freeze-hit','npc-lightning-return'):
        base['extra_families']+=(family,)
        base['extra_connected_checks']+=(family+'-check',family+'-reload-check')
    base['extra_files']['npc-freeze-hit-cases.json']=ROOT/'.local/class-skills/npc-freeze-hit-source/cases.json'
    base['stage']=ROOT/'.local/class-skills/npc-item-fear-retained'
    return base


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--retained',action='store_true');parser.add_argument('--attract-input',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    c=configuration(args.retained)
    if args.attract_input:
        c.update(stage=ROOT/'.local/class-skills/npc-item-fear-attract-input',extra_families=(),extra_checks=(),extra_live_commands=(),extra_connected_checks=(),
            extra_target_input_commands=('attract-cast-right','attract-cast-quick'),extra_input_timeouts={'attract-cast-right':18,'attract-cast-quick':18})
    check(**c)
