using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class MenuService
    {
        private readonly MyDbContext db;

        public MenuService(MyDbContext context)
        {
            db = context;
        }

        public List<object> GetMenuItems(string category = "")
        {
            var query = db.MenuItems.Where(m => m.IsAvailable == true);

            if (!string.IsNullOrEmpty(category) && category != "all")
            {
                query = query.Where(m => m.Category == category);
            }

            var menuItems = query
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Name)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    category = m.Category,
                    price = m.Price,
                    description = m.Description,
                    imageUrl = m.ImageUrl,
                    preparationTime = m.PreparationTime,
                    isAvailable = m.IsAvailable
                })
                .ToList<object>();

            return menuItems;
        }
    }
}
