#!/usr/bin/env python3
"""
Muse Memory Test Suite – Cognitive Profiling for Science Fair
Author: [Your Name]
Date: 2026-04-05
"""

import sys
import os
import time
import subprocess
import datetime
import numpy as np
import socket
import threading
import csv
import traceback
from PyQt5.QtWidgets import (QApplication, QMainWindow, QPushButton, QLabel,
                             QVBoxLayout, QHBoxLayout, QWidget, QGroupBox,
                             QMessageBox, QProgressBar)
from PyQt5.QtCore import QTimer, Qt
import mne
from mne.preprocessing import ICA
from mne_icalabel import label_components
from brainflow.board_shim import BoardShim, BrainFlowInputParams, BoardIds
from reportlab.lib import colors
from reportlab.lib.pagesizes import letter
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_LEFT

# ----------------------------------------------------------------------
# 1. Muse Controller
# ----------------------------------------------------------------------
class MuseController:
    """Handles Muse 2 connection, streaming, and recording."""
    def __init__(self):
        BoardShim.enable_dev_board_logger()
        self.params = BrainFlowInputParams()
        self.board = None
        self.is_connected = False
        self.recording = False
        self.data_buffer = []
        self.eeg_channels = None

    def connect(self):
        try:
            self.board = BoardShim(BoardIds.MUSE_2_BOARD, self.params)
            self.board.prepare_session()
            self.board.start_stream()
            self.is_connected = True
            self.eeg_channels = BoardShim.get_eeg_channels(BoardIds.MUSE_2_BOARD)
            print("Muse connected.")
            return True
        except Exception as e:
            print(f"Connection failed: {e}")
            return False

    def disconnect(self):
        if self.board:
            self.board.stop_stream()
            self.board.release_session()
            self.is_connected = False
            print("Muse disconnected.")

    def start_recording(self):
        if self.is_connected:
            self.recording = True
            self.data_buffer = []
            print("Recording started.")

    def stop_recording(self):
        self.recording = False
        if not self.data_buffer:
            return None
        data = np.hstack(self.data_buffer)
        print(f"Recording stopped. {data.shape[1]} samples collected.")
        return data

    def poll_data(self):
        if self.recording and self.board:
            data = self.board.get_current_board_data(256)
            if data.shape[1] > 0:
                self.data_buffer.append(data)

