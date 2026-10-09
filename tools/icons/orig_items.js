let SCALE = 4;
/** Soft dark edge on sprites (no comic outlines). */
const INK = 'rgba(6,6,6,0.5)';
const INK_W = 1;

function canvas(w, h = w) {
  const c = document.createElement('canvas');
  c.width = Math.round(w * SCALE);
  c.height = Math.round(h * SCALE);
  const ctx = c.getContext('2d');
  ctx.scale(SCALE, SCALE);
  ctx.lineJoin = 'round';
  ctx.lineCap = 'round';
  return [c, ctx];
}

function rgb(r, g, b, a = 1) {
  return a >= 1 ? `rgb(${r | 0},${g | 0},${b | 0})` : `rgba(${r | 0},${g | 0},${b | 0},${a})`;
}



function mix(a, b, t) {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

function hexRgb(hex) {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

/** A colour lightened (t > 0) or darkened (t < 0). */
function shade(hex, t) {
  const c = hexRgb(hex);
  const target = t >= 0 ? [255, 255, 255] : [16, 10, 30];
  const m = mix(c, target, Math.abs(t));
  return rgb(m[0], m[1], m[2]);
}

function ink(ctx, w = INK_W) {
  ctx.lineWidth = w;
  ctx.strokeStyle = INK;
  ctx.stroke();
}

/** Fills the current path, then outlines it. */
function fillInk(ctx, fill, w = INK_W) {
  ctx.fillStyle = fill;
  ctx.fill();
  ink(ctx, w);
}

function ellipse(ctx, x, y, rx, ry, rot = 0) {
  ctx.beginPath();
  ctx.ellipse(x, y, Math.max(0.1, rx), Math.max(0.1, ry), rot, 0, Math.PI * 2);
}

function circle(ctx, x, y, r) {
  ctx.beginPath();
  ctx.arc(x, y, Math.max(0.1, r), 0, Math.PI * 2);
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, r);
}

/** An irregular blob path (leaf clumps, rocks). */
/** Zach's machete, blade pointing +x from the grip (held in his right hand). */
const machete = () => {
  const [c, ctx] = canvas(64, 20);
  roundRect(ctx, 2, 7, 13, 6, 2);
  fillInk(ctx, '#5a3a22', 1.3);
  ctx.beginPath();
  ctx.moveTo(14, 5.5);
  ctx.lineTo(56, 4);
  ctx.quadraticCurveTo(62, 7, 58, 13);
  ctx.lineTo(14, 14);
  ctx.closePath();
  fillInk(ctx, '#c9d2dc', 1.4);
  ctx.fillStyle = '#eef3f8';
  ctx.fillRect(16, 6.5, 38, 2);
  ctx.fillStyle = 'rgba(200,20,30,0.9)';
  ellipse(ctx, 50, 10, 6, 3, 0.2);
  ctx.fill();
  return c;
};
function itemCanvas(draw, size = 40) {
  return () => {
    const [c, ctx] = canvas(size);
    ctx.translate(size / 2 - 20, size / 2 - 20);
    draw(ctx);
    return c;
  };
}

const itemBottle = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 20);
  ctx.rotate(-0.6);
  ctx.beginPath();
  ctx.moveTo(-14, -6);
  ctx.lineTo(4, -6);
  ctx.quadraticCurveTo(8, -6, 9, -3);
  ctx.lineTo(15, -2.5);
  ctx.lineTo(15, 2.5);
  ctx.lineTo(9, 3);
  ctx.quadraticCurveTo(8, 6, 4, 6);
  ctx.lineTo(-14, 6);
  ctx.closePath();
  fillInk(ctx, '#2fb45a');
  ctx.fillStyle = '#f2e6c0';
  ctx.fillRect(-10, -5, 9, 10);
  ctx.fillStyle = '#e8453c';
  ctx.fillRect(-9, -2, 7, 4);
  ctx.fillStyle = 'rgba(255,255,255,0.6)';
  ctx.fillRect(-12, -4, 14, 2);
  roundRect(ctx, 14, -2.5, 3, 5, 1);
  fillInk(ctx, '#f2b632', 1);
  ctx.restore();
});

