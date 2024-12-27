using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category,ProductImages");
            return View(productList);
        }

        [AllowAnonymous]
        // Đổi từ int productId -> string slug
        public IActionResult Details(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            // Tìm sản phẩm theo slug
            var product = _unitOfWork.Product.Get(u => u.Slug == slug, includeProperties: "Category,ProductImages");
            if (product == null)
            {
                return NotFound();
            }

            // Tạo ShoppingCart mặc định
            ShoppingCart cart = new()
            {
                Product = product,
                Count = 1,
                ProductId = product.Id
            };
            return View(cart);
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Details(ShoppingCart shoppingCart)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                // Nếu người dùng chưa đăng nhập, có thể redirect sang trang login hoặc handle khác
                return RedirectToAction("Index");
            }

            // Lấy sản phẩm theo productId từ shoppingCart
            var productFromDb = _unitOfWork.Product.Get(u => u.Id == shoppingCart.ProductId);

            if (productFromDb == null || productFromDb.StockQuantity < shoppingCart.Count)
            {
                TempData["error"] = "Không đủ số lượng hàng tồn kho.";
                return RedirectToAction(nameof(Index));
            }

            shoppingCart.ApplicationUserId = userId;
            ShoppingCart cartFromDb = _unitOfWork.ShoppingCart
                .Get(u => u.ApplicationUserId == userId &&
                          u.ProductId == shoppingCart.ProductId);

            if (cartFromDb != null)
            {
                // Kiểm tra tổng số lượng trong giỏ
                if (cartFromDb.Count + shoppingCart.Count > productFromDb.StockQuantity)
                {
                    TempData["error"] = "Không đủ số lượng hàng tồn kho.";
                    return RedirectToAction(nameof(Index));
                }

                cartFromDb.Count += shoppingCart.Count;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
                _unitOfWork.Save();
            }
            else
            {
                // Kiểm tra số lượng thêm mới
                if (shoppingCart.Count > productFromDb.StockQuantity)
                {
                    TempData["error"] = "Không đủ số lượng hàng tồn kho.";
                    return RedirectToAction(nameof(Index));
                }

                _unitOfWork.ShoppingCart.Add(shoppingCart);
                _unitOfWork.Save();
                HttpContext.Session.SetInt32(SD.SessionCart,
                _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId).Count());
            }

            TempData["success"] = "Cart updated successfully";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
