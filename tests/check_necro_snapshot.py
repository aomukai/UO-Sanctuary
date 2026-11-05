#!/usr/bin/env python3
"""Current Necromancer snapshots and explicit historical upgrade controls."""
import argparse
import os
from pathlib import Path
from check_warrior_snapshot import ROOT,configuration as previous
from check_blood_life_tap import configuration as legacy_configuration
from check_iron_golem_birth import check

FAMILIES=('necro-snapshot','mage-pet-buff','mage-basics','mage-chain','mage-glacial','mage-meteor','mage-static','clay-upgrades','fire-upgrades','npc-open-wounds','iron-golem-strong','iron-golem-spectral')
LIVE=('mage-basics-live','mage-chain-live','mage-glacial-live','mage-meteor-live','mage-static-live','clay-upgrades-live','fire-upgrades-live','iron-golem-strong-live','iron-golem-spectral-live')


def configuration(broader=False):
    c=previous(broader)
    c['stage']=ROOT/'.local/class-skills'/('necro-snapshot-broader' if broader else 'necro-snapshot')
    c['extra_families']=FAMILIES+c['extra_families']
    c['extra_live_commands']=LIVE+c['extra_live_commands']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files'].update(legacy_configuration('check_fire_upgrades.py')['extra_files'])
    return c


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader))
