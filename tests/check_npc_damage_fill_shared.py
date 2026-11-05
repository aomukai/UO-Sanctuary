#!/usr/bin/env python3
"""Retain actual equipment elemental/poison/critical and player Venom consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import check
ROOT=Path(__file__).resolve().parents[1]
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/npc-damage-fill-shared',extra_families=('npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','venom'),extra_inputs=(Path(__file__).resolve(),),extra_files={
      'npc-equipment-elements-source-cases.json':ROOT/'.local/class-skills/npc-equipment-elements-source/cases.json',
      'venom-damage-source-probe.json':ROOT/'.local/class-skills/venom-damage-source-probe.json',
      'npc-equipment-critical-source-cases.json':ROOT/'.local/class-skills/npc-equipment-critical-source/critical-cases.json',
      'npc-equipment-seed-source-cases.json':ROOT/'.local/class-skills/npc-equipment-seed-source-cases.json'},
      extra_wait_results={'venom-check':'venom-result','venom-reload-check':'venom-reload-result'})