const itemGoggles = itemCanvas((ctx) => {
  roundRect(ctx, 6, 14, 28, 12, 5);
  fillInk(ctx, '#2a2e36');
  for (const x of [12, 28]) {
    circle(ctx, x, 20, 6);
    fillInk(ctx, '#1a1a20', 1.4);
    circle(ctx, x, 20, 4.2);
    ctx.fillStyle = '#5cff6a';
    ctx.fill();
    ctx.fillStyle = 'rgba(255,255,255,0.8)';
    circle(ctx, x - 1.5, 18.5, 1.3);
    ctx.fill();
  }
  ctx.beginPath();
  ctx.moveTo(6, 20);
  ctx.lineTo(1, 22);
  ctx.moveTo(34, 20);
  ctx.lineTo(39, 22);
  ctx.lineWidth = 3;
  ctx.strokeStyle = '#2a2e36';
  ctx.stroke();
});

const itemShotgun = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 20);
  ctx.rotate(-0.35);
  // Stock.
  ctx.beginPath();
  ctx.moveTo(-18, -2);
  ctx.lineTo(-6, -4);
  ctx.lineTo(-4, 3);
  ctx.lineTo(-18, 5);
  ctx.closePath();
  fillInk(ctx, '#9a5a2c');
  // Barrels.
  roundRect(ctx, -6, -4, 25, 3.2, 1);
  fillInk(ctx, '#5a6270', 1.2);
  roundRect(ctx, -6, -0.6, 25, 3.2, 1);
  fillInk(ctx, '#6b7482', 1.2);
  roundRect(ctx, -2, 2, 9, 4, 1.5);
  fillInk(ctx, '#7a4520', 1.2);
  ctx.restore();
}, 44);

/** Doctor Pepper: a deep red can with a white oval and a dark red "23" swoosh. */
const itemEnergy = itemCanvas((ctx) => {
  roundRect(ctx, 12, 6, 16, 28, 4);
  fillInk(ctx, '#a3122a');
  ctx.fillStyle = '#d8d8e0';
  ctx.fillRect(13, 6, 14, 3);
  ctx.fillRect(13, 31, 14, 2);
  // The white oval label.
  ellipse(ctx, 20, 19, 6, 7.5);
  fillInk(ctx, '#f4f0ea', 1);
  ctx.strokeStyle = '#6a0a18';
  ctx.lineWidth = 1.6;
  ctx.beginPath();
  ctx.moveTo(16.5, 21);
  ctx.quadraticCurveTo(20, 14, 23.5, 17);
  ctx.stroke();
  ctx.fillStyle = '#6a0a18';
  ctx.fillRect(17, 22, 6, 1.6);
  ctx.fillStyle = 'rgba(255,255,255,0.4)';
  ctx.fillRect(14, 10, 2.5, 18);
});

/** The Grapes of Wrath: a worn hardback, grape-purple cover and a gold title band. */
const itemBook = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 20);
  ctx.rotate(-0.2);
  roundRect(ctx, -12, -15, 24, 30, 2);
  fillInk(ctx, '#f0e8d0', 1.4);
  roundRect(ctx, -13, -16, 24, 30, 2);
  fillInk(ctx, '#5a2a6a');
  ctx.fillStyle = '#3e1a4a';
  ctx.fillRect(-13, -16, 4, 30);
  ctx.fillStyle = '#d8b04a';
  ctx.fillRect(-7, -9, 16, 3);
  ctx.fillRect(-7, -4, 12, 1.5);
  // A small bunch of grapes.
  ctx.fillStyle = '#9a5ab8';
  for (const [x, y] of [
    [1, 4],
    [5, 4],
    [3, 7],
    [-1, 7],
    [1, 10],
  ]) {
    circle(ctx, x, y, 2);
    ctx.fill();
  }
  ctx.restore();
});

