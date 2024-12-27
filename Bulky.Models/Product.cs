using BulkyBook.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; }

    public string Description { get; set; }

    [Required]
    public string ISBN { get; set; }

    [Required]
    public string Author { get; set; }

    [Required]
    [Display(Name = "List Price")]
    [Range(1, 1000)]
    public double ListPrice { get; set; }

    [Required]
    [Display(Name = "Price for 1-50")]
    [Range(1, 1000)]
    public double Price { get; set; }

    [Required]
    [Display(Name = "Price for 50+")]
    [Range(1, 1000)]
    public double Price50 { get; set; }

    [Required]
    [Display(Name = "Price for 100+")]
    [Range(1, 1000)]
    public double Price100 { get; set; }

    public int CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    [ValidateNever]
    public Category Category { get; set; }

    [ValidateNever]
    public List<ProductImage> ProductImages { get; set; }

    // Thuộc tính số lượng tồn kho
    [Required]
    [Display(Name = "Stock Quantity")]
    [Range(0, int.MaxValue, ErrorMessage = "Please enter a valid quantity")]
    public int StockQuantity { get; set; }

    // Thêm trường Slug để lưu đường dẫn thân thiện
    [Required]
    [MaxLength(200)] // giới hạn độ dài slug, có thể thay đổi tùy ý
    public string Slug { get; set; }
}
