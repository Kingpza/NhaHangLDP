using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NhaHangLDP.Models
{
    public class PriceUpdateViewModel
    {
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; }
        public decimal CurrentPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá mới")]
        [Display(Name = "Giá mới")]
        public decimal NewPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do thay đổi")]
        [Display(Name = "Lý do thay đổi")]
        public string ChangeReason { get; set; }

        [Display(Name = "Áp dụng ngay lập tức")]
        public bool ApplyImmediately { get; set; }

        [Display(Name = "Ngày hiệu lực")]
        public DateTime EffectiveDate { get; set; }
    }
}