/** Mr Beast bar: a chocolate bar half out of its bright blue wrapper. */
const itemBeastBar = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 20);
  ctx.rotate(-0.35);
  // Chocolate, in squares.
  roundRect(ctx, -16, -8, 18, 16, 1.5);
  fillInk(ctx, '#5a3218');
  ctx.strokeStyle = '#3a1e0c';
  ctx.lineWidth = 1;
  for (const x of [-10, -4]) {
    ctx.beginPath();
    ctx.moveTo(x, -8);
    ctx.lineTo(x, 8);
    ctx.stroke();
  }
  ctx.beginPath();
  ctx.moveTo(-16, 0);
  ctx.lineTo(2, 0);
  ctx.stroke();
  ctx.fillStyle = 'rgba(255,220,180,0.25)';
  ctx.fillRect(-15, -7, 16, 2);
  // Wrapper.
  roundRect(ctx, 0, -9, 17, 18, 2);
  fillInk(ctx, '#2a8cff');
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 6px sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText('BEAST', 8.5, 2);
  ctx.fillStyle = '#ffd23a';
  ctx.fillRect(1, 5, 15, 2);
  ctx.restore();
});

/** Mini shield: a small round flask of glowing blue liquid with a stopper, Fortnite style. */
const itemShield = itemCanvas((ctx) => {
  // Glow.
  const g = ctx.createRadialGradient(20, 24, 2, 20, 24, 16);
  g.addColorStop(0, 'rgba(90,190,255,0.55)');
  g.addColorStop(1, 'rgba(90,190,255,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 40, 40);
  // Neck and stopper.
  roundRect(ctx, 17, 8, 6, 8, 1.5);
  fillInk(ctx, '#d8eefc', 1.2);
  roundRect(ctx, 16, 5, 8, 5, 1.5);
  fillInk(ctx, '#9a6a3a', 1.2);
  // Round flask.
  circle(ctx, 20, 25, 10);
  fillInk(ctx, '#bfe4ff', 1.4);
  ctx.save();
  circle(ctx, 20, 25, 9);
  ctx.clip();
  ctx.fillStyle = '#2e9cff';
  ctx.fillRect(9, 23, 22, 14);
  ctx.fillStyle = '#7fd0ff';
  ctx.fillRect(9, 23, 22, 2);
  ctx.restore();
  ctx.fillStyle = 'rgba(255,255,255,0.7)';
  ellipse(ctx, 16, 21, 2, 3.5, -0.4);
  ctx.fill();
});

/** Jaden's P250: a compact black handgun. */
/** The 0.50 cal: a long dark rifle with a scope and a red lens. */
const itemSniper = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 20);
  ctx.rotate(-0.55);
  roundRect(ctx, -19, -2.5, 38, 5, 1.5);
  fillInk(ctx, '#33353a');
  roundRect(ctx, -19, -4, 11, 8, 2);
  fillInk(ctx, '#4a3a2a');
  roundRect(ctx, -6, -8, 15, 4.5, 2);
  fillInk(ctx, '#1c1c20');
  ctx.fillStyle = '#ff3030';
  ctx.beginPath();
  ctx.arc(9, -5.7, 1.6, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = 'rgba(255,255,255,0.25)';
  ctx.fillRect(-8, -1.8, 24, 1);
  ctx.restore();
});

/** A jar of piss: a jam jar of murky yellow with a tin lid. */
const itemPiss = itemCanvas((ctx) => {
  roundRect(ctx, 11, 12, 18, 22, 5);
  fillInk(ctx, '#c9d6c8', 1.2);
  ctx.fillStyle = '#e6c820';
  ctx.fillRect(13, 18, 14, 14);
  ctx.fillStyle = 'rgba(255,255,200,0.45)';
  ctx.fillRect(15, 20, 3, 10);
  roundRect(ctx, 10, 8, 20, 6, 2);
  fillInk(ctx, '#6a6e74', 1.2);
});

