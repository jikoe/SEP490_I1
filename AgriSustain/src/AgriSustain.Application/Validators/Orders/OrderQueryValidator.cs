using AgriSustain.Application.DTOs.Orders;
using FluentValidation;

namespace AgriSustain.Application.Validators.Orders;

public sealed class OrderQueryValidator : AbstractValidator<OrderQuery>
{
    public OrderQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số lượng mục mỗi trang phải từ 1 đến 100.");
    }
}
