/*
 AGRISUSTAIN - COMPLETE RECREATE SCRIPT - 2026-10-20
 WARNING: THIS SCRIPT DROPS AgriSustain AND ALL OF ITS EXISTING DATA.
 Save a backup first if you need the existing data. This file has not been run against your database.
 Run the WHOLE file in SSMS against your development SQL Server/LocalDB, using Windows Authentication.
 SQLCMD Mode is NOT required. No :r includes or external SQL files are needed.
 Includes: the exact supplied baseline + approved checkout migration + commerce schema (51 tables).
 Optional synthetic data is enabled by @LoadDemoData = 1 near section 3; set it to 0 for schema only.
 Demonstration accounts have unusable login hashes: use the existing Development Demo role selector.
 This creates schema/data only; application startup, connection configuration and external integrations
 are configured separately. No real payment, shipment, refund or payout is performed by this script.
*/

-- SECTION 1: supplied database script, including the approved checkout migration.
USE master;
GO
IF DB_ID(N'AgriSustain') IS NOT NULL
BEGIN
    ALTER DATABASE AgriSustain SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE AgriSustain;
END
GO
CREATE DATABASE AgriSustain;
GO
USE AgriSustain;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- PHÂN HỆ 1: TÀI KHOẢN VÀ HỘI VIÊN (6 Bảng)
-- =========================================================================

-- 1. Bảng User
CREATE TABLE [User] (
    UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    PhoneNumber VARCHAR(20) NULL,
    AvatarUrl NVARCHAR(500) NULL,
    [Role] VARCHAR(50) NOT NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Active',
    IsEmailVerified BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FailedLoginCount INT NOT NULL DEFAULT 0,
    LockedUntil DATETIME2 NULL
);
GO

-- 2. Bảng FarmerProfile (Quan hệ 1 - 0..1 với User)
CREATE TABLE FarmerProfile (
    FarmerProfileId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    FarmName NVARCHAR(200) NOT NULL,
    RepresentativeName NVARCHAR(150) NOT NULL,
    ProductionScale NVARCHAR(200) NULL,
    BusinessLicenseNumber VARCHAR(100) NULL,
    [Address] NVARCHAR(500) NOT NULL,
    ProvinceCode VARCHAR(20) NOT NULL,
    CultivationZone NVARCHAR(200) NULL,
    GPSCoordinates VARCHAR(100) NULL,
    VerificationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedAt DATETIME2 NULL,
    RejectReason NVARCHAR(500) NULL,
    ReputationScore DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT FK_FarmerProfile_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 3. Bảng WholesaleBuyerProfile (Quan hệ 1 - 0..1 với User)
CREATE TABLE WholesaleBuyerProfile (
    WholesaleBuyerProfileId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    CompanyName NVARCHAR(255) NOT NULL,
    TaxCode VARCHAR(50) NOT NULL UNIQUE,
    RepresentativeName NVARCHAR(150) NOT NULL,
    BusinessType NVARCHAR(100) NULL,
    BusinessAddress NVARCHAR(500) NOT NULL,
    DocumentUrl NVARCHAR(500) NULL,
    VerificationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedAt DATETIME2 NULL,
    RejectReason NVARCHAR(500) NULL,
    CONSTRAINT FK_WholesaleBuyerProfile_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 4. Bảng DeliveryAddress (Quan hệ 1 - 0..* từ User)
CREATE TABLE DeliveryAddress (
    DeliveryAddressId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    RecipientName NVARCHAR(150) NOT NULL,
    RecipientPhone VARCHAR(20) NOT NULL,
    AddressDetail NVARCHAR(500) NOT NULL,
    ProvinceCity NVARCHAR(100) NOT NULL,
    District NVARCHAR(100) NOT NULL,
    Ward NVARCHAR(100) NOT NULL,
    IsDefault BIT NOT NULL DEFAULT 0,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Active',
    CONSTRAINT FK_DeliveryAddress_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 5. Bảng B2BSubscriptionPackage
CREATE TABLE B2BSubscriptionPackage (
    PackageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PackageName NVARCHAR(150) NOT NULL,
    PackageDescription NVARCHAR(1000) NULL,
    DurationDays INT NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    UnlocksFarmerDirectContact BIT NOT NULL DEFAULT 0,
    CanDownloadContractTemplate BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    TransactionLimit INT NULL
);
GO

-- 6. Bảng B2BSubscription
CREATE TABLE B2BSubscription (
    B2BSubscriptionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    WholesaleBuyerProfileId BIGINT NOT NULL,
    PackageId BIGINT NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    SubscriptionStatus VARCHAR(50) NOT NULL DEFAULT 'Active',
    AutoRenew BIT NOT NULL DEFAULT 0,
    PriceSnapshot DECIMAL(18,2) NOT NULL,
    LastRenewedAt DATETIME2 NULL,
    CONSTRAINT FK_B2BSubscription_WholesaleBuyer FOREIGN KEY (WholesaleBuyerProfileId) REFERENCES WholesaleBuyerProfile(WholesaleBuyerProfileId),
    CONSTRAINT FK_B2BSubscription_Package FOREIGN KEY (PackageId) REFERENCES B2BSubscriptionPackage(PackageId)
);
GO

-- =========================================================================
-- PHÂN HỆ 2: NÔNG TRẠI, SẢN PHẨM VÀ TRUY XUẤT (11 Bảng)
-- =========================================================================

-- 7. Bảng Category (Đệ quy 0..1 - 0..* danh mục cha con)
CREATE TABLE Category (
    CategoryId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ParentCategoryId BIGINT NULL,
    CategoryName NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [Priority] INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Category_ParentCategory FOREIGN KEY (ParentCategoryId) REFERENCES Category(CategoryId)
);
GO

-- 8. Bảng Product
CREATE TABLE Product (
    ProductId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CategoryId BIGINT NOT NULL,
    FarmerProfileId BIGINT NOT NULL,
    ProductName NVARCHAR(255) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    Unit NVARCHAR(50) NOT NULL,
    OriginProvince NVARCHAR(100) NULL,
    BaseRetailPrice DECIMAL(18,2) NOT NULL,
    WholesaleMinQuantity DECIMAL(18,3) NULL,
    WholesalePrice DECIMAL(18,2) NULL,
    ApprovalStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    IsPublished BIT NOT NULL DEFAULT 0,
    RejectionReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId),
    CONSTRAINT FK_Product_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 9. Bảng CropSeason
CREATE TABLE CropSeason (
    CropSeasonId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NOT NULL,
    SeasonName NVARCHAR(200) NOT NULL,
    CropVariety NVARCHAR(150) NOT NULL,
    CultivatedArea DECIMAL(18,3) NOT NULL,
    PlantingDate DATE NOT NULL,
    EstimatedHarvestDate DATE NOT NULL,
    ActualHarvestDate DATE NULL,
    EstimatedYield DECIMAL(18,3) NOT NULL,
    ActualYield DECIMAL(18,3) NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Planning',
    CONSTRAINT FK_CropSeason_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 10. Bảng FarmCertificate
CREATE TABLE FarmCertificate (
    FarmCertificateId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NOT NULL,
    ReviewedByUserId BIGINT NULL, -- User thẩm định (0..1)
    CertificateType NVARCHAR(100) NOT NULL,
    CertificateNumber VARCHAR(100) NOT NULL,
    IssuedBy NVARCHAR(200) NOT NULL,
    IssueDate DATE NOT NULL,
    ExpiryDate DATE NULL,
    DocumentUrl NVARCHAR(500) NOT NULL,
    VerificationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    RevocationReason NVARCHAR(500) NULL,
    ReviewedAt DATETIME2 NULL,
    CONSTRAINT FK_FarmCertificate_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId),
    CONSTRAINT FK_FarmCertificate_ReviewedByUser FOREIGN KEY (ReviewedByUserId) REFERENCES [User](UserId)
);
GO

-- 11. Bảng ProductImage
CREATE TABLE ProductImage (
    ProductImageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    ImageUrl NVARCHAR(500) NOT NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsPrimary BIT NOT NULL DEFAULT 0,
    AltText NVARCHAR(255) NULL,
    CONSTRAINT FK_ProductImage_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId)
);
GO

-- 12. Bảng PreOrderCampaign (Đã sửa điểm nghẽn 2: Liên kết Product 1 - 0..* và CropSeason 0..1 - 0..*)
CREATE TABLE PreOrderCampaign (
    PreOrderCampaignId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    CropSeasonId BIGINT NULL,
    CampaignName NVARCHAR(255) NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    TargetYield DECIMAL(18,3) NOT NULL,
    CommittedYield DECIMAL(18,3) NOT NULL DEFAULT 0,
    DepositPercentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    PreOrderPrice DECIMAL(18,2) NOT NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Draft',
    CONSTRAINT FK_PreOrderCampaign_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_PreOrderCampaign_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId)
);
GO

-- 13. Bảng HarvestBatch (Đã sửa điểm nghẽn 2: Liên kết PreOrderCampaign 0..1 - 0..*)
CREATE TABLE HarvestBatch (
    HarvestBatchId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    CropSeasonId BIGINT NOT NULL,
    PreOrderCampaignId BIGINT NULL,
    BatchCode VARCHAR(100) NOT NULL UNIQUE,
    EstimatedHarvestDate DATE NULL,
    ActualHarvestDate DATE NULL,
    HarvestYield DECIMAL(18,3) NOT NULL DEFAULT 0,
    ExpirationDate DATE NULL,
    QualityNotes NVARCHAR(500) NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Available',
    StockEnteredAt DATETIME2 NULL,
    ReservedQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    AvailableQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    CONSTRAINT FK_HarvestBatch_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_HarvestBatch_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId),
    CONSTRAINT FK_HarvestBatch_PreOrderCampaign FOREIGN KEY (PreOrderCampaignId) REFERENCES PreOrderCampaign(PreOrderCampaignId)
);
GO

