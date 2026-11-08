#!/usr/bin/env python3
"""Focused proposed Death Mark stat rule;three native boots and actual saves."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/death-mark-debuff',extra_families=('death-mark-debuff',),extra_inputs=(ROOT/'tests/check_death_mark_debuff.py',))
