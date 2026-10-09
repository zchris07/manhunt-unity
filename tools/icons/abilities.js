// Ability icons the original leaves blank (it tints the slot instead), drawn in the same style as its item art.

const itemLunge = itemCanvas((ctx) => {
  // Speed lines trailing a forward chevron, in Zach's blood red.
  ctx.lineCap = 'round';
  ctx.strokeStyle = 'rgba(230,200,190,0.55)';
  ctx.lineWidth = 2;
  for (const [y, x0] of [[13, 4], [20, 2], [27, 4]]) {
    ctx.beginPath();
    ctx.moveTo(x0, y);
    ctx.lineTo(x0 + 11, y);
    ctx.stroke();
  }
  ctx.beginPath();
  ctx.moveTo(15, 7);
  ctx.lineTo(36, 20);
  ctx.lineTo(15, 33);
  ctx.lineTo(21, 20);
  ctx.closePath();
  fillInk(ctx, '#c0202a', 1.4);
  ctx.fillStyle = 'rgba(255,190,180,0.45)';
  ctx.beginPath();
  ctx.moveTo(17, 10);
  ctx.lineTo(31, 19);
  ctx.lineTo(21.5, 19);
  ctx.closePath();
  ctx.fill();
});

const itemBurst = itemCanvas((ctx) => {
  // A speaker throwing out the purple wave (concave fronts, brightest at the crest).
  const g = ctx.createRadialGradient(24, 20, 2, 24, 20, 20);
  g.addColorStop(0, 'rgba(150,100,240,0.55)');
  g.addColorStop(1, 'rgba(150,100,240,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 40, 40);
  ctx.beginPath();
  ctx.moveTo(4, 15);
  ctx.lineTo(10, 15);
  ctx.lineTo(17, 8);
  ctx.lineTo(17, 32);
  ctx.lineTo(10, 25);
  ctx.lineTo(4, 25);
  ctx.closePath();
  fillInk(ctx, '#d9d3c1', 1.4);
  ctx.lineCap = 'round';
  const arcs = [[6, 3.2, '#c8a8ff'], [11, 2.8, '#9a6ae8'], [16, 2.4, '#7a3ae0']];
  for (const [r, w, c] of arcs) {
    ctx.beginPath();
    ctx.arc(14, 20, r + 6, -0.85, 0.85);
    ctx.lineWidth = w + 1.6;
    ctx.strokeStyle = 'rgba(6,6,6,0.5)';
    ctx.stroke();
    ctx.lineWidth = w;
    ctx.strokeStyle = c;
    ctx.stroke();
  }
});

function vapeIcon(cloud, cloudHi) {
  return itemCanvas((ctx) => {
    // The cloud, soft lumps rolling out of the mouthpiece.
    for (const [x, y, r, a] of [[27, 13, 8, 0.75], [33, 20, 6.5, 0.6], [22, 8, 5.5, 0.6], [32, 9, 5, 0.5], [26, 21, 5, 0.45]]) {
      const g = ctx.createRadialGradient(x, y, 0, x, y, r);
      g.addColorStop(0, cloudHi);
      g.addColorStop(0.55, cloud);
      g.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.globalAlpha = a + 0.25;
      ctx.fillStyle = g;
      circle(ctx, x, y, r);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
    // The pen: a slim dark body, a gold band, a light mouthpiece and the LED.
    ctx.save();
    ctx.translate(14, 27);
    ctx.rotate(-0.8);
    roundRect(ctx, -12, -3.2, 22, 6.4, 3);
    fillInk(ctx, '#2a2c32', 1.3);
    ctx.fillStyle = 'rgba(255,255,255,0.18)';
    ctx.fillRect(-10, -2.2, 18, 1.2);
    ctx.fillStyle = '#d8b04a';
    ctx.fillRect(6, -3.2, 2, 6.4);
    roundRect(ctx, 9, -2.4, 6, 4.8, 2);
    fillInk(ctx, '#c9c2b0', 1.1);
    ctx.fillStyle = cloudHi;
    circle(ctx, -8, 0, 1.2);
    ctx.fill();
    ctx.restore();
  });
}

const itemVape = vapeIcon('rgba(220,203,90,0.9)', 'rgba(255,245,180,1)');
const itemNic = vapeIcon('rgba(114,188,240,0.9)', 'rgba(210,236,255,1)');

/** The machete, turned up to the right as the original's HUD shows it. */
const itemMachete = (variant) => {
  const src = machete(variant);
  const [c, ctx] = canvas(48);
  ctx.save();
  ctx.translate(24, 24);
  ctx.rotate(-35 * Math.PI / 180);
  ctx.drawImage(src, -27, -8.4, 54, 16.9);
  ctx.restore();
  return c;
};