-- 14. Bảng CultivationLog
CREATE TABLE CultivationLog (
    CultivationLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CropSeasonId BIGINT NOT NULL,
    RecordedByUserId BIGINT NOT NULL, -- User ghi nhận (1 - 0..*)
    ActionDate DATETIME2 NOT NULL,
    ActionType NVARCHAR(100) NOT NULL,
    DetailDescription NVARCHAR(1000) NULL,
    InputMaterial NVARCHAR(255) NULL,
    Dosage NVARCHAR(150) NULL,
    PerformedBy NVARCHAR(150) NULL,
    ImageUrl NVARCHAR(500) NULL,
    RecordedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_CultivationLog_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId),
    CONSTRAINT FK_CultivationLog_RecordedByUser FOREIGN KEY (RecordedByUserId) REFERENCES [User](UserId)
);
GO

-- 15. Bảng TraceabilityQRCode (Quan hệ 1 - 0..1 với HarvestBatch)
CREATE TABLE TraceabilityQRCode (
    TraceabilityQRCodeId BIGINT IDENTITY(1,1) PRIMARY KEY,
    HarvestBatchId BIGINT NOT NULL UNIQUE,
    QRContentString NVARCHAR(500) NOT NULL UNIQUE,
    TargetUrl NVARCHAR(500) NOT NULL,
    ScanCount INT NOT NULL DEFAULT 0,
    IsRevoked BIT NOT NULL DEFAULT 0,
    GeneratedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_TraceabilityQRCode_HarvestBatch FOREIGN KEY (HarvestBatchId) REFERENCES HarvestBatch(HarvestBatchId)
);
GO

-- 16. Bảng RegionalPriceBenchmark
CREATE TABLE RegionalPriceBenchmark (
    BenchmarkId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CategoryId BIGINT NOT NULL,
    RegionName NVARCHAR(100) NOT NULL,
    ProvinceName NVARCHAR(100) NOT NULL,
    CommodityName NVARCHAR(150) NOT NULL,
    MinPrice DECIMAL(18,2) NOT NULL,
    MaxPrice DECIMAL(18,2) NOT NULL,
    AveragePrice DECIMAL(18,2) NOT NULL,
    RecordedDate DATE NOT NULL,
    CONSTRAINT FK_RegionalPriceBenchmark_Category FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId)
);
GO

-- 17. Bảng InventoryMovement (Khóa ngoại tới OrderItemBatchAllocation được thêm ở cuối script)
CREATE TABLE InventoryMovement (
    InventoryMovementId BIGINT IDENTITY(1,1) PRIMARY KEY,
    HarvestBatchId BIGINT NOT NULL,
    OrderItemBatchAllocationId BIGINT NULL,
    RecordedByUserId BIGINT NULL, -- User ghi nhận (0..1)
    MovementType VARCHAR(50) NOT NULL,
    QuantityChange DECIMAL(18,3) NOT NULL,
    Reason NVARCHAR(500) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_InventoryMovement_HarvestBatch FOREIGN KEY (HarvestBatchId) REFERENCES HarvestBatch(HarvestBatchId),
    CONSTRAINT FK_InventoryMovement_RecordedByUser FOREIGN KEY (RecordedByUserId) REFERENCES [User](UserId)
);
GO

-- =========================================================================
-- PHÂN HỆ 3: ĐƠN HÀNG VÀ GIAO NHẬN (12 Bảng)
-- =========================================================================

-- 18. Bảng Order
CREATE TABLE [Order] (
    OrderId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BuyerUserId BIGINT NOT NULL, -- User đặt mua (1 - 0..*)
    OrderCode VARCHAR(100) NOT NULL UNIQUE,
    OrderType VARCHAR(50) NOT NULL,
    IsPreOrder BIT NOT NULL DEFAULT 0,
    OrderDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ExpectedDeliveryDate DATETIME2 NULL,
    ShippingAddress NVARCHAR(500) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryFee DECIMAL(18,2) NOT NULL DEFAULT 0,
    FinalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    OrderStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ReservationExpiresAt DATETIME2 NULL,
    RecipientNameSnapshot NVARCHAR(150) NOT NULL,
    RecipientPhoneSnapshot VARCHAR(20) NOT NULL,
    CompletedAt DATETIME2 NULL,
    CONSTRAINT FK_Order_BuyerUser FOREIGN KEY (BuyerUserId) REFERENCES [User](UserId)
);
GO

-- 19. Bảng SellerOrder (Khóa ngoại B2BContractId được thêm ở cuối script)
CREATE TABLE SellerOrder (
    SellerOrderId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId BIGINT NOT NULL,
    FarmerProfileId BIGINT NOT NULL,
    B2BContractId BIGINT NULL, -- Đơn thực hiện hợp đồng (0..1)
    SellerOrderCode VARCHAR(100) NOT NULL UNIQUE,
    Subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountShare DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryFee DECIMAL(18,2) NOT NULL DEFAULT 0,
    FinalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Pending',
    DeliveredAt DATETIME2 NULL,
    ReceivedAt DATETIME2 NULL,
    CONSTRAINT FK_SellerOrder_Order FOREIGN KEY (OrderId) REFERENCES [Order](OrderId),
    CONSTRAINT FK_SellerOrder_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 20. Bảng OrderItem (Khóa ngoại ContractLineId được thêm ở cuối script)
CREATE TABLE OrderItem (
    OrderItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    PreOrderCampaignId BIGINT NULL,
    ContractLineId BIGINT NULL, -- Thực hiện lượng cam kết (0..1)
    Quantity DECIMAL(18,3) NOT NULL,
    UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
    UnitSnapshot NVARCHAR(50) NOT NULL,
    DiscountApplied DECIMAL(18,2) NOT NULL DEFAULT 0,
    SubTotal DECIMAL(18,2) NOT NULL,
    ItemStatus VARCHAR(50) NOT NULL DEFAULT 'Normal',
    CONSTRAINT FK_OrderItem_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_OrderItem_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_OrderItem_PreOrderCampaign FOREIGN KEY (PreOrderCampaignId) REFERENCES PreOrderCampaign(PreOrderCampaignId)
);
GO

-- 21. Bảng OrderItemBatchAllocation
CREATE TABLE OrderItemBatchAllocation (
    OrderItemBatchAllocationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId BIGINT NOT NULL,
    HarvestBatchId BIGINT NOT NULL,
    AllocationSequence INT NOT NULL DEFAULT 1,
    AllocatedQuantity DECIMAL(18,3) NOT NULL,
    AllocationStatus VARCHAR(50) NOT NULL DEFAULT 'Reserved',
    ReservedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ExpiresAt DATETIME2 NULL,
    ReleasedAt DATETIME2 NULL,
    CONSTRAINT FK_BatchAllocation_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_BatchAllocation_HarvestBatch FOREIGN KEY (HarvestBatchId) REFERENCES HarvestBatch(HarvestBatchId)
);
GO

-- 22. Bảng Shipment (Khóa ngoại ContractMilestoneId được thêm ở cuối script)
CREATE TABLE Shipment (
    ShipmentId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NOT NULL,
    ContractMilestoneId BIGINT NULL,
    TrackingNumber VARCHAR(100) NULL UNIQUE,
    PlannedDeliveryAt DATETIME2 NULL,
    ShippedAt DATETIME2 NULL,
    DeliveredAt DATETIME2 NULL,
    ReceivedAt DATETIME2 NULL,
    ShipmentStatus VARCHAR(50) NOT NULL DEFAULT 'Preparing',
    CONSTRAINT FK_Shipment_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId)
);
GO

