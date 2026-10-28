#!/usr/bin/env python3
"""Group PE x64 chained unwind fragments; not a control-flow certificate.

Layout: https://learn.microsoft.com/en-us/cpp/build/exception-handling-x64
"""
from functools import lru_cache
import struct
class UnwindGroups:
 def __init__(self,image):
  self.image=image;self.entries=set(image.functions);self.groups={}
  for entry in image.functions:self.groups.setdefault(self.root(entry),[]).append(entry)
 @lru_cache(None)
 def root(self,entry):
  return self._root(entry,())
 def _root(self,entry,seen):
  assert entry not in seen,'Cyclic unwind chain';begin,end,rva=entry;assert rva&3==0
  off=self.image.offset(rva);version_flags,prolog,count,frame=struct.unpack_from('<BBBB',self.image.data,off);version=version_flags&7;flags=version_flags>>3
  assert version in [1,2],('Unsupported x64 unwind version',version)
  if flags&4:
   assert flags&3==0
   at=off+4+((count+1)&~1)*2;parent=struct.unpack_from('<III',self.image.data,at);assert parent in self.entries,'Chained record absent from .pdata'
   return self._root(parent,(*seen,entry))
  return entry
 def fragments(self,rva):
  entry=next(x for x in self.entries if x[0]==rva);root=self.root(entry);rows=sorted(self.groups[root]);assert root[0]==rva,'Pointer is not the primary entry'
  assert all(a<b and (i==0 or rows[i-1][1]<=a) for i,(a,b,u) in enumerate(rows))
  return rows
