import os
import numpy as np
import mne
import matplotlib.pyplot as plt
from scipy.stats import pearsonr

# List your FIF files here (or auto-detect)
fif_files = [f for f in os.listdir('.') if f.endswith('.fif')]

original_results = {}   # store your original bandpower values
# You'll need to manually input your original results from the PDF
# Or re-run the processing exactly as before. For simplicity, we'll re-process
# using the same method but compare with a slightly different filter order.

def process_game(filename):
    raw = mne.io.read_raw_fif(filename, preload=True)
    raw.filter(1, 40, fir_design='firwin')
    raw.set_eeg_reference('average')
    # Skip ICA for validation (to avoid variability)
    spectrum = raw.compute_psd(fmin=1, fmax=40, n_fft=256, average='mean')
    psds = spectrum.get_data()
    freqs = spectrum.freqs
    theta_mask = (freqs >= 4) & (freqs <= 8)
    theta_frontal = (psds[1, theta_mask].mean() + psds[2, theta_mask].mean()) / 2.0
    return theta_frontal

# Process with two different filter designs
results_method1 = []
results_method2 = []
for f in fif_files:
    raw = mne.io.read_raw_fif(f, preload=True)
    # Method 1: original filter
    raw1 = raw.copy().filter(1, 40, fir_design='firwin')
    raw1.set_eeg_reference('average')
    spec1 = raw1.compute_psd(fmin=1, fmax=40, n_fft=256, average='mean')
    psds1 = spec1.get_data()
    freqs = spec1.freqs
    theta1 = (psds1[1, (freqs>=4)&(freqs<=8)].mean() + psds1[2, (freqs>=4)&(freqs<=8)].mean())/2.0
    results_method1.append(theta1)

    # Method 2: different filter (e.g., longer filter)
    raw2 = raw.copy().filter(1, 40, fir_design='firwin', filter_length='auto')
    raw2.set_eeg_reference('average')
    spec2 = raw2.compute_psd(fmin=1, fmax=40, n_fft=256, average='mean')
    psds2 = spec2.get_data()
    theta2 = (psds2[1, (freqs>=4)&(freqs<=8)].mean() + psds2[2, (freqs>=4)&(freqs<=8)].mean())/2.0
    results_method2.append(theta2)

# Correlation
corr, p = pearsonr(results_method1, results_method2)
print(f"Correlation between two processing methods: r = {corr:.3f}, p = {p:.4f}")

# Scatter plot
plt.figure(figsize=(5,5))
plt.scatter(results_method1, results_method2)
plt.xlabel('Method 1 (Theta power)')
plt.ylabel('Method 2 (Theta power)')
plt.title(f'Test‑retest reliability of processing\nr = {corr:.3f}')
plt.plot([min(results_method1), max(results_method1)], [min(results_method1), max(results_method1)], 'r--')
plt.tight_layout()
plt.savefig('validation_scatter.png')
print("Scatter plot saved as validation_scatter.png")
