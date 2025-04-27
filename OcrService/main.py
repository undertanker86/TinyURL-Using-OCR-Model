import os
from io import BytesIO
import re
import httpx # Thư viện HTTP client tốt hơn requests cho async
import validators # Thư viện kiểm tra URL

import easyocr
import imagehash
import numpy as np
from fastapi import FastAPI, File, UploadFile, HTTPException, Request, Depends
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from loguru import logger
from PIL import Image

# --- Cấu hình ---
OCR_CONFIDENCE_THRESHOLD = 0.0
# Lấy URL của API Gateway từ biến môi trường, trỏ đến cổng nội bộ 80
URL_SHORTENER_ENDPOINT = f"{os.getenv('URL_SHORTENER_SERVICE_URL', 'http://api-gateway:80')}/api/url/shorten"

# --- Khởi tạo EasyOCR Reader (Preload) ---
logger.info("Loading EasyOCR model...")
try:
    # Thử dùng GPU nếu có, nếu không fallback về CPU
    # Cần kiểm tra Dockerfile và môi trường host có hỗ trợ CUDA không
    reader = easyocr.Reader(
        ["vi", "en"],
        gpu=False, # Mặc định là False, đổi thành True nếu có GPU và cài đặt đúng
        detect_network="craft",
        model_storage_directory="/app/my_model",
 # Đường dẫn trong container
        download_enabled=False, # Model đã có sẵn trong image
    )
    logger.info("EasyOCR model loaded successfully.")
except Exception as e:
    logger.error(f"Failed to load EasyOCR model: {e}")
    # Có thể thoát ứng dụng hoặc hoạt động ở chế độ hạn chế nếu model là bắt buộc
    # raise e # Thoát nếu không load được model

# --- FastAPI App ---
app = FastAPI()
security = HTTPBearer() # Để lấy token từ header Authorization

# --- Helper Functions ---
def find_first_valid_url(detections):
    """Tìm URL hợp lệ đầu tiên từ kết quả OCR với độ tin cậy >= ngưỡng."""
    potential_urls = []
    for bbox, text, prob in detections:
        if prob >= OCR_CONFIDENCE_THRESHOLD:
            # Regex đơn giản để tìm chuỗi giống URL
            # Có thể cần cải thiện regex này
            if re.match(r'^https?://[^\s/$.?#].[^\s]*$', text.strip(), re.IGNORECASE):
                 potential_urls.append({"url": text.strip(), "prob": prob})

    if not potential_urls:
        return None

    # Sắp xếp theo xác suất giảm dần và trả về URL đầu tiên
    potential_urls.sort(key=lambda x: x['prob'], reverse=True)
    
    # Kiểm tra lại bằng thư viện validators
    for item in potential_urls:
        if validators.url(item['url']):
             logger.info(f"Validated URL found: {item['url']} (Prob: {item['prob']})")
             return item['url']
        else:
             logger.warning(f"Potential URL failed validation: {item['url']}")
             
    logger.warning("No valid URL found after validation.")
    return None

# --- API Endpoints ---
@app.post("/api/ocr/upload")
async def upload_and_shorten(
    request: Request, # Thêm Request để truy cập header
    file: UploadFile = File(...),
    # Lấy thông tin xác thực từ header Authorization
    auth: HTTPAuthorizationCredentials = Depends(security)
):
    """
    Nhận ảnh, trích xuất URL, và gọi dịch vụ UrlShortener để tạo link rút gọn.
    Yêu cầu header 'Authorization: Bearer {token}'.
    """
    logger.info(f"Received OCR request for file: {file.filename}")

    # Đọc ảnh
    try:
        request_object_content = await file.read()
        pil_image = Image.open(BytesIO(request_object_content))
        # Chuyển sang RGB nếu là RGBA hoặc P để tránh lỗi EasyOCR
        if pil_image.mode in ('RGBA', 'P'):
            pil_image = pil_image.convert('RGB')
        np_image = np.array(pil_image)
    except Exception as e:
        logger.error(f"Failed to read or process image: {e}")
        raise HTTPException(status_code=400, detail="Invalid or corrupted image file.")

    # Thực hiện OCR
    try:
        logger.info("Performing OCR...")
        detection = reader.readtext(np_image)
        logger.info(f"OCR detected {len(detection)} text blocks.")
        # In toàn bộ text đọc được
        for idx, (bbox, text, prob) in enumerate(detection):
            logger.info(f"[Block {idx+1}] Text: {text} | Confidence: {prob:.2f}")
    except Exception as e:
        logger.error(f"EasyOCR failed: {e}")
        raise HTTPException(status_code=500, detail="OCR processing failed.")

    # Tìm URL hợp lệ đầu tiên với độ tin cậy cao
    extracted_url = find_first_valid_url(detection)

    if not extracted_url:
        logger.warning(f"No valid URL found with confidence >= {OCR_CONFIDENCE_THRESHOLD}")
        raise HTTPException(status_code=400, detail=f"No valid URL found with sufficient confidence (>= {OCR_CONFIDENCE_THRESHOLD}).")

    # Gọi dịch vụ UrlShortener (qua Gateway)
    logger.info(f"Calling UrlShortener for URL: {extracted_url}")
    headers = {
        # Chuyển tiếp token xác thực của người dùng
        "Authorization": f"Bearer {auth.credentials}",
        "Content-Type": "application/json"
    }
    payload = {
        "url": extracted_url
        # Không gửi customAlias và expiryDate để dùng mặc định của UrlShortener
    }

    try:
        async with httpx.AsyncClient() as client:
            response = await client.post(URL_SHORTENER_ENDPOINT, json=payload, headers=headers)
            response.raise_for_status() # Ném lỗi nếu status code không phải 2xx
            shortener_response = response.json()
            logger.info(f"UrlShortener responded successfully: {shortener_response}")
            # Trả về kết quả từ UrlShortener
            return shortener_response
    except httpx.HTTPStatusError as exc:
        # Lỗi từ phía UrlShortener hoặc Gateway
        error_detail = f"UrlShortener service returned error: {exc.response.status_code}"
        try:
            error_body = exc.response.json()
            error_detail += f" - {error_body.get('message', exc.response.text)}"
        except ValueError: # Nếu response không phải JSON
             error_detail += f" - {exc.response.text}"
        logger.error(error_detail)
        raise HTTPException(status_code=exc.response.status_code, detail=error_detail)
    except httpx.RequestError as exc:
        # Lỗi kết nối mạng
        logger.error(f"Could not connect to UrlShortener service: {exc}")
        raise HTTPException(status_code=503, detail=f"Could not reach UrlShortener service: {exc}")
    except Exception as e:
        logger.error(f"Unexpected error calling UrlShortener: {e}")
        raise HTTPException(status_code=500, detail="An unexpected error occurred while shortening the URL.")

@app.get("/health")
async def health_check():
    # Endpoint kiểm tra sức khỏe đơn giản
    return {"status": "ok"}

# (Tùy chọn) Bạn có thể giữ lại endpoint /preloaded_ocr để test riêng OCR
# @app.post("/preloaded_ocr_test")
# async def ocr_test(file: UploadFile = File(...)):
#     ... (logic OCR giống như trong main_preloaded.py gốc) ...