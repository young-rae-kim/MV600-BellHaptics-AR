from flask import Flask, request, jsonify, render_template
from flask_sock import Sock
import json

app = Flask(__name__)
sock = Sock(app)

# 전역 변수: HMD 및 유저 데이터를 ID별로 저장
# 구조 예시: { "user_abc123": { "hmd": {...}, "left_hand": [...], "timestamp": ... } }
connected_users = {}
button_pressed_count = 0   # 몇 번 눌렸는지
current_code = {"x": 0, "y": 1}  # 기본 코드 (0,1)로 시작

@app.route('/')
def index():
    return render_template('index.html')

@sock.route('/ws')
def websocket(ws):
    global connected_users, button_pressed_count

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
                
                # 디버깅: 접촉 상태인 유저가 있으면 로그 출력
                if msg.get("is_contact"):
                    print(f"User {user_id} is in contact! Hand Data Len: {len(msg.get('left_hand', []))}")

            # 버튼 눌림 이벤트 전송 
            if button_pressed_count > 0:
                payload = {
                    "pressed": True,
                    "codeX": int(current_code["x"]),
                    "codeY": int(current_code["y"])
                }
                ws.send(json.dumps(payload))
                print("Sent Pressed Event:", payload)
                button_pressed_count -= 1

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

@app.route("/press_button", methods=["POST"])
def press_button():
    global button_pressed_count, current_code
    data = request.get_json(silent=True) or {}

    # 드롭다운에서 선택한 코드값 갱신
    code_x = data.get("codeX")
    code_y = data.get("codeY")
    if code_x is not None and code_y is not None:
        current_code["x"] = int(code_x)
        current_code["y"] = int(code_y)

    button_pressed_count += 1
    print(f"Button armed: will send Pressed x{button_pressed_count}, code=({current_code['x']},{current_code['y']})")
    return jsonify({
        "status": "armed",
        "count": button_pressed_count,
        "codeX": current_code["x"],
        "codeY": current_code["y"]
    })

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=False)
