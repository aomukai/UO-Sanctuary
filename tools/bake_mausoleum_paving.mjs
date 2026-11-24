// Bake the UO floor projection once, so the live client still draws one chamber texture.
import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const art = fileURLToPath(new URL("../art/world/lazarus-mausoleum/", import.meta.url));
const width = 1960, height = 2880;
function readPixels(file, filter = "null", format = "rgb24") {
    return execFileSync("ffmpeg", ["-loglevel", "error", "-i", art + file, "-vf", filter,
        "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", format, "-"], { maxBuffer: 32 << 20 });
}
const painting = readPixels("sources/undercroft.jpg");
const lighting = readPixels("sources/undercroft.jpg", "gblur=sigma=12");
const sampleSize = 128;
const masonry = readPixels("sources/paving.jpg", "scale=128:128:flags=lanczos");
const mean = masonry.reduce((sum, n) => sum + n, 0) / masonry.length;
const piers = [[472,1280,256,660], [1200,1280,272,660], [840,1980,248,536]].map(
    ([x,y,w,h],i) => ({ x,y,w,h,rgba: readPixels(`pier-${i}.png`, "null", "rgba") })
);

// Wall feet and perimeter of the original registered painting in quarter-size pixels.
const westWall = [[2,472],[14,472],[20,458],[20,448],[42,427],[49,418],[51,412],
    [57,411],[59,419],[66,412],[66,400],[94,370],[99,366],[100,356],[108,355],
    [110,359],[116,351],[116,344],[147,305],[150,296],[155,293],[157,297],
    [165,286],[166,278],[191,251],[198,245],[201,244],[202,248],[210,239],[211,234],[224,228]];
