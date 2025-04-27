# Sử dụng base image Python
FROM python:3.9-slim

# Đặt thư mục làm việc
WORKDIR /app

# Cài đặt các dependencies hệ thống có thể cần cho OpenCV và các thư viện khác
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1-mesa-glx \
    libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

# Copy file requirements trước để tận dụng Docker cache
COPY ./OcrService/requirements.txt /app/requirements.txt

# Cài đặt thư viện Python
# --no-cache-dir để giảm kích thước image
RUN pip install --no-cache-dir -r requirements.txt

# Copy mã nguồn ứng dụng và model
COPY ./OcrService /app
COPY ./my_model /app/my_model

# Mở cổng mà FastAPI sẽ chạy
EXPOSE 8000

# Lệnh để chạy ứng dụng khi container khởi động
# Sử dụng main_preloaded.py nhưng đổi tên thành main.py để tiện gọi
# Hoặc bạn có thể giữ tên và gọi uvicorn main_preloaded:app
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]