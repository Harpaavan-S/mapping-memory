import os
import re
import mne
import numpy as np
import matplotlib.pyplot as plt
from scipy.stats import pearsonr
from mne.preprocessing import ICA

# ================== USER INPUT ==================
folder1 = "/Users/harpaavansahota/Downloads/Memory App Scripts/Session1"
folder2 = "/Users/harpaavansahota/Downloads/Memory App Scripts/Session2"
# ================================================

def get_fif_files(folder):
    return [os.path.join(folder, f) for f in os.listdir(folder) if f.endswith('.fif')]

def extract_game_name(filepath):
    """Extract game name from filename, e.g., 'Stroop_Test' from 'Stroop_Test_raw_20260331.fif'"""
    base = os.path.basename(filepath)
    # Remove '_raw_...' part
    match = re.match(r'(.+?)_raw_', base)
    if match:
        return match.group(1)
    # fallback: remove extension and hope
    return base.split('_raw_')[0]

def compute_frontal_bandpower(fif_file):
    raw = mne.io.read_raw_fif(fif_file, preload=True)
    raw.filter(1, 40, fir_design='firwin')
    raw.set_eeg_reference('average')
    ica = ICA(n_components=4, random_state=97)
    ica.fit(raw)
    ica.exclude = [0]
    raw_clean = ica.apply(raw)
    psd = raw_clean.compute_psd(fmin=1, fmax=40, n_fft=256, average='mean')
    psds = psd.get_data()
    freqs = psd.freqs
    bands = {'Theta': (4,8), 'Alpha': (8,12), 'Beta': (12,30)}
    powers = {}
    for band, (fmin, fmax) in bands.items():
        mask = (freqs >= fmin) & (freqs <= fmax)
        power = (psds[1, mask].mean() + psds[2, mask].mean()) / 2.0
        powers[band] = power
    return powers

def main():
    if not os.path.isdir(folder1) or not os.path.isdir(folder2):
        print("One of the folders does not exist.")
        return

    files1 = get_fif_files(folder1)
    files2 = get_fif_files(folder2)

    # Build dictionaries keyed by game name
    dict1 = {extract_game_name(f): f for f in files1}
    dict2 = {extract_game_name(f): f for f in files2}

    common_games = set(dict1.keys()) & set(dict2.keys())
    if not common_games:
        print("No matching game names found between folders.")
        return

    print(f"Found {len(common_games)} common games: {common_games}")

    # Store values per band
    data1 = {band: [] for band in ['Theta', 'Alpha', 'Beta']}
    data2 = {band: [] for band in ['Theta', 'Alpha', 'Beta']}

    for game in sorted(common_games):
        print(f"Processing {game}...")
        p1 = compute_frontal_bandpower(dict1[game])
        p2 = compute_frontal_bandpower(dict2[game])
        for band in ['Theta', 'Alpha', 'Beta']:
            data1[band].append(p1[band])
            data2[band].append(p2[band])

    # Scatter plots
    fig, axes = plt.subplots(1, 3, figsize=(15, 5))
    for ax, band in zip(axes, ['Theta', 'Alpha', 'Beta']):
        x = data1[band]
        y = data2[band]
        if len(x) < 2:
            ax.text(0.5, 0.5, f'Only {len(x)} point(s)', ha='center', va='center')
            continue
        r, p = pearsonr(x, y)
        ax.scatter(x, y, color='blue', alpha=0.7, s=80)
        # Diagonal
        min_val = min(min(x), min(y))
        max_val = max(max(x), max(y))
        ax.plot([min_val, max_val], [min_val, max_val], 'r--', label='Perfect correlation')
        ax.set_xlabel(f'Folder1 {band} Power')
        ax.set_ylabel(f'Folder2 {band} Power')
        ax.set_title(f'{band} (r = {r:.3f}, p = {p:.3f})')
        ax.legend()
        ax.grid(alpha=0.3)
    plt.tight_layout()
    plt.savefig('folder_test_retest_fixed.png', dpi=150)
    plt.show()
    print("Scatter plot saved as 'folder_test_retest_fixed.png'.")

if __name__ == "__main__":
    main()