-- 23. Bảng ShipmentItem
CREATE TABLE ShipmentItem (
    ShipmentItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ShipmentId BIGINT NOT NULL,
    OrderItemBatchAllocationId BIGINT NOT NULL,
    ShippedQuantity DECIMAL(18,3) NOT NULL,
    ReceivedQuantity DECIMAL(18,3) NULL,
    AcceptedQuantity DECIMAL(18,3) NULL,
    QualityResult NVARCHAR(255) NULL,
    ReconciliationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ReconciledAt DATETIME2 NULL,
    CONSTRAINT FK_ShipmentItem_Shipment FOREIGN KEY (ShipmentId) REFERENCES Shipment(ShipmentId),
    CONSTRAINT FK_ShipmentItem_BatchAllocation FOREIGN KEY (OrderItemBatchAllocationId) REFERENCES OrderItemBatchAllocation(OrderItemBatchAllocationId)
);
GO

-- 24. Bảng Cart (Quan hệ 1 - 0..1 với User)
CREATE TABLE Cart (
    CartId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Cart_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 25. Bảng CartItem
CREATE TABLE CartItem (
    CartItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CartId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    PreOrderCampaignId BIGINT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    IsSelected BIT NOT NULL DEFAULT 1,
    AddedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_CartItem_Cart FOREIGN KEY (CartId) REFERENCES Cart(CartId),
    CONSTRAINT FK_CartItem_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_CartItem_PreOrderCampaign FOREIGN KEY (PreOrderCampaignId) REFERENCES PreOrderCampaign(PreOrderCampaignId)
);
GO

-- 26. Bảng ProductReview (Quan hệ 1 - 0..1 với OrderItem)
CREATE TABLE ProductReview (
    ProductReviewId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId BIGINT NOT NULL UNIQUE,
    UserId BIGINT NOT NULL,
    RatingStars TINYINT NOT NULL CHECK (RatingStars BETWEEN 1 AND 5),
    Comment NVARCHAR(1000) NULL,
    ReviewDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    IsPurchasedVerified BIT NOT NULL DEFAULT 1,
    ModerationStatus VARCHAR(50) NOT NULL DEFAULT 'Approved',
    ModerationReason NVARCHAR(500) NULL,
    CONSTRAINT FK_ProductReview_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_ProductReview_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 27. Bảng Voucher (Đã sửa điểm nghẽn 5: FarmerProfileId NULL nếu là voucher của Sàn)
CREATE TABLE Voucher (
    VoucherId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NULL,
    VoucherCode VARCHAR(100) NOT NULL UNIQUE,
    DiscountType VARCHAR(50) NOT NULL,
    DiscountValue DECIMAL(18,2) NOT NULL,
    MaxDiscountAmount DECIMAL(18,2) NULL,
    MinOrderAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    UsageLimit INT NULL,
    UsedCount INT NOT NULL DEFAULT 0,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Voucher_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 28. Bảng VoucherRedemption (Đã sửa điểm nghẽn 5: Áp dụng trên Order tổng và tùy chọn SellerOrder)
CREATE TABLE VoucherRedemption (
    VoucherRedemptionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    VoucherId BIGINT NOT NULL,
    OrderId BIGINT NOT NULL,
    SellerOrderId BIGINT NULL,
    DiscountSnapshot DECIMAL(18,2) NOT NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Reserved',
    ReservedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    AppliedAt DATETIME2 NULL,
    ReleasedAt DATETIME2 NULL,
    CONSTRAINT FK_VoucherRedemption_Voucher FOREIGN KEY (VoucherId) REFERENCES Voucher(VoucherId),
    CONSTRAINT FK_VoucherRedemption_Order FOREIGN KEY (OrderId) REFERENCES [Order](OrderId),
    CONSTRAINT FK_VoucherRedemption_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId)
);
GO

-- 29. Bảng OrderStatusHistory
CREATE TABLE OrderStatusHistory (
    OrderStatusHistoryId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NOT NULL,
    ChangedByUserId BIGINT NULL, -- User đổi trạng thái (0..1)
    OldStatus VARCHAR(50) NULL,
    NewStatus VARCHAR(50) NOT NULL,
    Reason NVARCHAR(500) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_OrderStatusHistory_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_OrderStatusHistory_ChangedByUser FOREIGN KEY (ChangedByUserId) REFERENCES [User](UserId)
);
GO

-- =========================================================================
-- PHÂN HỆ 4: HỢP ĐỒNG BAO TIÊU (8 Bảng)
-- =========================================================================

-- 30. Bảng B2BContract
CREATE TABLE B2BContract (
    B2BContractId BIGINT IDENTITY(1,1) PRIMARY KEY,
    WholesaleBuyerProfileId BIGINT NOT NULL,
    FarmerProfileId BIGINT NOT NULL,
    ContractNumber VARCHAR(100) NOT NULL UNIQUE,
    DepositAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaymentTerms NVARCHAR(1000) NULL,
    DigitalSignatureStatus VARCHAR(50) NOT NULL DEFAULT 'Unsigned',
    SignedDate DATETIME2 NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Draft',
    CurrentVersionNo INT NOT NULL DEFAULT 1,
    ValidFrom DATE NOT NULL,
    ValidUntil DATE NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CompletedAt DATETIME2 NULL,
    CONSTRAINT FK_B2BContract_WholesaleBuyer FOREIGN KEY (WholesaleBuyerProfileId) REFERENCES WholesaleBuyerProfile(WholesaleBuyerProfileId),
    CONSTRAINT FK_B2BContract_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 31. Bảng ContractVersion
CREATE TABLE ContractVersion (
    ContractVersionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    ProposedByUserId BIGINT NOT NULL, -- User đề xuất phiên bản (1 - 0..*)
    VersionNo INT NOT NULL,
    TermsAndConditions NVARCHAR(MAX) NOT NULL,
    LineSnapshotJson NVARCHAR(MAX) NULL CHECK (LineSnapshotJson IS NULL OR ISJSON(LineSnapshotJson) = 1),
    DocumentHash VARCHAR(256) NULL,
    VersionStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FeedbackReason NVARCHAR(500) NULL,
    CONSTRAINT FK_ContractVersion_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_ContractVersion_ProposedByUser FOREIGN KEY (ProposedByUserId) REFERENCES [User](UserId)
);
GO

-- 32. Bảng ContractLine (Đã sửa điểm nghẽn 3: Dòng cam kết sản lượng gắn trực tiếp B2BContract)
CREATE TABLE ContractLine (
    ContractLineId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    CropSeasonId BIGINT NULL,
    CommittedQuantity DECIMAL(18,3) NOT NULL,
    UnitSnapshot NVARCHAR(50) NOT NULL,
    QualitySpecification NVARCHAR(500) NULL,
    PriceType VARCHAR(50) NOT NULL,
    AgreedPrice DECIMAL(18,2) NULL,
    MinPrice DECIMAL(18,2) NULL,
    MaxPrice DECIMAL(18,2) NULL,
    PriceFormula NVARCHAR(255) NULL,
    CONSTRAINT FK_ContractLine_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_ContractLine_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_ContractLine_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId)
);
GO

-- 33. Bảng ContractMilestone
CREATE TABLE ContractMilestone (
    ContractMilestoneId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    ContractLineId BIGINT NULL,
    MilestoneTitle NVARCHAR(200) NOT NULL,
    MilestoneType VARCHAR(50) NOT NULL,
    DueDate DATE NOT NULL,
    Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryQuantity DECIMAL(18,3) NULL,
    FulfillmentStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    PaymentStatus VARCHAR(50) NOT NULL DEFAULT 'Unpaid',
    DeliveredQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    AcceptedQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_ContractMilestone_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_ContractMilestone_ContractLine FOREIGN KEY (ContractLineId) REFERENCES ContractLine(ContractLineId)
);
GO

-- 34. Bảng ContractSignature
CREATE TABLE ContractSignature (
    ContractSignatureId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ContractVersionId BIGINT NOT NULL,
    SignedByUserId BIGINT NOT NULL, -- User xác nhận ký (1 - 0..*)
    PartyRole VARCHAR(50) NOT NULL,
    ConfirmationMethod VARCHAR(50) NOT NULL,
    ConfirmedAt DATETIME2 NULL,
    ConfirmationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_ContractSignature_ContractVersion FOREIGN KEY (ContractVersionId) REFERENCES ContractVersion(ContractVersionId),
    CONSTRAINT FK_ContractSignature_SignedByUser FOREIGN KEY (SignedByUserId) REFERENCES [User](UserId)
);
GO

-- 35. Bảng ContractTerminationRequest
CREATE TABLE ContractTerminationRequest (
    ContractTerminationRequestId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    RequestedByUserId BIGINT NOT NULL, -- User yêu cầu chấm dứt (1 - 0..*)
    Reason NVARCHAR(1000) NOT NULL,
    EvidenceUrl NVARCHAR(500) NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    RequestStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    SettlementAmount DECIMAL(18,2) NULL,
    ResolvedAt DATETIME2 NULL,
    CONSTRAINT FK_TerminationRequest_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_TerminationRequest_RequestedByUser FOREIGN KEY (RequestedByUserId) REFERENCES [User](UserId)
);
GO

-- 36. Bảng Invoice (Đã sửa điểm nghẽn 1: Hóa đơn theo SellerOrder hoặc ContractMilestone)
CREATE TABLE Invoice (
    InvoiceId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NULL,
    ContractMilestoneId BIGINT NULL,
    InvoiceNumber VARCHAR(100) NOT NULL UNIQUE,
    IssuedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DueAt DATETIME2 NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    InvoiceStatus VARCHAR(50) NOT NULL DEFAULT 'Issued',
    CONSTRAINT FK_Invoice_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_Invoice_ContractMilestone FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId)
);
GO

-- 37. Bảng InvoiceLine (Đã sửa điểm nghẽn 1: Liên kết OrderItem hoặc ShipmentItem)
CREATE TABLE InvoiceLine (
    InvoiceLineId BIGINT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    OrderItemId BIGINT NULL,
    ShipmentItemId BIGINT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
    LineAmount DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_InvoiceLine_Invoice FOREIGN KEY (InvoiceId) REFERENCES Invoice(InvoiceId),
    CONSTRAINT FK_InvoiceLine_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_InvoiceLine_ShipmentItem FOREIGN KEY (ShipmentItemId) REFERENCES ShipmentItem(ShipmentItemId)
);
GO

-- =========================================================================
-- PHÂN HỆ 5: THANH TOÁN, QUYẾT TOÁN VÀ KHIẾU NẠI (9 Bảng)
-- =========================================================================

-- 38. Bảng PaymentTransaction (Đã sửa điểm nghẽn 4: Thêm B2BContractId cho đặt cọc hợp đồng)
CREATE TABLE PaymentTransaction (
    PaymentTransactionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId BIGINT NULL,
    B2BSubscriptionId BIGINT NULL,
    ContractMilestoneId BIGINT NULL,
    B2BContractId BIGINT NULL,
    TransactionCode VARCHAR(100) NOT NULL UNIQUE,
    GatewayProvider VARCHAR(100) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentType VARCHAR(50) NOT NULL,
    PaymentStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    WebhookPayload NVARCHAR(MAX) NULL,
    PaidAt DATETIME2 NULL,
    PaymentPurpose VARCHAR(100) NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ExpiresAt DATETIME2 NULL,
    ReconciledAt DATETIME2 NULL,
    CONSTRAINT FK_PaymentTransaction_Order FOREIGN KEY (OrderId) REFERENCES [Order](OrderId),
    CONSTRAINT FK_PaymentTransaction_B2BSubscription FOREIGN KEY (B2BSubscriptionId) REFERENCES B2BSubscription(B2BSubscriptionId),
    CONSTRAINT FK_PaymentTransaction_ContractMilestone FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId),
    CONSTRAINT FK_PaymentTransaction_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId)
);
GO

