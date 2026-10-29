#!/usr/bin/env python3
"""Freeze source/data before a clean build and compare it with native inputs."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def current():
 sources=[p for d in ['src/Sanctuary.Content','shared','tests/Sanctuary.Integration'] for p in (ROOT/d).glob('*.cs')]
 projects=[ROOT/n for n in ['src/Sanctuary.Content/Sanctuary.Content.csproj','tests/Sanctuary.Integration/Sanctuary.Integration.csproj','Directory.Build.props','Directory.Packages.props'] if (ROOT/n).exists()]
 data=[p for p in (ROOT/'data/Sanctuary').rglob('*') if p.is_file()]
 return dict(sourceInputs={str(p.relative_to(ROOT)):sha(p) for p in sorted(sources)},projectInputs={str(p.relative_to(ROOT)):sha(p) for p in sorted(projects)},copiedData={'Data/Sanctuary/'+str(p.relative_to(ROOT/'data/Sanctuary')):sha(p) for p in sorted(data)})
def check(path,native=None):
 saved=json.loads(path.read_text());assert saved['inputs']==current(),'Source/data changed after the pre-build snapshot'
 if native:
  tested=json.loads(native.read_text());assert {n:h for n,h in tested['sourceInputs'].items() if n.endswith('.cs')}==saved['inputs']['sourceInputs']
  assert tested['copiedData']==saved['inputs']['copiedData']
 return saved
def main():
 p=argparse.ArgumentParser();p.add_argument('path',type=Path);p.add_argument('--check',action='store_true');p.add_argument('--native',type=Path);a=p.parse_args()
 if a.check:check(a.path,a.native)
 else:
  assert not a.path.exists(),'Preserve prior build snapshots;choose a fresh path'
  a.path.parent.mkdir(parents=True,exist_ok=True);a.path.write_text(json.dumps(dict(format=1,scope='Inputs recorded before clean content and integration rebuilds;post-build/native comparison must match exactly.',inputs=current()),indent=2)+'\n')
 print('Pre-build source/data snapshot matches'+(' native tested inputs' if a.native else '')+'.')
if __name__=='__main__':main()
