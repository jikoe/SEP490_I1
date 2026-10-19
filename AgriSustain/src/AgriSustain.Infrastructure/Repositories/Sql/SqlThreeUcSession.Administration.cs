using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using Microsoft.Data.SqlClient;

namespace AgriSustain.Infrastructure.Repositories.Sql;

internal sealed partial class SqlThreeUcSession
{
    private const string AccountSelect = """
        SELECT u.UserId,u.FullName,u.Email,u.PhoneNumber,u.Role,u.Status,u.CreatedAt,
            u.LockedUntil,u.FailedLoginCount,COALESCE(f.FarmName,w.CompanyName) OrganizationName,
            COALESCE(f.VerificationStatus,w.VerificationStatus) VerificationStatus
        FROM dbo.[User] u LEFT JOIN dbo.FarmerProfile f ON f.UserId=u.UserId
        LEFT JOIN dbo.WholesaleBuyerProfile w ON w.UserId=u.UserId
        """;
    private const string AccountFilter = """
        WHERE (@Role IS NULL OR u.Role=@Role) AND (@Status IS NULL OR u.Status=@Status)
        AND (@Keyword IS NULL OR u.FullName LIKE @Keyword ESCAPE '~'
            OR u.Email LIKE @Keyword ESCAPE '~' OR u.PhoneNumber LIKE @Keyword ESCAPE '~'
            OR CONVERT(nvarchar(30),u.UserId) LIKE @Keyword ESCAPE '~'
            OR f.FarmName LIKE @Keyword ESCAPE '~' OR w.CompanyName LIKE @Keyword ESCAPE '~')
        """;

    /// <summary>
    /// Maps approved dbo.User/profile columns and external mock session state into the domain
    /// entity. Credentials are neither selected nor exposed.
    /// </summary>
    private UserAccount Account(SqlDataReader reader)
    {
        long id = (long)reader["UserId"];
        return UserAccount.RestorePersisted(new UserAccount
        {
            Id = id,
            Name = (string)reader["FullName"],
            Email = (string)reader["Email"],
            Phone = Text(reader, "PhoneNumber"),
            Role = (string)reader["Role"],
            OrganizationName = Text(reader, "OrganizationName"),
            VerificationStatus = Text(reader, "VerificationStatus"),
            CreatedAt = Utc(reader, "CreatedAt")
        }, (string)reader["Status"], reader["LockedUntil"] is DBNull
            ? null : Utc(reader, "LockedUntil"), (int)reader["FailedLoginCount"],
            SessionVersions.GetValueOrDefault(id));
    }

    /// <summary>
    /// Loads one current account in this transaction; missing identities remain null.
    /// </summary>
    async Task<UserAccount?> IAccountRepository.FindAsync(
        long id, CancellationToken cancellationToken) =>
        (await QueryAsync(AccountSelect + " WHERE u.UserId=@Id", Account,
            cancellationToken, ("@Id", id))).SingleOrDefault();

    /// <summary>
    /// Escapes LIKE metacharacters so keyword search means a literal substring rather than an
    /// uncontrolled wildcard pattern; parameterization independently prevents SQL injection.
    /// </summary>
    private static string? Keyword(string? value) => string.IsNullOrWhiteSpace(value) ? null
        : "%" + value.Trim().Replace("~", "~~").Replace("%", "~%")
            .Replace("_", "~_").Replace("[", "~[") + "%";

    /// <summary>
    /// Filters, counts and pages accounts in SQL, then applies the existing permitted DTO mapping.
    /// The same transaction provides a consistent list/count view.
    /// </summary>
    public async Task<PagedResponse<UserResponse>> SearchAsync(
        UserQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (string, object?)[] parameters = [("@Role", query.Role), ("@Status", query.Status),
            ("@Keyword", Keyword(query.Keyword)), ("@Skip", (query.Page - 1) * query.PageSize),
            ("@Take", query.PageSize)];
        var users = await QueryAsync(AccountSelect + " " + AccountFilter +
            " ORDER BY u.UserId OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
            Account, cancellationToken, parameters);
        long count = await ScalarAsync("""
            SELECT COUNT_BIG(*) FROM dbo.[User] u
            LEFT JOIN dbo.FarmerProfile f ON f.UserId=u.UserId
            LEFT JOIN dbo.WholesaleBuyerProfile w ON w.UserId=u.UserId
            """ + " " + AccountFilter, cancellationToken, parameters);
        return new PagedResponse<UserResponse>(users.Select(user =>
            Application.Mappings.UserMappings.ToResponse(user, now)).ToArray(),
            checked((int)count), query.Page, query.PageSize);
    }

    /// <summary>
    /// Persists only account status/lock fields owned by UC-ADM-03. Real auth revocation is still
    /// a missing dependency; its local projection is published only after this SQL transaction.
    /// </summary>
    public async Task SaveAsync(UserAccount account, CancellationToken cancellationToken)
    {
        int affected = await ExecuteAsync("""
            UPDATE dbo.[User] SET Status=@Status,LockedUntil=@Locked,FailedLoginCount=@Failures
            WHERE UserId=@Id
            """, cancellationToken, ("@Id", account.Id), ("@Status", account.Status),
            ("@Locked", account.LockedUntil?.UtcDateTime), ("@Failures", account.FailedLoginCount));
        if (affected != 1)
        {
            throw new InvalidOperationException("Account disappeared inside transaction.");
        }
        SessionVersions[account.Id] = account.SessionVersion;
    }

