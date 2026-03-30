import RPi.GPIO as GPIO
import time

# 제어할 GPIO 핀 번호 (BCM 모드 기준 17번)
RELAY_PIN = 17

def reset_board():
    GPIO.setwarnings(False)
    GPIO.setmode(GPIO.BCM)
    
    # 평상시(로우 상태)에는 릴레이가 작동하지 않아 NC 접점이 유지됨 (전원 공급)
    GPIO.setup(RELAY_PIN, GPIO.OUT, initial=GPIO.LOW)

    print("통신보드 전원 차단 (리셋 시작)...")
    # 릴레이 작동 -> NC 접점이 떨어짐 -> 전원 차단
    GPIO.output(RELAY_PIN, GPIO.HIGH) 
    
    # 보드 내부 캐시 메모리가 완전히 방전되도록 5초 대기
    time.sleep(5) 

    print("통신보드 전원 복구...")
    # 릴레이 해제 -> NC 접점 다시 붙음 -> 전원 공급
    GPIO.output(RELAY_PIN, GPIO.LOW) 

    GPIO.cleanup()
    print("리셋 완료. 보드 부팅 대기 중...")

if __name__ == '__main__':
    reset_board()