const itemPistol = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 20);
  ctx.rotate(-0.3);
  roundRect(ctx, -12, -6, 26, 7, 1.5);
  fillInk(ctx, '#2a2a2e');
  ctx.beginPath();
  ctx.moveTo(-10, 0);
  ctx.lineTo(-3, 0);
  ctx.lineTo(-5, 12);
  ctx.lineTo(-12, 12);
  ctx.closePath();
  fillInk(ctx, '#1c1c20');
  ctx.strokeStyle = '#0a0a0a';
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.arc(-1, 3, 3, 0, Math.PI);
  ctx.stroke();
  ctx.fillStyle = 'rgba(255,255,255,0.25)';
  ctx.fillRect(-10, -5, 20, 1.5);
  ctx.restore();
});

/**
 * Galaxy gas trap: a Galaxy Gas-style nitrous canister (a big whippit tank): a stout
 * cylinder wrapped in a colourful galaxy label, a silver shoulder and a valve on top.
 */
const itemTrap = itemCanvas((ctx) => {
  // Body with the galaxy wrap.
  roundRect(ctx, 11, 12, 18, 25, 5);
  fillInk(ctx, '#1a1030', 1.6);
  ctx.save();
  roundRect(ctx, 11, 12, 18, 25, 5);
  ctx.clip();
  const g = ctx.createLinearGradient(11, 12, 29, 37);
  g.addColorStop(0, '#2a1a6a');
  g.addColorStop(0.35, '#b03ad8');
  g.addColorStop(0.6, '#ff5ab0');
  g.addColorStop(0.85, '#3a6aff');
  g.addColorStop(1, '#1a1030');
  ctx.fillStyle = g;
  ctx.fillRect(11, 16, 18, 17);
  // Stars.
  ctx.fillStyle = '#ffffff';
  for (const [x, y, r] of [
    [14, 19, 0.9],
    [25, 22, 1.1],
    [18, 29, 0.8],
    [23, 31, 0.7],
    [16, 24, 0.6],
  ]) {
    circle(ctx, x, y, r);
    ctx.fill();
  }
  // Label band and a highlight down the side.
  ctx.fillStyle = 'rgba(255,255,255,0.85)';
  ctx.fillRect(11, 25, 18, 1.2);
  ctx.fillStyle = 'rgba(255,255,255,0.22)';
  ctx.fillRect(13, 12, 3, 25);
  ctx.restore();
  // Silver shoulder, neck and valve.
  ctx.beginPath();
  ctx.moveTo(11.5, 14);
  ctx.quadraticCurveTo(20, 4, 28.5, 14);
  ctx.closePath();
  fillInk(ctx, '#c9d2dc', 1.4);
  roundRect(ctx, 17, 4, 6, 5, 1.5);
  fillInk(ctx, '#9aa4b0', 1.2);
  roundRect(ctx, 15, 2, 10, 3, 1.5);
  fillInk(ctx, '#1a1a1a', 1);
  roundRect(ctx, 22, 5, 6, 2.4, 1);
  fillInk(ctx, '#7a8490', 1);
});

/** Plasma's golden pump: a gold tactical shotgun (the legendary drop). */
const itemGoldenPump = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(24, 22);
  ctx.rotate(-0.3);
  // Stock.
  ctx.beginPath();
  ctx.moveTo(-22, -3);
  ctx.lineTo(-9, -4);
  ctx.lineTo(-7, 3);
  ctx.lineTo(-20, 6);
  ctx.closePath();
  fillInk(ctx, '#b8862a');
  // Receiver.
  roundRect(ctx, -10, -5, 16, 8, 2);
  fillInk(ctx, '#f0c040', 1.4);
  ctx.fillStyle = '#fff0a0';
  ctx.fillRect(-8, -4, 12, 1.5);
  // Barrel and magazine tube.
  roundRect(ctx, 4, -4.5, 20, 3.4, 1);
  fillInk(ctx, '#d8a830', 1.2);
  roundRect(ctx, 4, -0.8, 17, 2.6, 1);
  fillInk(ctx, '#a07820', 1.1);
  // Pump grip.
  roundRect(ctx, 8, -1.6, 9, 4.6, 1.5);
  fillInk(ctx, '#2a2218', 1.1);
  // Grip.
  roundRect(ctx, -9, 2, 4, 6, 1.5);
  fillInk(ctx, '#b8862a', 1.1);
  ctx.restore();
}, 48);

