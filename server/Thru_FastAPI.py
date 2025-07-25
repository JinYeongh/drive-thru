# zza.py
from fastapi import FastAPI, Request
from pydantic import BaseModel
from Thru_Parse import process_order
# from zzprom import classify_items

from transformers import AutoTokenizer, AutoModelForSequenceClassification
import torch
import json

# 모델 로드 (분류용)
print("[INIT] Loading classification model...")
tokenizer  = AutoTokenizer.from_pretrained(r"./models", use_fast=True)
classifier = AutoModelForSequenceClassification.from_pretrained(r"./models")
print("[INIT] Classification model loaded.")

# FastAPI 앱 인스턴스 생성
app = FastAPI(
    title="Order Parser (kogpt2-base-v2)",
    description="SKT kogpt2-base-v2 모델 기반 주문 파싱 전용 API"
)

# 요청 바디 스키마 정의
class TextRequest(BaseModel):
    text: str

# 미들웨어: 요청 로깅
@app.middleware("http")
async def log_request(request: Request, call_next):
    body = await request.body()
    print(f"[REQUEST] {request.method} {request.url.path} - Body: {body.decode('utf-8')}")
    response = await call_next(request)
    print(f"[RESPONSE] {request.method} {request.url.path} - Status: {response.status_code}")
    return response

# 주문 파싱 전용 엔드포인트
@app.post("/parse_order")
def parse_order(req: TextRequest):
    print(f"[PARSE_ORDER] Input text: {req.text}")
    items = process_order(req.text)
    # items = classify_items(req.text) 
    print(f"[PARSE_ORDER] Parsed items: {items}")
    return items

# classify 엔드포인트 추가
@app.post("/classify")
def classify(req: TextRequest):
    print(f"[CLASSIFY] Input text: {req.text}")
    enc = tokenizer(req.text, return_tensors="pt")
    with torch.no_grad():
        logits = classifier(**enc).logits
    probs = torch.softmax(logits, dim=-1)[0]
    pred = torch.argmax(probs).item()
    label = "menu" if pred == 1 else "noise"
    result = {"label": label, "confidence": probs[pred].item()}
    print(f"[CLASSIFY] Result: {result}")
    return result

# 실행 예:
# uvicorn zza:app --host 0.0.0.0 --port 8000 --reload --log-level info
