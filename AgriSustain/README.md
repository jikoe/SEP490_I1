# AgriSustain Code Structure

Bộ khung ASP.NET Core Web API 8.0 được tổ chức theo package diagram mới nhất đã chốt trong Report 4. Solution giữ 4 project backend, với 18 package nghiệp vụ/kỹ thuật bên trong.

| Project | Package chính |
| --- | --- |
| `src/AgriSustain.Api` | `Controllers`, `Security`, `Middlewares` |
| `src/AgriSustain.Application` | `Services`, `Interfaces`, `DTOs`, `Mappings`, `Validators`, `Common` |
| `src/AgriSustain.Domain` | `Entities` |
| `src/AgriSustain.Infrastructure` | `Data`, `Repositories`, `UnitOfWork`, `Payments`, `FileStorage`, `Email`, `Auditing`, `BackgroundWorkers` |

`Program.cs` là điểm khởi động và đăng ký DI. Các lớp mở rộng đăng ký Application/Infrastructure đặt ở cấp gốc của project tương ứng; không tạo package `CompositionRoot` hoặc `DependencyInjection` riêng. `Properties` là cấu hình chạy của Visual Studio, không phải package nghiệp vụ.

Các thư mục con theo feature được giữ trong Services, DTOs và Validators. Domain hiện chỉ có Entities; chưa thêm Enums, DomainServices hoặc ValueObjects.

## Mở và chạy backend

Mở `AgriSustain.sln` trong Visual Studio và chọn startup project `AgriSustain.Api`. Cài .NET SDK phù hợp với `global.json`.

```powershell
dotnet build AgriSustain.sln
dotnet run --project src/AgriSustain.Api
```

Endpoint kiểm tra bộ khung: `http://localhost:5179/health`.

## Trạng thái triển khai

Repository hiện là bộ khung: chưa có code nghiệp vụ, EF Core/SQL Server, JWT, PayOS, Gmail, file storage hoặc background worker thực tế. Các thư mục và hàm đăng ký DI là vị trí để nhóm bổ sung implementation. Frontend React và tests cũng mới là thư mục dự kiến.

Xem [docs/package-map.md](docs/package-map.md) để đối chiếu 18 package, hướng phụ thuộc và quy ước namespace.
