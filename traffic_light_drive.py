import asyncio
import json
import base64
import sys
from io import BytesIO

# Fix console encoding on Windows
if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

try:
    import websockets
except ImportError:
    print("Vui lòng cài đặt: pip install websockets")
    raise

try:
    from PIL import Image
    import numpy as np
except ImportError:
    print("Vui lòng cài đặt: pip install Pillow numpy")
    raise

try:
    import cv2
    HAS_CV2 = True
except ImportError:
    HAS_CV2 = False
    print("Lưu ý: Chưa cài opencv-python, script sẽ dùng thuật toán PIL/numpy cơ bản.")


def detect_traffic_light(image_np):
    """
    Phát hiện trạng thái đèn giao thông từ ảnh camera trước:
    Trả về: 'RED', 'YELLOW', 'GREEN', hoặc 'NONE'
    """
    if HAS_CV2:
        # Chuyển đổi sang không gian màu HSV để lọc màu chính xác
        hsv = cv2.cvtColor(image_np, cv2.COLOR_RGB2HSV)

        # Ngưỡng màu Đỏ (Red trong HSV có 2 dải)
        red_mask1 = cv2.inRange(hsv, np.array([0, 120, 150]), np.array([10, 255, 255]))
        red_mask2 = cv2.inRange(hsv, np.array([170, 120, 150]), np.array([180, 255, 255]))
        red_mask = red_mask1 | red_mask2

        # Ngưỡng màu Vàng (Yellow)
        yellow_mask = cv2.inRange(hsv, np.array([18, 120, 150]), np.array([35, 255, 255]))

        # Ngưỡng màu Xanh lá (Green)
        green_mask = cv2.inRange(hsv, np.array([40, 100, 120]), np.array([90, 255, 255]))

        red_pixels = cv2.countNonZero(red_mask)
        yellow_pixels = cv2.countNonZero(yellow_mask)
        green_pixels = cv2.countNonZero(green_mask)
    else:
        # Fallback bằng numpy dựa trên kênh màu RGB
        r = image_np[:, :, 0].astype(int)
        g = image_np[:, :, 1].astype(int)
        b = image_np[:, :, 2].astype(int)

        red_mask = (r > 180) & (g < 80) & (b < 80)
        yellow_mask = (r > 180) & (g > 160) & (b < 70)
        green_mask = (g > 160) & (r < 100) & (b < 120)

        red_pixels = int(np.sum(red_mask))
        yellow_pixels = int(np.sum(yellow_mask))
        green_pixels = int(np.sum(green_mask))

    THRESHOLD = 25  # Số pixel tối thiểu để xác nhận đèn sáng
    counts = [("RED", red_pixels), ("YELLOW", yellow_pixels), ("GREEN", green_pixels)]
    counts.sort(key=lambda x: x[1], reverse=True)

    if counts[0][1] >= THRESHOLD:
        return counts[0][0]
    return "NONE"


async def telemetry_handler(websocket, *args, **kwargs):
    print("\n[Simulator Kết Nối Thành Công!]")
    print("Đang nhận dữ liệu hình ảnh và điều khiển xe...\n")

    try:
        async for message in websocket:
            data = json.loads(message)

            # Giải mã hình ảnh từ base64 JPEG
            img_bytes = base64.b64decode(data["image"])
            pil_image = Image.open(BytesIO(img_bytes)).convert("RGB")
            image_np = np.array(pil_image)

            current_speed = float(data.get("speed", 0.0))
            steer_angle = float(data.get("steering_angle", 0.0))

            # Nhận diện đèn giao thông
            light_state = detect_traffic_light(image_np)

            throttle = 0.3
            steering = 0.0

            if light_state == "RED":
                throttle = 0.0  # Dừng xe khi đèn đỏ
                action_text = ">> ĐÈN ĐỎ -> PHANH / DỪNG XE (Throttle: 0.0)"
            elif light_state == "YELLOW":
                throttle = 0.12  # Giảm tốc độ khi đèn vàng
                action_text = ">> ĐÈN VÀNG -> GIẢM TỐC ĐỘ (Throttle: 0.12)"
            elif light_state == "GREEN":
                throttle = 0.35  # Tiếp tục chạy khi đèn xanh
                action_text = ">> ĐÈN XANH -> DI CHUYỂN BÌNH THƯỜNG (Throttle: 0.35)"
            else:
                throttle = 0.3
                action_text = ">> Không thấy đèn giao thông -> Lái theo làn (Throttle: 0.3)"

            print(f"Tốc độ: {current_speed:.2f} | Đèn: {light_state:6s} | {action_text}")

            if HAS_CV2:
                # Hiển thị cửa sổ quan sát hình ảnh từ xe
                bgr_view = cv2.cvtColor(image_np, cv2.COLOR_RGB2BGR)
                color_badge = (0, 255, 0) if light_state == "GREEN" else (0, 255, 255) if light_state == "YELLOW" else (0, 0, 255) if light_state == "RED" else (200, 200, 200)
                cv2.putText(bgr_view, f"Traffic Light: {light_state}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, color_badge, 2)
                cv2.imshow("Front Camera Stream (VIA Jeep Simulator)", bgr_view)
                cv2.waitKey(1)

            # Gửi tín hiệu điều khiển ngược lại Unity
            response = json.dumps({"throttle": throttle, "steering": steering})
            await websocket.send(response)

    except websockets.exceptions.ConnectionClosed:
        print("\n[Simulator đã ngắt kết nối]")
    except Exception as e:
        print(f"\n[Lỗi xử lý]: {e}")


async def main():
    port = 4567
    print(f"=====================================================")
    print(f" VIA SIMULATION - PYTHON TRAFFIC LIGHT AUTOPILOT ")
    print(f" Khởi động WebSocket Server tại ws://127.0.0.1:{port}")
    print(f" Đang chờ simulator kết nối...")
    print(f"=====================================================")

    async with websockets.serve(telemetry_handler, "0.0.0.0", port, ping_interval=None):
        await asyncio.Future()  # Giữ server chạy liên tục


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nĐã tắt server.")
