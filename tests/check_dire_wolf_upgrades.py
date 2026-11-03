#!/usr/bin/env python3
"""Approved Dire Wolf affixes with retained Fenris world, feeding and hit coverage."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/dire-wolf-upgrades',
        extra_files={'dire-wolf-hit-source-probe.json':ROOT/'.local/class-skills/dire-wolf-hit-source-probe.json'},
        extra_families=('dire-wolf-upgrades','dire-wolf-world'),
        extra_checks=('dire-wolf-upgrades-live-check',),
        extra_connected_checks=('dire-wolf-upgrades-live-check',),
        extra_wait_results={'dire-wolf-upgrades-live-check':'dire-wolf-upgrades-live-result'},
        extra_inputs=(Path(__file__).resolve(),))
