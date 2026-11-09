#!/usr/bin/env python3
"""Prepared Purge reaction policy;no learned route enabled."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/purge-reaction-proposal',extra_families=('purge-reaction','skill-damage-event'),
          extra_inputs=(ROOT/'tests/check_hex_purge_reaction.py',))