-- 39. Bảng DisputeTicket
CREATE TABLE DisputeTicket (
    DisputeTicketId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SubmittedByUserId BIGINT NOT NULL, -- User gửi khiếu nại (1 - 0..*)
    AssignedStaffUserId BIGINT NULL,   -- Nhân viên được phân công (0..1 - 0..*)
    SellerOrderId BIGINT NULL,
    B2BContractId BIGINT NULL,
    DisputeReason NVARCHAR(255) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    TicketStatus VARCHAR(50) NOT NULL DEFAULT 'Open',
    FaultParty VARCHAR(50) NULL,
    RefundAmount DECIMAL(18,2) NULL,
    ResolutionNotes NVARCHAR(1000) NULL,
    ResolvedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DecisionAt DATETIME2 NULL,
    CONSTRAINT FK_DisputeTicket_SubmittedByUser FOREIGN KEY (SubmittedByUserId) REFERENCES [User](UserId),
    CONSTRAINT FK_DisputeTicket_AssignedStaffUser FOREIGN KEY (AssignedStaffUserId) REFERENCES [User](UserId),
    CONSTRAINT FK_DisputeTicket_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_DisputeTicket_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId)
);
GO

-- 40. Bảng DisputeEvidence
CREATE TABLE DisputeEvidence (
    DisputeEvidenceId BIGINT IDENTITY(1,1) PRIMARY KEY,
    DisputeTicketId BIGINT NOT NULL,
    SubmittedByUserId BIGINT NOT NULL, -- User nộp bằng chứng (1 - 0..*)
    FileUrl NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    SubmittedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_DisputeEvidence_DisputeTicket FOREIGN KEY (DisputeTicketId) REFERENCES DisputeTicket(DisputeTicketId),
    CONSTRAINT FK_DisputeEvidence_SubmittedByUser FOREIGN KEY (SubmittedByUserId) REFERENCES [User](UserId)
);
GO

-- 41. Bảng RefundTransaction
CREATE TABLE RefundTransaction (
    RefundTransactionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PaymentTransactionId BIGINT NOT NULL,
    DisputeTicketId BIGINT NULL,
    ContractTerminationRequestId BIGINT NULL,
    ProcessedByUserId BIGINT NOT NULL, -- User xử lý hoàn tiền (1 - 0..*)
    RefundCode VARCHAR(100) NOT NULL UNIQUE,
    Amount DECIMAL(18,2) NOT NULL,
    Reason NVARCHAR(500) NULL,
    RefundStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ProcessedAt DATETIME2 NULL,
    GatewayReference VARCHAR(150) NULL,
    CONSTRAINT FK_RefundTransaction_PaymentTransaction FOREIGN KEY (PaymentTransactionId) REFERENCES PaymentTransaction(PaymentTransactionId),
    CONSTRAINT FK_RefundTransaction_DisputeTicket FOREIGN KEY (DisputeTicketId) REFERENCES DisputeTicket(DisputeTicketId),
    CONSTRAINT FK_RefundTransaction_TerminationRequest FOREIGN KEY (ContractTerminationRequestId) REFERENCES ContractTerminationRequest(ContractTerminationRequestId),
    CONSTRAINT FK_RefundTransaction_ProcessedByUser FOREIGN KEY (ProcessedByUserId) REFERENCES [User](UserId)
);
GO

-- 42. Bảng CommissionPolicy
CREATE TABLE CommissionPolicy (
    CommissionPolicyId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CategoryId BIGINT NOT NULL,
    ApplicableOrderType VARCHAR(50) NOT NULL,
    CommissionRate DECIMAL(5,2) NOT NULL,
    EffectiveDate DATE NOT NULL,
    Note NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_CommissionPolicy_Category FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId)
);
GO

-- 43. Bảng FarmerPayout
CREATE TABLE FarmerPayout (
    FarmerPayoutId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NOT NULL,
    ApprovedByUserId BIGINT NULL, -- User duyệt quyết toán (0..1 - 0..*)
    PayoutCode VARCHAR(100) NOT NULL UNIQUE,
    Amount DECIMAL(18,2) NOT NULL,
    DeductedCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetAmount DECIMAL(18,2) NOT NULL,
    PayoutStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    BankTransferRef VARCHAR(150) NULL,
    ProcessedAt DATETIME2 NULL,
    ApprovedAt DATETIME2 NULL,
    EligibleAt DATETIME2 NULL,
    CONSTRAINT FK_FarmerPayout_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId),
    CONSTRAINT FK_FarmerPayout_ApprovedByUser FOREIGN KEY (ApprovedByUserId) REFERENCES [User](UserId)
);
GO

-- 44. Bảng PayoutItem
CREATE TABLE PayoutItem (
    PayoutItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerPayoutId BIGINT NOT NULL,
    OrderItemId BIGINT NULL,
    ContractMilestoneId BIGINT NULL,
    CommissionPolicyId BIGINT NULL,
    GrossAmount DECIMAL(18,2) NOT NULL,
    CommissionRateSnapshot DECIMAL(5,2) NOT NULL DEFAULT 0,
    CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetAmount DECIMAL(18,2) NOT NULL,
    SourceAmountSettled DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_PayoutItem_FarmerPayout FOREIGN KEY (FarmerPayoutId) REFERENCES FarmerPayout(FarmerPayoutId),
    CONSTRAINT FK_PayoutItem_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_PayoutItem_ContractMilestone FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId),
    CONSTRAINT FK_PayoutItem_CommissionPolicy FOREIGN KEY (CommissionPolicyId) REFERENCES CommissionPolicy(CommissionPolicyId)
);
GO

