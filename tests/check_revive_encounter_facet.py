#!/usr/bin/env python3
"""Retain shared mechanics with original encounter and native Revive facets."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_elite_aura_winner.py')
    config['stage']=ROOT/'.local/class-skills/revive-encounter-facet'
    config['extra_families']=(*config['extra_families'],'revive-encounter-facet','revive-facet','revive-cast','revive-native-activation','revive-before-death')
    live=('revive-encounter-facet-live-check','revive-facet-live-check')
    config['extra_checks']=(*config['extra_checks'],*live)
    config['extra_connected_checks']=(*config['extra_connected_checks'],*live)
    config['extra_wait_results'].update({'revive-encounter-facet-live-check':'revive-encounter-facet-live-result','revive-facet-live-check':'revive-facet-live-result'})
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_elite_aura_winner.py')
    check(**config)
