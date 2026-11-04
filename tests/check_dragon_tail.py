#!/usr/bin/env python3
"""Learned Tail with original kick/charge, NPC geometry and sentry regressions."""
import argparse,os
from pathlib import Path
from check_dragon_tail_preparation import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('dragon-tail-learned-broader'if broader else'dragon-tail-learned')
    c['extra_files'].update({'player-melee-geometry-cases.json':ROOT/'.local/class-skills/player-melee-geometry-source/cases.json',
                             'melee-geometry-source-probe.json':ROOT/'.local/class-skills/melee-geometry-source-probe.json'})
    c['extra_families']+=('dragon-tail',)
    c['extra_checks']=(*c.get('extra_checks',()),'dragon-tail-mechanics-check','melee-geometry-check')
    c['extra_target_input_commands']=(*c.get('extra_target_input_commands',()),'dragon-tail-left','dragon-tail-right','dragon-tail-quick')
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/player-melee-geometry-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
