"""Generates the Be My Arms production-test SFX and music beds.

Run with Blender's bundled Python (no extra toolchain):
  tools/pipeline/build-audio.ps1

These are deterministic, stylized procedural sounds that complete the content pipeline. Final audio
sourcing/licensing is a content decision (see docs/M7_CONTENT.md) and is not locked here.
"""

import math
import os
import random
import struct
import wave

SR = 44100
random.seed(20260919)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SCRIPT_DIR, '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'Art', 'Audio')


def clamp(v, lo=-1.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def sine(f, t):
    return math.sin(2.0 * math.pi * f * t)


def saw(f, t):
    return 2.0 * ((f * t) % 1.0) - 1.0


def noise():
    return random.uniform(-1.0, 1.0)


def decay(t, tau):
    return math.exp(-t / max(1e-5, tau))


def render(duration, gen):
    n = int(duration * SR)
    return [gen(i, n) for i in range(n)]


def lowpass(samples, cutoff):
    a = math.exp(-2.0 * math.pi * cutoff / SR)
    y = 0.0
    out = []
    for x in samples:
        y = (1.0 - a) * x + a * y
        out.append(y)
    return out


def highpass(samples, cutoff):
    a = math.exp(-2.0 * math.pi * cutoff / SR)
    y = 0.0
    px = 0.0
    out = []
    for x in samples:
        y = a * (y + x - px)
        px = x
        out.append(y)
    return out


def gain(samples, g):
    return [s * g for s in samples]


def normalize(samples, peak=0.9):
    m = max(1e-6, max(abs(s) for s in samples))
    return [s * peak / m for s in samples]


def fade(samples, fin=0.003, fout=0.02):
    n = len(samples)
    a = max(1, int(fin * SR))
    b = max(1, int(fout * SR))
    for i in range(min(a, n)):
        samples[i] *= i / a
    for i in range(min(b, n)):
        samples[n - 1 - i] *= i / b
    return samples


def write_wav(name, samples, peak=0.9):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + '.wav')
    samples = normalize(samples, peak)
    with wave.open(path, 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(clamp(s) * 32767)) for s in samples))
    print('[audio] wrote %s (%.2fs)' % (name, len(samples) / SR))


def gunshot(dur, body_freq, bright, thump):
    def gen(i, n):
        t = i / SR
        body = sine(body_freq * (1.0 - 0.35 * t / dur), t) * decay(t, dur * 0.22)
        crack = noise() * decay(t, dur * 0.08)
        return body * thump + crack * bright
    s = render(dur, gen)
    return fade(highpass(lowpass(s, 5000.0), 90.0), 0.0008, dur * 0.5)


def impact(dur, freq, bright):
    def gen(i, n):
        t = i / SR
        return sine(freq, t) * decay(t, dur * 0.25) * 0.9 + noise() * decay(t, 0.02) * bright
    return fade(lowpass(render(dur, gen), 3500.0), 0.001, dur * 0.5)


def click(dur, freq, amp=0.6):
    return fade(highpass(render(dur, lambda i, n: noise() * decay(i / SR, 0.006)), 1500.0), 0.0005, dur * 0.6)


def tone_seq(dur, notes):
    """notes: list of (start_fraction, end_fraction, frequency, amp)."""
    def gen(i, n):
        t = i / SR
        f = t / dur
        v = 0.0
        env = math.sin(math.pi * min(1.0, max(0.0, f))) ** 0.5
        for (a, b, freq, amp) in notes:
            if a <= f < b:
                v += sine(freq, t) * amp
        return v * env
    return fade(lowpass(render(dur, gen), 6000.0), 0.005, 0.05)


def boom(dur, base):
    def gen(i, n):
        t = i / SR
        rumble = sine(base * (1.0 - 0.4 * t / dur), t) * decay(t, dur * 0.35)
        crack = noise() * decay(t, dur * 0.05)
        hiss = noise() * decay(t, dur * 0.3)
        return rumble * 1.0 + crack * 1.2 + hiss * 0.25
    return fade(lowpass(render(dur, gen), 1800.0), 0.001, dur * 0.5)


