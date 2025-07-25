import sys
import cv2
from PySide6.QtWidgets import QApplication, QLabel, QMainWindow, QVBoxLayout, QWidget
from PySide6.QtCore import QTimer, Qt
from PySide6.QtGui import QImage, QPixmap
from ultralytics import YOLO

import socket

def send_message_to_csharp(message):
    HOST = '10.10.20.99'  # C# 서버 IP  110
    PORT = 12345           # C# 서버 포트

    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.connect((HOST, PORT))
            s.sendall(message.encode())
            print(f"✅ 메시지 전송 완료: {message}")
    except Exception as e:
        print(f"❌ 전송 실패: {e}")

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("YOLO 실시간 감지 with 결과 출력")
        self.resize(800, 600)

        self.label = QLabel()
        self.label.setAlignment(Qt.AlignCenter)

        layout = QVBoxLayout()
        layout.addWidget(self.label)
        container = QWidget()
        container.setLayout(layout)
        self.setCentralWidget(container)

        self.model = YOLO('license_plate_detection/yolov8n_lpbest/weights/best.pt') #모델 뭐 쓸겨
        self.cap = cv2.VideoCapture(0)

        self.timer = QTimer()
        self.timer.timeout.connect(self.update_frame)
        self.timer.start(30)

        self.detect_timer = QTimer()
        self.detect_timer.timeout.connect(self.detect_objects)
        self.detect_timer.start(1000)  # 1초마다 감지

        self.last_result = None
        self.detected = False  # 감지 상태 플래그

    def update_frame(self):
        ret, frame = self.cap.read()
        if ret:
            if self.last_result:
                annotated_frame = self.last_result[0].plot()
                rgb_image = cv2.cvtColor(annotated_frame, cv2.COLOR_BGR2RGB)
            else:
                rgb_image = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

            h, w, ch = rgb_image.shape
            bytes_per_line = ch * w
            qimg = QImage(rgb_image.data, w, h, bytes_per_line, QImage.Format_RGB888)
            self.label.setPixmap(QPixmap.fromImage(qimg))

    def detect_objects(self):
        ret, frame = self.cap.read()
        if ret:
            self.last_result = self.model(frame, conf=0.25, verbose=False)
            boxes = self.last_result[0].boxes

            if len(boxes) > 0:
                print(f"✅ [{len(boxes)}개] 번호판 감지됨!")
                if not self.detected:
                    send_message_to_csharp("번호판 감지됨")
                    self.detected = True
            else:
                print("❌ 번호판 감지 못함")
                if self.detected:
                    send_message_to_csharp("번호판 없음")
                    self.detected = False

    def closeEvent(self, event):
        self.cap.release()
        event.accept()


if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec())
