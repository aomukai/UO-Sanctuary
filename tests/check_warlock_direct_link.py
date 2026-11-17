#!/usr/bin/env python3
"""Known Warlock direct components;retain four affected casts and saves."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/warlock-direct-link'
def configuration():
    families=('defiler-health-link','hellslayer-readiness','apocalypse-impact','apocalypse-area','apocalypse-potions','apocalypse-ground','apocalypse-cast','blade-warp','purge-reaction','purge-cast','weapon-landed','skill-damage-event','sigil-death','sigil-family','corpse-town','sigil-cast')
    connected=tuple(f+s for f in ('defiler-health-link','apocalypse-cast','purge-cast','sigil-death','sigil-family','sigil-cast')for s in ('-check','-reload-check'))
    files={'skill-resistance-cases.json':ROOT/'.local/class-skills/skill-resistance-source/cases.json','apocalypse-area-cases.json':ROOT/'.local/class-skills/apocalypse-area-source/cases.json','hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'}
    inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/warcry-potion-native-evidence.json',ROOT/'tools/reference/blade-warp-adaptation.json',ROOT/'tools/reference/hex-purge-reaction-approval.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_families=families,extra_files=files,extra_inputs=tuple(dict.fromkeys((*inputs,*helpers))),extra_connected_checks=connected,extra_wait_results={n:n+'-result'for n in connected},extra_connection_settle=.15,extra_target_input_commands=('apocalypse-cast-left','apocalypse-cast-right','apocalypse-cast-quick','blade-warp-left','blade-warp-right','blade-warp-quick','purge-cast-right','purge-cast-quick','sigil-cast-left','sigil-cast-right','sigil-cast-quick'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