const back = [...westWall, ...westWall.toReversed().map(([x,y]) => [490-x,y])];
const edge = [[2,475],[244,718],[488,475]];
function atX(poly, x) {
    let i = 1;
    while (i < poly.length - 1 && x > poly[i][0]) i++;
    const [a,b] = poly[i-1], [c,d] = poly[i];
    return b + (d-b)*(x-a)/(c-a);
}
// Mirrored texture addressing keeps opposite borders continuous. Both world axes
// traverse exactly 7 tiles per repeat, including beside the distant pentagrams.
function texel(world) {
    const cell = Math.floor(world / 7);
    const local = Math.floor((world / 7 - cell) * sampleSize);
    return cell % 2 ? sampleSize - 1 - local : local;
}
for (let x=8; x<width-8; x++) {
    const top = atX(back, x/4) * 4, bottom = atX(edge, x/4) * 4;
    for (let y=Math.ceil(top); y<bottom && y<height; y++) {
        let blend = Math.min(1, (y-top)/25, (bottom-y)/12);
        for (const p of piers) {
            if (x>=p.x && y>=p.y && x<p.x+p.w && y<p.y+p.h)
                blend *= 1 - p.rgba[4*((y-p.y)*p.w+x-p.x)+3]/255;
        }
        const sx=x-980, sy=y-1880, index=3*(y*width+x);
        // At half native resolution a UO step projects to (+/-11, +11).
        const u=texel((sy+sx)/22), v=texel((sy-sx)/22);
        const tile = 3*(v*sampleSize+u);
        const stone = (masonry[tile]+masonry[tile+1]+masonry[tile+2])/3;
        for (let c=0; c<3; c++) {
            const relief = (0.8*stone + 0.2*masonry[tile+c])/mean;
            const shade = lighting[index+c] * Math.max(0.28, 1 + 1.18*(relief-1));
            painting[index+c] = Math.max(0, Math.min(255,
                Math.round(painting[index+c]*(1-blend) + shade*blend)));
        }
    }
}
// Register the painted stair noses, risers and upper landing to the same
// octagons that provide walkable heights on the server.
const tiers = JSON.parse(readFileSync(art + "chancel.json", "utf8"));
const platform = readPixels("sources/chancel.png", "null", "rgba");
// Clockwise vertices on each painted tread, starting at the far left bevel.
// Landmarks use the original painting's coordinates. The stored sprite is half size.
const painted = [
    [[284,84],[1286,84],[1427,224],[1427,710],[1284,851],[287,851],[148,713],[148,224]],
    [[310,108],[1259,108],[1397,239],[1397,576],[1253,710],[319,710],[177,577],[177,239]],
    [[354,145],[1221,145],[1319,246],[1319,466],[1192,603],[375,603],[251,468],[251,247]]
];
const paintedRisers = [49,43,34];
function shiftY(points, distance) { return points.map(([x,y]) => [x,y+distance]); }
function tread(t) {
    const w=(t.HalfWidth+0.5)*11, h=(t.HalfDepth+0.5)*11, c=t.Chamfer*11;
    return [[c-w,-h],[w-c,-h],[w,c-h],[w,h-c],[w-c,h],[c-w,h],[-w,h-c],[-w,c-h]]
        .map(([x,y]) => [980+(x-y)*Math.SQRT1_2, 1880+(y+x)*Math.SQRT1_2-2*t.Z]);
}
function triangle(a,b,c,ta,tb,tc) {
    const det=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1]);
    if (Math.abs(det)<0.001) return;
    for (let y=Math.floor(Math.min(a[1],b[1],c[1])); y<=Math.ceil(Math.max(a[1],b[1],c[1])); y++)
    for (let x=Math.floor(Math.min(a[0],b[0],c[0])); x<=Math.ceil(Math.max(a[0],b[0],c[0])); x++) {
        const u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/det;
        const v=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/det;
        const w=1-u-v;
        if (Math.min(u,v,w)<-0.0001) continue;
        const tx=Math.round((u*ta[0]+v*tb[0]+w*tc[0])/2);
        const ty=Math.round((u*ta[1]+v*tb[1]+w*tc[1])/2);
        const src=4*(ty*788+tx), dst=3*(y*width+x);
        const alpha=platform[src+3]/255;
        for (let k=0;k<3;k++)
            painting[dst+k]=Math.round(painting[dst+k]*(1-alpha)+platform[src+k]*0.73*alpha);
    }
}
function band(outer,inner,uvOuter,uvInner,first=0,last=7) {
    for (let i=first; i<=last; i++) {
        const j=(i+1)%8;
        triangle(outer[i],outer[j],inner[j],uvOuter[i],uvOuter[j],uvInner[j]);
        triangle(outer[i],inner[j],inner[i],uvOuter[i],uvInner[j],uvInner[i]);
    }
}
// A small contact shadow sits under the plinth, strongest at the front right.
const base=tiers[0];
const radius=Math.ceil((base.HalfWidth+base.HalfDepth+1)*11*Math.SQRT1_2)+6;
for (let y=1883-radius;y<=1883+radius;y++) for (let x=982-radius;x<=982+radius;x++) {
    const sx=x-982, sy=y-1883;
    const dx=Math.abs((sx+sy)*Math.SQRT1_2), dy=Math.abs((sy-sx)*Math.SQRT1_2);
    const w=(base.HalfWidth+0.5)*11, h=(base.HalfDepth+0.5)*11;
    const d=Math.min(w-dx,h-dy,(w+h-11*base.Chamfer-dx-dy)/Math.SQRT2);
    const shadow=1-0.36*Math.max(0,Math.min(1,(d+4)/5));
    const index=3*(y*width+x);
    for (let k=0;k<3;k++) painting[index+k]*=shadow;
}
for (let i=0; i<tiers.length; i++) {
    const top=tread(tiers[i]), uv=painted[i];
    const rise=2*(tiers[i].Z-(tiers[i-1]?.Z ?? 0));
    // Paint exposed vertical stone faces. Give previously edge-on returns real
    // fascia stone from the front of the same step, keeping all risers upright.
    const bottom=shiftY(top,rise);
    const uvBottom=shiftY(uv,paintedRisers[i]);
    for (let edge=0; edge<8; edge++) {
        const next=(edge+1)%8;
        if (top[next][0]>=top[edge][0]) continue;
        const sourceEdge = edge===2 ? 4 : edge;
        const sourceNext=(sourceEdge+1)%8;
        triangle(bottom[edge],bottom[next],top[next],uvBottom[sourceEdge],uvBottom[sourceNext],uv[sourceNext]);
        triangle(bottom[edge],top[next],top[edge],uvBottom[sourceEdge],uv[sourceNext],uv[sourceEdge]);
    }
    if (i < tiers.length-1) {
        const nextBottom=shiftY(tread(tiers[i+1]),2*(tiers[i+1].Z-tiers[i].Z));
        band(top,nextBottom,uv,shiftY(painted[i+1],paintedRisers[i+1]));
    } else {
        for (let j=0;j<8;j++)
            triangle([980,1880-2*tiers[i].Z],top[j],top[(j+1)%8],[785,375],uv[j],uv[(j+1)%8]);
    }
}
// The foreground pier cap overlaps a corner of the rotated platform in projection.
// The far column mirrors the entrance column 48 map tiles along each axis.
const farPier = { ...piers[2], y: piers[2].y - 2*48*11 };
for (const pier of [...piers, farPier]) {
    for (let y=0; y<pier.h; y++) for (let x=0; x<pier.w; x++) {
        const src=4*(y*pier.w+x), alpha=pier.rgba[src+3]/255;
        if (!alpha) continue;
        const dst=3*((pier.y+y)*width+pier.x+x);
        for (let c=0; c<3; c++)
            painting[dst+c]=Math.round(painting[dst+c]*(1-alpha)+pier.rgba[src+c]*alpha);
    }
}
execFileSync("ffmpeg", ["-loglevel","error","-f","rawvideo","-pix_fmt","rgb24","-s",
    `${width}x${height}`,"-i","-","-frames:v","1","-q:v","2","-y",art+"chamber.jpg"], { input:painting });
