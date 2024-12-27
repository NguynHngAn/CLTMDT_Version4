using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Mvc;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RevenueController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public RevenueController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            // Lấy dữ liệu đơn hàng từ cơ sở dữ liệu và bao gồm thông tin người dùng
            var orders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");

            // Tính toán doanh thu và số lượng đơn hàng
            var totalRevenue = orders.Sum(o => o.OrderTotal);
            var totalOrders = orders.Count();


            // Lấy các đơn hàng đã thanh toán
            var paidOrders = orders.Where(o =>
                o.PaymentStatus.Equals(SD.StatusPending, StringComparison.OrdinalIgnoreCase) ||
                o.PaymentStatus.Equals(SD.PaymentStatusApproved, StringComparison.OrdinalIgnoreCase)).ToList();

            var paymentStatuses = orders.Select(o => o.PaymentStatus).Distinct();
            foreach (var status in paymentStatuses)
            {
                Console.WriteLine("Payment Status: " + status);
            }


            var viewModel = new RevenueVM
            {
                Date = DateTime.Now,
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                PaidOrders = paidOrders,
                // Gán giá trị OrderStatus cho ViewModel
                OrderStatus = paidOrders.FirstOrDefault()?.OrderStatus ?? SD.StatusPending // Tùy chỉnh theo nhu cầu của bạn
            };

            return View(viewModel);
        }
    }
}
