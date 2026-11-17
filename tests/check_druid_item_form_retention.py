#!/usr/bin/env python3
"""Five existing item/form save contracts reuse the established inspection hold."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
STAGE=ROOT/'.local/class-skills/druid-item-form-retention'
FAMILIES=('druid-basic-proc','druid-captured-proc','druid-action-proc','druid-setup-items','druid-weapon-poison')
def configuration():
    return dict(stage=STAGE,extra_families=FAMILIES,extra_inputs=(Path(__file__).resolve(),))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
