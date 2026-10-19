# Tạo lại database AgriSustain

File `AgriSustain_Full_Recreate.sql` là một script độc lập, chạy trực tiếp trong SSMS, không cần SQLCMD Mode hay file SQL đi kèm.

**Chạy toàn bộ file sẽ xóa database AgriSustain đang có và tất cả dữ liệu, rồi tạo lại.** Dừng API trước khi chạy; sao lưu nếu cần giữ dữ liệu cũ. File chỉ được chuẩn bị để bạn sử dụng, chưa được thực thi lên database trên máy.

## Nội dung

- Giữ cấu trúc gốc trong script bạn gửi và phần cập nhật checkout có sẵn.
- Đủ 51 bảng, gồm `RetailCheckoutQuote` và 4 bảng `CommerceSubmission`, `CommerceMedia`, `CommerceEvidenceRequest`, `CommerceRefundRequest`.
- Bổ sung `Product.Revision`, `Product.QuantityStep`, `CartItem.HarvestBatchId`, `SellerOrder.RequiredPaymentAmount`, kèm default, CHECK và khóa ngoại tương ứng.
- Kèm dữ liệu mẫu cục bộ: tài khoản Demo, nông trại, danh mục, sản phẩm, lô, ảnh mẫu, giỏ, địa chỉ và voucher. Giỏ mẫu có lô thực tế tương ứng. Không tạo thanh toán, đơn giao hàng, hoàn tiền hoặc chi trả giả. Danh sách xử lý đơn sẽ trống cho đến khi có đơn hàng.
- Để tạo schema trống, sửa `DECLARE @LoadDemoData bit = 1;` thành `DECLARE @LoadDemoData bit = 0;` trong phần 3.

## Cách chạy

1. Dừng API và kết nối SSMS tới SQL Server/LocalDB dùng cho ứng dụng, thông thường là `(localdb)\MSSQLLocalDB`, Windows Authentication.
2. Mở `AgriSustain_Full_Recreate.sql`, chạy **toàn bộ file** bằng Execute/F5.
3. Kiểm tra phần Messages không có lỗi; kết quả cuối phải có `AgriSustain`, `51`, `Schema ready`.
4. Khởi động lại API và tải lại trang. Database này khớp code hiện tại, không cần scaffold lại các entity để dùng bốn cột/bảng bổ sung.

Dữ liệu tài khoản mẫu sử dụng các username `demo3uc_farmer_green`, `demo3uc_retail`, `demo3uc_admin`, v.v. Chọn vai trò trên giao diện Development để dùng cơ chế Demo hiện có; password hash mẫu không dùng để đăng nhập bằng mật khẩu thật.

Nếu ứng dụng còn báo `DATABASE_UNAVAILABLE` do cấu hình named pipe cũ, chạy PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File D:\SEP\git\SEP490_I1\AgriSustain\tools\use-running-localdb.ps1 -StartIfStopped -UseInstanceName
```

Sau đó khởi động lại API. Tạo database không tự sửa cấu hình kết nối của ứng dụng.

## Kiểm tra đã thực hiện

Đã phân tích toàn bộ file bằng bộ phân tích T-SQL của SSMS (`TSql160Parser`): 0 lỗi cú pháp, 59 batch, đủ 51 tên bảng ứng dụng, không có phụ thuộc SQLCMD. Các phần schema được ghép từ script nguồn và migration đang dùng trong project. Chưa chạy thử tạo database bằng file tổng hợp này trên SQL Server; kiểm tra cú pháp không thay thế kiểm thử thực thi. Chi tiết và SHA-256 ở `AgriSustain_Full_Recreate.validation.txt`.
