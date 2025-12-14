using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaHangLDP.Models
{
    public class MenuItemDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }

        // Chứa danh sách các nguyên liệu
        public List<MenuItemIngredientViewModel> Ingredients { get; set; }

        public MenuItemDetailViewModel()
        {
            // Khởi tạo để tránh lỗi null
            Ingredients = new List<MenuItemIngredientViewModel>();
        }
    }
}