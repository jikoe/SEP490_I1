# UC-FAR-03, UC-FAR-06, UC-RC-01

Triển khai trên nhánh `ThuongNQ`, đối chiếu Report 3, Report 4, Class/Sequence Diagrams,
thiết kế Unit/Integration Test và giao diện tham chiếu trong `D:\SEP\UI`.

## Chức năng

| Use case | Giao diện | Nghiệp vụ chính |
| --- | --- | --- |
| UC-FAR-03 | `/farmer/products` | Danh sách, lọc, phân trang, thêm/sửa, gửi duyệt, ảnh đại diện, hiển thị/ẩn có xác nhận |
| UC-FAR-06 | `/farmer/orders`, `/receipts` | Xác nhận, chuẩn bị, giao hàng, từ chối, in chứng từ, xác nhận nhận hàng và phản hồi bằng chứng |
| UC-RC-01 | `/cart`, `/checkout` | Giỏ theo sản phẩm và lô thực tế, chọn dòng, số lượng/bước bán, giá lẻ/sỉ, xóa có xác nhận, kiểm tra lại trước checkout |

Backend kiểm tra lại quyền sở hữu, vai trò, trạng thái tài khoản/nông trại, dữ liệu đầu vào,
đơn vị, giá, ảnh JPG/JPEG/PNG tối đa 5 MB/ảnh, tồn kho, điều kiện thanh toán và chuyển trạng thái.
Thay đổi nội dung sản phẩm đưa về chờ duyệt; cập nhật giá đơn thuần giữ trạng thái duyệt.
Ảnh riêng tư được kiểm soát quyền truy cập. Revision và request key bảo vệ cập nhật/xử lý lặp.

Transaction SQL bao gồm dữ liệu nghiệp vụ, audit và thông báo. Checkout dùng chung khóa SQL
với luồng checkout cũ; kiểm tra giỏ không tạo đơn hoặc giữ tồn. Xuất kho khi giao chỉ thực hiện
một lần. Từ chối có xác nhận và lý do; yêu cầu hoàn tiền chờ Finance xử lý. Hàng trả không tự
cộng vào tồn bán. Thời hạn phản hồi bằng chứng là 48 giờ.

## Chạy tại máy phát triển

Từ thư mục `AgriSustain`, dùng database phát triển đã có dữ liệu:

```powershell
powershell -ExecutionPolicy Bypass -File tools/enable-farmer-retail.ps1
npm --prefix frontend ci
npm --prefix frontend run build
dotnet run --project src/AgriSustain.Api --launch-profile AgriSustain.Api
```

Mở `http://localhost:5179`, hoặc chạy profile Development hiện có trong Visual Studio.
Script tự khởi động LocalDB, xác minh database `AgriSustain`, áp dụng schema bổ sung có thể
chạy lại và cập nhật `appsettings.Local.json`. Không reset dữ liệu. Khởi động lại API sau khi chạy.
Kết nối phát triển mới dùng `(localdb)\MSSQLLocalDB` sau khi xác minh thành công, thay cho
pipe tạm đổi theo mỗi lần LocalDB khởi động. Với cấu hình pipe cũ bị lỗi sau khi LocalDB tự dừng,
chạy `powershell -ExecutionPolicy Bypass -File tools/use-running-localdb.ps1 -StartIfStopped -UseInstanceName`
trên PowerShell của máy, rồi khởi động lại API. Lệnh sửa kết nối chỉ đọc database để xác minh,
không áp dụng schema hay thay đổi dữ liệu.
Kết quả thực tế đã xác nhận: `Commerce schema ready`, cột lô giỏ hàng 8 bytes, đủ 4 bảng bổ sung.

Chọn phiên fixture Farmer hoặc Retail Customer trong giao diện Development. Bộ xác thực Demo
hiện có chỉ phục vụ kiểm thử cục bộ; JWT, cổng thanh toán và nhà vận chuyển thật chưa được tích hợp.

## Tổ chức feature và Unit Test (20/10/2026)

Các file của UC-FAR-03 và UC-FAR-06 nằm trong nhóm Farmer; UC-RC-01 nằm trong RetailCustomer.
Frontend dùng `src/features/farmer`, `src/features/retailCustomer` và `src/features/shared`.
Backend chia controller, DTO, service và repository theo Farmer/RetailCustomer; phần giao dịch,
quy tắc và kiểu dữ liệu dùng chung nằm trong Shared hoặc mang tên FarmerRetail.
Đường dẫn API, các trường giao diện và tên bảng SQL hiện có giữ nguyên.