-- 45. Bảng Notification
CREATE TABLE [Notification] (
    NotificationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Title NVARCHAR(255) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    NotificationType VARCHAR(50) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    SentAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DeliveryChannel VARCHAR(50) NOT NULL,
    DeliveryStatus VARCHAR(50) NOT NULL DEFAULT 'Sent',
    RetryCount INT NOT NULL DEFAULT 0,
    RelatedRecordType VARCHAR(100) NULL,
    CONSTRAINT FK_Notification_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 46. Bảng AuditLog
CREATE TABLE AuditLog (
    AuditLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PerformedByUserId BIGINT NULL, -- Người thực hiện hoặc NULL nếu là Hệ thống (0..1 - 0..*)
    EntityType VARCHAR(100) NOT NULL,
    [Action] VARCHAR(100) NOT NULL,
    OldValue NVARCHAR(MAX) NULL,
    NewValue NVARCHAR(MAX) NULL,
    Reason NVARCHAR(500) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AuditLog_PerformedByUser FOREIGN KEY (PerformedByUserId) REFERENCES [User](UserId)
);
GO

-- =========================================================================
-- RÀNG BUỘC KHÓA NGOẠI LIÊN PHÂN HỆ (CROSS-MODULE FOREIGN KEYS)
-- =========================================================================

-- 1. InventoryMovement (Phân hệ 2) -> OrderItemBatchAllocation (Phân hệ 3)
ALTER TABLE InventoryMovement
ADD CONSTRAINT FK_InventoryMovement_BatchAllocation
FOREIGN KEY (OrderItemBatchAllocationId) REFERENCES OrderItemBatchAllocation(OrderItemBatchAllocationId);
GO

-- 2. SellerOrder (Phân hệ 3) -> B2BContract (Phân hệ 4)
ALTER TABLE SellerOrder
ADD CONSTRAINT FK_SellerOrder_B2BContract
FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId);
GO

-- 3. OrderItem (Phân hệ 3) -> ContractLine (Phân hệ 4)
ALTER TABLE OrderItem
ADD CONSTRAINT FK_OrderItem_ContractLine
FOREIGN KEY (ContractLineId) REFERENCES ContractLine(ContractLineId);
GO

