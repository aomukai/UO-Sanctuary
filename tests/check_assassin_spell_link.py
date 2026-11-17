#!/usr/bin/env python3
"""Four Assassin spell contacts publish settled components to Health Link."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
import check_assassin_planned_acceptance as legacy
STAGE=ROOT/'.local/class-skills/assassin-spell-link'
SPELLS=('fire-blast','shock-web','psychic-hammer','mind-blast')
def wires(session,port,boot):
    original=legacy.LEGACY
    try:legacy.LEGACY=SPELLS;return legacy.legacy_wires(session,port,boot)
    finally:legacy.LEGACY=original
def configuration():
    names=('defiler-health-link-check','defiler-health-link-reload-check');waits={n:n+'-result'for n in names}
    for f in SPELLS:waits.update({f+'-check':f+'-result',f+'-reload-check':f+'-reload-result'})
    c=dict(stage=STAGE,extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=('defiler-health-link','hellslayer-readiness',*SPELLS),extra_connected_checks=names,extra_wait_results=waits,extra_connection_settle=.15,extra_wire_callbacks=(wires,),extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/sentry-missile-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py'))
    for n in ('fire-blast-timing','shock-web','shock-web-damage','psychic-hammer','psychic-hammer-damage','mind-blast'):c['extra_files'][n+'-source-probe.json']=ROOT/'.local/class-skills'/(n+'-source-probe.json')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with legacy.paced_clients():check(**configuration())
