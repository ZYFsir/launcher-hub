const fs = require('fs');
const path = require('path');
let sharp;
try { sharp = require(process.env.SHARP_MODULE || 'sharp'); }
catch (e) { throw new Error('Install sharp or set SHARP_MODULE to its module path.'); }

const root = 'D:/Project/services/launcher-hub';
const gen = path.join(root, 'assets/placeholders/generated');
const ids = ['llama', 'comfy', 'dsh'];

async function makeTile(id) {
  const src = path.join(gen, id + '-source.webp');
  const dst = path.join(gen, id + '-tile-1024.png');
  const mask = Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024"><rect width="1024" height="1024" rx="220" fill="#fff"/></svg>');
  await sharp(src).resize(1024, 1024, { kernel: 'lanczos3' })
    .composite([{ input: mask, blend: 'dest-in' }]).png().toFile(dst);
  return dst;
}

async function makeMark(id) {
  const src = path.join(gen, id + '-tile-1024.png');
  const { data, info } = await sharp(src).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const w = info.width, h = info.height, ch = info.channels;
  // Background sample: top-center inside the rounded tile, away from the head/symbol.
  const sx = w >> 1, sy = 45;
  const si = (sy * w + sx) * ch;
  const br = data[si], bg = data[si + 1], bb = data[si + 2];

  // Flood-fill the tile background from a known background seed. This keeps
  // navy hair on the DSH avatar even when it is color-close to the tile.
  const removed = new Uint8Array(w * h);
  const stackX = new Int32Array(w * h);
  const stackY = new Int32Array(w * h);
  let sp = 0;
  stackX[sp] = sx; stackY[sp] = sy; sp++;
  while (sp > 0) {
    sp--;
    const x = stackX[sp], y = stackY[sp];
    if (x < 0 || y < 0 || x >= w || y >= h) continue;
    const idx = y * w + x;
    if (removed[idx]) continue;
    const i = idx * ch;
    const dr = data[i] - br, dg = data[i + 1] - bg, db = data[i + 2] - bb;
    if (Math.sqrt(dr * dr + dg * dg + db * db) > 30) continue;
    removed[idx] = 1;
    if (sp + 4 >= stackX.length) break;
    stackX[sp] = x + 1; stackY[sp] = y; sp++;
    stackX[sp] = x - 1; stackY[sp] = y; sp++;
    stackX[sp] = x; stackY[sp] = y + 1; sp++;
    stackX[sp] = x; stackY[sp] = y - 1; sp++;
  }

  let minX = w, minY = h, maxX = 0, maxY = 0;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const idx = y * w + x;
      const corner = (x < 12 && y < 12) || (x < 12 && y >= h - 12) || (x >= w - 12 && y < 12) || (x >= w - 12 && y >= h - 12);
      if (removed[idx] || corner) continue;
      if (x < minX) minX = x; if (x > maxX) maxX = x;
      if (y < minY) minY = y; if (y > maxY) maxY = y;
    }
  }
  if (maxX <= minX || maxY <= minY) throw new Error('empty mark ' + id);
  const cw = maxX - minX + 1, chh = maxY - minY + 1;
  const crop = Buffer.alloc(cw * chh * 4);
  for (let y = 0; y < chh; y++) {
    for (let x = 0; x < cw; x++) {
      const si2 = ((minY + y) * w + (minX + x)) * ch;
      const di = (y * cw + x) * 4;
      const idx = (minY + y) * w + (minX + x);
      crop[di] = data[si2]; crop[di + 1] = data[si2 + 1]; crop[di + 2] = data[si2 + 2];
      crop[di + 3] = removed[idx] ? 0 : 255;
    }
  }
  const inner = await sharp(crop, { raw: { width: cw, height: chh, channels: 4 } })
    .resize(400, 400, { fit: 'inside', kernel: 'lanczos3' })
    .png().toBuffer();
  const meta = await sharp(inner).metadata();
  const out = path.join(gen, id + '-mark.png');
  const base = await sharp({ create: { width: 512, height: 512, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
    .composite([{ input: inner, left: Math.round((512 - meta.width) / 2), top: Math.round((512 - meta.height) / 2) }])
    .png().toBuffer();
  const rounded = Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512"><rect width="512" height="512" rx="110" fill="#fff"/></svg>');
  await sharp(base).composite([{ input: rounded, blend: 'dest-in' }]).png().toFile(out);
  return out;
}

(async () => {
  const tiles = [];
  for (const id of ids) {
    const tile = await makeTile(id);
    const mark = await makeMark(id);
    tiles.push({ id, tile, mark });
    console.log(id, tile, mark);
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1260" height="820">
    <rect width="1260" height="820" fill="#F4F7FB"/>
    <text x="40" y="52" font-family="Segoe UI, Microsoft YaHei" font-size="28" font-weight="700" fill="#1D2430">Launcher Hub · 原创占位图标</text>
    <text x="40" y="78" font-family="Segoe UI, Microsoft YaHei" font-size="14" fill="#68758A">L/C CC0-1.0 · DSH CC BY-SA 4.0 · Qwen-Image Edit</text>
  </svg>`;
  const canvas = sharp(Buffer.from(svg));
  const comps = [];
  for (let i = 0; i < tiles.length; i++) {
    const t = tiles[i];
    const x = 40 + i * 410;
    const thumb = await sharp(t.tile).resize(360, 360).png().toBuffer();
    comps.push({ input: thumb, left: x, top: 130 });
    comps.push({ input: Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="360" height="40"><text x="0" y="26" font-family="Segoe UI, Microsoft YaHei" font-size="18" font-weight="600" fill="#1D2430">${t.id}</text></svg>`), left: x, top: 505 });
  }
  await canvas.composite(comps).png().toFile(path.join(gen, 'placeholder-preview.png'));
  console.log('preview written');
})().catch(e => { console.error(e); process.exit(1); });
