# Tests

Các project xUnit đã có:

- Unit/AgriSustain.UnitTests: domain invariant và validator.
- Integration/AgriSustain.IntegrationTests: pipeline HTTP/Application với demo adapters,
  mỗi test tự tạo server/fixtures. Không phải kiểm chứng SQL Server hoặc JWT thật.
- Architecture/AgriSustain.ArchitectureTests: chiều phụ thuộc assembly.
- ../frontend/tests/three-uc.spec.js: bốn luồng trình duyệt cho ba UC qua API localhost.

Từ thư mục solution: `dotnet test AgriSustain.sln --configuration Release`.
Từ frontend: `npm run test:e2e` sau khi backend đã build Release và Edge đã cài;
để trống port 5179/5173 vì suite tự chạy/dừng server.

Xem [phạm vi và giới hạn kiểm chứng](../docs/three-uc-demo-and-integration.md).

Sau review DB: xem [code/comment và kết quả kiểm tra](../docs/three-uc-code-review.md).
Các hàm đã có XML/JSDoc; contract mới bỏ ảnh/archive/Version, dùng RetailCustomer và FEFO.
