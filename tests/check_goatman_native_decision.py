#!/usr/bin/env python3
"""Bound Goatman source decision root with durable controller/RNG controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/goatman-native-decision',extra_families=('goatman-native-decision','goatman-npc-body-action'),
          extra_checks=('goatman-npc-action-check',),extra_inputs=(Path(__file__).resolve(),))
