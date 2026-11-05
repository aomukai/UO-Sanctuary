#!/usr/bin/env python3
"""Pinned Mage town permission,legal cast/parking and actual learned delivery."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration():
    return dict(stage=ROOT/'.local/class-skills/mage-safe-area',
        extra_families=('safe-area-minion','mage-world'),
        extra_live_commands=('safe-area-minion-live','mage-world-live'),
        extra_target_input_commands=('mage-world-right','mage-world-quick'),
        extra_inputs=(Path(__file__).resolve(),))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration())
