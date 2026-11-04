#!/usr/bin/env python3
from pathlib import Path
from check_shiver_armor import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration(True);config['stage']=ROOT/'.local/class-skills/player-melee-return'
    config['extra_connected_checks']=(*config['extra_connected_checks'],'frozen-orb-check')
    config['extra_families']=(*config['extra_families'],'player-melee-return')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve())
    check(**config)
