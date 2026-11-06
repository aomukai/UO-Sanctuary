#!/usr/bin/env python3
"""Retain player/Mage flights and existing Hydra/Tainted ordered fire consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
def configuration(fire=False):
 if not fire:
  from check_player_chain import configuration as previous
  c=previous(False);assert {'charged-flight','nova','frost-nova','fire-ball','ice-bolt','player-chain','mage-chain'}<=set(c['extra_families'])
  c['stage']=ROOT/'.local/class-skills/npc-item-spell-flights-player';c['extra_inputs']+=(Path(__file__).resolve(),);return c
 return dict(stage=ROOT/'.local/class-skills/npc-item-spell-flights-fire',
  extra_files={'tainted-formula-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/formula.json',
               'tainted-fireball-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/fireball.json',
               'goatman-creation-source-probe.json':ROOT/'.local/class-skills/goatman-creation-source-probe.json'},
  extra_families=('tainted-fireball','tainted-body','tainted-foundation','goatman-body','hydra-world'),
  extra_connected_checks=('tainted-fireball-reload-check','hydra-world-reload-check'),
  extra_wait_results={'tainted-fireball-reload-check':'tainted-fireball-reload-result','hydra-world-reload-check':'hydra-world-reload-result'},
  extra_target_input_commands=('hydra-world-right','hydra-world-quick'),extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_tainted_fireball_focus.py'))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--fire',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.fire))
