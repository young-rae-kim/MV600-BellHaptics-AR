from flask import Flask, request, jsonify, render_template
from flask_sock import Sock
import json

app = Flask(__name__)
sock = Sock(app)

# 전역 상태
connected_users = {}
# button_pressed_count 대신 "이벤트 번호" 사용
button_pressed_seq = 0          # 0, 1, 2, 3, ... (버튼 누를 때마다 +1)
current_code = {"x": 0, "y": 1} # 현재 선택된 code
haptic_seq = 0

@app.route('/')
def index():
    return render_template('index.html')

@sock.route('/ws')
def websocket(ws):
    global connected_users, button_pressed_seq, haptic_seq

    # 이 클라이언트가 마지막으로 본 버튼 이벤트 번호
    # 현재까지 발생한 이벤트는 "이미 본 것으로" 처리하고 시작하고 싶으면 이렇게:
    last_seen_seq = button_pressed_seq
    last_seen_haptic_seq = haptic_seq

    while True:
        data = ws.receive()
        if not data:
            break

        try:
            msg = json.loads(data)

            # user_id 확인
            user_id = msg.get("user_id")

            if user_id:
                # 해당 유저의 데이터 갱신
                connected_users[user_id] = {
                    "hmd": msg.get("hmd"),
                    "is_contact": msg.get("is_contact"),
                    "left_hand": msg.get("left_hand", []),
                    "right_hand": msg.get("right_hand", [])
                }

                if msg.get("is_contact"):
                    print(f"User {user_id} is in contact! Hand Data Len: {len(msg.get('left_hand', []))}")

            # 버튼 이벤트 브로드캐스트 로직
            # 전역 button_pressed_seq가 이 클라이언트가 마지막으로 본 seq보다 크면
            # 아직 이 이벤트를 안 받은 것 → payload 보내기
            if button_pressed_seq > last_seen_seq:
                payload = {
                    "pressed": True,
                    "codeX": int(current_code["x"]),
                    "codeY": int(current_code["y"])
                }
                ws.send(json.dumps(payload))
                print(f"Sent Pressed Event to {user_id or 'unknown'}:", payload)

                # 이 클라이언트는 이제 최신 이벤트까지 봤다고 표시
                last_seen_seq = button_pressed_seq
            
            if haptic_seq > last_seen_haptic_seq:
                payload = {
                    "haptic": True
                }
                ws.send(json.dumps(payload))
                print(f"Sent Haptic Event to {user_id or 'unknown'}:", payload)
                last_seen_haptic_seq = haptic_seq

        except Exception as e:
            print("WS Error:", e)

@app.route("/get_users_data", methods=["GET"])
def get_users_data():
    return jsonify(connected_users)

@app.route("/get_user", methods=["GET"])
def get_user():
    uid = request.args.get("id")
    if uid in connected_users:
        return jsonify(connected_users[uid])
    return jsonify({"error": "User not found"}), 404

@app.route("/trigger_haptic", methods=["POST"])
def trigger_haptic():
    """
    PC에서 벨 햅틱 울리라고 신호만 쏘는 API.
    Unity(PC)가 /ws로 붙어있다가 이 이벤트를 보고 HapticRenderer를 실행.
    """
    global haptic_seq
    data = request.get_json(silent=True) or {}

    haptic_seq += 1
    print(f"Haptic trigger requested! haptic_seq={haptic_seq}")

    return jsonify({
        "status": "haptic_armed",
        "haptic_seq": haptic_seq
    })

@app.route("/press_button", methods=["POST"])
def press_button():
    global button_pressed_seq, current_code
    data = request.get_json(silent=True) or {}

    # 드롭다운에서 선택한 코드값 갱신
    code_x = data.get("codeX")
    code_y = data.get("codeY")
    if code_x is not None and code_y is not None:
        current_code["x"] = int(code_x)
        current_code["y"] = int(code_y)

    # 새로운 버튼 이벤트 발생
    button_pressed_seq += 1

    print(f"Button pressed! seq={button_pressed_seq}, code=({current_code['x']},{current_code['y']})")

    return jsonify({
        "status": "armed",
        "seq": button_pressed_seq,
        "codeX": current_code["x"],
        "codeY": current_code["y"]
    })

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=False)
