#!/usr/bin/env python3
"""Focused martial contact,charge/release,whole-item and saved proc regression."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/assassin-weapon-contact'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for f in ('TigerStrike','CobraStrike','FistsClaw','ThunderClaw','IceClaw','PhoenixBuilder','FistsFire','ThunderRelease','IceRelease','PhoenixRelease'):add_fixture(c,f)
    c.update(extra_families=(*c['extra_families'],'assassin-weapon-contact'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'assassin-weapon-{skill}-{mode}' for skill in ('tiger','cobra','phoenix','fire','thunder','ice') for mode in ('right','quick')),'assassin-weapon-basic-left','assassin-weapon-basic-right'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/rabies-poison-contact-native-evidence.json',ROOT/'tools/reference/assassin-weapon-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve() for m in tuple(sys.modules.values()) if getattr(m,'__file__',None) and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
