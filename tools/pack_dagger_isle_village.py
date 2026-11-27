#!/usr/bin/env python3
"""Project the four painted materials onto UO's 44-pixel diamond."""
from pathlib import Path
from PIL import Image, ImageOps
from build_succubus_bolt import encode_static

ART = Path(__file__).resolve().parents[1] / 'art/world/dagger-isle'
materials = Image.open(ART / 'materials.png').convert('RGB')
fittings = Image.open(ART / 'fittings.png').convert('RGBA')

def write(graphic, sprite):
    colors = [max(1, (r >> 3) << 10 | (g >> 3) << 5 | b >> 3) if a > 120 else 0
              for r, g, b, a in sprite.convert('RGBA').getdata()]
    (ART / 'statics' / f'{graphic}.art').write_bytes(encode_static(*sprite.size, colors))

for group in range(4):
    half = materials.width // 2
    x0, y0 = group % 2 * half, group // 2 * half
    texture = materials.crop((x0+4,y0+4,x0+half-4,y0+half-4)).resize((128,128), Image.Resampling.LANCZOS)
    for variant in range(32 if group == 3 else 16):
        roof = group == 3
        far = variant >= 16
        rise = 16 if roof else 0
        # North and south eave heights above the tile's base, in screen pixels.
        north, south = (0,rise) if far else (rise,0)
        sprite = Image.new('RGBA',(44,44+rise))
        for y in range(sprite.height):
            for x in range(44):
                dx = (x+.5-22)/22
                v = (y+.5-rise+north-22*dx)/(44+north-south)
                u = dx+v
                if 0 <= u < 1 and 0 <= v < 1:
                    tx, ty = int((variant%4+u)*32)%128, int((variant//4%4+v)*32)%128
                    rgb = texture.getpixel((tx,ty))
                    if far:
                        rgb = tuple(int(c*.76) for c in rgb)
                    sprite.putpixel((x,y),(*rgb,255))
        write(59000+group*16+variant,sprite)

# Each carved prop has its own transparent silhouette. The reflected palisade
# is the perpendicular side of a continuous enclosure.
w,h=fittings.size
for i,(l,t,r,b,width,height) in enumerate([
    (0,0,.50,.47,112,90),(.60,0,.91,.474,29,76),
    (0,.474,.50,1,84,82),(.510,.479,1,1,77,80)]):
    bounds=tuple(round(v*k) for v,k in zip((l,t,r,b),(w,h,w,h)))
    cut=fittings.crop(bounds)
    cut=cut.crop(cut.getchannel('A').point(lambda a: 255 if a>128 else 0).getbbox()).resize((width,height),Image.Resampling.LANCZOS)
    write(59080+i,cut)
    if i==1:
        write(59084,ImageOps.mirror(cut))

# Feather each meadow into the actual snow beneath it. Four coverage bands
# share a continuous field, so the surrounding tundra shows through in drifts.
import math
grass = materials.crop((4,4,622,622)).resize((256,256),Image.Resampling.LANCZOS)
for band in range(4):
    for cell in range(64):
        sprite = Image.new('RGBA',(44,44))
        for y in range(44):
            for x in range(44):
                u=(y+x+1-22)/44
                v=(y-x+22)/44
                if not (0<=u<1 and 0<=v<1): continue
                a=(cell%8+u)/8*2*math.pi
                b=(cell//8+v)/8*2*math.pi
                rgb=grass.getpixel((int(a*256/(2*math.pi))%256,int(b*256/(2*math.pi))%256))
                wind=math.sin(a+.65*math.sin(b))+0.7*math.cos(b*2+a)+.42*math.sin(3*a-b)+.24*math.cos(a+4*b)
                if wind + (max(rgb)-80)/140 > 1.55-band*.70:
                    sprite.putpixel((x,y),(*rgb,255))
        write(58500+64*band+cell,sprite)

# The gable and the long roof plane share the same two control points. Register
# each plane independently so its pitch does not depend on the source camera.
from PIL import ImageDraw
roof=Image.open(ART/'assembly-roof.png').convert('RGBA')
ox,oy=520,930
front=(0,0); ridge=(154,-382)
def screen(p): return p[0]+ox,p[1]+oy
def plane(third,source):
    matrix=[]
    rx,ry=ridge
    tx,ty=third
    for f,r,t in zip((1098,952),(1366,534),source):
        dx=((r-f)*ty-(t-f)*ry)/(rx*ty-tx*ry)
        dy=(rx*(t-f)-tx*(r-f))/(rx*ty-tx*ry)
        matrix.extend((dx,dy,f-ox*dx-oy*dy))
    return roof.transform((900,980),Image.Transform.AFFINE,matrix,Image.Resampling.BICUBIC)
longface=plane((-462,-462),(18,535))
gable=plane((308,-308),(1565,792))
mask=Image.new('L',longface.size)
ImageDraw.Draw(mask).polygon([(ox-50*154,oy+50*382),(ox+50*154,oy-50*382),(20000,20000)],fill=255)
projected=Image.composite(gable,longface,mask)
anchors=[(-20,0),(-17,0),(-14,0),(-11,0),(-8,0),(-5,0),(-2,0),(0,0),(0,-3),(0,-6),(0,-9),(0,-12)]
centers=[ox+22*(x-y) for x,y in anchors]
for i,(dx,dy) in enumerate(anchors):
    left=(centers[i-1]+centers[i])//2 if i else 0
    right=(centers[i]+centers[i+1])//2 if i+1<len(centers) else projected.width
    section=projected.crop((left,0,right,projected.height))
    bbox=section.getchannel('A').point(lambda a:255 if a>120 else 0).getbbox()
    bottom=oy+22*(dx+dy)+144
    assert bbox and bbox[3]<=bottom, (i,bbox,bottom)
    radius=max(centers[i]-left-bbox[0],right-centers[i],1)
    img=Image.new('RGBA',(2*radius,bottom-bbox[1]))
    img.paste(section,(radius+left-centers[i],-bbox[1]))
    write(59100+i,img)

# A board gable follows the exact rising edge on each end of a pitched roof.
wood=materials.crop((10,680,615,916)).resize((96,160)).transpose(Image.Transpose.ROTATE_90)
for end in range(2):
    for face in range(2):
        for step in range(6):
            high=(step+1)*16
            sprite=Image.new('RGBA',(44,44+high))
            for x in range(44):
                v=((44 if end else 22)-x-.5)/22
                if not 0<=v<1: continue
                base=high+(22 if end else 0)+22*v
                rise=16*(step+(v if face else 1-v))
                for y in range(int(base-rise+.5),int(base+.5)):
                    rgb=wood.getpixel(((step*22+x)%wood.width,y%wood.height))
                    tone = sum(rgb)//3
                    sprite.putpixel((x,y),(28+tone//2,16+tone*4//10,9+tone*3//10,255))
            write(59120+end*12+face*6+step,sprite)

bollards=Image.open(ART/'mooring.png').convert('RGBA')
bollards=bollards.crop(bollards.getchannel('A').point(lambda a: 255 if a>128 else 0).getbbox())
bollards=bollards.resize((82,66),Image.Resampling.LANCZOS)
rail=Image.new('RGBA',(148,109))
rail.paste(bollards.transform((82,109),Image.Transform.AFFINE,(1,0,0,-.52,1,0),Image.Resampling.BICUBIC))
write(59085,rail)