-- 4. Shipment (Phân hệ 3) -> ContractMilestone (Phân hệ 4)
ALTER TABLE Shipment
ADD CONSTRAINT FK_Shipment_ContractMilestone
FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId);
GO
GO
/* SQLCMD mode: pass -v TargetDatabase="AgriSustain" and select the same database with -d.
   Upgrade ONLY the supplied 46-table BIGINT schema. No legacy data is rewritten.
   Re-runnable; all schema changes commit together. Read ../README.md for decisions.
   DB-06 request-key/unique-quote and DB-09 reward tables are deliberately deferred.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;


IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Category', N'U') IS NULL
    OR OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
    OR OBJECT_ID(N'dbo.HarvestBatch', N'U') IS NULL
    OR OBJECT_ID(N'dbo.CartItem', N'U') IS NULL
    OR OBJECT_ID(N'dbo.OrderItemBatchAllocation', N'U') IS NULL
    OR OBJECT_ID(N'dbo.VoucherRedemption', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
    THROW 51001, 'Expected the reviewed User/Category/Order 46-table schema, not Accounts/Categories/Orders.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UserId'
      AND TYPE_NAME(user_type_id) = N'bigint')
    THROW 51002, 'Expected BIGINT UserId from the supplied baseline.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.HarvestBatch
        WHERE AvailableQuantity < 0 OR ReservedQuantity < 0)
        THROW 51003, 'Reconcile negative batch quantities before migration.', 1;

    -- DB-08: the snapshot includes FEFO allocations, NOT a stock reservation.
    IF OBJECT_ID(N'dbo.RetailCheckoutQuote', N'U') IS NULL
        EXEC(N'CREATE TABLE dbo.RetailCheckoutQuote
        (
            QuoteId uniqueidentifier NOT NULL CONSTRAINT PK_RetailCheckoutQuote PRIMARY KEY,
            BuyerUserId bigint NOT NULL,
            AcceptedSnapshotJson nvarchar(max) NOT NULL,
            Fingerprint char(64) NOT NULL,
            CreatedAt datetime2 NOT NULL
                CONSTRAINT DF_RetailQuote_Created DEFAULT SYSUTCDATETIME(),
            ExpiresAt datetime2 NOT NULL,
            CONSTRAINT CK_RetailQuote_Json CHECK (ISJSON(AcceptedSnapshotJson) = 1),
            CONSTRAINT CK_RetailQuote_Expiry CHECK (ExpiresAt > CreatedAt),
            CONSTRAINT FK_RetailQuote_Buyer FOREIGN KEY (BuyerUserId)
                REFERENCES dbo.[User](UserId)
        )');

    -- DB-07: NULL means an unknown legacy snapshot, never invented historical values.
    -- DB-09: point fields are order snapshots, not a balance/loyalty implementation.
    CREATE TABLE #ColumnsToAdd
    (
        TableName sysname NOT NULL,
        ColumnName sysname NOT NULL,
        ColumnDefinition nvarchar(128) NOT NULL
    );
    INSERT INTO #ColumnsToAdd (TableName, ColumnName, ColumnDefinition) VALUES
        (N'Order', N'RetailQuoteId', N'uniqueidentifier NULL'),
        (N'Order', N'DeliveryMethod', N'varchar(50) NULL'),
        (N'Order', N'PaymentMethod', N'varchar(50) NULL'),
        (N'Order', N'OrderNotes', N'nvarchar(300) NULL'),
        (N'Order', N'VoucherCodeSnapshot', N'varchar(100) NULL'),
        (N'Order', N'VoucherDiscount', N'decimal(18,2) NULL'),
        (N'Order', N'PointDiscount', N'decimal(18,2) NULL'),
        (N'Order', N'RedeemedPoints', N'int NULL'),
        (N'Order', N'ProvinceCodeSnapshot', N'varchar(20) NULL'),
        (N'Order', N'ProvinceNameSnapshot', N'nvarchar(100) NULL'),
        (N'Order', N'WardCodeSnapshot', N'varchar(20) NULL'),
        (N'Order', N'WardNameSnapshot', N'nvarchar(100) NULL'),
        (N'Order', N'AddressDetailSnapshot', N'nvarchar(255) NULL'),
        (N'Order', N'AddressCatalogVersionSnapshot', N'varchar(100) NULL'),
        (N'SellerOrder', N'SellerNameSnapshot', N'nvarchar(200) NULL'),
        (N'OrderItem', N'ProductNameSnapshot', N'nvarchar(255) NULL'),
        (N'DeliveryAddress', N'ProvinceCode', N'varchar(20) NULL'),
        (N'DeliveryAddress', N'WardCode', N'varchar(20) NULL'),
        (N'DeliveryAddress', N'AddressCatalogVersion', N'varchar(100) NULL'),
        (N'VoucherRedemption', N'ExpiresAt', N'datetime2 NULL'),
        (N'AuditLog', N'EntityId', N'bigint NULL'),
        (N'AuditLog', N'TraceId', N'nvarchar(64) NULL');

    DECLARE @TableName sysname, @ColumnName sysname, @Definition nvarchar(128);
    DECLARE @Sql nvarchar(max);
    DECLARE column_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName, ColumnName, ColumnDefinition FROM #ColumnsToAdd;
    OPEN column_cursor;
    FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName, @Definition;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF COL_LENGTH(N'dbo.' + QUOTENAME(@TableName), @ColumnName) IS NULL
        BEGIN
            SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
                + N' ADD ' + QUOTENAME(@ColumnName) + N' ' + @Definition;
            EXEC sys.sp_executesql @Sql;
        END;
        FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName, @Definition;
    END;
    CLOSE column_cursor;
    DEALLOCATE column_cursor;
    DROP TABLE #ColumnsToAdd;

    -- Two-level province/ward addresses. Preserve existing District text verbatim.
    IF EXISTS (SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.DeliveryAddress') AND name = N'District'
          AND is_nullable = 0)
        ALTER TABLE dbo.DeliveryAddress ALTER COLUMN District nvarchar(100) NULL;

    IF OBJECT_ID(N'dbo.FK_Order_RetailQuote', N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.[Order] WITH CHECK ADD CONSTRAINT FK_Order_RetailQuote
            FOREIGN KEY (RetailQuoteId) REFERENCES dbo.RetailCheckoutQuote(QuoteId)');

    -- DB-11. Conditional stock updates and transactions remain application duties.
    IF OBJECT_ID(N'dbo.CK_HarvestBatch_Nonnegative', N'C') IS NULL
        ALTER TABLE dbo.HarvestBatch WITH CHECK ADD CONSTRAINT CK_HarvestBatch_Nonnegative
            CHECK (AvailableQuantity >= 0 AND ReservedQuantity >= 0);

    -- DB-05 B: existing OrderItemBatchAllocation supports multiple batches per line.
    -- FEFO allocation/revalidation is application logic; no CartItem batch column.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.HarvestBatch') AND name = N'IX_HarvestBatch_Fefo')
        CREATE INDEX IX_HarvestBatch_Fefo
            ON dbo.HarvestBatch(ProductId, [Status], ExpirationDate, HarvestBatchId)
            INCLUDE (AvailableQuantity, ReservedQuantity, BatchCode);

    -- DB-10/12: supports quota queries. It does not itself reserve/release a voucher.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.VoucherRedemption')
          AND name = N'IX_VoucherRedemption_Quota')
        EXEC(N'CREATE INDEX IX_VoucherRedemption_Quota
            ON dbo.VoucherRedemption(VoucherId, [Status], ExpiresAt) INCLUDE (OrderId)');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RetailCheckoutQuote')
          AND name = N'IX_RetailQuote_Buyer_Expiry')
        EXEC(N'CREATE INDEX IX_RetailQuote_Buyer_Expiry
            ON dbo.RetailCheckoutQuote(BuyerUserId, ExpiresAt)');

    -- DB-15: change defaults only for timestamps read/written by these three UCs.
    -- Existing values and DATE-only harvest/expiration fields are never converted.
    DECLARE @DefaultName sysname;
    DECLARE default_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT t.name, c.name, d.name
        FROM sys.default_constraints d
        JOIN sys.columns c ON c.object_id = d.parent_object_id
            AND c.column_id = d.parent_column_id
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'dbo' AND LOWER(d.definition) LIKE N'%sysdatetime()%'
          AND t.name IN (N'User', N'Product', N'Cart', N'CartItem', N'InventoryMovement',
              N'Order', N'OrderItemBatchAllocation', N'VoucherRedemption', N'AuditLog');
    OPEN default_cursor;
    FETCH NEXT FROM default_cursor INTO @TableName, @ColumnName, @DefaultName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
            + N' DROP CONSTRAINT ' + QUOTENAME(@DefaultName) + N'; ALTER TABLE dbo.'
            + QUOTENAME(@TableName) + N' ADD CONSTRAINT ' + QUOTENAME(@DefaultName)
            + N' DEFAULT SYSUTCDATETIME() FOR ' + QUOTENAME(@ColumnName);
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM default_cursor INTO @TableName, @ColumnName, @DefaultName;
    END;
    CLOSE default_cursor;
    DEALLOCATE default_cursor;

    -- Category: existing NO ACTION FKs already block deletion with products/other refs.
    -- Application must use IsActive=false if products exist and audit successful DELETE.
    -- Do not add image/archive/name-unique/Version columns or constraints (DB-01..04).
    -- DB-14 auth sessions, DB-16 staff permission matrix and subscription commission
    -- integration are external contracts; do not invent schema or rewrite legacy roles.
    -- Retail writers explicitly supply OrderType=Retail and OrderStatus=PendingPayment;
    -- leave the shared Pending default intact for other UCs.
    COMMIT TRANSACTION;
    SELECT DB_NAME() AS DatabaseName, N'20261018_approved_three_uc' AS Migration,
        N'Applied' AS Result;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
-- SECTION 2: schema for UC-FAR-03, UC-FAR-06 and UC-RC-01.
-- Additive upgrade for UC-FAR-03, UC-FAR-06, UC-RC-01. Run in the chosen database.
-- Existing cart rows remain product-based until the customer selects an actual batch.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.CommerceSubmission') IS NULL CREATE TABLE dbo.CommerceSubmission(
 ActorId BIGINT NOT NULL REFERENCES dbo.[User](UserId), RequestKey UNIQUEIDENTIFIER NOT NULL,
 ProductId BIGINT NOT NULL REFERENCES dbo.Product(ProductId), PRIMARY KEY(ActorId,RequestKey));
IF COL_LENGTH('dbo.Product','Revision') IS NULL ALTER TABLE dbo.Product ADD Revision BIGINT NOT NULL CONSTRAINT DF_Product_Revision DEFAULT 0;
IF COL_LENGTH('dbo.Product','QuantityStep') IS NULL ALTER TABLE dbo.Product ADD QuantityStep DECIMAL(18,3) NOT NULL CONSTRAINT DF_Product_QuantityStep DEFAULT 1 CONSTRAINT CK_Product_QuantityStep CHECK(QuantityStep>0);
IF COL_LENGTH('dbo.CartItem','HarvestBatchId') IS NULL ALTER TABLE dbo.CartItem ADD HarvestBatchId BIGINT NULL CONSTRAINT FK_CartItem_HarvestBatch REFERENCES dbo.HarvestBatch(HarvestBatchId);
IF COL_LENGTH('dbo.SellerOrder','RequiredPaymentAmount') IS NULL ALTER TABLE dbo.SellerOrder ADD RequiredPaymentAmount DECIMAL(18,2) NULL CONSTRAINT CK_SellerOrder_RequiredPayment CHECK(RequiredPaymentAmount>=0);
IF OBJECT_ID('dbo.CommerceMedia') IS NULL CREATE TABLE dbo.CommerceMedia(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, OwnerUserId BIGINT NOT NULL REFERENCES dbo.[User](UserId),
 ContentType NVARCHAR(50) NOT NULL, Content VARBINARY(MAX) NOT NULL, PublicProductImage BIT NOT NULL,
 CONSTRAINT CK_CommerceMedia_Size CHECK(DATALENGTH(Content) BETWEEN 8 AND 5242880));
IF OBJECT_ID('dbo.CommerceEvidenceRequest') IS NULL CREATE TABLE dbo.CommerceEvidenceRequest(
 Id BIGINT IDENTITY PRIMARY KEY, DisputeTicketId BIGINT NOT NULL REFERENCES dbo.DisputeTicket(DisputeTicketId),
 RequestedUserId BIGINT NOT NULL REFERENCES dbo.[User](UserId), RequestedAt DATETIME2 NOT NULL,
 RespondedAt DATETIME2 NULL, Response NVARCHAR(1000) NULL);
IF OBJECT_ID('dbo.CommerceRefundRequest') IS NULL CREATE TABLE dbo.CommerceRefundRequest(
 Id BIGINT IDENTITY PRIMARY KEY, SellerOrderId BIGINT NOT NULL UNIQUE REFERENCES dbo.SellerOrder(SellerOrderId),
 Reason NVARCHAR(1300) NOT NULL, Status NVARCHAR(50) NOT NULL, CreatedAt DATETIME2 NOT NULL);
COMMIT;
GO
-- SECTION 3: optional synthetic local demonstration data.
DECLARE @LoadDemoData bit = 1; -- Change to 0 for an empty database.
IF @LoadDemoData=1
BEGIN
/*
  Synthetic local demonstration data for UC-ADM-03, UC-ADM-05 and UC-RC-02.
  Runs after the complete schema above. No SQLCMD variables are required here.
  This script inserts missing samples only: it never resets accounts, stock or carts.
  Every lookup uses a semantic seed key, not an assumed IDENTITY sequence.
  PasswordHash is deliberately unusable: the missing login module remains a mock.
  No reward balance, fee policy, accepted quote, payment or completed order is invented.

  sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 ^
    -i AgriSustain_Full_Recreate.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AgriSustain' OR DB_NAME() IN ('master','model','msdb','tempdb')
    THROW 51100, 'Select the explicitly named application database.', 1;
IF OBJECT_ID('dbo.RetailCheckoutQuote','U') IS NULL
    OR COL_LENGTH('dbo.DeliveryAddress','ProvinceCode') IS NULL
    THROW 51101, 'Apply the approved 47-table schema before seeding.', 1;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, @Now);
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'AgriSustain.ThreeUcDemoSeed',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
    IF @LockResult < 0 THROW 51102, 'Another seed process holds the demonstration lock.', 1;

    DECLARE @Users TABLE
    (
        SeedKey nvarchar(100) PRIMARY KEY, FullName nvarchar(150),
        RoleName varchar(50), StatusName varchar(50), LockHours int, Failures int
    );
    INSERT INTO @Users VALUES
        (N'demo3uc_admin',N'Quản trị viên demo','SystemAdmin','Active',0,0),
        (N'demo3uc_admin_backup',N'Quản trị viên dự phòng','SystemAdmin','Active',0,0),
        (N'demo3uc_retail',N'Khách mua demo','RetailCustomer','Active',0,0),
        (N'demo3uc_retail_other',N'Khách mua đối chứng','RetailCustomer','Active',0,0),
        (N'demo3uc_locked',N'Tài khoản khóa tạm','RetailCustomer','Active',24,5),
        (N'demo3uc_support',N'Hỗ trợ khách hàng demo','CustomerSupport','Active',0,0),
        (N'demo3uc_farmer_green',N'HTX Rau Xanh','Farmer','Active',0,0),
        (N'demo3uc_farmer_gold',N'Nông trại Mùa Vàng','Farmer','Active',0,0),
        (N'demo3uc_wholesale',N'Doanh nghiệp thu mua','WholesaleBuyer','Active',0,0),
        (N'demo3uc_suspended',N'Tài khoản tạm ngưng','RetailCustomer','Suspended',0,0);
    INSERT INTO dbo.[User]
        (Username,Email,PasswordHash,FullName,PhoneNumber,Role,Status,
         IsEmailVerified,CreatedAt,FailedLoginCount,LockedUntil)
    SELECT s.SeedKey,s.SeedKey+N'@example.test',N'!DEMO-NO-LOGIN-HASH!',s.FullName,
        '0901234567',s.RoleName,s.StatusName,1,DATEADD(day,-10,@Now),s.Failures,
        CASE WHEN s.LockHours>0 THEN DATEADD(hour,s.LockHours,@Now) END
    FROM @Users s WHERE NOT EXISTS
        (SELECT 1 FROM dbo.[User] u WHERE u.Username=s.SeedKey);

    -- Local demonstration farmer profiles for the current development screens.
    INSERT INTO dbo.FarmerProfile
        (UserId,FarmName,RepresentativeName,Address,ProvinceCode,
         VerificationStatus,ApprovedAt,ReputationScore)
    SELECT u.UserId,u.FullName,N'Đại diện dữ liệu mẫu',N'Địa chỉ mô phỏng cho ba UC',
        '12','Approved',@Now,4.50
    FROM dbo.[User] u WHERE u.Username IN
        (N'demo3uc_farmer_green',N'demo3uc_farmer_gold')
      AND NOT EXISTS (SELECT 1 FROM dbo.FarmerProfile f WHERE f.UserId=u.UserId);

    DECLARE @Categories TABLE
    (SeedKey nvarchar(100) PRIMARY KEY,Name nvarchar(150),ParentKey nvarchar(100),
        Priority int,IsActive bit);
    INSERT INTO @Categories VALUES
        (N'vegetables',N'[DEMO] Rau củ',NULL,1,1),
        (N'fruit',N'[DEMO] Trái cây',NULL,2,1),
        (N'leaf',N'[DEMO] Rau ăn lá',N'vegetables',3,1),
        (N'unused',N'[DEMO] Danh mục chưa dùng',NULL,4,1),
        (N'hidden',N'[DEMO] Danh mục đang ẩn',NULL,5,0),
        (N'unpublished',N'[DEMO] Có sản phẩm chưa công bố',NULL,6,1);
    INSERT INTO dbo.Category(CategoryName,Description,Priority,IsActive)
    SELECT s.Name,N'DEMO-THREE-UC:'+s.SeedKey,s.Priority,s.IsActive
    FROM @Categories s WHERE s.ParentKey IS NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.Category c WHERE c.Description=N'DEMO-THREE-UC:'+s.SeedKey);
    INSERT INTO dbo.Category(ParentCategoryId,CategoryName,Description,Priority,IsActive)
    SELECT parent.CategoryId,s.Name,N'DEMO-THREE-UC:'+s.SeedKey,s.Priority,s.IsActive
    FROM @Categories s JOIN dbo.Category parent
        ON parent.Description=N'DEMO-THREE-UC:'+s.ParentKey
    WHERE s.ParentKey IS NOT NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.Category c WHERE c.Description=N'DEMO-THREE-UC:'+s.SeedKey);

    -- HarvestBatch requires a real CropSeason FK. These are static prerequisite fixtures.
    INSERT INTO dbo.CropSeason
        (FarmerProfileId,SeasonName,CropVariety,CultivatedArea,PlantingDate,
         EstimatedHarvestDate,ActualHarvestDate,EstimatedYield,ActualYield,Status)
    SELECT f.FarmerProfileId,N'DEMO-THREE-UC: season',N'Giống trồng mô phỏng',1,
        DATEADD(day,-60,@Today),@Today,@Today,500,500,'Harvested'
    FROM dbo.FarmerProfile f JOIN dbo.[User] u ON u.UserId=f.UserId
    WHERE u.Username IN (N'demo3uc_farmer_green',N'demo3uc_farmer_gold')
      AND NOT EXISTS (SELECT 1 FROM dbo.CropSeason cs
          WHERE cs.FarmerProfileId=f.FarmerProfileId
            AND cs.SeasonName=N'DEMO-THREE-UC: season');

    DECLARE @Products TABLE
    (SeedKey nvarchar(100) PRIMARY KEY,Name nvarchar(255),CategoryKey nvarchar(100),
        SellerKey nvarchar(100),Price decimal(18,2),TierQty decimal(18,3),
        TierPrice decimal(18,2),Approved varchar(50),Published bit);
    INSERT INTO @Products VALUES
        (N'greens',N'Cải ngọt hữu cơ',N'leaf',N'demo3uc_farmer_green',30000,5,25000,'Approved',1),
        (N'orange',N'Cam sành',N'fruit',N'demo3uc_farmer_gold',40000,10,35000,'Approved',1),
        (N'carrot',N'Cà rốt',N'vegetables',N'demo3uc_farmer_green',20000,NULL,NULL,'Approved',1),
        (N'pending',N'Rau mẫu chưa công bố',N'unpublished',N'demo3uc_farmer_green',15000,NULL,NULL,'Pending',0),
        (N'expired',N'Rau mẫu chỉ có lô hết hạn',N'leaf',N'demo3uc_farmer_green',10000,NULL,NULL,'Approved',1);
    INSERT INTO dbo.Product
        (CategoryId,FarmerProfileId,ProductName,Description,Unit,OriginProvince,
         BaseRetailPrice,WholesaleMinQuantity,WholesalePrice,ApprovalStatus,IsPublished,CreatedAt)
    SELECT c.CategoryId,f.FarmerProfileId,s.Name,N'DEMO-THREE-UC:'+s.SeedKey,N'kg',
        N'Thành phố Hồ Chí Minh',s.Price,s.TierQty,s.TierPrice,s.Approved,s.Published,@Now
    FROM @Products s JOIN dbo.Category c
        ON c.Description=N'DEMO-THREE-UC:'+s.CategoryKey
    JOIN dbo.[User] u ON u.Username=s.SellerKey
    JOIN dbo.FarmerProfile f ON f.UserId=u.UserId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Product p
        WHERE p.Description=N'DEMO-THREE-UC:'+s.SeedKey);

    DECLARE @Batches TABLE
    (Code varchar(100) PRIMARY KEY,ProductKey nvarchar(100),Quantity decimal(18,3),ExpiryDays int);
    INSERT INTO @Batches VALUES
        ('DEMO3UC-RAU-FIRST',N'greens',3,10),
        ('DEMO3UC-RAU-SECOND',N'greens',20,20),
        ('DEMO3UC-CAM',N'orange',20,30),
        ('DEMO3UC-CAROT',N'carrot',20,30),
        ('DEMO3UC-EXPIRED',N'expired',10,-1);
    INSERT INTO dbo.HarvestBatch
        (ProductId,CropSeasonId,BatchCode,ActualHarvestDate,HarvestYield,
         ExpirationDate,Status,StockEnteredAt,AvailableQuantity,ReservedQuantity,QualityNotes)
    SELECT p.ProductId,cs.CropSeasonId,s.Code,@Today,s.Quantity,
        DATEADD(day,s.ExpiryDays,@Today),'Available',@Now,s.Quantity,0,
        N'DEMO-THREE-UC: static inventory prerequisite'
    FROM @Batches s JOIN dbo.Product p ON p.Description=N'DEMO-THREE-UC:'+s.ProductKey
    JOIN dbo.CropSeason cs ON cs.FarmerProfileId=p.FarmerProfileId
        AND cs.SeasonName=N'DEMO-THREE-UC: season'
    WHERE NOT EXISTS (SELECT 1 FROM dbo.HarvestBatch b WHERE b.BatchCode=s.Code);

    -- On a rerun, an existing buyer cart is not replenished after checkout.
    DECLARE @NewCarts TABLE(CartId bigint,UserId bigint);
    INSERT INTO dbo.Cart(UserId,UpdatedAt) OUTPUT inserted.CartId,inserted.UserId INTO @NewCarts
    SELECT u.UserId,@Now FROM dbo.[User] u
    WHERE u.Username IN (N'demo3uc_retail',N'demo3uc_retail_other')
      AND NOT EXISTS (SELECT 1 FROM dbo.Cart c WHERE c.UserId=u.UserId);
    INSERT INTO dbo.CartItem(CartId,ProductId,Quantity,IsSelected,AddedAt)
    SELECT c.CartId,p.ProductId,s.Quantity,s.Selected,@Now
    FROM @NewCarts c JOIN dbo.[User] u ON u.UserId=c.UserId
    CROSS APPLY (VALUES (N'greens',CONVERT(decimal(18,3),5),CONVERT(bit,1)),
        (N'orange',CONVERT(decimal(18,3),2),CONVERT(bit,1)),
        (N'carrot',CONVERT(decimal(18,3),1),CONVERT(bit,0))) s(ProductKey,Quantity,Selected)
    JOIN dbo.Product p ON p.Description=N'DEMO-THREE-UC:'+s.ProductKey
    WHERE u.Username=N'demo3uc_retail' OR s.ProductKey=N'greens';

    INSERT INTO dbo.DeliveryAddress
        (UserId,RecipientName,RecipientPhone,AddressDetail,ProvinceCity,District,Ward,
         IsDefault,Status,ProvinceCode,WardCode,AddressCatalogVersion)
    SELECT u.UserId,u.FullName,'0901234567',N'123 Đường Mẫu',N'Thành phố Hồ Chí Minh',
        NULL,N'Xã Thạnh An',1,'Active','12','268',NULL
    FROM dbo.[User] u WHERE u.Username IN (N'demo3uc_retail',N'demo3uc_retail_other')
      AND NOT EXISTS (SELECT 1 FROM dbo.DeliveryAddress a WHERE a.UserId=u.UserId);

    INSERT INTO dbo.Voucher
        (FarmerProfileId,VoucherCode,DiscountType,DiscountValue,MaxDiscountAmount,
         MinOrderAmount,UsageLimit,UsedCount,StartDate,EndDate,IsActive)
    SELECT NULL,'DEMO3UC-WELCOME10','Percent',10,50000,100000,100,0,
        DATEADD(day,-1,@Now),DATEADD(day,30,@Now),1
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Voucher WHERE VoucherCode='DEMO3UC-WELCOME10');
    INSERT INTO dbo.Voucher
        (FarmerProfileId,VoucherCode,DiscountType,DiscountValue,MaxDiscountAmount,
         MinOrderAmount,UsageLimit,UsedCount,StartDate,EndDate,IsActive)
    SELECT f.FarmerProfileId,'DEMO3UC-RAUXANH20','Fixed',20000,NULL,100000,20,0,
        DATEADD(day,-1,@Now),DATEADD(day,30,@Now),1
    FROM dbo.FarmerProfile f JOIN dbo.[User] u ON u.UserId=f.UserId
    WHERE u.Username=N'demo3uc_farmer_green' AND NOT EXISTS
        (SELECT 1 FROM dbo.Voucher WHERE VoucherCode='DEMO3UC-RAUXANH20');

    -- Placeholder PNGs are stored locally; no external image service is required.
    DECLARE @DemoImages TABLE(ProductId bigint PRIMARY KEY, OwnerUserId bigint, MediaId uniqueidentifier);
    INSERT INTO @DemoImages(ProductId,OwnerUserId,MediaId)
    SELECT p.ProductId,f.UserId,NEWID()
    FROM dbo.Product p JOIN dbo.FarmerProfile f ON f.FarmerProfileId=p.FarmerProfileId
    WHERE p.Description LIKE N'DEMO-THREE-UC:%'
      AND NOT EXISTS (SELECT 1 FROM dbo.ProductImage i WHERE i.ProductId=p.ProductId);
    INSERT INTO dbo.CommerceMedia(Id,OwnerUserId,ContentType,Content,PublicProductImage)
    SELECT MediaId,OwnerUserId,N'image/png',
      0x89504E470D0A1A0A0000000D4948445200000001000000010804000000B51C0C020000000B4944415478DA63FCFF1F0003030200EF9A0F5B0000000049454E44AE426082,1
    FROM @DemoImages;
    INSERT INTO dbo.ProductImage(ProductId,ImageUrl,DisplayOrder,IsPrimary,AltText)
    SELECT i.ProductId,N'/api/product-media/'+CONVERT(nvarchar(36),i.MediaId),0,1,N'Ảnh mẫu cục bộ'
    FROM @DemoImages i;

    -- Current carts identify an actual batch. Use a batch with enough stock for the sample line.
    UPDATE ci SET HarvestBatchId=chosen.HarvestBatchId
    FROM dbo.CartItem ci JOIN dbo.Cart c ON c.CartId=ci.CartId
    JOIN dbo.[User] u ON u.UserId=c.UserId
    CROSS APPLY (SELECT TOP(1) b.HarvestBatchId FROM dbo.HarvestBatch b
       WHERE b.ProductId=ci.ProductId AND b.ActualHarvestDate IS NOT NULL
         AND b.StockEnteredAt IS NOT NULL AND b.Status='Available'
         AND b.AvailableQuantity>=ci.Quantity
         AND (b.ExpirationDate IS NULL OR b.ExpirationDate>CONVERT(date,DATEADD(hour,7,@Now)))
       ORDER BY CASE WHEN b.ExpirationDate IS NULL THEN 1 ELSE 0 END,b.ExpirationDate,b.HarvestBatchId) chosen
    WHERE u.Username IN (N'demo3uc_retail',N'demo3uc_retail_other') AND ci.HarvestBatchId IS NULL;

    COMMIT TRANSACTION;
    SELECT N'User' AS TableName,COUNT_BIG(*) AS SampleRows FROM dbo.[User]
        WHERE Username LIKE N'demo3uc[_]%'
    UNION ALL SELECT N'Category',COUNT_BIG(*) FROM dbo.Category
        WHERE Description LIKE N'DEMO-THREE-UC:%'
    UNION ALL SELECT N'Product',COUNT_BIG(*) FROM dbo.Product
        WHERE Description LIKE N'DEMO-THREE-UC:%'
    UNION ALL SELECT N'HarvestBatch',COUNT_BIG(*) FROM dbo.HarvestBatch
        WHERE BatchCode LIKE 'DEMO3UC-%';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
END;

GO
-- SECTION 4: verify required current schema. Any missing table/column is an error.
IF DB_NAME()<>N'AgriSustain' THROW 51200,'Wrong target database.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>51
    THROW 51201,'Expected exactly 51 application tables after a complete recreate.',1;
IF OBJECT_ID(N'dbo.CommerceSubmission',N'U') IS NULL
 OR OBJECT_ID(N'dbo.CommerceMedia',N'U') IS NULL
 OR OBJECT_ID(N'dbo.CommerceEvidenceRequest',N'U') IS NULL
 OR OBJECT_ID(N'dbo.CommerceRefundRequest',N'U') IS NULL
 OR OBJECT_ID(N'dbo.RetailCheckoutQuote',N'U') IS NULL
    THROW 51202,'Missing required application tables.',1;
IF COL_LENGTH(N'dbo.Product',N'Revision') IS NULL
 OR COL_LENGTH(N'dbo.Product',N'QuantityStep') IS NULL
 OR COL_LENGTH(N'dbo.CartItem',N'HarvestBatchId') IS NULL
 OR COL_LENGTH(N'dbo.SellerOrder',N'RequiredPaymentAmount') IS NULL
 OR COL_LENGTH(N'dbo.Order',N'RetailQuoteId') IS NULL
 OR COL_LENGTH(N'dbo.SellerOrder',N'SellerNameSnapshot') IS NULL
 OR COL_LENGTH(N'dbo.OrderItem',N'ProductNameSnapshot') IS NULL
 OR COL_LENGTH(N'dbo.DeliveryAddress',N'ProvinceCode') IS NULL
 OR COL_LENGTH(N'dbo.AuditLog',N'TraceId') IS NULL
    THROW 51203,'Missing required application columns.',1;
SELECT DB_NAME() AS DatabaseName,COUNT(*) AS ApplicationTables,N'Schema ready' AS Result
FROM sys.tables WHERE is_ms_shipped=0;
SELECT N'User' AS TableName,COUNT_BIG(*) AS RowsInTable FROM dbo.[User]
UNION ALL SELECT N'Product',COUNT_BIG(*) FROM dbo.Product
UNION ALL SELECT N'CartItem',COUNT_BIG(*) FROM dbo.CartItem
UNION ALL SELECT N'SellerOrder',COUNT_BIG(*) FROM dbo.SellerOrder;
GO