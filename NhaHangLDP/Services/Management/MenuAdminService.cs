using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Text;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Management
{
    public class MenuAdminService
    {
        private readonly MyDbContext db;

        public MenuAdminService(MyDbContext context)
        {
            db = context;
        }

        public List<MenuItem> GetFilteredMenuItems(string search, string category, string status)
        {
            var query = db.MenuItems.AsQueryable();
            
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(m => m.Category == category);
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "available")
                {
                    query = query.Where(m => m.IsAvailable);
                }
                else if (status == "unavailable")
                {
                    query = query.Where(m => !m.IsAvailable);
                }
            }

            var orderedQuery = query.OrderByDescending(m => m.CreatedDate);
            var menuItemsList = orderedQuery.ToList();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLowerNoDiacritics = StripDiacritics(search).ToLower();
                menuItemsList = menuItemsList.Where(m =>
                {
                    string itemNameLowerNoDiacritics = StripDiacritics(m.Name).ToLower();
                    return itemNameLowerNoDiacritics.Contains(searchLowerNoDiacritics);
                }).ToList();
            }

            return menuItemsList;
        }

        public List<MenuCombo> GetAllMenuCombos()
        {
            try
            {
                return db.MenuCombos
                    .Include(mc => mc.MenuComboItems).ThenInclude(mci => mci.MenuItem)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<MenuCombo>();
            }
        }

        public MenuStatsViewModel GetMenuStats()
        {
            int comboCount = 0;
            try
            {
                comboCount = db.MenuCombos.Count();
            }
            catch (Exception) { }

            return new MenuStatsViewModel
            {
                TotalItems = db.MenuItems.Count(),
                AvailableItems = db.MenuItems.Count(m => m.IsAvailable),
                UnavailableItems = db.MenuItems.Count(m => !m.IsAvailable),
                TotalCombos = comboCount
            };
        }

        public List<Ingredient> GetAllIngredients()
        {
            return db.Ingredients.OrderBy(i => i.Name).ToList();
        }

        public MenuItem GetMenuItemById(int id)
        {
            return db.MenuItems.Find(id);
        }

        public List<MenuItemIngredientViewModel> GetMenuItemIngredients(int menuItemId)
        {
            return db.MenuItemIngredients
                .Where(mii => mii.MenuItemId == menuItemId)
                .Include(mii => mii.Ingredient)
                .Select(mii => new MenuItemIngredientViewModel
                {
                    IngredientId = mii.IngredientId,
                    IngredientName = mii.Ingredient.Name,
                    RequiredQuantity = mii.RequiredQuantity,
                    Unit = mii.Unit,
                    AvailableStock = mii.Ingredient.AvailableStock
                })
                .ToList();
        }

        public bool CreateMenuItem(MenuItem menuItem, List<MenuItemIngredientViewModel> ingredients, string createdBy, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    menuItem.CreatedDate = DateTime.Now;
                    menuItem.CreatedBy = createdBy;
                    if (menuItem.OriginalPrice == 0)
                    {
                        menuItem.OriginalPrice = menuItem.Price;
                    }

                    db.MenuItems.Add(menuItem);
                    db.SaveChanges();

                    if (ingredients != null && ingredients.Any())
                    {
                        foreach (var ing in ingredients)
                        {
                            var newMenuItemIngredient = new MenuItemIngredient
                            {
                                MenuItemId = menuItem.Id,
                                IngredientId = ing.IngredientId,
                                RequiredQuantity = ing.RequiredQuantity,
                                Unit = ing.Unit
                            };
                            db.MenuItemIngredients.Add(newMenuItemIngredient);
                        }
                        db.SaveChanges();
                    }

                    transaction.Commit();
                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra khi thêm món ăn: " + ex.Message;
                    return false;
                }
            }
        }

        public bool UpdateMenuItem(MenuItem menuItem, List<MenuItemIngredientViewModel> ingredients, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var itemInDb = db.MenuItems.Find(menuItem.Id);
                    if (itemInDb == null)
                    {
                        errorMessage = "Không tìm thấy món ăn.";
                        return false;
                    }

                    itemInDb.Name = menuItem.Name;
                    itemInDb.Description = menuItem.Description;
                    itemInDb.Category = menuItem.Category;
                    itemInDb.Price = menuItem.Price;
                    itemInDb.PreparationTime = menuItem.PreparationTime;
                    itemInDb.IsAvailable = menuItem.IsAvailable;
                    itemInDb.ImageUrl = menuItem.ImageUrl ?? itemInDb.ImageUrl;

                    var oldIngredients = db.MenuItemIngredients.Where(mi => mi.MenuItemId == itemInDb.Id);
                    db.MenuItemIngredients.RemoveRange(oldIngredients);

                    if (ingredients != null)
                    {
                        foreach (var ing in ingredients)
                        {
                            var newMenuItemIngredient = new MenuItemIngredient
                            {
                                MenuItemId = itemInDb.Id,
                                IngredientId = ing.IngredientId,
                                RequiredQuantity = ing.RequiredQuantity,
                                Unit = ing.Unit
                            };
                            db.MenuItemIngredients.Add(newMenuItemIngredient);
                        }
                    }

                    db.Entry(itemInDb).State = EntityState.Modified;
                    db.SaveChanges();

                    transaction.Commit();
                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                    return false;
                }
            }
        }

        public bool ToggleMenuItemStatus(int id, out bool newStatus, out string errorMessage)
        {
            try
            {
                var menuItem = db.MenuItems.Find(id);
                if (menuItem == null)
                {
                    errorMessage = "Không tìm thấy món ăn";
                    newStatus = false;
                    return false;
                }

                menuItem.IsAvailable = !menuItem.IsAvailable;
                menuItem.UpdatedDate = DateTime.Now;

                db.SaveChanges();
                newStatus = menuItem.IsAvailable;
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                newStatus = false;
                return false;
            }
        }

        public bool DeleteMenuItem(int id, out string errorMessage)
        {
            try
            {
                var menuItem = db.MenuItems.Find(id);
                if (menuItem == null)
                {
                    errorMessage = "Không tìm thấy món ăn.";
                    return false;
                }

                bool isInOrder = db.OrderDetails.Any(od => od.MenuItemId == id);
                if (isInOrder)
                {
                    errorMessage = "Không thể xóa món ăn đã có trong lịch sử bán hàng. Bạn nên 'Tạm ngưng' (ẩn) món ăn này.";
                    return false;
                }

                db.MenuItems.Remove(menuItem);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi khi xóa: " + ex.Message;
                return false;
            }
        }

        #region Combo Management

        public MenuCombo GetComboById(int id)
        {
            return db.MenuCombos
                .Include(c => c.MenuComboItems).ThenInclude(ci => ci.MenuItem)
                .FirstOrDefault(c => c.Id == id);
        }

        public List<MenuItem> GetAvailableMenuItems()
        {
            return db.MenuItems
                .Where(m => m.IsAvailable)
                .OrderBy(m => m.Name)
                .ToList();
        }

        public bool CreateCombo(MenuCombo combo, List<int> selectedMenuItems, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    combo.StartDate = DateTime.Now;
                    combo.IsActive = true;

                    db.MenuCombos.Add(combo);
                    db.SaveChanges();

                    foreach (int menuItemId in selectedMenuItems)
                    {
                        var comboItem = new MenuComboItem
                        {
                            MenuComboId = combo.Id,
                            MenuItemId = menuItemId
                        };
                        db.MenuComboItems.Add(comboItem);
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra khi tạo combo: " + ex.Message;
                    return false;
                }
            }
        }

        public bool UpdateCombo(MenuCombo combo, List<int> selectedMenuItems, out string errorMessage)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var comboInDb = db.MenuCombos
                        .Include(c => c.MenuComboItems)
                        .FirstOrDefault(c => c.Id == combo.Id);

                    if (comboInDb == null)
                    {
                        errorMessage = "Không tìm thấy combo.";
                        return false;
                    }

                    comboInDb.Name = combo.Name;
                    comboInDb.Description = combo.Description;
                    comboInDb.ComboPrice = combo.ComboPrice;
                    comboInDb.StartDate = DateTime.Now;

                    var currentItemIds = comboInDb.MenuComboItems
                        .Select(ci => ci.MenuItemId)
                        .ToList();

                    var newItemIds = selectedMenuItems ?? new List<int>();

                    var itemsToRemove = comboInDb.MenuComboItems
                        .Where(ci => !newItemIds.Contains(ci.MenuItemId))
                        .ToList();

                    var itemIdsToAdd = newItemIds
                        .Where(id => !currentItemIds.Contains(id))
                        .ToList();

                    db.MenuComboItems.RemoveRange(itemsToRemove);

                    foreach (int menuItemId in itemIdsToAdd)
                    {
                        db.MenuComboItems.Add(new MenuComboItem
                        {
                            MenuComboId = comboInDb.Id,
                            MenuItemId = menuItemId
                        });
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Lỗi khi cập nhật combo: " + ex.Message;
                    return false;
                }
            }
        }

        public bool DeleteCombo(int id, out string errorMessage)
        {
            try
            {
                var combo = db.MenuCombos.Find(id);
                if (combo == null)
                {
                    errorMessage = "Không tìm thấy combo.";
                    return false;
                }

                db.MenuCombos.Remove(combo);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi khi xóa: " + ex.Message;
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private static string StripDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            text = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in text)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            sb.Replace('Đ', 'D');
            sb.Replace('đ', 'd');

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        #endregion
    }
}
