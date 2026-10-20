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