const itemConfit = itemCanvas((ctx) => {
  ellipse(ctx, 20, 24, 17, 10);
  fillInk(ctx, '#f4f4f4');
  ellipse(ctx, 20, 24, 12, 6.5);
  ctx.strokeStyle = '#c9c9d2';
  ctx.lineWidth = 1;
  ctx.stroke();
  // Duck leg.
  ellipse(ctx, 17, 22, 9, 6.5, -0.3);
  fillInk(ctx, '#c8752e');
  ctx.fillStyle = '#e8a050';
  ellipse(ctx, 15, 20, 4, 2.5, -0.3);
  ctx.fill();
  roundRect(ctx, 24, 15, 9, 3.5, 1.5);
  fillInk(ctx, '#f2e6c0', 1.2);
  circle(ctx, 33, 16.5, 2.3);
  fillInk(ctx, '#f2e6c0', 1.1);
  // Garnish.
  ctx.fillStyle = '#3fae4f';
  ellipse(ctx, 26, 27, 3, 1.6, 0.4);
  ctx.fill();
});

const itemTablet = itemCanvas((ctx) => {
  roundRect(ctx, 8, 6, 24, 30, 4);
  fillInk(ctx, '#22242c');
  const g = ctx.createLinearGradient(10, 8, 30, 34);
  g.addColorStop(0, '#7af8ff');
  g.addColorStop(1, '#2a8cff');
  ctx.fillStyle = g;
  ctx.fillRect(11, 9, 18, 23);
  ctx.strokeStyle = 'rgba(255,255,255,0.8)';
  ctx.lineWidth = 1;
  circle(ctx, 20, 20, 5);
  ctx.stroke();
  circle(ctx, 20, 20, 2);
  ctx.stroke();
});

const itemHemp = itemCanvas((ctx) => {
  roundRect(ctx, 6, 12, 26, 16, 3);
  fillInk(ctx, '#2a3a24');
  roundRect(ctx, 32, 16, 4, 8, 1);
  fillInk(ctx, '#c9d2dc', 1.2);
  ctx.fillStyle = '#6dff6a';
  ctx.fillRect(9, 15, 12, 10);
  // Leaf.
  ctx.fillStyle = '#2a3a24';
  for (let i = -2; i <= 2; i++) {
    const a = -Math.PI / 2 + i * 0.5;
    ellipse(ctx, 25 + Math.cos(a) * 3, 20 + Math.sin(a) * 3, 3.5, 1.2, a);
    ctx.fill();
  }
});

/** A marijuana leaf: seven serrated fingers fanned from the stem. */
const itemLeaf = itemCanvas((ctx) => {
  ctx.save();
  ctx.translate(20, 27);
  const fingers = [
    [-90, 17],
    [-58, 14],
    [-122, 14],
    [-28, 10.5],
    [-152, 10.5],
    [-8, 7],
    [-172, 7],
  ];
  ctx.fillStyle = '#3fae3a';
  ctx.strokeStyle = '#143212';
  ctx.lineWidth = 1.2;
  for (const [deg, len] of fingers) {
    ctx.save();
    ctx.rotate((deg * Math.PI) / 180);
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(len * 0.3, -2.6);
    ctx.lineTo(len * 0.5, -1.6);
    ctx.lineTo(len * 0.62, -3.4);
    ctx.lineTo(len * 0.8, -1.8);
    ctx.lineTo(len, 0);
    ctx.lineTo(len * 0.8, 1.8);
    ctx.lineTo(len * 0.62, 3.4);
    ctx.lineTo(len * 0.5, 1.6);
    ctx.lineTo(len * 0.3, 2.6);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }
  ctx.strokeStyle = '#143212';
  ctx.lineWidth = 1.6;
  ctx.beginPath();
  ctx.moveTo(0, 0);
  ctx.lineTo(0, 9);
  ctx.stroke();
  ctx.restore();
});

