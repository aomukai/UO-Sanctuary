#!/usr/bin/env python3
"""Current reach and actual Warlock/Witch basic attack, original weapon reloads."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/player-current-reach',extra_families=('player-current-reach',),
          extra_files={'player-current-reach-cases.json':ROOT/'.local/class-skills/player-current-reach-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_player_current_reach.py',))