    /// <summary>
    /// Maps the approved Category columns only; no image/archive/Version contract is introduced.
    /// </summary>
    private static Category Category(SqlDataReader reader)
    {
        var category = new Category { Id = (long)reader["CategoryId"] };
        category.Update((string)reader["CategoryName"], reader["ParentCategoryId"] is DBNull
            ? null : (long)reader["ParentCategoryId"], Text(reader, "Description"),
            (int)reader["Priority"], (bool)reader["IsActive"]);
        return category;
    }

    /// <summary>
    /// Reads one category for parent/state/reference validation in the same SQL transaction.
    /// </summary>
    async Task<Category?> ICategoryRepository.FindAsync(
        long id, CancellationToken cancellationToken) =>
        (await QueryAsync("SELECT * FROM dbo.Category WHERE CategoryId=@Id", Category,
            cancellationToken, ("@Id", id))).SingleOrDefault();

    /// <summary>
    /// Pages category read models in SQL and counts every product reference, including products
    /// that are unpublished or unapproved. Hidden categories remain visible to administrators.
    /// </summary>
    public async Task<PagedResponse<CategoryResponse>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken)
    {
        const string filter = """
            WHERE (@Active IS NULL OR c.IsActive=@Active)
                AND (@Root=0 OR c.ParentCategoryId IS NULL)
                AND (@Parent IS NULL OR c.ParentCategoryId=@Parent)
                AND (@Keyword IS NULL OR c.CategoryName LIKE @Keyword ESCAPE '~')
            """;
        (string, object?)[] parameters = [("@Active", query.IsActive),
            ("@Root", query.RootOnly), ("@Parent", query.ParentId),
            ("@Keyword", Keyword(query.Keyword)), ("@Skip", (query.Page - 1) * query.PageSize),
            ("@Take", query.PageSize)];
        var rows = await QueryAsync("""
            SELECT c.*,p.CategoryName ParentName,
                (SELECT COUNT(*) FROM dbo.Product pr WHERE pr.CategoryId=c.CategoryId) ProductCount
            FROM dbo.Category c LEFT JOIN dbo.Category p ON p.CategoryId=c.ParentCategoryId
            """ + " " + filter + " ORDER BY c.Priority,c.CategoryId "
            + "OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
            reader => new CategoryResponse((long)reader["CategoryId"],
                (string)reader["CategoryName"], reader["ParentCategoryId"] is DBNull
                    ? null : (long)reader["ParentCategoryId"], Text(reader, "ParentName"),
                Text(reader, "Description"), (int)reader["Priority"],
                (bool)reader["IsActive"], (int)reader["ProductCount"]),
            cancellationToken, parameters);
        long count = await ScalarAsync("SELECT COUNT_BIG(*) FROM dbo.Category c " + filter,
            cancellationToken, parameters);
        return new PagedResponse<CategoryResponse>(rows, checked((int)count),
            query.Page, query.PageSize);
    }

    /// <summary>
    /// Checks all persisted product references, not just current marketplace-visible products.
    /// </summary>
    public async Task<bool> HasProductsAsync(long id, CancellationToken cancellationToken) =>
        await ScalarAsync("SELECT COUNT_BIG(*) FROM dbo.Product WHERE CategoryId=@Id",
            cancellationToken, ("@Id", id)) > 0;

    /// <summary>
    /// Checks child, benchmark and commission references before deletion; SQL NO ACTION foreign
    /// keys remain enabled to protect against writers outside these three UCs.
    /// </summary>
    public async Task<bool> HasOtherReferencesAsync(long id, CancellationToken cancellationToken) =>
        await ScalarAsync("""
            SELECT (SELECT COUNT_BIG(*) FROM dbo.Category WHERE ParentCategoryId=@Id)
                +(SELECT COUNT_BIG(*) FROM dbo.RegionalPriceBenchmark WHERE CategoryId=@Id)
                +(SELECT COUNT_BIG(*) FROM dbo.CommissionPolicy WHERE CategoryId=@Id)
            """, cancellationToken, ("@Id", id)) > 0;

    /// <summary>
    /// Deletes the unreferenced row inside the shared business/audit transaction. A later audit
    /// failure rolls back this physical delete; no FK is disabled.
    /// </summary>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken) =>
        await ExecuteAsync("DELETE dbo.Category WHERE CategoryId=@Id",
            cancellationToken, ("@Id", id));

    /// <summary>
    /// Reserves a SQL IDENTITY by inserting an uncommitted placeholder. SaveAsync replaces it
    /// before success; any earlier failure rolls it back. No other connection can observe it.
    /// Identity gaps after rollback are normal and are not used as semantic test identifiers.
    /// </summary>
    public Task<long> NextIdAsync(CancellationToken cancellationToken) => ScalarAsync("""
        INSERT dbo.Category(CategoryName,Priority,IsActive)
        OUTPUT inserted.CategoryId VALUES(N'__UNCOMMITTED_CATEGORY__',0,1)
        """, cancellationToken);

    /// <summary>
    /// Saves final approved category fields for both inserts and updates within the transaction.
    /// It does not add a duplicate-name or Version rule declined by the user.
    /// </summary>
    public async Task SaveAsync(Category category, CancellationToken cancellationToken) =>
        await ExecuteAsync("""
            UPDATE dbo.Category SET CategoryName=@Name,ParentCategoryId=@Parent,
                Description=@Description,Priority=@Priority,IsActive=@Active WHERE CategoryId=@Id
            """, cancellationToken, ("@Id", category.Id), ("@Name", category.Name),
            ("@Parent", category.ParentId), ("@Description", category.Description),
            ("@Priority", category.Priority), ("@Active", category.IsActive));
}
