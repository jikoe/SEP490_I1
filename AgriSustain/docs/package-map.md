# Ánh xạ Package Diagram Report 4

Bảng này ánh xạ đúng 18 package con của sơ đồ đã chốt. Tên lớp là quy ước cho implementation sau này; bộ khung hiện chưa triển khai nghiệp vụ.

| Layer | Package | Thư mục code | Trách nhiệm | Quy ước lớp |
| --- | --- | --- | --- | --- |
| Api | Controllers | `src/AgriSustain.Api/Controllers` | REST endpoints và payment webhook. | `{Feature}Controller` |
| Api | Security | `src/AgriSustain.Api/Security` | Cấu hình xác thực JWT và authorization policies/handlers. | `{Purpose}Requirement`, `{Purpose}AuthorizationHandler` |
| Api | Middlewares | `src/AgriSustain.Api/Middlewares` | Exception handling, request logging và lỗi HTTP thống nhất. | `{Purpose}Middleware` |
| Application | Services | `src/AgriSustain.Application/Services` | Điều phối use case, chia theo feature. | `{Feature}Service` |
| Application | Interfaces | `src/AgriSustain.Application/Interfaces` | Hợp đồng services, repositories, UnitOfWork và tích hợp. | `I{Purpose}`, ví dụ `IOrderRepository`, `IUnitOfWork` |
| Application | DTOs | `src/AgriSustain.Application/DTOs` | Dữ liệu đầu vào và đầu ra của use case. | `{Action}{Entity}Request`, `{Entity}Response` |
| Application | Mappings | `src/AgriSustain.Application/Mappings` | Ánh xạ Entity và DTO. | `{Feature}MappingProfile` hoặc `{Entity}Mapper` |
| Application | Validators | `src/AgriSustain.Application/Validators` | Kiểm tra dữ liệu đầu vào. | `{RequestName}Validator` |
| Application | Common | `src/AgriSustain.Application/Common` | Các kiểu kết quả, phân trang và lỗi dùng chung trong Application. | `Result<T>`, `PagedResult<T>`, `NotFoundException` |
| Domain | Entities | `src/AgriSustain.Domain/Entities` | Thực thể nghiệp vụ theo Database Design. | Danh từ số ít PascalCase, ví dụ `Order` |
| Infrastructure | Data | `src/AgriSustain.Infrastructure/Data` | EF Core DbContext, entity configurations và migrations cho SQL Server. | `AppDbContext`, `{Entity}Configuration` |
| Infrastructure | Repositories | `src/AgriSustain.Infrastructure/Repositories` | Triển khai repository interfaces qua EF Core. | `{Entity}Repository` |
| Infrastructure | UnitOfWork | `src/AgriSustain.Infrastructure/UnitOfWork` | Phối hợp lưu thay đổi và transaction database. | `UnitOfWork` triển khai `IUnitOfWork` |
| Infrastructure | Payments | `src/AgriSustain.Infrastructure/Payments` | Tích hợp PayOS/VietQR. | `PayOsPaymentService`, `PayOsClient` |
| Infrastructure | FileStorage | `src/AgriSustain.Infrastructure/FileStorage` | Lưu/truy xuất file và kiểm tra upload. | `FileStorageService` |
| Infrastructure | Email | `src/AgriSustain.Infrastructure/Email` | Gửi email qua Google Gmail API. | `GmailEmailService` |
| Infrastructure | Auditing | `src/AgriSustain.Infrastructure/Auditing` | Ghi lại actor, thao tác, tài nguyên, thời gian và kết quả. | `AuditService` |
| Infrastructure | BackgroundWorkers | `src/AgriSustain.Infrastructure/BackgroundWorkers` | Đối soát thanh toán, hết hạn đơn, thông báo và công việc định kỳ. | `{Purpose}Worker` |

## Hướng phụ thuộc

- Api -> Application: Controllers gọi Services và dùng DTOs.
- Api -> Infrastructure: `Program.cs` đăng ký implementation qua DI.
- Application -> Domain: Services sử dụng Entities.
- Infrastructure -> Application, Domain: triển khai Interfaces và sử dụng Entities.
- Domain không tham chiếu layer khác.
- BackgroundWorkers gọi Application Services qua DI scope khi cần.
- React SPA gọi API qua HTTPS/JSON; SQL Server, PayOS/VietQR và Google Gmail API là hệ thống ngoài backend.

## Namespace và DI

Namespace đi theo project và đường dẫn, ví dụ `AgriSustain.Application.Services.Orders` hoặc `AgriSustain.Infrastructure.Repositories`. Các hợp đồng Unit of Work đặt ở `Application/Interfaces/UnitOfWork`, implementation đặt ở `Infrastructure/UnitOfWork`.

`Program.cs` gọi `AddApplication()` và `AddInfrastructure(configuration)`. Các extension class nằm tại gốc project Api/Infrastructure để không tạo package ngoài sơ đồ. Hai hàm hiện chỉ là điểm đăng ký, chưa cấu hình JWT, database hoặc dịch vụ tích hợp.

`Data/Contexts`, `Data/Configurations`, `Data/Migrations`, `Payments/PayOS`, `Email/Gmail` và các thư mục feature chỉ là nhóm con của package tương ứng. `Properties` trong Api phục vụ Visual Studio. Không thêm Domain Enums, DomainServices hay ValueObjects khi chưa sử dụng.

Các thư mục `Caching` và `Identity` dưới Interfaces là vị trí hợp đồng dự kiến được giữ lại từ bộ khung cũ; chưa có implementation tương ứng.

## Nguồn

- Report 4: https://docs.google.com/document/d/1gRUQwJAMfWAywcQd9lA82P-DaHC4E1s9/edit
