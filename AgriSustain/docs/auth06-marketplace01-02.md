# UC-AUTH-06, UC-MKT-01, UC-MKT-02

Triển khai trên nhánh `ThuongNQ`, theo Report 3 (luồng UC, GBR-21/22/23, thông báo),
Report 4 (class/sequence diagram và schema hiện có), `coding-standards.md` và ba mockup.

## Phạm vi và đường dẫn

- `/#/marketplace`: Marketplace công khai, tìm theo tên/danh mục/nhà cung cấp; lọc danh mục,
  địa phương, nhà cung cấp, mùa vụ, chứng nhận, giá và đánh giá; sắp xếp và phân trang ở SQL.
- `/#/products/{id}`: gallery, giá/đơn vị, tồn kho, nhà cung cấp, chứng nhận còn hiệu lực,
  lô hàng và liên kết truy xuất không bị thu hồi; chỉ hiển thị đánh giá hợp lệ.
- `/#/change-password`: yêu cầu đăng nhập JWT; kiểm tra mật khẩu hiện tại, chính sách mật
  khẩu mới, không trùng mật khẩu cũ và xác nhận; hủy trở về màn trước, xóa dữ liệu nhập.
- `/#/login`: nối lại API đăng nhập hiện có để phục vụ UC-AUTH-06.
- Các route hồ sơ, đăng ký, đơn mua, checkout và quản trị cũ vẫn được giữ.

UI dùng bố cục xanh/kem, header, sidebar, card và gallery theo mockup; có bố cục mobile.
Thông tin tùy chọn chưa có trong schema (giá gốc, số đã bán, quy cách, thời hạn bảo quản)
không được dựng thành số liệu thật. Chức năng yêu thích và các luồng giao dịch B2B nằm ngoài
ba UC này. Nút thêm vào giỏ nối vào `Cart`/`CartItem` hiện có, chỉ dành cho RetailCustomer;
không giữ tồn kho và không bổ sung toàn bộ CRUD giỏ hàng.

## Backend và dữ liệu

Controller gọi Application service; request dùng DTO/FluentValidation, trả Result và
ProblemDetails với code ổn định. Repository EF đọc schema Database First hiện có;
không thêm cột hoặc migration. Không trả entity graph, hash, email người đánh giá hoặc
đường dẫn tài liệu chứng nhận riêng tư.

- `GET /api/products`: `keyword`, `categoryId`, `supplierId`, `region`, `cropSeasonId`,
  `certification`, `minPrice`, `maxPrice`, `minRating`, `sortBy`, `page`, `pageSize`.
  Sắp xếp: `newest`, `price-asc`, `price-desc`, `rating`; mặc định trang 1, 12 sản phẩm;
  tối đa 100 sản phẩm/trang. Kết quả `{ items, total, page, pageSize }`.
- `GET /api/products/filters`: các lựa chọn có nguồn từ dữ liệu công khai.
- `GET /api/products/{id}`: sản phẩm không tồn tại/chưa duyệt/ẩn trả 404 `MSG16`.
- `POST /api/passwords/change`: Bearer token và JSON
  `{ currentPassword, newPassword, confirmNewPassword }`. Danh tính lấy từ principal;
  không nhận UserId do client chỉ định. Các lỗi dùng MSG02/04/05/06/07/12.
- `POST /api/carts/current/items`: Bearer RetailCustomer và `{ productId, quantity }`;
  số lượng dương, tối đa ba số lẻ; kiểm tra tổng lượng trong giỏ với tồn kho hiện tại.

Danh sách và chi tiết dùng chung điều kiện công khai: sản phẩm Approved/IsPublished,
danh mục hoạt động, nhà cung cấp đã duyệt và tài khoản hoạt động, không bị khóa.
Tồn kho chỉ tính lô Available chưa hết hạn, theo ngày Việt Nam (UTC+7).
Chứng nhận phải được duyệt, đã đến ngày cấp và chưa hết hạn.
Đánh giá phải được duyệt, xác thực đã mua, thuộc chính người mua của đơn Completed.
Chi tiết hiện lấy 100 đánh giá gần nhất; cần phân trang đánh giá khi số lượng tăng.