| Use case | File Unit Test | Số ca |
| --- | --- | --- |
| UC-FAR-03 | `tests/Unit/AgriSustain.UnitTests/Farmer/ProductServiceTests.cs` | 21 theo báo cáo + 2 hồi quy |
| UC-FAR-06 | `tests/Unit/AgriSustain.UnitTests/Farmer/OrderFulfillmentServiceTests.cs` | 16 theo báo cáo + 1 hồi quy |
| UC-RC-01 | `tests/Unit/AgriSustain.UnitTests/RetailCustomer/CartServiceTests.cs` | 22 theo báo cáo + 1 hồi quy |

Mỗi file chứa fake repository, người dùng, đồng hồ và dữ liệu ban đầu ngay trong file test.
Các test gọi service thật, giả lập tầng lưu trữ trong bộ nhớ; không kết nối database,
không khởi động LocalDB và không cần chạy API. 63/63 ca đã pass khi cấu hình kết nối
cố tình trỏ tới địa chỉ SQL không hợp lệ. Toàn bộ Unit suite: 140/140 Passed;
API integration trong bộ nhớ: 44 Passed; Architecture: 1 Passed.
Giao diện Chrome: 38/38 Passed (27 ca cho ba UC và 11 ca các module đang tích hợp). Backend và frontend build thành công. Các SQL integration opt-in được bỏ qua trong lượt này.

Từ thư mục `AgriSustain`, chạy Unit Test không dùng database:

```powershell
powershell -ExecutionPolicy Bypass -File tools/test-farmer-retail.ps1
```

Kết quả: `TestResults/farmer-retail-unit.trx`.
Chỉ khi cần kiểm tra SQL thật mới thêm `-SqlOnly` (hoặc `-IncludeSql` để chạy cả Unit và SQL).
Bộ SQL vẫn giữ 56 ca theo báo cáo và 1 hồi quy marketplace/cart; kết quả Unit fake
không được dùng để kết luận SQL integration đã pass.

## Chạy SQL Integration Test bằng nút Run trong IDE

SQL test được bảo vệ bằng `THREE_UC_SQL_TESTS=1`; thiếu biến này thì kết quả là
Skipped, không phải Failed. File `sql-integration.runsettings` bật biến cho test host.
Đây là SQL thật, khác với fake dữ liệu của Unit Test.

- VS Code: mở thư mục `SEP490_I1` hoặc `AgriSustain`, chạy **Developer: Reload Window**
  rồi bấm Run Test/Debug Test trong file. Workspace đã cấu hình
  `dotnet.unitTests.runSettingsPath` tương ứng với cả hai thư mục.
- Visual Studio: project Integration đã khai báo file runsettings khi chạy trong IDE.
  Nếu IDE đang chọn file khác, vào **Test > Configure Run Settings > Select Solution Wide
  runsettings File**, chọn `AgriSustain/sql-integration.runsettings`, rồi làm mới Test Explorer.
- Trước khi chạy, mở PowerShell của máy và chạy `sqllocaldb start MSSQLLocalDB`.
  Cần có `sqlcmd` trong PATH. Fixture mặc định kết nối `(localdb)\MSSQLLocalDB`,
  tự tạo/seed và xóa database `AgriSustain_ThreeUcTest_<GUID>` riêng cho mỗi ca;
  không cần chạy API hay tạo lại database phát triển.
- Cấu hình này không biến SQL test thành fake và không làm Unit Test kết nối database.
  Lệnh CLI thông thường vẫn giữ cơ chế SQL opt-in; chạy script `-SqlOnly` khi cần.

Cảnh báo NU1900 báo không lấy được dữ liệu kiểm tra lỗ hổng package từ NuGet;
nó không gây ra thông báo Skipped do thiếu biến SQL.

## Lịch sử kiểm thử và bằng chứng

| Bộ kiểm tra | Kết quả ngày 19/10/2026 | Bằng chứng |
| --- | --- | --- |
| Unit suite | 117 Passed, 0 Failed; gồm 59 ca theo báo cáo và 4 hồi quy mới | `TestResults/commerce-unit.trx` |
| SQL/API ba UC | 56 Passed, 0 Failed, 0 Skipped | `TestResults/commerce-sql.trx` |
| API integration có sẵn | 44 Passed; 7 SQL opt-in không chạy trong lượt này | `TestResults/commerce-integration.trx` |
| Giao diện Chrome | 25 Passed, 0 Failed sau sửa giao diện/điều hướng; dùng API stub, không phải SQL end-to-end | `frontend/test-results/commerce-ui.json` |
| Architecture | 1 Passed | xUnit architecture project |
| Build | Backend và frontend thành công | `dotnet build`, `npm run build` |

