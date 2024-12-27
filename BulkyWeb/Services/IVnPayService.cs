using BulkyBook.Models.ViewModels;

namespace BulkyBookWeb.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(HttpContext context, VnPaymentRequestModel model);
        VnPaymentResponseModel PaymentExecute(IQueryCollection collections);

    }
}
