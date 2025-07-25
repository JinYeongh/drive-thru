import json
import re
import torch
from rapidfuzz import process, fuzz
from transformers import AutoTokenizer, AutoModelForCausalLM

# ───────────────────────────────────────────────
# 0. 전처리: 오타 교정 · 괄호 제거 · 공백 정리
# ───────────────────────────────────────────────
TYPO_MAP = {
    "미디움": "미디엄",
    "미티엄": "미디엄",
    # 필요시 추가...
}

def preprocess_text(text: str) -> str:
    # 1) 오타 교정
    for wrong, right in TYPO_MAP.items():
        text = text.replace(wrong, right)
    # 2) 괄호 제거하되, 안의 글자는 살리고 주변에 공백 추가
    text = re.sub(r"\((.*?)\)", r" \1 ", text)
    # 3) 여러 칸 공백 → 한 칸
    text = re.sub(r"\s+", " ", text)
    return text.strip()

# ───────────────────────────────────────────────
# 1. 모델 설정 (GTX 1050 최적화)
# ───────────────────────────────────────────────
MODEL = "skt/kogpt2-base-v2"
CACHE = r"D:\model_cache\kogpt2-base-v2"

print(f"[INIT] Loading {MODEL} ...")
tokenizer = AutoTokenizer.from_pretrained(
    MODEL, cache_dir=CACHE, use_fast=True, trust_remote_code=True
)
model = AutoModelForCausalLM.from_pretrained(
    MODEL,
    cache_dir=CACHE,
    torch_dtype=torch.float32,
    device_map="auto",
    trust_remote_code=True
)
print("[INIT] Model loaded.")

# ───────────────────────────────────────────────
# 2. 메뉴 목록 및 정규화
# ───────────────────────────────────────────────
MAIN_MENUS = [
    "익산 고구마 모짜렐라 버거","더블 맥스파이시 상하이 버거","더블 쿼터파운더 치즈",
    "맥스파이시 상하이 버거","쿼터파운더 치즈","토마토 치즈 비프 버거","빅맥",
    "맥크리스피 디럭스 버거","맥크리스피 클래식 버거","1955 버거","맥치킨 모짜렐라",
    "맥치킨","더블 불고기 버거","불고기 버거","슈슈 버거","슈비 버거",
    "베이컨 토마토 디럭스","치즈버거","더블 치즈버거","트리플 치즈버거","햄버거"
]
SIDE_MENUS = [
    "클래식 핫도그","맥윙2조각","맥윙4조각","맥윙8조각",
    "1955 스낵랩","코울슬로","골든 모짜렐라 치즈스틱 2조각","골든 모짜렐라 치즈스틱 4조각",
    "맥너겟 4조각","맥너겟 6조각","맥스파이시 치킨텐더 2조각","상하이 치킨 스낵랩",
    "후렌치 후라이 스몰","후렌치 후라이 미디엄","후렌치 후라이 라지",
    "디핑 소스 스위트 앤 사워","디핑 소스 스위트 앤 칠리","디핑 소스 케이준",
    "해피밀 치즈버거","해피밀 맥너겟 4조각","해피밀 햄버거","해피밀 불고기버거",
    "피치 아이스티 미디엄","피치 아이스티 라지","망고 피치 아이스티 미디엄","망고 피치 아이스티 라지",
    "바닐라 라떼 미디엄","바닐라 라떼 라지","아이스 바닐라 라떼 미디엄","아이스 바닐라 라떼 라지",
    "코카 콜라 미디엄","코카 콜라 라지","코카 콜라 제로 미디엄","코카 콜라 제로 라지",
    "환타 미디엄","환타 라지","스프라이트 미디엄","스프라이트 라지",
    "카페라떼 미디엄","카페라떼 라지","카푸치노 미디엄","카푸치노 라지",
    "아메리카노 미디엄","아메리카노 라지","드립 커피 미디엄","드립 커피 라지",
    "아이스 드립 커피 시럽없음 미디엄","아이스 드립 커피 시럽없음 라지",
    "아이스 아메리카노 시럽없음 미디엄","아이스 아메리카노 시럽없음 라지",
    "아이스 카페라떼 시럽없음 미디엄","아이스 카페라떼 시럽없음 라지",
    "딸기 쉐이크 미디엄","초코 쉐이크 미디엄","바닐라 쉐이크 미디엄"
]
ALL_MENUS = MAIN_MENUS + SIDE_MENUS

NORMALIZED_MAP = {
    name: re.sub(r"\(.*?\)", "", name).replace("  "," ").strip()
    for name in ALL_MENUS
}
INVERTED_NORMALIZED = {v:k for k,v in NORMALIZED_MAP.items()}

