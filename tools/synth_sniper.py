"""Synthesises the 0.50 cal's shot (Assets/Vision/Resources/Audio/Fx/synth_sniper_*.wav): original sound made here,
in the character of a Noita rifle crack (measured, not copied): a click-sharp attack, a bright tone near 1.5 kHz with a
partial near 2.1 kHz that drops in pitch and is gone in about 15 ms, a burst of air noise, then a low 60-100 Hz thump and
a short dull rumble of about a tenth of a second. Three variants. Run: python tools/synth_sniper.py"""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Vision', 'Resources', 'Audio', 'Fx')


def lowpass(x, cutoff):
    a = np.exp(-2.0 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1.0 - a) * v + a * acc
        y[i] = acc
    return y


def shot(seed):
    rng = np.random.default_rng(seed)
    n = int(SR * 0.42)
    t = np.arange(n) / SR
    jit = 1.0 + rng.uniform(-0.04, 0.04)
    # The crack: a falling bright tone and its partial, dying in ~15 ms.
    f0 = 1700.0 * jit * np.exp(-t / 0.02) + 1350.0 * jit * (1.0 - np.exp(-t / 0.02))
    phase = 2.0 * np.pi * np.cumsum(f0) / SR
    crack = (np.sin(phase) + 0.35 * np.sin(phase * 1.4 + 0.7)) * np.exp(-t / 0.0065)
    # Air: a hard click of white noise, brightest first.
    noise = rng.standard_normal(n)
    air = (noise - lowpass(noise, 900.0)) * np.exp(-t / 0.004) * 0.9
    # The thump: a low sine sliding from 100 Hz to 60 Hz, and a dull rumble of low-passed noise.
    fl = 60.0 + 40.0 * np.exp(-t / 0.03)
    thump = np.sin(2.0 * np.pi * np.cumsum(fl) / SR) * np.exp(-t / 0.05) * 0.8
    rumble = lowpass(noise * 1.0, 260.0) * np.exp(-t / 0.09) * 2.2
    x = 0.85 * crack + air + thump + rumble
    attack = np.clip(t / 0.0006, 0.0, 1.0)
    x *= attack
    fade = np.clip((t[-1] - t) / 0.05, 0.0, 1.0)
    x *= fade
    # A little grit, like an old game's mix.
    x = np.tanh(x * 1.6)
    x /= np.max(np.abs(x)) * 1.05
    return x


def write(path, x):
    pcm = (x * 32767.0).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


if __name__ == '__main__':
    for i in range(3):
        p = os.path.join(OUT, 'synth_sniper_%d.wav' % (i + 1))
        write(p, shot(1000 + i))
        print(p)
