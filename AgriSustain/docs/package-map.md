# Ánh xạ Package Diagram Report 4

| Package trong Report 4 | Thư mục code |
| --- | --- |
| API Layer – Controllers | src/AgriSustain.Api/Controllers |
| API Layer – Security | src/AgriSustain.Api/Security |
| API Layer – Request Pipeline | src/AgriSustain.Api/RequestPipeline |
| API Layer – Composition Root | src/AgriSustain.Api/CompositionRoot và Program.cs |
| Application Layer – UseCase | src/AgriSustain.Application/UseCases/{Feature} |
| Application Layer – Ports/Interfaces | src/AgriSustain.Application/Ports |
| Application Layer – DTOs & Validation | src/AgriSustain.Application/DTOs và Validation |
| Domain Layer – Entities & Value Objects | src/AgriSustain.Domain/Entities và ValueObjects |
| Domain Layer – Rules & Types | src/AgriSustain.Domain/Rules, Services và Types |
| Infrastructure Layer – Persistence | src/AgriSustain.Infrastructure/Persistence |
| Infrastructure Layer – Payment Adapter | src/AgriSustain.Infrastructure/Adapters/Payments/PayOS |
| Infrastructure Layer – Email & Media Adapters | src/AgriSustain.Infrastructure/Adapters/Email/Gmail và Adapters/Media/FileStorage |
| Infrastructure Layer – Operational Services | src/AgriSustain.Infrastructure/OperationalServices |
| Infrastructure Layer – Hosted Background Workers | src/AgriSustain.Infrastructure/HostedBackgroundWorkers |

## Hướng phụ thuộc

- API -> Application, Infrastructure (composition root đăng ký implementation).
- Application -> Domain.
- Infrastructure -> Application, Domain (triển khai các port).
- Domain không tham chiếu layer khác.
- React SPA gọi API qua HTTPS/JSON, không tham chiếu trực tiếp các project C#.
- Hosted workers gọi Application use cases qua DI scope.

## Quy ước lớp

- Controllers: `{Feature}Controller`.
- UseCases: `{Feature}Service`.
- Ports: `I{Purpose}`.
- DTOs: `{Action}{Entity}Request`, `{Entity}Response`.
- Validation: `{RequestName}Validator`.
- Persistence: `AppDbContext`, `{Entity}Configuration`, `{Entity}Repository`, `UnitOfWork`.
- Adapters: `PayOsPaymentService`, `GmailEmailService`, `FileStorageService`.
- Workers: `{Purpose}Worker`.

## Nguồn

- Report 4: https://docs.google.com/document/d/1gRUQwJAMfWAywcQd9lA82P-DaHC4E1s9/edit
- Report 1: https://docs.google.com/document/d/1DWlKcqItA4Ah6QQbzJEr-9Qbc90idHQVJEg2JRw03vU/edit
- Report 2: https://docs.google.com/document/d/164dQoZm9ztiurbmIr7NxOlSwwT-5UOmt39JKJUEV2OY/edit