# ───────────────────────────────────────────────
# 3. 수량 한글 → 숫자 변환
# ───────────────────────────────────────────────
KOR_NUM = {
    "한":1,"하나":1,"두":2,"둘":2,"세":3,"셋":3,
    "네":4,"넷":4,"다섯":5,"여섯":6,"일곱":7,
    "여덟":8,"아홉":9,"열":10
}
def normalize_qty(text:str)->str:
    for k,v in KOR_NUM.items():
        text = re.sub(f"{k}(?=(개|조각))", str(v), text)
    pat = "("+"|".join(map(re.escape,KOR_NUM.keys()))+")(?=(\s|$|,))"
    
    return re.sub(pat, lambda m: str(KOR_NUM[m.group(1)]), text)

# ───────────────────────────────────────────────
# 4. lookup parser
# ───────────────────────────────────────────────
def parse_text_by_lookup(text:str)->list[dict]:
    t = normalize_qty(text)
    matches=[]
    for orig in ALL_MENUS:
        norm = NORMALIZED_MAP[orig]
        esc = [r"\s*"+re.escape(c) if not c.isspace() else r"\s*" for c in norm]
        pat = rf"(?P<name>{''.join(esc)})(?:\s*(?P<qty>\d+))?(?:\s*(?:개|조각))?"
        for m in re.finditer(pat, t):
            qty = int(m.group("qty")) if m.group("qty") else 1
            matches.append({
                "name":orig, "qty":qty,
                "type":"main" if orig in MAIN_MENUS else "side",
                "span":m.span()
            })
    matches.sort(key=lambda x:x["span"][0])
    sel,last= [],0
    for m in matches:
        s,e = m["span"]
        if s>=last:
            sel.append({k:m[k] for k in("name","qty","type")})
            last=e
    return sel

# ───────────────────────────────────────────────
# 5. RapidFuzz fuzzy parser
# ───────────────────────────────────────────────
def parse_text_with_rapidfuzz(text:str, score_cutoff=70)->list[dict]:
    t = normalize_qty(text)
    res={}
    for orig in ALL_MENUS:
        norm = NORMALIZED_MAP[orig]
        score = fuzz.token_sort_ratio(norm, t)
        if score>=score_cutoff:
            m = re.search(rf"{re.escape(norm)}\s*(\d+)", t)
            qty = int(m.group(1)) if m else 1
            res[orig] = max(res.get(orig,0), qty)
    return [{"name":k,"qty":v,"type":"main" if k in MAIN_MENUS else "side"} for k,v in res.items()]

# ───────────────────────────────────────────────
# 6. Chunk 기반 fuzzy fallback
# ───────────────────────────────────────────────
def parse_text_fuzzy_chunk(text:str, score_cutoff=60)->list[dict]:
    t = normalize_qty(text)
    seen, items = set(), []
    parts = re.split(r"\s*(?:,|와|과|랑|이랑|그리고|및|&)\s*|\s+", t)
    for chunk in parts:
        if not chunk: continue
        m = re.match(r"(.+?)(\d+)$", chunk)
        rawc, qty = (m.group(1),int(m.group(2))) if m else (chunk,1)
        match,score,_ = process.extractOne(rawc, list(NORMALIZED_MAP.values()), scorer=fuzz.token_sort_ratio)
        if score>=score_cutoff:
            orig = INVERTED_NORMALIZED.get(match,match)
            if orig not in seen:
                seen.add(orig)
                items.append({"name":orig,"qty":qty,
                              "type":"main" if orig in MAIN_MENUS else "side"})
    return items

# ───────────────────────────────────────────────
# 7. 부분 문자열 필터링: 실제 입력에 없는 메뉴 제거
# ───────────────────────────────────────────────
def filter_real_input(items:list[dict], raw:str)->list[dict]:
    raw_ns = re.sub(r"\s+","", raw)
    return [o for o in items if re.sub(r"\s+","",o["name"]) in raw_ns]

# ───────────────────────────────────────────────
# 8. 부분 문자열(subname) 필터: 긴 이름 포함된 짧은 이름 제거
# ───────────────────────────────────────────────
def remove_subname_items(items:list[dict])->list[dict]:
    sorted_items = sorted(items, key=lambda o:len(o["name"].replace(" ","")), reverse=True)
    kept=[]
    for o in sorted_items:
        name_ns = o["name"].replace(" ","")
        if any(name_ns in other["name"].replace(" ","") for other in kept):
            continue
        kept.append(o)
    return [o for o in items if o in kept]

# ───────────────────────────────────────────────
# 9. Default size mapping
# ───────────────────────────────────────────────
DEFAULT_SIZE = {
    "아메리카노":"아메리카노 미디엄",
    "카페라떼":"카페라떼 미디엄",
    "카푸치노":"카푸치노 미디엄",
    "바닐라 라떼":"바닐라 라떼 미디엄",
    "드립 커피":"드립 커피 미디엄",
    "코카 콜라":"코카 콜라 미디엄",
}