# ----------------------------------------------------------------------
# 2. Main Window
# ----------------------------------------------------------------------
class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Muse Memory Test Suite – Cognitive Profile")
        self.setGeometry(100, 100, 800, 600)

        self.muse = MuseController()
        self.recorded_files = []          # list of FIF filenames
        self.current_game = None
        self.markers_for_game = []        # markers for the current game

        # UDP listener setup
        self.udp_port = 12345
        self.udp_running = True
        self.udp_thread = threading.Thread(target=self.udp_listener, daemon=True)
        self.udp_thread.start()

        # Timer to poll Muse data
        self.poll_timer = QTimer()
        self.poll_timer.timeout.connect(self.poll_muse)
        self.poll_timer.start(100)

        self.init_ui()

    # ------------------------------------------------------------------
    # UI Construction
    # ------------------------------------------------------------------
    def init_ui(self):
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        layout = QVBoxLayout(central_widget)

        # Connection section
        conn_group = QGroupBox("Muse 2 Connection")
        conn_layout = QHBoxLayout()
        self.btn_connect = QPushButton("Connect Muse")
        self.btn_connect.clicked.connect(self.connect_muse)
        self.btn_disconnect = QPushButton("Disconnect")
        self.btn_disconnect.clicked.connect(self.disconnect_muse)
        self.btn_disconnect.setEnabled(False)
        self.lbl_status = QLabel("Status: Not connected")
        conn_layout.addWidget(self.btn_connect)
        conn_layout.addWidget(self.btn_disconnect)
        conn_layout.addWidget(self.lbl_status)
        conn_group.setLayout(conn_layout)
        layout.addWidget(conn_group)

        # Game control section
        game_group = QGroupBox("Game Control")
        game_layout = QVBoxLayout()
        self.btn_launch = QPushButton("Launch Memory Tests")
        self.btn_launch.clicked.connect(self.launch_games)
        game_layout.addWidget(self.btn_launch)
        game_group.setLayout(game_layout)
        layout.addWidget(game_group)

        # Status area
        self.status_text = QLabel("Ready. Connect Muse and launch the game.")
        self.status_text.setWordWrap(True)
        layout.addWidget(self.status_text)

        # Analysis section
        analysis_group = QGroupBox("Analysis & Report")
        analysis_layout = QVBoxLayout()
        self.btn_analyze = QPushButton("Generate Cognitive Profile")
        self.btn_analyze.clicked.connect(self.analyze_all)
        self.progress_bar = QProgressBar()
        self.progress_bar.setVisible(False)
        self.lbl_analysis_status = QLabel("")
        analysis_layout.addWidget(self.btn_analyze)
        analysis_layout.addWidget(self.progress_bar)
        analysis_layout.addWidget(self.lbl_analysis_status)
        analysis_group.setLayout(analysis_layout)
        layout.addWidget(analysis_group)

        layout.addStretch()

    # ------------------------------------------------------------------
    # Muse Connection
    # ------------------------------------------------------------------
    def connect_muse(self):
        if self.muse.connect():
            self.lbl_status.setText("Status: Connected")
            self.btn_connect.setEnabled(False)
            self.btn_disconnect.setEnabled(True)
        else:
            QMessageBox.critical(self, "Connection Error",
                                 "Could not connect to Muse.\nMake sure Bluetooth is on and Muse is paired.")

    def disconnect_muse(self):
        self.muse.disconnect()
        self.lbl_status.setText("Status: Not connected")
        self.btn_connect.setEnabled(True)
        self.btn_disconnect.setEnabled(False)

    def poll_muse(self):
        self.muse.poll_data()

    # ------------------------------------------------------------------
    # Launch Game
    # ------------------------------------------------------------------
    def launch_games(self):
        """Launches the built Unity app containing all four games."""
        # UPDATE THIS PATH TO YOUR BUILT APP
        game_app_path = "/Users/harpaavansahota/Downloads/MemoryGames2026.app"   # <-- CHANGE THIS

        if not os.path.exists(game_app_path):
            QMessageBox.warning(self, "Game Not Found",
                                f"Game app not found at:\n{game_app_path}\n\nPlease update the path in main.py.")
            return

        try:
            subprocess.Popen(["open", game_app_path])
            self.status_text.setText("Game launched. The app will start recording when it receives the first 'Start' marker.")
        except Exception as e:
            QMessageBox.critical(self, "Launch Error", f"Could not launch game:\n{e}")

    # ------------------------------------------------------------------
    # UDP Marker Listener
    # ------------------------------------------------------------------
    def udp_listener(self):
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.bind(('127.0.0.1', self.udp_port))
        while self.udp_running:
            try:
                data, addr = sock.recvfrom(1024)
                marker = data.decode('utf-8').strip()
                print(f"Received marker: {marker}")
                self.process_marker(marker)
            except Exception as e:
                print(f"UDP error: {e}")

    def process_marker(self, marker):
        timestamp = time.time()

        # Game start markers
        if marker == "Stroop_Start":
            self.start_game("Stroop Test")
        elif marker == "Stroop_End":
            self.stop_game("Stroop Test")
        elif marker == "NBack_Start":
            self.start_game("N-Back Test")
        elif marker == "NBack_End":
            self.stop_game("N-Back Test")
        elif marker == "Pattern_Start":
            self.start_game("Pattern Recognition")
        elif marker == "Pattern_End":
            self.stop_game("Pattern Recognition")
        elif marker == "Corsi_Start":
            self.start_game("Corsi Block Test")
        elif marker == "Corsi_End":
            self.stop_game("Corsi Block Test")
        else:
            # All other markers: store with current game name
            if self.current_game:
                self.markers_for_game.append((timestamp, marker))
            else:
                print(f"Marker received outside game: {marker}")

    # ------------------------------------------------------------------
    # Recording Control
    # ------------------------------------------------------------------
    def start_game(self, game):
        if self.current_game:
            print(f"Warning: {self.current_game} already recording. Stopping it first.")
            self.stop_game(self.current_game)

        self.current_game = game
        self.markers_for_game = []
        self.muse.start_recording()
        self.status_text.setText(f"Recording: {game}")

    def stop_game(self, game):
        if self.current_game != game:
            print(f"Unexpected stop for {game}, current is {self.current_game}")
            return

        data = self.muse.stop_recording()
        if data is None:
            print("No data recorded.")
            self.current_game = None
            return

        # Save EEG data
        self.save_game_data(game, data)
        # Save markers for this game
        self.save_markers(game)

        self.current_game = None
        self.status_text.setText(f"Saved: {game}")

    def save_game_data(self, game, data):
        eeg_indices = self.muse.eeg_channels
        if eeg_indices is None:
            eeg_indices = [1, 2, 3, 4]   # fallback
        eeg_data = data[eeg_indices, :]

        sfreq = 256.0
        ch_names = ['TP9', 'AF7', 'AF8', 'TP10']
        ch_types = ['eeg'] * 4

        info = mne.create_info(ch_names=ch_names, sfreq=sfreq, ch_types=ch_types)
        raw = mne.io.RawArray(eeg_data, info)

        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        safe_game_name = game.replace(' ', '_')
        filename = f"{safe_game_name}_raw_{timestamp}.fif"
        raw.save(filename, overwrite=True)
        print(f"Saved EEG to {filename}")

        self.recorded_files.append(filename)

    def save_markers(self, game):
        if not self.markers_for_game:
            return
        filename = f"{game.replace(' ', '_')}_markers.csv"
        with open(filename, 'w', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(["timestamp", "marker"])
            writer.writerows(self.markers_for_game)
        print(f"Saved markers to {filename}")

    # ------------------------------------------------------------------
    # Score Extraction from Markers
    # ------------------------------------------------------------------
    def load_game_scores(self):
        """Extract final accuracy from marker CSV files."""
        scores = {}
        for fif_file in self.recorded_files:
            base = os.path.basename(fif_file)
            game_name = base.split('_raw_')[0].replace('_', ' ')
            marker_file = f"{game_name.replace(' ', '_')}_markers.csv"
            if not os.path.exists(marker_file):
                print(f"Marker file not found for {game_name}: {marker_file}")
                scores[game_name] = None
                continue

            accuracy = None
            try:
                with open(marker_file, 'r') as f:
                    reader = csv.reader(f)
                    next(reader, None)
                    for row in reader:
                        if len(row) < 2:
                            continue
                        marker = row[1].strip()
                        if 'Score' in marker or 'score' in marker:
                            parts = marker.split(':')
                            if len(parts) == 2:
                                try:
                                    accuracy = float(parts[1])
                                    break
                                except:
                                    pass
                # If no explicit score, compute from correct/incorrect markers
                if accuracy is None:
                    correct = 0
                    total = 0
                    with open(marker_file, 'r') as f:
                        reader = csv.reader(f)
                        next(reader, None)
                        for row in reader:
                            if len(row) < 2:
                                continue
                            marker = row[1]
                            if 'Correct' in marker:
                                correct += 1
                                total += 1
                            elif 'Incorrect' in marker or 'Timeout' in marker:
                                total += 1
                    if total > 0:
                        accuracy = (correct / total) * 100.0
            except Exception as e:
                print(f"Error reading {marker_file}: {e}")
            scores[game_name] = accuracy
        return scores

    # ------------------------------------------------------------------
    # EEG Processing
    # ------------------------------------------------------------------
    def analyze_all(self):
        if not self.recorded_files:
            QMessageBox.information(self, "No Data", "No game recordings found. Please play some games first.")
            return

        self.btn_analyze.setEnabled(False)
        self.progress_bar.setVisible(True)
        self.progress_bar.setValue(0)
        self.lbl_analysis_status.setText("Processing EEG data...")
        QApplication.processEvents()

        results = {}
        total = len(self.recorded_files)
        for idx, filename in enumerate(self.recorded_files):
            base = os.path.basename(filename)
            game_name = base.split('_raw_')[0].replace('_', ' ')
            self.lbl_analysis_status.setText(f"Processing {game_name}...")
            QApplication.processEvents()

            try:
                bandpower = self.process_game(filename)
                if bandpower is not None:
                    results[game_name] = bandpower
                else:
                    results[game_name] = None
            except Exception as e:
                print(f"Error processing {filename}: {e}")
                traceback.print_exc()
                results[game_name] = None

            self.progress_bar.setValue(int((idx+1)/total * 100))
            QApplication.processEvents()

        # Load performance scores
        scores = self.load_game_scores()

        if any(v is not None for v in results.values()):
            self.generate_pdf_report(results, scores)
            self.lbl_analysis_status.setText("Report generated.")
        else:
            self.lbl_analysis_status.setText("No valid data to process (check console).")
            QMessageBox.warning(self, "Analysis Failed",
                                "No data could be processed. Check the terminal for error messages.")

        self.progress_bar.setVisible(False)
        self.btn_analyze.setEnabled(True)

    def process_game(self, filename):
        """Load, filter, ICA, and compute bandpower for one FIF file."""
        raw = mne.io.read_raw_fif(filename, preload=True)
        print(f"Loaded {filename}: {raw.n_times} samples")

        # 1. Bandpass filter 1–40 Hz
        raw.filter(1, 40, fir_design='firwin')

        # 2. Average reference
        raw.set_eeg_reference('average', projection=False)

        # 3. ICA to remove eye blinks
        ica = ICA(n_components=4, random_state=97, method='fastica')
        ica.fit(raw)
        try:
            from mne_icalabel import label_components
            ic_labels = label_components(raw, ica, method='iclabel')
            exclude_idx = [idx for idx, label in enumerate(ic_labels['labels'])
                           if 'eye' in label.lower()]
            ica.exclude = exclude_idx
        except Exception:
            ica.exclude = [0]  # fallback
        raw_clean = ica.apply(raw)

        # 4. Compute PSD and bandpower
        spectrum = raw_clean.compute_psd(fmin=1, fmax=40, n_fft=256, average='mean')
        psds = spectrum.get_data()  # shape (4, n_freqs)
        freqs = spectrum.freqs

        bands = {
            'Theta': (4, 8),
            'Alpha': (8, 12),
            'Beta': (12, 30)
        }
        bandpower = {}
        for band_name, (fmin, fmax) in bands.items():
            freq_mask = (freqs >= fmin) & (freqs <= fmax)
            bp = psds[:, freq_mask].mean(axis=1).tolist()  # list of 4 values
            bandpower[band_name] = bp
        return bandpower

    # ------------------------------------------------------------------
    # PDF Report Generation (with improved normalization)
    # ------------------------------------------------------------------
    def normalize_percentile(self, values_dict):
        """Normalize values to 0-100 using 10th-90th percentile clipping to avoid extremes."""
        vals = [v for v in values_dict.values() if v is not None]
        if not vals:
            return {k: 50 for k in values_dict.keys()}
        p10 = np.percentile(vals, 10)
        p90 = np.percentile(vals, 90)
        if p90 == p10:
            return {k: 50 for k in values_dict.keys()}
        normed = {}
        for k, v in values_dict.items():
            if v is None:
                normed[k] = 50
            else:
                clipped = max(p10, min(v, p90))
                normed[k] = (clipped - p10) / (p90 - p10) * 100
        return normed

    def generate_pdf_report(self, results, scores):
        """Create a polished PDF with table, normalized scores, and interpretation."""
        # Order of games
        game_order = ["Stroop Test", "N-Back Test", "Pattern Recognition", "Corsi Block Test"]
        games_present = [g for g in game_order if g in results and results[g] is not None]
        if not games_present:
            return

        # Extract frontal metrics (AF7=index1, AF8=index2)
        raw_theta = {}
        raw_alpha = {}
        raw_beta = {}
        for game in games_present:
            bp = results[game]
            theta_frontal = (bp['Theta'][1] + bp['Theta'][2]) / 2.0
            alpha_frontal = (bp['Alpha'][1] + bp['Alpha'][2]) / 2.0
            beta_frontal = (bp['Beta'][1] + bp['Beta'][2]) / 2.0
            raw_theta[game] = theta_frontal
            raw_alpha[game] = alpha_frontal
            raw_beta[game] = beta_frontal

        # Normalize using percentile clipping
        norm_theta = self.normalize_percentile(raw_theta)
        norm_alpha = self.normalize_percentile(raw_alpha)
        norm_beta = self.normalize_percentile(raw_beta)

        # Accuracy values (may be None)
        acc = {g: scores.get(g) for g in games_present}
        # Normalize only those with valid accuracy
        acc_valid = {g: a for g, a in acc.items() if a is not None}
        norm_acc = self.normalize_percentile(acc_valid) if acc_valid else {g: 50 for g in games_present}

        # Build PDF
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        pdf_filename = f"Cognitive_Profile_{timestamp}.pdf"
        doc = SimpleDocTemplate(pdf_filename, pagesize=letter,
                                topMargin=0.7*inch, bottomMargin=0.7*inch,
                                leftMargin=0.7*inch, rightMargin=0.7*inch)
        styles = getSampleStyleSheet()
        title_style = styles['Title']
        heading_style = styles['Heading2']
        normal_style = styles['Normal']

        story = []

        # Title
        story.append(Paragraph("Cognitive Profile Report", title_style))
        story.append(Spacer(1, 0.2*inch))
        story.append(Paragraph(f"Date: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M')}", normal_style))
        story.append(Paragraph("Based on EEG (Muse 2) and four memory games", normal_style))
        story.append(Spacer(1, 0.3*inch))

        # Explanation
        explanation = """
        <b>What the numbers mean (normalized 0-100):</b><br/>
        • <b>Theta (Engagement):</b> Mental effort and working memory load. Higher = brain working harder.<br/>
        • <b>Alpha (Relaxation):</b> Calmness and resting state. Higher = more relaxed.<br/>
        • <b>Beta (Focus):</b> Active concentration and alertness. Higher = more focused.<br/>
        • <b>Accuracy:</b> Game performance (% correct).<br/>
        A higher score means more of that attribute relative to your other games.
        """
        story.append(Paragraph(explanation, normal_style))
        story.append(Spacer(1, 0.3*inch))

        # Data table
        table_data = [["Game", "Theta (Engagement)", "Alpha (Relaxation)", "Beta (Focus)", "Accuracy (%)"]]
        for game in games_present:
            acc_val = acc[game] if acc[game] is not None else 0.0
            table_data.append([
                game,
                f"{norm_theta[game]:.0f}",
                f"{norm_alpha[game]:.0f}",
                f"{norm_beta[game]:.0f}",
                f"{acc_val:.1f}" if acc[game] is not None else "N/A"
            ])

        t = Table(table_data, colWidths=[2.2*inch, 1.2*inch, 1.2*inch, 1.2*inch, 1.2*inch])
        t.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,0), colors.grey),
            ('TEXTCOLOR', (0,0), (-1,0), colors.whitesmoke),
            ('ALIGN', (0,0), (-1,-1), 'CENTER'),
            ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
            ('FONTSIZE', (0,0), (-1,0), 11),
            ('BOTTOMPADDING', (0,0), (-1,0), 8),
            ('BACKGROUND', (0,1), (-1,-1), colors.beige),
            ('GRID', (0,0), (-1,-1), 0.5, colors.grey),
            ('FONTSIZE', (0,1), (-1,-1), 10),
        ]))
        story.append(t)
        story.append(Spacer(1, 0.3*inch))

        # Key findings
        best_engagement = max(norm_theta, key=norm_theta.get)
        best_focus = max(norm_beta, key=norm_beta.get)
        best_acc = max(acc, key=acc.get) if any(acc.values()) else "N/A"
        best_acc_val = acc[best_acc] if best_acc != "N/A" else 0

        story.append(Paragraph("Key Findings", heading_style))
        story.append(Spacer(1, 0.1*inch))
        story.append(Paragraph(f"• Highest engagement (Theta): <b>{best_engagement}</b>", normal_style))
        story.append(Paragraph(f"• Highest focus (Beta): <b>{best_focus}</b>", normal_style))
        story.append(Paragraph(f"• Highest accuracy: <b>{best_acc}</b> ({best_acc_val:.1f}%)", normal_style))
        story.append(Spacer(1, 0.2*inch))

        # Game-by-game interpretation
        story.append(Paragraph("Game‑by‑Game Interpretation", heading_style))
        story.append(Spacer(1, 0.1*inch))
        for game in games_present:
            t_val = norm_theta[game]
            a_val = norm_alpha[game]
            b_val = norm_beta[game]
            acc_val = acc[game]
            if acc_val is None:
                insight = f"{game}: Theta={t_val:.0f}, Alpha={a_val:.0f}, Beta={b_val:.0f}. No performance data."
            else:
                if t_val > 70 and acc_val > 70:
                    insight = f"{game}: <b>Optimal state</b> – high engagement and high accuracy. Your brain worked efficiently."
                elif t_val > 70 and acc_val < 50:
                    insight = f"{game}: <b>High effort</b> – your brain worked hard but accuracy suffered. The task may be too difficult."
                elif t_val < 30 and acc_val > 70:
                    insight = f"{game}: <b>Effortless performance</b> – low mental effort with high accuracy. This may be a natural strength."
                elif t_val < 30 and acc_val < 50:
                    insight = f"{game}: <b>Low engagement</b> – both engagement and accuracy are low. Consider rest or task adjustments."
                else:
                    insight = f"{game}: Moderate engagement and accuracy – keep practicing to improve."
            story.append(Paragraph(f"• {insight}", normal_style))
            story.append(Spacer(1, 0.1*inch))

        # Real-world implications
        story.append(Spacer(1, 0.2*inch))
        story.append(Paragraph("Why This Matters", heading_style))
        story.append(Paragraph(
            "Traditional memory tests give only a score. This system reveals <i>how</i> your brain achieves that score – "
            "whether through high effort, effortless strength, or focused concentration. Such metacognitive feedback "
            "can guide personalized training, education, and early detection of cognitive decline.",
            normal_style))
        story.append(Spacer(1, 0.2*inch))
        story.append(Paragraph("Report generated by Muse Memory Test Suite – an open‑source tool for accessible cognitive assessment.", normal_style))

        doc.build(story)
        print(f"PDF report saved: {pdf_filename}")
        QMessageBox.information(self, "Report Ready", f"Cognitive profile saved as {pdf_filename}")

# ----------------------------------------------------------------------
# 3. Entry Point
# ----------------------------------------------------------------------
if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec_())
