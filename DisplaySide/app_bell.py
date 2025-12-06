from flask import Flask, request, jsonify, render_template
from flask_sock import Sock
import json

app = Flask(__name__)
sock = Sock(app)

# 전역 상태
connected_users = {}
button_pressed_seq = 0          # 0, 1, 2, 3, ...
current_code = {"x": 0, "y": 1}
haptic_seq = 0
cue_seq = 0

@app.route('/')
def index():
    return render_template('index.html')

@sock.route('/ws')
def websocket(ws):
    global connected_users, button_pressed_seq, haptic_seq, cue_seq

    last_seen_seq = button_pressed_seq
    last_seen_haptic_seq = haptic_seq
    last_seen_cue_seq = cue_seq

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

            # 버튼 이벤트 브로드캐스트
            if button_pressed_seq > last_seen_seq:
                payload = {
                    "pressed": True,
                    "codeX": int(current_code["x"]),
                    "codeY": int(current_code["y"])
                }
                ws.send(json.dumps(payload))
                print(f"Sent Pressed Event to {user_id or 'unknown'}:", payload)
                last_seen_seq = button_pressed_seq
            
            # 햅틱 이벤트 브로드캐스트
            if haptic_seq > last_seen_haptic_seq:
                payload = {
                    "haptic": True
                }
                ws.send(json.dumps(payload))
                print(f"Sent Haptic Event to {user_id or 'unknown'}:", payload)
                last_seen_haptic_seq = haptic_seq
            
            # Visual Cue 토글 이벤트 브로드캐스트
            if cue_seq > last_seen_cue_seq:
                payload = {
                    "cue_toggle": True
                }
                ws.send(json.dumps(payload))
                print(f"Sent Cue Toggle Event to {user_id or 'unknown'}:", payload)
                last_seen_cue_seq = cue_seq

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

# 1) 코드만 설정하는 API
@app.route("/set_code", methods=["POST"])
def set_code():
    """
    codeX, codeY만 설정하고, 신호는 보내지 않음.
    """
    global current_code
    data = request.get_json(silent=True) or {}

    code_x = data.get("codeX")
    code_y = data.get("codeY")

    if code_x is None or code_y is None:
        return jsonify({"error": "codeX and codeY required"}), 400

    current_code["x"] = int(code_x)
    current_code["y"] = int(code_y)

    print(f"Code set to ({current_code['x']},{current_code['y']})")

    return jsonify({
        "status": "code_set",
        "codeX": current_code["x"],
        "codeY": current_code["y"]
    })

# 2) PC 햅틱 트리거
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

# 3) 현재 설정된 code로 Pressed 이벤트만 브로드캐스트
@app.route("/press_button", methods=["POST"])
def press_button():
    global button_pressed_seq, current_code
    data = request.get_json(silent=True) or {}

    # 여기서는 더 이상 codeX, codeY를 안 바꿈
    # current_code에 이미 저장된 값 그대로 사용
    button_pressed_seq += 1

    print(f"Button pressed! seq={button_pressed_seq}, code=({current_code['x']},{current_code['y']})")

    return jsonify({
        "status": "armed",
        "seq": button_pressed_seq,
        "codeX": current_code["x"],
        "codeY": current_code["y"]
    })

# 4) HMD 쪽 Visual Cue 토글 신호 브로드캐스트
@app.route("/toggle_cue", methods=["POST"])
def toggle_cue():
    """
    HMD 쪽 Visual Cue 토글하라고 신호만 쏘는 API.
    Unity(HMD)가 /ws로 붙어있다가 이 이벤트를 보고 visual cue를 토글.
    """
    global cue_seq
    data = request.get_json(silent=True) or {}

    cue_seq += 1
    print(f"Visual cue toggle requested! cue_seq={cue_seq}")

    return jsonify({
        "status": "cue_armed",
        "cue_seq": cue_seq
    })

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=False)