def process_order(raw_text: str) -> dict:
    raw = preprocess_text(raw_text)
    print(f"[DEBUG] 원문: {raw_text!r} → 전처리: {raw!r}")

    # 1) lookup
    items_lu = parse_text_by_lookup(raw)

    # 2) rapidfuzz
    names_lu = {o["name"] for o in items_lu}
    items_fz = [
        o for o in parse_text_with_rapidfuzz(raw, score_cutoff=65)
        if o["name"] not in names_lu
    ]
    items_fz = filter_real_input(items_fz, raw)

    # 3) chunk
    names_lu_fz = names_lu | {o["name"] for o in items_fz}
    items_ch = [
        o for o in parse_text_fuzzy_chunk(raw, score_cutoff=60)
        if o["name"] not in names_lu_fz
    ]
    items_ch = filter_real_input(items_ch, raw)

    # 4) 합치기 + 부분 문자열(subname) 제거
    items = items_lu + items_fz + items_ch
    items = remove_subname_items(items)

    # 5) 메뉴명+수량 풀 매칭 (lookup/fuzzy/chunk 전부 실패했을 때만)
    if not items:
        # 한글 숫자 → 숫자로 변환
        norm = normalize_qty(raw)  # ex: "불독버거 2개", "후란치 후라이 슴 5개"
        m = re.match(r"(.+?)(\d+)(?:개|조각)?$", norm)
        if m:
            name_part = m.group(1).strip()
            qty       = int(m.group(2))
            match, score, _ = process.extractOne(
                name_part,
                list(NORMALIZED_MAP.values()),
                scorer=fuzz.token_set_ratio
            )
            if score >= 60:
                orig = INVERTED_NORMALIZED.get(match, match)
                items = [{
                    "name": orig,
                    "qty": qty,
                    "type": "main" if orig in MAIN_MENUS else "side"
                }]

    # 6) full-string fallback
    if not items:
        cleaned = re.sub(r"주세요", "", raw).strip()
        match, score, _ = process.extractOne(
            cleaned, list(NORMALIZED_MAP.values()),
            scorer=fuzz.token_sort_ratio
        )
        if score >= 50:
            orig = INVERTED_NORMALIZED.get(match, match)
            m = re.search(r"(\d+)", cleaned)
            qty = int(m.group(1)) if m else 1
            items = [{
                "name": orig,
                "qty": qty,
                "type": "main" if orig in MAIN_MENUS else "side"
            }]

    # 7) 기본 사이즈 매핑
    if not items and raw in DEFAULT_SIZE:
        items = [{
            "name": DEFAULT_SIZE[raw],
            "qty": 1,
            "type": "side"
        }]

    # 포맷 정리
    orders = [{"name": o["name"], "qty": o["qty"], "type": o["type"]} for o in items]
    result = {"items": {"orders": orders, "request": ""}}
    print(f"[DEBUG] Result: {result}")
    return result





# ───────────────────────────────────────────────
# 11. 테스트
# ───────────────────────────────────────────────
if __name__ == "__main__":
    tests = [
        # 기존 케이스
        "치즈버거 한개",
        "슈슈버거 2개 콜라 1개",
        "불고기버거 3개, 코울슬로 2개",
        "딸기 쉐이크 미디엄 한개 트리플 치즈버거 한개",
        "불고기버거 3개 해피밀햄버거 2개",
        "토마토 디렉스 볼거 하나 주세요",
        "코카콜라 미티엄",
        "후란치 후라이 슴 다섯개",
        "치즈 버거 하나 코카 콜라 미디움 하나",

        # 추가 테스트 케이스
        "빅맥 2개와 맥너겟 4조각 6개",                         # '와' 결합
        "맥스파이시 상하이버거 하나",                   # 공백 누락
        "아이스 카페라떼 라지 두",                       # 한글 숫자 + 단위 없는 경우
        "더블치즈버거3개",                              # 메뉴+수량 붙여쓰기
        "후렌치 후라이 스몰 3개",                       # 사이드 + 숫자
        "불독버거 2개",                                 # 리스트에 없는 메뉴
        "치즈버거, 맥너겟 4조각 다섯개",                       # 콤마 + 한글 숫자
        "1955스낵랩 두개 추가",                         # 숫자로 시작 + 키워드 추가
        "아이스 드립 커피(시럽없음)라지 하나",           # 괄호 포함 이름
        "베이컨 토마토 디럭스 1개",                     # 메뉴명 띄어쓰기 오타
        "맥윙4조각",                                   # 사이드만 단독
        "트리플 치즈버거 한 개 맥윙 8조각 코카 콜라 제로 라지 둘개",  # 복합 주문
        "쿼터파운더 치즈 하나",                         # 영어+한글 혼용
        "아메리카노",                                  # 수량 없는 경우
        "골든 모짜렐라 치즈스틱 4조각",                   # 긴 메뉴명
        "맥치킨 하나 후렌치 후라이 스몰 한개 카페라떼 라지 하나 바닐라 쉐이크 미디엄 두개"
    ]
   
    for txt in tests:
        print("\n[INPUT]:", txt)
        print(json.dumps(process_order(txt), indent=2, ensure_ascii=False))
