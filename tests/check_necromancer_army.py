#!/usr/bin/env python3
"""Approved effective-rank caps, original Mage/Revive rosters and native reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration():
    return dict(stage=ROOT/'.local/class-skills/necromancer-army',
        extra_families=('necromancer-army','skeleton-roster','revive-native-activation','safe-area-minion','full-wardrobe'),
        extra_files={'skeleton-roster-source-cases.json':ROOT/'.local/class-skills/skeleton-roster-source/cases.json'},
        extra_manifest_names={'full-wardrobe':'full-wardrobe-checks.json'},
        extra_live_commands=('necromancer-army-live','safe-area-minion-live'),
        extra_inputs=(Path(__file__).resolve(),))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration())
