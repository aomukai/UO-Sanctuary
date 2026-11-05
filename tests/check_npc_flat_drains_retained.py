#!/usr/bin/env python3
"""Retain actual zero-drain equipment/player consumers and ordered contact/HP lifecycle."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/npc-flat-drains-retained',extra_families=('npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','venom','monster-ordered-release'),extra_checks=('monster-contact-check','iron-golem-ordered-contact-check'),extra_inputs=(Path(__file__).resolve(),),extra_files={
      'npc-equipment-elements-source-cases.json':ROOT/'.local/class-skills/npc-equipment-elements-source/cases.json',
      'venom-damage-source-probe.json':ROOT/'.local/class-skills/venom-damage-source-probe.json',
      'npc-equipment-critical-source-cases.json':ROOT/'.local/class-skills/npc-equipment-critical-source/critical-cases.json',
      'npc-equipment-seed-source-cases.json':ROOT/'.local/class-skills/npc-equipment-seed-source-cases.json',
      'goatman-npc-release-source-probe.json':ROOT/'.local/class-skills/goatman-npc-release-source-probe.json'},
      extra_wait_results={'venom-check':'venom-result','venom-reload-check':'venom-reload-result'})
