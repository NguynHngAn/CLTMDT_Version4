using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using BulkyBookWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{

    [Area("customer")]
    public class CartController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
		private readonly IVnPayService _vnPayservice;

		[BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork, IEmailSender emailSender, IVnPayService vnPayservice)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
			_vnPayservice = vnPayservice;

		}


        public IActionResult Index()
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            IEnumerable<ProductImage> productImages = _unitOfWork.ProductImage.GetAll();

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Product.ProductImages = productImages.Where(u => u.ProductId == cart.Product.Id).ToList();
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;



            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST(string payment = "stripe")
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId,
                includeProperties: "Product");

            ShoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserId = userId;

            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);


            // Kiểm tra tồn kho và điều chỉnh giỏ hàng
            bool outOfStock = false;
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                var productFromDb = _unitOfWork.Product.Get(u => u.Id == cart.ProductId);
                if (productFromDb.StockQuantity < cart.Count)
                {
                    outOfStock = true;
                    cart.Count = productFromDb.StockQuantity; // Điều chỉnh số lượng giỏ hàng về tối đa tồn kho
                    _unitOfWork.ShoppingCart.Update(cart);
                    _unitOfWork.Save();
                }
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            if (outOfStock)
            {
                TempData["error"] = "Một số sản phẩm trong giỏ hàng đã vượt quá số lượng tồn kho. Số lượng trong giỏ đã được điều chỉnh.";
                return RedirectToAction(nameof(Index));
            }


            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                // It's a regular customer 
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            }
            else
            {
                // It's a company user
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
            }

			_unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
			_unitOfWork.Save();

			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				OrderDetail orderDetail = new()
				{
					ProductId = cart.ProductId,
					OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
					Price = cart.Price,
					Count = cart.Count
				};
				_unitOfWork.OrderDetail.Add(orderDetail);
			}
			_unitOfWork.Save();


            if (payment == "VnPay Payment")
            {
                var vnPayModel = new VnPaymentRequestModel
                {
                    Amount = (long)(ShoppingCartVM.OrderHeader.OrderTotal) * 24000,
                    CreatedDate = DateTime.Now,
                    Description = $"Payment for Order #{ShoppingCartVM.OrderHeader.Id}",
                    FullName = applicationUser.Name,
                    OrderId = applicationUser.Id
                };

                return Redirect(_vnPayservice.CreatePaymentUrl(HttpContext, vnPayModel));
            }


            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                // Handle Stripe payment
                var domain = Request.Scheme + "://" + Request.Host.Value + "/";
                var options = new SessionCreateOptions
                {
                    SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
                    CancelUrl = domain + "customer/cart/index",
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment",
                };

                foreach (var item in ShoppingCartVM.ShoppingCartList)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Title
                            }
                        },
                        Quantity = item.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }
                var service = new SessionService();
                Session session = service.Create(options);
                _unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }

            return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
        }

        public IActionResult OrderConfirmation(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");

            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                // This is an order by a customer
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.OrderHeader.UpdateStripePaymentID(id, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitOfWork.Save();

                    // Giảm số lượng tồn kho
                    var orderDetails = _unitOfWork.OrderDetail.GetAll(od => od.OrderHeaderId == id);
                    foreach (var detail in orderDetails)
                    {
                        var product = _unitOfWork.Product.Get(p => p.Id == detail.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity -= detail.Count;
                            _unitOfWork.Product.Update(product);
                        }
                    }
                    _unitOfWork.Save();
                }
                HttpContext.Session.Clear();
            }

            _emailSender.SendEmailAsync(orderHeader.ApplicationUser.Email, "New Order - Bulky Book",
                $"<p>New Order Created - {orderHeader.Id}</p>");

            List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
                .GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();

            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();

            return View(id);
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            var productFromDb = _unitOfWork.Product.Get(p => p.Id == cartFromDb.ProductId); // Lấy thông tin sản phẩm từ DB

            if (cartFromDb.Count < productFromDb.StockQuantity)
            {
                // Kiểm tra nếu số lượng trong giỏ nhỏ hơn số lượng tồn kho thì mới tăng
                cartFromDb.Count += 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
                _unitOfWork.Save();
            }
            else
            {
                TempData["error"] = "Không đủ số lượng hàng tồn kho.";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            if (cartFromDb.Count <= 1)
            {
                //remove that from cart
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
                    .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }

            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);

            _unitOfWork.ShoppingCart.Remove(cartFromDb);

            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
              .GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }



        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }

        public IActionResult PaymentFail()
        {
            return View("Fail");
        }

        public IActionResult PaymentSuccess()
        {
            return View("Success");
        }

        public IActionResult PaymentCallBack(int id)
        {
            var response = _vnPayservice.PaymentExecute(Request.Query);
            if(response == null || response.VnPayResponseCode != "00")
            {
                TempData["Message"] = $"Lỗi thanh toán VnPay: {response.VnPayResponseCode}";
                return RedirectToAction("PaymentFail");
            }

			var orderDetails = _unitOfWork.OrderDetail.GetAll(od => od.OrderHeaderId == id);
			foreach (var detail in orderDetails)
			{
				var product = _unitOfWork.Product.Get(p => p.Id == detail.ProductId);
				if (product != null)
				{
					product.StockQuantity -= detail.Count;
					_unitOfWork.Product.Update(product);
				}
			}
			_unitOfWork.Save();


			TempData["Message"] = $"Thanh toán VnPay thành công!!";
			return RedirectToAction("PaymentSuccess");
            
        }

	}
}
