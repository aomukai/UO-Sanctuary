#!/usr/bin/env python3
"""Telekinesis ordered NPC delivery with retained Sorceress/shared melee contracts."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_telekinesis_world import configuration as world


def configuration(full=False):
    config=world(full);stage=ROOT/'.local/class-skills/sorceress-telekinesis-combat';config['stage']=stage
    config['extra_families']+=('telekinesis-combat','monster-return','monster-ordered-release')
    config['extra_checks']=(*config.get('extra_checks',()),'poison-dagger-contact-check','prepared-player-native-damage-check','prepared-native-damage-check')
    config['extra_wait_results']['telekinesis-combat-reload-check']='telekinesis-combat-reload-result'
    config['extra_wait_results']['monster-return-check']='monster-return-result'
    config['extra_connected_checks']+=('telekinesis-combat-reload-check',)
    config['extra_target_input_commands']+=('telekinesis-combat-right','telekinesis-combat-quick')
    config['extra_files']['telekinesis-combat-source-cases.json']=ROOT/'.local/class-skills/telekinesis-combat-source/cases.json'
    config['extra_files']['goatman-npc-release-source-probe.json']=ROOT/'.local/class-skills/goatman-npc-release-source-probe.json'
    for variant in ('classic','ubers'):config['extra_files']['monster-return-'+variant+'.json']=stage/'source-inputs'/(variant+'.json')
    config['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/monster-return-source-evidence.json')
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
