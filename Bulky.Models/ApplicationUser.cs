using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Models {
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Name { get; set; }

        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        [Required(ErrorMessage = "Postal Code is required.")]
        [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Invalid Postal Code format.")]
        public string? PostalCode { get; set; }

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Invalid Phone Number format. It should be a 10-digit number.")]
        public string? PhoneNumber { get; set; }

        public int? CompanyId { get; set; }
        [ForeignKey("CompanyId")]
        [ValidateNever]
        public Company? Company { get; set; }
        [NotMapped]
        public string Role { get; set; }
    }
}
