#!/usr/bin/env python3
"""Focused current Chain maker requests and original gear/DTO continuation."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/miasma-chain-launch',extra_families=('miasma-chain-launch',),
  extra_files={'miasma-chain-launch-cases.json':ROOT/'.local/class-skills/miasma-chain-launch-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_miasma_chain_launch.py',ROOT/'tools/reference/miasma-chain-launch-current-evidence.json'))
