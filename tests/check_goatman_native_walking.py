#!/usr/bin/env python3
"""Real source walking/room/geometry transport and retained controller state."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/goatman-native-walking',extra_families=('goatman-native-walking','goatman-native-decision','goatman-npc-body-action'),
          extra_inputs=(Path(__file__).resolve(),))