def pad(dur, freqs, cutoff=1400.0, wobble=0.3):
    def gen(i, n):
        t = i / SR
        v = 0.0
        for k, f in enumerate(freqs):
            v += sine(f, t) * (0.5 / (k + 1))
        lfo = (1.0 - wobble) + wobble * math.sin(2.0 * math.pi * (1.0 / dur) * t)
        return v * lfo
    return lowpass(render(dur, gen), cutoff)


def main():
    # Weapons.
    write_wav('sfx_rifle_shot', gunshot(0.20, 190.0, 0.9, 0.8), 0.85)
    write_wav('sfx_pistol_shot', gunshot(0.15, 260.0, 1.0, 0.6), 0.8)
    write_wav('sfx_shotgun_shot', gunshot(0.34, 120.0, 0.8, 1.0), 0.9)
    write_wav('sfx_knife_swing', fade(highpass(render(0.22, lambda i, n: noise() * decay(i / SR, 0.05) * (1.0 - i / n)), 2000.0), 0.004, 0.06), 0.5)

    # Handling / movement.
    write_wav('sfx_reload', [v for v in gain(fade(render(0.55, lambda i, n: noise() * decay((i / SR) % 0.18, 0.01)), 0.008, 0.05), 0.6)], 0.7)
    write_wav('sfx_footstep', fade(lowpass(render(0.12, lambda i, n: noise() * decay(i / SR, 0.03)), 1200.0), 0.002, 0.05), 0.5)

    # Combat results.
    write_wav('sfx_hit_body', impact(0.12, 160.0, 0.5), 0.8)
    write_wav('sfx_headshot', impact(0.20, 520.0, 0.8), 0.9)
    write_wav('sfx_elimination', tone_seq(0.55, [(0.0, 0.4, 660.0, 0.5), (0.35, 1.0, 330.0, 0.6)]), 0.8)

    # Round / match.
    write_wav('sfx_round_start', tone_seq(0.85, [(0.0, 0.5, 440.0, 0.5), (0.45, 1.0, 660.0, 0.6)]), 0.8)
    write_wav('sfx_round_end', tone_seq(0.95, [(0.0, 0.5, 660.0, 0.5), (0.45, 1.0, 440.0, 0.6)]), 0.8)
    write_wav('sfx_match_end', tone_seq(1.6, [(0.0, 0.34, 440.0, 0.4), (0.3, 0.67, 554.0, 0.45), (0.6, 1.0, 660.0, 0.5)]), 0.85)

    # UI.
    write_wav('sfx_ui_click', click(0.06, 1200.0, 0.7), 0.7)
    write_wav('sfx_ui_hover', click(0.05, 800.0, 0.4), 0.5)

    # Utility.
    write_wav('sfx_grenade_explosion', boom(0.75, 90.0), 0.95)
    write_wav('sfx_flash_bang', lowpass(boom(0.55, 220.0), 6000.0), 0.95)
    write_wav('sfx_smoke_deploy', fade(highpass(render(0.8, lambda i, n: noise() * min(1.0, i / (0.15 * SR)) * decay(i / SR, 0.6)), 900.0), 0.02, 0.2), 0.6)
    write_wav('sfx_zone_warning', tone_seq(1.0, [(0.0, 0.25, 880.0, 0.5), (0.3, 0.55, 880.0, 0.5), (0.6, 0.85, 880.0, 0.5)]), 0.7)

    # Loops (frequencies snapped so the loop is seamless).
    def loop_freq(f, dur):
        return round(f * dur) / dur

    write_wav('amb_arena_loop', pad(4.0, [loop_freq(55.0, 4.0), loop_freq(82.5, 4.0), loop_freq(110.0, 4.0)], 900.0, 0.35), 0.5)
    write_wav('music_menu_loop', pad(8.0, [loop_freq(130.81, 8.0), loop_freq(164.81, 8.0), loop_freq(196.0, 8.0)], 1600.0, 0.25), 0.45)
    write_wav('music_match_loop', pad(8.0, [loop_freq(110.0, 8.0), loop_freq(146.83, 8.0), loop_freq(220.0, 8.0)], 1800.0, 0.3), 0.45)

    print('[audio] done')


main()
