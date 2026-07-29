from brainflow.board_shim import BoardShim, BrainFlowInputParams, BoardIds

class MuseController:
    def __init__(self):
        BoardShim.enable_dev_board_logger()
        self.params = BrainFlowInputParams()
        self.board = None
        self.is_connected = False
        self.recording = False
        self.data_buffer = []

    def connect(self):
        try:
            self.board = BoardShim(BoardIds.MUSE_2_BOARD, self.params)
            self.board.prepare_session()
            self.board.start_stream()
            self.is_connected = True
            print("Muse connected.")
        except Exception as e:
            print(f"Connection failed: {e}")

    def disconnect(self):
        if self.board:
            self.board.stop_stream()
            self.board.release_session()
            self.is_connected = False
            print("Muse disconnected.")

    def start_recording(self):
        if self.is_connected:
            self.recording = True
            self.data_buffer = []  # clear old data
            print("Recording started.")
        else:
            print("Not connected.")

    def stop_recording(self):
        self.recording = False
        # Return recorded data as numpy array (channels × samples)
        if self.data_buffer:
            import numpy as np
            return np.hstack(self.data_buffer)
        else:
            return None

    def poll_data(self):
        """Call this frequently (e.g., every 100 ms) to fetch new data."""
        if self.recording and self.board:
            # Get up to 256 new samples (about 1 second at 256 Hz)
            data = self.board.get_current_board_data(256)
            if data.shape[1] > 0:
                self.data_buffer.append(data)
