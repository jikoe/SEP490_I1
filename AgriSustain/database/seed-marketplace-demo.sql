/* Optional synthetic Marketplace samples. Run after seed-three-uc-demo.sql.
   Inserts missing samples only; never resets existing accounts, products, stock or carts.
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 -i seed-marketplace-demo.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() IN ('master', 'model', 'msdb', 'tempdb')
    THROW 51120, 'Select an application or disposable test database.', 1;

DECLARE @FarmId bigint = (
    SELECT f.FarmerProfileId FROM dbo.FarmerProfile f
    JOIN dbo.[User] u ON u.UserId = f.UserId WHERE u.Username = N'demo3uc_farmer_green'
);
IF @FarmId IS NULL
    THROW 51121, 'Run seed-three-uc-demo.sql first. No account is created or changed here.', 1;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, @Now);
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource = N'AgriSustain.MarketplaceDemoSeed',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
    IF @LockResult < 0 THROW 51122, 'Another marketplace seed is running.', 1;

    DECLARE @Categories TABLE (Name nvarchar(150), Priority int);
    INSERT INTO @Categories VALUES
        (N'Rau củ quả', 1), (N'Trái cây', 2), (N'Gạo & ngũ cốc', 3),
        (N'Thực phẩm chế biến', 4), (N'Thủy hải sản', 5),
        (N'Gia súc, gia cầm', 6), (N'Sản phẩm khác', 7);
    INSERT INTO dbo.Category (CategoryName, Description, Priority, IsActive)
    SELECT c.Name, N'MARKETPLACE-DEMO', c.Priority, 1 FROM @Categories c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Category existing WHERE existing.CategoryName = c.Name);

    DECLARE @Products TABLE (
        Name nvarchar(255), CategoryName nvarchar(150), Region nvarchar(100),
        Price decimal(18,2), ImageUrl nvarchar(500), Description nvarchar(max)
    );
    INSERT INTO @Products VALUES
        (N'Rau xà lách thủy canh', N'Rau củ quả', N'Lâm Đồng', 25000,
         N'/marketplace/catalog-reference.png#lettuce',
         N'Xà lách thủy canh tươi xanh, thu hoạch từ nhà vườn. Dữ liệu minh họa Marketplace.'),
        (N'Xoài cát Hòa Lộc', N'Trái cây', N'Tiền Giang', 45000,
         N'/marketplace/catalog-reference.png#mango',
         N'Xoài cát Hòa Lộc chín vàng, thơm ngọt. Dữ liệu minh họa Marketplace.'),
        (N'Gạo ST25', N'Gạo & ngũ cốc', N'Sóc Trăng', 32000,
         N'/marketplace/gao_st25_main.png',
         N'Đặc sản Sóc Trăng – Hương thơm tự nhiên, vị ngọt thanh.' + NCHAR(10) + NCHAR(10)
         + N'Gạo ST25 có hạt dài, trong và mùi thơm tự nhiên. Khi nấu lên cơm dẻo mềm, '
         + N'vị ngọt thanh. Bảo quản nơi khô ráo, thoáng mát. Dữ liệu minh họa Marketplace.'),
        (N'Tôm sú tươi', N'Thủy hải sản', N'Bến Tre', 180000,
         N'/marketplace/catalog-reference.png#shrimp',
         N'Tôm sú tươi được bảo quản lạnh. Dữ liệu minh họa Marketplace.'),
        (N'Gạo Jasmine', N'Gạo & ngũ cốc', N'Sóc Trăng', 25000,
         N'/marketplace/gao_jasmine_similar.png', N'Gạo thơm Jasmine. Dữ liệu minh họa Marketplace.'),
        (N'Gạo nếp cái hoa vàng', N'Gạo & ngũ cốc', N'Sóc Trăng', 28000,
         N'/marketplace/gao_nep_cai_hoa_vang_similar.png',
         N'Gạo nếp cái hoa vàng. Dữ liệu minh họa Marketplace.'),
        (N'Gạo lứt hữu cơ', N'Gạo & ngũ cốc', N'Sóc Trăng', 45000,
         N'/marketplace/gao_lut_huu_co_similar.png',
         N'Gạo lứt nguyên cám. Dữ liệu minh họa; tên mẫu không thay thế chứng nhận còn hiệu lực.');
    INSERT INTO dbo.Product (
        CategoryId, FarmerProfileId, ProductName, Description, Unit, OriginProvince,
        BaseRetailPrice, ApprovalStatus, IsPublished, CreatedAt
    )
    SELECT c.CategoryId, @FarmId, p.Name, p.Description, N'kg', p.Region,
        p.Price, 'Approved', 1, @Now
    FROM @Products p JOIN dbo.Category c ON c.CategoryName = p.CategoryName
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Product existing
        WHERE existing.FarmerProfileId = @FarmId AND existing.ProductName = p.Name);

    INSERT INTO dbo.ProductImage (ProductId, ImageUrl, DisplayOrder, IsPrimary, AltText)
    SELECT product.ProductId, sample.ImageUrl, 0, 1, sample.Name
    FROM @Products sample JOIN dbo.Product product
        ON product.ProductName = sample.Name AND product.FarmerProfileId = @FarmId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImage image WHERE image.ProductId = product.ProductId);

    INSERT INTO dbo.ProductImage (ProductId, ImageUrl, DisplayOrder, IsPrimary, AltText)
    SELECT product.ProductId, image.Url, image.SortOrder, 0, N'Gạo ST25 – ảnh minh họa'
    FROM dbo.Product product CROSS JOIN (VALUES
        (N'/marketplace/gao_st25_thumb_bowl.png', 1),
        (N'/marketplace/ruong_lua_thumb.png', 2),
        (N'/marketplace/bao_bi_gao_thumb.png', 3)
    ) image(Url, SortOrder)
    WHERE product.ProductName = N'Gạo ST25' AND product.FarmerProfileId = @FarmId
        AND NOT EXISTS (SELECT 1 FROM dbo.ProductImage existing
            WHERE existing.ProductId = product.ProductId AND existing.ImageUrl = image.Url);

    INSERT INTO dbo.HarvestBatch (
        ProductId, CropSeasonId, BatchCode, ActualHarvestDate, HarvestYield,
        ExpirationDate, Status, StockEnteredAt, AvailableQuantity, ReservedQuantity
    )
    SELECT product.ProductId, season.CropSeasonId,
        'MARKET-DEMO-' + CONVERT(varchar(20), product.ProductId), @Today, 100,
        DATEADD(day, 90, @Today), 'Available', @Now, 100, 0
    FROM @Products sample JOIN dbo.Product product
        ON product.ProductName = sample.Name AND product.FarmerProfileId = @FarmId
    JOIN dbo.CropSeason season ON season.FarmerProfileId = @FarmId
        AND season.SeasonName = N'DEMO-THREE-UC: season'
    WHERE NOT EXISTS (SELECT 1 FROM dbo.HarvestBatch batch
        WHERE batch.BatchCode = 'MARKET-DEMO-' + CONVERT(varchar(20), product.ProductId));
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
