# HUD icons

The inventory and ability icons in `Assets/Vision/Resources/Icons` are the original game's own item art, drawn by
its canvas code, plus four ability icons in the same style that the original leaves blank.

- `orig_items.js`: the item drawing functions from the original's `client/src/assets/procedural/textures.ts`
  (types stripped, drawn at 4x scale).
- `abilities.js`: the lunge, Soundcloud Burst, Penjamin and 50 Nic icons, and the machete turned as the original's HUD
  shows it.
- `render.html`: draws every icon and posts each one back to `serve.py`.

To redraw them:

```bash
python -I tools/icons/serve.py Assets/Vision/Resources/Icons
```

Then open http://127.0.0.1:8765/render.html in a browser. The page title reads `done 21/21` when every icon is
saved. Stop the server afterwards.
