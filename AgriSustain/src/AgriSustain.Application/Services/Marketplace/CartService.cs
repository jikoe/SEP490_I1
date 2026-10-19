using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using FluentValidation;

namespace AgriSustain.Application.Services.Marketplace;

public sealed class CartService(
    ICartRepository repository,
    ICurrentUser currentUser,
    IValidator<AddCartItemRequest> validator) : ICartService
{
    public async Task<Result<decimal>> AddAsync(
        AddCartItemRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<decimal>.Failure(new Error(
                "UNAUTHENTICATED", "Vui lòng đăng nhập.", "Unauthorized"));
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<decimal>.Failure(new Error(
                "INVALID_QUANTITY", "Số lượng không hợp lệ (tối đa 3 chữ số thập phân)."));
        }

        return await repository.UpdateAsync(userId, request.ProductId, context =>
        {
            if (!context.IsEligible)
            {
                return Result<decimal>.Failure(new Error(
                    "FORBIDDEN", "Tài khoản không được phép dùng giỏ hàng bán lẻ.", "Forbidden"));
            }

            if (context.Product == null)
            {
                return Result<decimal>.Failure(new Error(
                    "MSG16", "Sản phẩm không còn được công khai.", "NotFound"));
            }

            decimal total = context.CurrentQuantity + request.Quantity;
            return total > context.Product.AvailableQuantity
                ? Result<decimal>.Failure(new Error(
                    "MSG38", "Số lượng trong giỏ vượt quá tồn kho khả dụng.", "Conflict"))
                : Result<decimal>.Success(total);
        }, cancellationToken);
    }
}
