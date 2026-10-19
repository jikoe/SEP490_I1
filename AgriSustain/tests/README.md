# Tests

Các project xUnit đã có:

- Unit/AgriSustain.UnitTests: domain invariant và validator.
- Integration/AgriSustain.IntegrationTests: pipeline HTTP/Application với demo adapters,
  mỗi test tự tạo server/fixtures. Các case cũ ép InMemory; nhóm SqlPersistenceTests riêng kiểm chứng SQL, không có JWT thật.
- Architecture/AgriSustain.ArchitectureTests: chiều phụ thuộc assembly.
- ../frontend/tests/three-uc.spec.js: bốn luồng trình duyệt cho ba UC qua API localhost.

Từ thư mục solution: `dotnet test AgriSustain.sln --configuration Release`.
Từ frontend: `npm run test:e2e` sau khi backend đã build Release và Edge đã cài;
để trống port 5179/5173 vì suite tự chạy/dừng server.

Xem [phạm vi và giới hạn kiểm chứng](../docs/three-uc-demo-and-integration.md).

Sau review DB: xem [code/comment và kết quả kiểm tra](../docs/three-uc-code-review.md).
Các hàm đã có XML/JSDoc; contract mới bỏ ảnh/archive/Version, dùng RetailCustomer và FEFO.

## SQL integration opt-in

```powershell
$env:THREE_UC_SQL_TESTS='1'
dotnet test tests/Integration/AgriSustain.IntegrationTests -c Release --filter FullyQualifiedName~SqlPersistenceTests --logger trx --results-directory ../tmp/sql-runs
Remove-Item Env:THREE_UC_SQL_TESTS
```

Mỗi SQL test tự tạo database `AgriSustain_ThreeUcTest_<GUID>` từ schema+seed và tự dọn;
không dùng AgriSustain/AgriSustainDB. Cần LocalDB và sqlcmd. Không bật biến thì bốn SQL
case Skip có lý do; không được ghi chúng Passed. Hai API host trong concurrency test có
mock gate độc lập nhưng cùng OS process, chưa thay case hai OS process độc lập.
Bằng chứng 19/10:22Unit+29suiteIntegration+1 Architecture cũ đạt,4SQL đạt,4Edge E2E đạt.
Report phân loại DI guard vào component nên số theo report khác tên runner assembly.
Xem [hướng dẫn và review report](../docs/three-uc-testing-guide-and-report-review.md).
