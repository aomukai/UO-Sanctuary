// Project the approved painted slab and its two visible upright faces onto the UO grid.
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const art = fileURLToPath(new URL("../art/world/lazarus-mausoleum/", import.meta.url));
const source = execFileSync("ffmpeg", ["-loglevel","error","-i",art+"sources/altar-oblique.png",
    "-f","rawvideo","-pix_fmt","rgba","-"], { maxBuffer: 4 << 20 });
const width=128, height=160, samples=4;
const pixels = Buffer.alloc(width * height * samples * samples * 4);
for (let j=0; j<height*samples; j++) for (let i=0; i<width*samples; i++) {
    // Camera-space basis from the west corner of the slab: (84,84) east,
    // (33,-33) north. Carved sides extend straight down beneath the lip.
    const x=(i+0.5)/samples-5, y=(j+0.5)/samples-37;
    let east=(x+y)/168, north=(x-y)/66, down=0;
    if (x<=84 && y>x) {
        east=x/84;
        north=0;
        down=(y-x)/36;
    } else if (x>84 && y>168-x) {
        east=1;
        north=(x-84)/33;
        down=(y-168+x)/36;
    }
    // Corresponding painted edges in the source cutout (480 × 592).
    const u=Math.round(9 + 263*east + 200*north);
    const v=Math.round(48 + 356*east - 43*north + 184*down);
    if (u<0 || u>=480 || v<0 || v>=592) continue;
    const src=4*(480*v+u), dst=4*(j*width*samples+i);
    pixels.writeUInt32LE(source.readUInt32LE(src),dst);
}
execFileSync("ffmpeg",["-loglevel","error","-f","rawvideo","-s",`${width*samples}x${height*samples}`,
    "-pix_fmt","rgba","-i","-","-vf",`scale=${width}:${height}:flags=lanczos`,"-frames:v","1","-y",art+"altar.png"],
    {input:pixels});
