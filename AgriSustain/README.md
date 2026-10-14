# AgriSustain Code Structure

Bộ khung code theo package diagram trong Report 4, sử dụng ASP.NET Core Web API 8.0, React 19.1.0 và SQL Server như Report 1–2.

```text
AgriSustain/
├── AgriSustain.sln
├── src/
│   ├── AgriSustain.Api/
│   │   ├── Controllers/
│   │   ├── Security/
│   │   ├── RequestPipeline/
│   │   ├── CompositionRoot/
│   │   └── Program.cs
│   ├── AgriSustain.Application/
│   │   ├── UseCases/{Feature}/
│   │   ├── Ports/
│   │   ├── DTOs/{Feature}/
│   │   └── Validation/{Feature}/
│   ├── AgriSustain.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Rules/
│   │   ├── Services/
│   │   ├── Types/
│   │   └── Events/
│   └── AgriSustain.Infrastructure/
│       ├── Persistence/
│       ├── Adapters/
│       ├── OperationalServices/
│       ├── HostedBackgroundWorkers/
│       └── DependencyInjection/
├── frontend/
│   └── src/{app,features,api,session,access,components,layouts}/
├── tests/{Unit,Integration,Architecture}/
└── docs/package-map.md
```

## Mở và chạy backend

Mở `AgriSustain.sln` trong Visual Studio. Startup project là `AgriSustain.Api`.

```powershell
dotnet build AgriSustain.sln
dotnet run --project src/AgriSustain.Api
```

Endpoint kiểm tra bộ khung: `http://localhost:5179/health`.

Solution chứa 4 project .NET có đúng hướng phụ thuộc. Các thư mục feature đã được tạo để nhóm bổ sung code. EF Core/SQL Server, JWT, Redis, PayOS, Gmail, media storage và workers mới được bố trí thư mục; chưa có triển khai nghiệp vụ hay dependency tích hợp. Frontend và tests mới là bộ khung thư mục.

Xem `docs/package-map.md` để đối chiếu từng package và quy ước đặt tên lớp.
