#!/usr/bin/env python3
"""Review-only Inferno input prototype; current gameplay remains gated."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_player_inferno_modes import configuration as prior


def configuration(retained=False):
    if retained:
        c=prior();c['stage']=ROOT/'.local/class-skills/inferno-held-candidate-retained'
        c['extra_families']+=('inferno-held-candidate',)
        c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'inferno-held-candidate-check','inferno-held-candidate-reload-check')
        c['extra_inputs']+=(Path(__file__).resolve(),)
        c.update(save_command='inferno-held-candidate-save',connected_save=True)
        return c
    return dict(stage=ROOT/'.local/class-skills/inferno-held-candidate',extra_families=('inferno-held-candidate',),
        extra_connected_checks=('inferno-held-candidate-check','inferno-held-candidate-reload-check'),extra_inputs=(Path(__file__).resolve(),),save_command='inferno-held-candidate-save',connected_save=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--retained',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.retained))