Mật khẩu dùng BCrypt cost 12. Cập nhật có điều kiện theo hash cũ, trạng thái tài khoản
và khóa tạm; so sánh hash bằng collation nhị phân. Hash mới và audit không chứa credential
được commit trong cùng transaction; audit thất bại thì rollback mật khẩu.
Đăng nhập/đổi mật khẩu giới hạn 10 request/phút theo IP hoặc danh tính.

Chính sách mặc định theo Report 3: tối thiểu 8 ký tự, có ít nhất một chữ và một số,
tối đa 72 byte UTF-8 theo giới hạn BCrypt. Có thể cấu hình `PasswordPolicy:MinimumLength`, `RequireUppercase`,
`RequireLowercase`, `RequireDigit`. Không trim hoặc thay đổi giá trị mật khẩu.

## Chạy và kiểm thử

Từ thư mục chứa solution, dùng SDK phù hợp `global.json`; không thay đổi TargetFramework net8.0.
Máy kiểm thử phiên này có SDK 9.0.308/runtime 8 và đã build từ thư mục cha để giữ nguyên
thay đổi `global.json` có sẵn của người dùng. Nếu Windows sandbox chặn workload resolver,
có thể đặt `$env:MSBuildEnableWorkloadResolver='false'` trong phiên terminal chạy kiểm thử.

```powershell
dotnet build AgriSustain.sln -m:1
dotnet test AgriSustain.sln --no-restore -m:1
$env:THREE_UC_SQL_TESTS='1'
dotnet test tests/Integration/AgriSustain.IntegrationTests --no-restore -m:1
npm --prefix frontend ci
npm --prefix frontend run build
npm --prefix frontend run test:e2e
```

Playwright dùng API Debug đã build và Edge với profile kiểm thử. Chạy lần lượt backend
tests rồi Playwright để tránh Windows khóa DLL đang được API sử dụng.
Các test SQL tạo/xóa database tên ngẫu nhiên riêng, không ghi database ứng dụng.
Các test UI mới mock HTTP để kiểm tra thao tác và chụp layout; kết quả SQL/API được xác minh
riêng bằng integration tests. Ảnh desktop/mobile nằm trong `frontend/test-results/` sau khi chạy.

Tài khoản đăng nhập phải có BCrypt hash hợp lệ trong SQL. Seed OTP cũ vẫn là chức năng demo,
không tạo phiên JWT thật; không dùng OTP/token demo thay cho đăng nhập của UC-AUTH-06.
Access token chỉ giữ trong bộ nhớ; reload khôi phục qua refresh cookie HttpOnly.
`Jwt:SecretKey` lấy từ cấu hình môi trường/secrets, không commit. Trong Development,
nếu thiếu khóa thì sinh khóa tạm theo process; restart API sẽ làm phiên trước hết hiệu lực.

Ứng dụng vẫn có guard Development + Demo:Enabled và các adapter checkout/OTP cũ;
đây chưa phải cấu hình triển khai production hoặc cơ chế thu hồi refresh token bền vững.

## Dữ liệu minh họa tùy chọn

Kết quả kiểm tra trong phiên triển khai: build backend không có warning/error;
77 unit tests, 52 integration tests (gồm 8 SQL tests) và 1 architecture test đều qua.
Toàn bộ 11 Playwright tests đã qua; 4 test của ba UC mới được chạy lại sau chỉnh CSS mobile.
Frontend production build thành công. Không có test bị bỏ hoặc xóa để làm bộ kiểm thử qua.

`database/seed-marketplace-demo.sql` thêm các sản phẩm theo mockup và ảnh kèm theo,
sau khi đã có schema và `seed-three-uc-demo.sql`. Script có khóa transaction, chạy lặp
không tạo bản ghi trùng; không reset tài khoản, tồn kho hoặc giỏ hàng đang có.
Script không tự chạy khi khởi động API và chưa được áp dụng vào database ứng dụng trong phiên này.
Ảnh được tái sử dụng từ thư mục Mockup do người dùng cung cấp; ảnh catalogue được hiển thị
qua CSS theo vùng ảnh gốc. Đây là dữ liệu minh họa, không phải chứng nhận hay đánh giá thật.

## Nguồn kỹ thuật đối chiếu

- [JWT bearer authentication — Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [EF Core transactions — Microsoft](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [EF Core concurrency — Microsoft](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
