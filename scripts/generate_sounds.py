import os
import wave
import struct
import math
import random

def generate_all_sounds():
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    sounds_dir = os.path.join(base_dir, "PixelCompanion", "Assets", "Sounds")
    os.makedirs(sounds_dir, exist_ok=True)

    sample_rate = 44100

    # 1. clack.wav: Vintage typewriter keypress (filtered noise burst ~80ms, quiet gentle amplitude)
    clack_path = os.path.join(sounds_dir, "clack.wav")
    duration_clack = 0.08  # 80 ms
    total_samples_clack = int(sample_rate * duration_clack)
    clack_samples = []

    # Initial sharp mechanical transient + resonant body thud
    f_res = 380.0
    for i in range(total_samples_clack):
        t = i / sample_rate
        env = math.exp(-t * 65.0)  # Fast decay envelope
        noise = (random.random() * 2.0 - 1.0) * 0.4
        resonance = math.sin(2.0 * math.pi * f_res * t) * 0.6
        # Click click transient in the first 5ms
        click = math.sin(2.0 * math.pi * 1800.0 * t) * 0.4 * math.exp(-t * 400.0)
        sample_val = (noise + resonance + click) * env * 0.22  # Gentle amplitude (~0.2)
        sample_int = int(max(-1.0, min(1.0, sample_val)) * 32767.0)
        clack_samples.append(sample_int)

    write_wav(clack_path, clack_samples, sample_rate)

    # 2. pop.wav: Task completed soft sine blip (~150ms)
    pop_path = os.path.join(sounds_dir, "pop.wav")
    duration_pop = 0.15  # 150 ms
    total_samples_pop = int(sample_rate * duration_pop)
    pop_samples = []

    # Upward pleasant sine chirp from 520 Hz to 880 Hz
    for i in range(total_samples_pop):
        t = i / sample_rate
        norm_t = t / duration_pop
        freq = 520.0 + 360.0 * (1.0 - math.exp(-t * 25.0))
        env = math.sin(math.pi * norm_t ** 0.5) * math.exp(-t * 18.0)
        val = math.sin(2.0 * math.pi * freq * t) * env * 0.25  # Gentle amplitude
        sample_int = int(max(-1.0, min(1.0, val)) * 32767.0)
        pop_samples.append(sample_int)

    write_wav(pop_path, pop_samples, sample_rate)

    # 3. slide.wav: Paper extend/retract soft noise sweep (~200ms)
    slide_path = os.path.join(sounds_dir, "slide.wav")
    duration_slide = 0.20  # 200 ms
    total_samples_slide = int(sample_rate * duration_slide)
    slide_samples = []

    # Soft rustling friction / sweep
    for i in range(total_samples_slide):
        t = i / sample_rate
        norm_t = t / duration_slide
        # Hann window envelope
        env = 0.5 * (1.0 - math.cos(2.0 * math.pi * norm_t))
        noise = (random.random() * 2.0 - 1.0) * 0.5
        tone = math.sin(2.0 * math.pi * (240.0 + 120.0 * norm_t) * t) * 0.3
        val = (noise + tone) * env * 0.18  # Very gentle ambient level
        sample_int = int(max(-1.0, min(1.0, val)) * 32767.0)
        slide_samples.append(sample_int)

    write_wav(slide_path, slide_samples, sample_rate)
    print(f"Generated 3 sound files in {sounds_dir}")

def write_wav(path, samples, sample_rate):
    with wave.open(path, "w") as wav_file:
        wav_file.setnchannels(1)  # Mono
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(sample_rate)
        data = struct.pack(f"<{len(samples)}h", *samples)
        wav_file.writeframes(data)

if __name__ == "__main__":
    generate_all_sounds()