59 ca Unit theo báo cáo: ProductService 21, CartService 22, OrderFulfillmentService 16.
56 ca SQL/API: UC-FAR-03 16, UC-RC-01 21, UC-FAR-06 19. Mỗi ca SQL sử dụng database tạm
riêng và dọn sau khi chạy; không kiểm thử phá dữ liệu trên database phát triển.

Lượt SQL đầu: 55 Passed, 1 Failed (`IT-RC-01-002`). Đã sửa `DEF-CART-001`: chuẩn hóa decimal
khi tạo review token để `2` và `2.000` cho cùng token, thêm test hồi quy. Lượt sau: 56/56 Passed.
SQL thật được chạy bằng PowerShell của người dùng do sandbox không truy cập được named pipe;
kết quả TRX đã được đọc và đối chiếu.

Chạy lại:

```powershell
powershell -ExecutionPolicy Bypass -File tools/test-farmer-retail.ps1
# Mặc định chỉ Unit Test với fake; SQL thật phải thêm -SqlOnly hoặc -IncludeSql
cd frontend
npx playwright test --config playwright.farmer-retail.config.js
```

Playwright cần Chrome trên máy. Khi dùng Vite đã chạy ở cổng 5192, đặt
`$env:FARMER_RETAIL_EXTERNAL_UI='1'` trước lệnh Playwright.

## Báo cáo đã cập nhật

### Đối chiếu giao diện với `D:\SEP\UI`

Đã chỉnh lại ba màn hình theo các HTML tham chiếu, giữ dữ liệu thật và kiểm tra nghiệp vụ:

- FAR-03: hai tab danh sách/form, bộ lọc ngày và kiểm duyệt/hiển thị, nhóm/xuất xứ,
  số lô thực tế, danh sách tỉnh/thành, đủ nhóm nút duyệt/giá/hiển thị. Không đưa bước số lượng,
  bộ lọc danh mục và danh sách lô chi tiết vào form tham chiếu.
- FAR-06: bốn tab theo mẫu; hàng hóa ở danh sách; tách phiếu đóng gói/in phiếu;
  tách lý do từ chối, báo giao thất bại, giải trình khiếu nại và từng bộ tệp bằng chứng.
  Hiển thị hồ sơ khiếu nại, trạng thái hoàn tiền/chi trả từ dữ liệu lưu; không thêm quyền quyết định tài chính.
- RC-01: đúng các nhóm nhà cung cấp, ảnh/tên/đơn vị/lô/tồn, giá gạch ngang và nhãn giá sỉ,
  số lượng, thành tiền, khối tóm tắt và hướng dẫn checkout. Bộ chọn nông sản có sẵn được chuyển
  sang đích của nút Tiếp tục mua sắm (`/products`), không nằm dưới giỏ hàng.

Lượt kiểm tra sau chỉnh giao diện: 23 UI, 117 Unit và 44 API integration Passed;
4 SQL opt-in trong bộ API có sẵn được bỏ qua. Backend build không cảnh báo/lỗi, frontend build thành công.
56 SQL/API Passed ở bảng trên là lượt trước chỉnh giao diện; chưa chạy lại SQL thật sau các thay đổi
truy vấn đọc phục vụ lọc ngày và hiển thị thông tin còn thiếu. Không cần nâng cấp schema mới cho lượt sửa UI này.

### Tiến trình đã ghi trong workbook

- Unit: 59 ca được giao ở Round 1 đều Passed, ngày chạy và người chạy đã ghi;
  các vòng chưa chạy giữ Pending.
- Integration: giữ lịch sử Round 1 (55 Passed, 1 Failed), ghi đủ 56 Passed ở Round 2;
  ca lỗi có mã defect và mô tả bản sửa. Thống kê ba UC tính theo Round 2.
- Các module khác được giữ nguyên. Tổng thống kê toàn workbook bao gồm kết quả các module đó,
  không đồng nghĩa toàn bộ ca của workbook đã được chạy trong công việc này.

[Unit Test report](https://docs.google.com/spreadsheets/d/1xmI2OY0wYR0C0Gla-Qwy0lan9zq98c7N/edit)
và [Integration Test report](https://docs.google.com/spreadsheets/d/1XElMMHXIidfNXPFXYiZqKN8BWMemRpZX/edit).
