using NhaHangLDP.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace NhaHangLDP.Controllers
{
    public class ManagementController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        [HttpPost]
        public JsonResult GetDishProfitAnalysisData(string period)
        {
            try
            {
                var model = new NhaHangLDP.Models.ViewModels.DishProfitAnalysisViewModel();
                model.DateRange = DateTime.Now.ToString("dd/MM/yyyy");

                model.DishProfits = new List<NhaHangLDP.Models.ViewModels.DishProfitItemViewModel>
        {
            new NhaHangLDP.Models.ViewModels.DishProfitItemViewModel { DishName = "Bò Beefsteak", Category = "Món chính", SellingPrice = 150000, CostPrice = 80000, UnitProfit = 70000, ProfitMargin = 46.6, SoldQuantity = 10, TotalProfit = 700000, TotalRevenue = 1500000 },
            new NhaHangLDP.Models.ViewModels.DishProfitItemViewModel { DishName = "Mỳ Ý Sốt Kem", Category = "Món chính", SellingPrice = 90000, CostPrice = 30000, UnitProfit = 60000, ProfitMargin = 66.7, SoldQuantity = 15, TotalProfit = 900000, TotalRevenue = 1350000 },
            new NhaHangLDP.Models.ViewModels.DishProfitItemViewModel { DishName = "Salad Cá Ngừ", Category = "Khai vị", SellingPrice = 60000, CostPrice = 20000, UnitProfit = 40000, ProfitMargin = 66.7, SoldQuantity = 5, TotalProfit = 200000, TotalRevenue = 300000 }
        };

                model.Summary = new NhaHangLDP.Models.ViewModels.ProfitSummaryViewModel
                {
                    TotalProfit = 1800000,
                    TotalRevenue = 3150000,
                    AverageProfitMargin = 57.1,
                    TotalDishes = 3,
                    BestDish = model.DishProfits[1]
                };

                model.ProfitTrends = new List<NhaHangLDP.Models.ViewModels.ProfitTrendViewModel>
        {
            new NhaHangLDP.Models.ViewModels.ProfitTrendViewModel { Label = "Sáng", Revenue = 1000000, Cost = 400000, Profit = 600000 },
            new NhaHangLDP.Models.ViewModels.ProfitTrendViewModel { Label = "Chiều", Revenue = 2150000, Cost = 950000, Profit = 1200000 }
        };

                model.CategoryProfits = new List<NhaHangLDP.Models.ViewModels.CategoryProfitViewModel>
        {
            new NhaHangLDP.Models.ViewModels.CategoryProfitViewModel { CategoryName = "Món chính", DishCount = 2, TotalSold = 25, TotalRevenue = 2850000, TotalProfit = 1600000, ProfitMargin = 56.1 },
            new NhaHangLDP.Models.ViewModels.CategoryProfitViewModel { CategoryName = "Khai vị", DishCount = 1, TotalSold = 5, TotalRevenue = 300000, TotalProfit = 200000, ProfitMargin = 66.7 }
        };

                return Json(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        public ActionResult Dashboard()
        {
            if (Session["UserRole"] == null ||
        (Session["UserRole"].ToString().ToLower() != "admin" &&
         Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var viewModel = new ManagementDashboardViewModel();
                var today = DateTime.Today;
                var sevenDaysAgo = today.AddDays(-7);

                viewModel.RecentOrders = db.Order
                    .Include(o => o.RestaurantTable)
                    .Include(o => o.Bill)
                    .OrderByDescending(o => o.OrderTime)
                    .Take(5)
                    .ToList();

                var paidBills = db.Bill
                    .Where(b => b.BillDate >= sevenDaysAgo && b.Status == "Paid")
                    .ToList();

                var revenueData = new List<DailyRevenue>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var revenue = paidBills
                        .Where(b => b.BillDate.Date == date)
                        .Sum(b => (decimal?)b.FinalAmount) ?? 0;

                    revenueData.Add(new DailyRevenue
                    {
                        Date = date.ToString("dd/MM"),
                        Revenue = revenue
                    });
                }
                viewModel.RevenueLast7Days = revenueData;

                viewModel.PopularItems = db.OrderDetail
                    .Where(od => od.Order.OrderTime >= sevenDaysAgo)
                    .GroupBy(od => od.MenuItem.Name)
                    .Select(g => new PopularItem
                    {
                        ItemName = g.Key,
                        Quantity = g.Sum(od => od.Quantity)
                    })
                    .OrderByDescending(pi => pi.Quantity)
                    .Take(5)
                    .ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải dữ liệu Dashboard: " + ex.Message;
                return View(new ManagementDashboardViewModel());
            }
        }

        private DashboardViewModel GetDashboardDataFromDB()
        {
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);

                var todayRevenue = db.Order
                    .Where(o => DbFunctions.TruncateTime(o.OrderTime) == today && o.Status == "Completed")
                    .SelectMany(o => o.OrderDetail)
                    .Sum(od => (decimal?)od.Quantity * od.PriceAtTime) ?? 0;

                var yesterdayRevenue = db.Order
                    .Where(o => DbFunctions.TruncateTime(o.OrderTime) == yesterday && o.Status == "Completed")
                    .SelectMany(o => o.OrderDetail)
                    .Sum(od => (decimal?)od.Quantity * od.PriceAtTime) ?? 0;

                var todayOrders = db.Order
                    .Count(o => DbFunctions.TruncateTime(o.OrderTime) == today);

                var yesterdayOrders = db.Order
                    .Count(o => DbFunctions.TruncateTime(o.OrderTime) == yesterday);

                var todayCustomers = db.Order
                    .Where(o => DbFunctions.TruncateTime(o.OrderTime) == today && (object)o.TableId != null)
                    .Select(o => o.TableId)
                    .Distinct()
                    .Count();

                var yesterdayCustomers = db.Order
                    .Where(o => DbFunctions.TruncateTime(o.OrderTime) == yesterday && (object)o.TableId != null)
                    .Select(o => o.TableId)
                    .Distinct()
                    .Count();

                var occupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied");
                var totalTables = db.RestaurantTable.Count();

                return new DashboardViewModel
                {
                    TodayRevenue = todayRevenue,
                    RevenueChangePercent = yesterdayRevenue > 0 ? ((todayRevenue - yesterdayRevenue) / yesterdayRevenue * 100) : 0,
                    OrdersChangePercent = yesterdayOrders > 0 ? ((float)(todayOrders - yesterdayOrders) / yesterdayOrders * 100) : 0,
                    CustomersChangePercent = yesterdayCustomers > 0 ? ((float)(todayCustomers - yesterdayCustomers) / yesterdayCustomers * 100) : 0,
                    TodayOrders = todayOrders,
                    TodayCustomers = todayCustomers,
                    OccupiedTables = occupiedTables,
                    TotalTables = totalTables,
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetDashboardDataFromDB: {ex.Message}");
                return new DashboardViewModel();
            }
        }

        #region Table Management

        public ActionResult TableManagement()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var tables = db.RestaurantTable.Include("TableArea").ToList();
                return View(tables);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách bàn: " + ex.Message;
                return View(new List<RestaurantTable>());
            }
        }

        public ActionResult CreateTable()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.TableAreaId = new SelectList(db.TableArea, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTable(RestaurantTable table)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var existingTable = db.RestaurantTable
                        .FirstOrDefault(t => t.TableNumber == table.TableNumber && t.TableAreaId == table.TableAreaId);

                    if (existingTable != null)
                    {
                        ModelState.AddModelError("TableNumber", "Số bàn này đã tồn tại trong khu vực được chọn.");
                        ViewBag.TableAreaId = new SelectList(db.TableArea, "Id", "Name", table.TableAreaId);
                        return View(table);
                    }

                    table.Status = "Available";
                    db.RestaurantTable.Add(table);
                    db.SaveChanges();

                    TempData["Success"] = "Thêm bàn thành công!";
                    return RedirectToAction("TableManagement");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi thêm bàn: " + ex.Message;
            }

            ViewBag.TableAreaId = new SelectList(db.TableArea, "Id", "Name", table.TableAreaId);
            return View(table);
        }

        public ActionResult EditTable(int id)
        {
            if (Session["UserRole"] == null ||
             (Session["UserRole"].ToString().ToLower() != "admin" &&
          Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var table = db.RestaurantTable.Find(id);
                if (table == null)
                {
                    TempData["Error"] = "Không tìm thấy bàn.";
                    return RedirectToAction("TableManagement");
                }

                ViewBag.TableAreaId = new SelectList(db.TableArea, "Id", "Name", table.TableAreaId);
                return View(table);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("TableManagement");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTable(RestaurantTable table)
        {
            if (Session["UserRole"] == null ||
           (Session["UserRole"].ToString().ToLower() != "admin" &&
            Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var existingTable = db.RestaurantTable
                   .FirstOrDefault(t => t.TableNumber == table.TableNumber &&
                                 t.TableAreaId == table.TableAreaId &&
                             t.Id != table.Id);

                    if (existingTable != null)
                    {
                        ModelState.AddModelError("TableNumber", "Số bàn này đã tồn tại trong khu vực được chọn.");
                        ViewBag.TableAreaId = new SelectList(db.TableArea, "Id", "Name", table.TableAreaId);
                        return View(table);
                    }

                    db.Entry(table).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Cập nhật bàn thành công!";
                    return RedirectToAction("TableManagement");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi cập nhật bàn: " + ex.Message;
            }

            ViewBag.TableAreaId = new SelectList(db.TableArea, "Id", "Name", table.TableAreaId);
            return View(table);
        }

        public JsonResult DeleteTable(int id)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập." });
            }

            try
            {
                var table = db.RestaurantTable.Find(id);
                if (table == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bàn." });
                }

                var hasActiveOrders = db.Order.Any(o => o.TableId == id && (o.Status == "Pending" || o.Status == "Processing"));
                if (hasActiveOrders)
                {
                    return Json(new { success = false, message = "Không thể xóa bàn đang có khách hoặc đang phục vụ." });
                }

                db.RestaurantTable.Remove(table);
                db.SaveChanges();

                return Json(new { success = true, message = "Xóa bàn thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Employee Management

        public ActionResult EmployeeManagement()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var employees = db.Employee.Include("Role").ToList();
                return View(employees);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách nhân viên: " + ex.Message;
                return View(new List<Employee>());
            }
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public ActionResult CreateEmployee()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ViewBag.RoleId = new SelectList(db.Role, "Id", "RoleName");
                return View(new Employee());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải trang thêm nhân viên: " + ex.Message;
                return RedirectToAction("EmployeeManagement");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateEmployee(Employee employee, string ConfirmPassword)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            if (employee.PasswordHash != ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
            }

            if (db.Employee.Any(e => e.UserName == employee.UserName))
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    employee.PasswordHash = HashPassword(employee.PasswordHash);
                    db.Employee.Add(employee);
                    db.SaveChanges();

                    TempData["Success"] = "Đã thêm nhân viên mới thành công!";
                    return RedirectToAction("EmployeeManagement");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Có lỗi xảy ra khi lưu: " + ex.Message;
                }
            }

            ViewBag.RoleId = new SelectList(db.Role, "Id", "RoleName", employee.RoleId);
            return View(employee);
        }

        public ActionResult EditEmployee(int? id)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Yêu cầu không hợp lệ");
            }

            Employee employee = db.Employee.Find(id);
            if (employee == null)
            {
                return HttpNotFound("Không tìm thấy nhân viên này.");
            }

            try
            {
                ViewBag.RoleId = new SelectList(db.Role, "Id", "RoleName", employee.RoleId);
                return View(employee);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải trang chỉnh sửa: " + ex.Message;
                return RedirectToAction("EmployeeManagement");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditEmployee(Employee employee, string NewPassword, string ConfirmPassword)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            if (db.Employee.Any(e => e.UserName == employee.UserName && e.Id != employee.Id))
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập này đã tồn tại.");
            }

            bool passwordChanged = false;
            string newHashedPassword = null;

            if (!string.IsNullOrEmpty(NewPassword))
            {
                if (NewPassword.Length < 6)
                {
                    ModelState.AddModelError("NewPassword", "Mật khẩu mới phải có ít nhất 6 ký tự.");
                }
                else if (NewPassword != ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
                }
                else
                {
                    passwordChanged = true;
                    newHashedPassword = HashPassword(NewPassword);
                }
            }

            if (ModelState.ContainsKey("PasswordHash"))
            {
                ModelState.Remove("PasswordHash");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var empInDb = db.Employee.Find(employee.Id);
                    if (empInDb == null)
                    {
                        return HttpNotFound();
                    }

                    empInDb.FullName = employee.FullName;
                    empInDb.Email = employee.Email;
                    empInDb.PhoneNumber = employee.PhoneNumber;
                    empInDb.RoleId = employee.RoleId;
                    empInDb.UserName = employee.UserName;
                    empInDb.IsActive = employee.IsActive;

                    if (passwordChanged)
                    {
                        empInDb.PasswordHash = newHashedPassword;
                    }

                    db.SaveChanges();

                    TempData["Success"] = "Cập nhật thông tin nhân viên thành công!";
                    return RedirectToAction("EmployeeManagement");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                }
            }

            ViewBag.RoleId = new SelectList(db.Role, "Id", "RoleName", employee.RoleId);
            return View(employee);
        }

        [HttpPost]
        public ActionResult ToggleEmployeeStatus(int id)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            try
            {
                var employee = db.Employee.Find(id);
                if (employee == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy nhân viên." });
                }

                string currentUserName = Session["UserName"]?.ToString();
                if (employee.UserName == currentUserName)
                {
                    return Json(new { success = false, message = "Bạn không thể tự khóa tài khoản của chính mình." });
                }

                employee.IsActive = !employee.IsActive;
                db.SaveChanges();

                string statusMessage = employee.IsActive ? "kích hoạt" : "khóa";
                return Json(new { success = true, message = $"Đã {statusMessage} tài khoản nhân viên thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Menu Management

        [HttpPost]
        public JsonResult DeleteCombo(int id)
        {
            if (Session["UserRole"] == null ||
             (Session["UserRole"].ToString().ToLower() != "admin" &&
             Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập." });
            }

            try
            {
                var combo = db.MenuCombo.Find(id);
                if (combo == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy combo." });
                }

                db.MenuCombo.Remove(combo);
                db.SaveChanges();

                return Json(new { success = true, message = "Xóa combo thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa: " + ex.Message });
            }
        }

        public ActionResult EditCombo(int id)
        {
            if (Session["UserRole"] == null ||
             (Session["UserRole"].ToString().ToLower() != "admin" &&
             Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            var combo = db.MenuCombo
                          .Include(c => c.MenuComboItem.Select(ci => ci.MenuItem))
                          .FirstOrDefault(c => c.Id == id);

            if (combo == null)
            {
                return HttpNotFound();
            }

            try
            {
                var availableItems = db.MenuItem
                                       .Where(m => m.IsAvailable)
                                       .OrderBy(m => m.Name)
                                       .ToList();

                ViewBag.AvailableMenuItems = availableItems;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải danh sách món ăn: " + ex.Message;
                ViewBag.AvailableMenuItems = new List<MenuItem>();
            }

            return View("EditCombo", combo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCombo([Bind(Include = "Id,Name,Description,ComboPrice")] MenuCombo combo, List<int> selectedMenuItems)
        {
            if (Session["UserRole"] == null ||
             (Session["UserRole"].ToString().ToLower() != "admin" &&
             Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }
            if (selectedMenuItems == null || selectedMenuItems.Count < 2)
            {
                ModelState.AddModelError("", "Bạn phải chọn ít nhất 2 món ăn cho combo.");
            }

            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var comboInDb = db.MenuCombo
                                          .Include(c => c.MenuComboItem)
                                          .FirstOrDefault(c => c.Id == combo.Id);

                        if (comboInDb == null) return HttpNotFound();

                        comboInDb.Name = combo.Name;
                        comboInDb.Description = combo.Description;
                        comboInDb.ComboPrice = combo.ComboPrice;
                        comboInDb.StartDate = DateTime.Now;

                        var currentItemIds = comboInDb.MenuComboItem
                                                    .Select(ci => ci.MenuItemId)
                                                    .ToList();

                        var newItemIds = selectedMenuItems ?? new List<int>();

                        var itemsToRemove = comboInDb.MenuComboItem
                                                 .Where(ci => !newItemIds.Contains(ci.MenuItemId))
                                                 .ToList();

                        var itemIdsToAdd = newItemIds
                                             .Where(id => !currentItemIds.Contains(id))
                                             .ToList();

                        db.MenuComboItem.RemoveRange(itemsToRemove);

                        foreach (int menuItemId in itemIdsToAdd)
                        {
                            db.MenuComboItem.Add(new MenuComboItem
                            {
                                MenuComboId = comboInDb.Id,
                                MenuItemId = menuItemId
                            });
                        }

                        db.SaveChanges();
                        transaction.Commit();

                        TempData["Success"] = "Cập nhật combo '" + comboInDb.Name + "' thành công!";
                        return RedirectToAction("MenuManagement");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = "Lỗi khi cập nhật combo: " + ex.Message;
                    }
                }
            }

            var availableItems = db.MenuItem
                                   .Where(m => m.IsAvailable)
                                   .OrderBy(m => m.Name)
                                   .ToList();
            ViewBag.AvailableMenuItems = availableItems;

            return View("EditCombo", combo);
        }

        public ActionResult CreateCombo()
        {
            if (Session["UserRole"] == null ||
             (Session["UserRole"].ToString().ToLower() != "admin" &&
             Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var availableItems = db.MenuItem
                                       .Where(m => m.IsAvailable)
                                       .OrderBy(m => m.Name)
                                       .ToList();

                ViewBag.AvailableMenuItems = availableItems;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải danh sách món ăn: " + ex.Message;
                ViewBag.AvailableMenuItems = new List<MenuItem>();
            }

            var model = new MenuCombo();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCombo([Bind(Include = "Name,Description,ComboPrice")] MenuCombo combo, List<int> selectedMenuItems)
        {
            if (Session["UserRole"] == null ||
             (Session["UserRole"].ToString().ToLower() != "admin" &&
             Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            if (selectedMenuItems == null || selectedMenuItems.Count < 2)
            {
                ModelState.AddModelError("", "Bạn phải chọn ít nhất 2 món ăn cho combo.");
            }

            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        combo.StartDate = DateTime.Now;
                        combo.IsActive = true;

                        db.MenuCombo.Add(combo);
                        db.SaveChanges();

                        int newComboId = combo.Id;

                        foreach (int menuItemId in selectedMenuItems)
                        {
                            var comboItem = new MenuComboItem
                            {
                                MenuComboId = newComboId,
                                MenuItemId = menuItemId
                            };
                            db.MenuComboItem.Add(comboItem);
                        }

                        db.SaveChanges();
                        transaction.Commit();

                        TempData["Success"] = "Tạo combo '" + combo.Name + "' thành công!";
                        return RedirectToAction("MenuManagement");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = "Có lỗi xảy ra khi tạo combo: " + ex.Message;
                    }
                }
            }

            var availableItems = db.MenuItem
                                   .Where(m => m.IsAvailable)
                                   .OrderBy(m => m.Name)
                                   .ToList();
            ViewBag.AvailableMenuItems = availableItems;

            return View(combo);
        }

        public ActionResult MenuManagement(string search = "", string category = "", string status = "")
        {
            if (Session["UserRole"] == null ||
              (Session["UserRole"].ToString().ToLower() != "admin" &&
             Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = new MenuManagementViewModel
            {
                MenuItems = GetFilteredMenuItems(search, category, status),
                MenuCombos = GetAllMenuCombos(),
                Stats = GetMenuStats(),
                SearchTerm = search,
                CategoryFilter = category,
                StatusFilter = status
            };
            return View(viewModel);
        }

        public ActionResult CreateMenuItem()
        {
            if (Session["UserRole"] == null ||
              (Session["UserRole"].ToString().ToLower() != "admin" &&
            Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = new MenuItemFormViewModel
            {
                MenuItem = new MenuItem { IsAvailable = true },
                IsEdit = false,
                AvailableIngredients = GetAllIngredients()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateMenuItem(MenuItemFormViewModel model)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }
            HttpPostedFileBase ImageFile = model.ImageFile;

            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        if (ImageFile != null && ImageFile.ContentLength > 0)
                        {
                            string fileExtension = Path.GetExtension(ImageFile.FileName);
                            string fileName = Guid.NewGuid().ToString() + fileExtension;
                            string serverPath = Path.Combine(Server.MapPath("~/images/menu/"), fileName);
                            Directory.CreateDirectory(Path.GetDirectoryName(serverPath));

                            ImageFile.SaveAs(serverPath);

                            model.MenuItem.ImageUrl = fileName;
                        }
                        else
                        {
                            model.MenuItem.ImageUrl = null;
                        }
                        model.MenuItem.CreatedDate = DateTime.Now;
                        model.MenuItem.CreatedBy = Session["Username"]?.ToString() ?? "Admin";
                        if (model.MenuItem.OriginalPrice == 0)
                        {
                            model.MenuItem.OriginalPrice = model.MenuItem.Price;
                        }

                        db.MenuItem.Add(model.MenuItem);
                        db.SaveChanges();

                        int newMenuItemId = model.MenuItem.Id;

                        if (model.SelectedIngredients != null && model.SelectedIngredients.Any())
                        {
                            foreach (var ing in model.SelectedIngredients)
                            {
                                var newMenuItemIngredient = new MenuItemIngredient
                                {
                                    MenuItemId = newMenuItemId,
                                    IngredientId = ing.IngredientId,
                                    RequiredQuantity = ing.RequiredQuantity,
                                    Unit = ing.Unit
                                };
                                db.MenuItemIngredient.Add(newMenuItemIngredient);
                            }
                            db.SaveChanges();
                        }

                        transaction.Commit();
                        TempData["Success"] = "Thêm món ăn thành công!";
                        return RedirectToAction("MenuManagement");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = "Có lỗi xảy ra khi thêm món ăn: " + ex.Message;
                    }
                }
            }
            return View(model);
        }

        public ActionResult EditMenuItem(int? id)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            MenuItem menuItem = db.MenuItem.Find(id);
            if (menuItem == null)
            {
                return HttpNotFound();
            }

            var selectedIngredients = db.MenuItemIngredient
                .Where(mi => mi.MenuItemId == id)
                .Select(mi => new MenuItemIngredientViewModel
                {
                    IngredientId = mi.IngredientId,
                    IngredientName = mi.Ingredient.Name,
                    RequiredQuantity = mi.RequiredQuantity,
                    Unit = mi.Unit,
                    AvailableStock = mi.Ingredient.AvailableStock
                })
                .ToList();

            var viewModel = new MenuItemFormViewModel
            {
                MenuItem = menuItem,
                IsEdit = true,
                AvailableIngredients = GetAllIngredients(),
                SelectedIngredients = selectedIngredients
            };

            return View("CreateMenuItem", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMenuItem(MenuItemFormViewModel model)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            HttpPostedFileBase ImageFile = model.ImageFile;
            if (ModelState.ContainsKey("ImageFile"))
            {
                ModelState["ImageFile"].Errors.Clear();
            }

            if (ModelState.IsValid)
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var itemInDb = db.MenuItem.Find(model.MenuItem.Id);
                        if (itemInDb == null) return HttpNotFound();

                        if (ImageFile != null && ImageFile.ContentLength > 0)
                        {
                            if (!string.IsNullOrEmpty(itemInDb.ImageUrl))
                            {
                                string oldFilePath = Path.Combine(Server.MapPath("~/images/menu/"), itemInDb.ImageUrl);
                                if (System.IO.File.Exists(oldFilePath))
                                {
                                    System.IO.File.Delete(oldFilePath);
                                }
                            }
                            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                            string serverPath = Path.Combine(Server.MapPath("~/images/menu/"), fileName);
                            Directory.CreateDirectory(Path.GetDirectoryName(serverPath));
                            ImageFile.SaveAs(serverPath);
                            itemInDb.ImageUrl = fileName;
                        }

                        itemInDb.Name = model.MenuItem.Name;
                        itemInDb.Description = model.MenuItem.Description;
                        itemInDb.Category = model.MenuItem.Category;
                        itemInDb.Price = model.MenuItem.Price;
                        itemInDb.PreparationTime = model.MenuItem.PreparationTime;
                        itemInDb.IsAvailable = model.MenuItem.IsAvailable;

                        var oldIngredients = db.MenuItemIngredient.Where(mi => mi.MenuItemId == itemInDb.Id);
                        db.MenuItemIngredient.RemoveRange(oldIngredients);

                        if (model.SelectedIngredients != null)
                        {
                            foreach (var ing in model.SelectedIngredients)
                            {
                                var newMenuItemIngredient = new MenuItemIngredient
                                {
                                    MenuItemId = itemInDb.Id,
                                    IngredientId = ing.IngredientId,
                                    RequiredQuantity = ing.RequiredQuantity,
                                    Unit = ing.Unit
                                };
                                db.MenuItemIngredient.Add(newMenuItemIngredient);
                            }
                        }

                        db.Entry(itemInDb).State = EntityState.Modified;
                        db.SaveChanges();

                        transaction.Commit();
                        TempData["Success"] = "Cập nhật món ăn thành công!";
                        return RedirectToAction("MenuManagement");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["Error"] = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                    }
                }
            }

            model.AvailableIngredients = GetAllIngredients();
            return View("CreateMenuItem", model);
        }

        [HttpPost]
        public JsonResult ToggleMenuItemStatus(int id)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập." });
            }

            try
            {
                var menuItem = db.MenuItem.Find(id);
                if (menuItem != null)
                {
                    menuItem.IsAvailable = !menuItem.IsAvailable;
                    menuItem.UpdatedDate = DateTime.Now;

                    db.SaveChanges();
                    return Json(new { success = true, isAvailable = menuItem.IsAvailable });
                }
                return Json(new { success = false, message = "Không tìm thấy món ăn" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteMenuItem(int id)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập." });
            }

            try
            {
                var menuItem = db.MenuItem.Find(id);
                if (menuItem == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy món ăn." });
                }

                bool isInOrder = db.OrderDetail.Any(od => od.MenuItemId == id);
                if (isInOrder)
                {
                    return Json(new { success = false, message = "Không thể xóa món ăn đã có trong lịch sử bán hàng. Bạn nên 'Tạm ngưng' (ẩn) món ăn này." });
                }

                db.MenuItem.Remove(menuItem);
                db.SaveChanges();

                return Json(new { success = true, message = "Xóa món ăn thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa: " + ex.Message });
            }
        }

        #endregion

        #region Inventory Management

        public ActionResult Inventory()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var inventoryStats = GetInventoryStatsFromDB();
                return View(inventoryStats);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải thông tin kho: " + ex.Message;
                return View(new InventoryStatsViewModel());
            }
        }

        private InventoryStatsViewModel GetInventoryStatsFromDB()
        {
            try
            {
                var ingredients = db.Ingredient.ToList();

                var totalIngredients = ingredients.Count;
                var lowStockItems = ingredients.Count(i => i.AvailableStock <= i.LowStockThreshold);
                var totalInventoryValue = ingredients.Sum(i => i.AvailableStock * i.EstimatedCost);

                var lowStockList = ingredients
                    .Where(i => i.AvailableStock <= i.LowStockThreshold)
                    .Select(i => new LowStockItem
                    {
                        IngredientName = i.Name,
                        CurrentStock = i.AvailableStock,
                        MinimumStock = i.LowStockThreshold ?? 0,
                        Unit = i.Unit,
                        StockPercentage = i.LowStockThreshold > 0 ? (int)((i.AvailableStock / i.LowStockThreshold.Value) * 100) : 0
                    })
                    .OrderBy(i => i.StockPercentage)
                    .ToList();

                return new InventoryStatsViewModel
                {
                    TotalIngredients = totalIngredients,
                    LowStockItems = lowStockItems,
                    ExpiredItems = 0,
                    TotalInventoryValue = totalInventoryValue,
                    LowStockList = lowStockList,
                    ExpiringList = new List<ExpiringItem>(),
                    MonthlyDamageValue = 1200000m
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetInventoryStatsFromDB: {ex.Message}");
                return new InventoryStatsViewModel
                {
                    TotalIngredients = 0,
                    LowStockItems = 0,
                    ExpiredItems = 0,
                    TotalInventoryValue = 0,
                    LowStockList = new List<LowStockItem>(),
                    ExpiringList = new List<ExpiringItem>(),
                    MonthlyDamageValue = 0
                };
            }
        }

        public ActionResult IngredientList()
        {
            if (Session["UserRole"] == null ||
        (Session["UserRole"].ToString().ToLower() != "admin" &&
        Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var ingredients = (from i in db.Ingredient
                                   let lastInbound = db.StockInboundDetail
                                       .Where(d => d.IngredientId == i.Id)
                                       .OrderByDescending(d => d.StockInbound.InboundDate)
                                       .Select(d => new
                                       {
                                           SupplierName = d.StockInbound.Supplier.Name,
                                           InboundDate = d.StockInbound.InboundDate,
                                           UnitPrice = d.UnitPrice
                                       })
                                       .FirstOrDefault()
                                   select new IngredientWithLatestSupplierViewModel
                                   {
                                       Id = i.Id,
                                       Name = i.Name,
                                       Unit = i.Unit,
                                       AvailableStock = i.AvailableStock,
                                       LowStockThreshold = i.LowStockThreshold,
                                       EstimatedCost = i.EstimatedCost,
                                       LatestSupplier = lastInbound != null ? lastInbound.SupplierName : null,
                                       LastInboundDate = lastInbound != null ? lastInbound.InboundDate : (DateTime?)null,

                                       LastInboundQuantity = lastInbound != null ? db.StockInboundDetail
                                           .Where(d => d.IngredientId == i.Id && d.StockInbound.InboundDate == lastInbound.InboundDate)
                                           .Sum(d => (decimal?)d.Quantity) : null,

                                       LastInboundUnitPrice = lastInbound != null ? lastInbound.UnitPrice : (decimal?)null
                                   })
                                   .OrderByDescending(x => x.LastInboundDate)
                                   .ToList();

                return View(ingredients);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách nguyên liệu: " + ex.Message;
                return View(new List<IngredientWithLatestSupplierViewModel>());
            }
        }

        public ActionResult CreateIngredient()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new Ingredient());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateIngredient(Ingredient ingredient)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var existingIngredient = db.Ingredient.FirstOrDefault(i => i.Name == ingredient.Name);
                    if (existingIngredient != null)
                    {
                        ModelState.AddModelError("Name", "Tên nguyên liệu đã tồn tại.");
                        return View(ingredient);
                    }

                    ingredient.AvailableStock = 0;
                    if (!ingredient.LowStockThreshold.HasValue)
                        ingredient.LowStockThreshold = 10;

                    db.Ingredient.Add(ingredient);
                    db.SaveChanges();

                    TempData["Success"] = "Thêm nguyên liệu thành công!";
                    return RedirectToAction("IngredientList");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi thêm nguyên liệu: " + ex.Message;
            }

            return View(ingredient);
        }

        public ActionResult StockInbound()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var viewModel = new StockInboundViewModel
                {
                    StockInbound = new StockInboundFormModel
                    {
                        InboundCode = "IN" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        InboundDate = DateTime.Now,
                        CreatedBy = Session["Username"]?.ToString() ?? "Admin",
                        Status = "Draft"
                    },
                    AvailableIngredients = db.Ingredient.OrderBy(i => i.Name).ToList()
                };

                ViewBag.Employees = db.Employee.Where(e => e.IsActive)
                    .Select(e => new
                    {
                        Id = e.Id,
                        FullName = e.FullName
                    }).ToList();

                try
                {
                    var suppliers = db.Supplier.ToList();
                    ViewBag.Suppliers = suppliers.Select(s => new
                    {
                        Id = s.Id,
                        Name = GetSupplierName(s)
                    }).ToList();
                }
                catch
                {
                    ViewBag.Suppliers = new List<dynamic>();
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Inventory");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StockInbound(StockInboundViewModel model, string submitType)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            if (model.Details == null || !model.Details.Any())
            {
                TempData["Error"] = "Vui lòng nhập ít nhất một nguyên liệu.";
                return ReloadViewOnError(model);
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu nhập không hợp lệ, vui lòng kiểm tra lại.";
                return ReloadViewOnError(model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var inbound = new StockInbound
                    {
                        InboundCode = model.StockInbound.InboundCode,
                        InboundDate = model.StockInbound.InboundDate,
                        EmployeeId = model.StockInbound.EmployeeId,
                        SupplierId = model.StockInbound.SupplierId,
                        Notes = model.StockInbound.Notes,
                        CreatedBy = Session["Username"]?.ToString(),
                        Status = (submitType == "draft" ? "Draft" : "Completed")
                    };
                    db.StockInbound.Add(inbound);
                    db.SaveChanges();

                    foreach (var d in model.Details)
                    {
                        var detail = new StockInboundDetail
                        {
                            StockInboundId = inbound.Id,
                            IngredientId = d.IngredientId,
                            Quantity = d.Quantity,
                            UnitPrice = d.UnitPrice,
                            ExpiryDate = d.ExpiryDate,
                            BatchNumber = d.BatchNumber
                        };
                        db.StockInboundDetail.Add(detail);

                        if (submitType != "draft")
                        {
                            var ing = db.Ingredient.Find(d.IngredientId);
                            if (ing != null)
                            {
                                ing.AvailableStock += d.Quantity;
                                ing.EstimatedCost = d.UnitPrice;
                            }
                        }
                    }

                    db.SaveChanges();

                    transaction.Commit();
                    TempData["Success"] = submitType == "draft"
                                    ? "Đã lưu nháp phiếu nhập kho!"
                                    : "Nhập kho thành công!";

                    return RedirectToAction("IngredientList", "Management");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Có lỗi xảy ra khi nhập kho: " + ex.Message;
                    return ReloadViewOnError(model);
                }
            }
        }

        public ActionResult StockOutbound()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var viewModel = new StockOutboundViewModel
                {
                    StockOutbound = new StockOutboundFormModel
                    {
                        OutboundCode = "OUT" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        OutboundDate = DateTime.Now,
                        CreatedBy = Session["Username"]?.ToString() ?? "Admin",
                        Status = "Draft"
                    },
                    AvailableIngredients = db.Ingredient.Where(i => i.AvailableStock > 0).OrderBy(i => i.Name).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Inventory");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessStockOutbound(int? ingredientId, decimal? quantity, string purpose, string notes)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (!ingredientId.HasValue)
                {
                    TempData["Error"] = "Vui lòng chọn nguyên liệu.";
                    return RedirectToAction("StockOutbound");
                }

                if (!quantity.HasValue || quantity <= 0)
                {
                    TempData["Error"] = "Số lượng phải lớn hơn 0.";
                    return RedirectToAction("StockOutbound");
                }

                if (string.IsNullOrEmpty(purpose))
                {
                    TempData["Error"] = "Vui lòng nhập lý do xuất kho.";
                    return RedirectToAction("StockOutbound");
                }

                var ingredient = db.Ingredient.Find(ingredientId.Value);
                if (ingredient == null)
                {
                    TempData["Error"] = "Không tìm thấy nguyên liệu.";
                    return RedirectToAction("StockOutbound");
                }

                if (ingredient.AvailableStock < quantity.Value)
                {
                    TempData["Error"] = $"Không đủ tồn kho! Chỉ còn {ingredient.AvailableStock} {ingredient.Unit}.";
                    return RedirectToAction("StockOutbound");
                }

                ingredient.AvailableStock -= quantity.Value;

                if (purpose == "Hỏng hóc" || purpose == "Hết hạn" || purpose == "Hư hỏng")
                {
                    var damagedStock = new DamagedStock
                    {
                        IngredientId = ingredientId.Value,
                        Quantity = quantity.Value,
                        DamageDate = DateTime.Now,
                        Reason = purpose + (string.IsNullOrEmpty(notes) ? "" : " - " + notes),
                        ReportedByEmployeeId = GetCurrentEmployeeIdFromSession()
                    };

                    db.DamagedStock.Add(damagedStock);
                }

                db.SaveChanges();

                TempData["Success"] = $"Xuất kho thành công! Đã xuất {quantity.Value} {ingredient.Unit} {ingredient.Name}";
                return RedirectToAction("Inventory");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xuất kho: " + ex.Message;
                return RedirectToAction("StockOutbound");
            }
        }

        public ActionResult StockInboundList()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var inboundList = db.StockInbound
                    .Include("Employee")
                    .Include("Supplier")
                    .Include("StockInboundDetails")
                    .OrderByDescending(s => s.InboundDate)
                    .Select(s => new StockInboundListItem
                    {
                        Id = s.Id,
                        InboundCode = "IN" + s.Id.ToString().PadLeft(6, '0'),
                        InboundDate = s.InboundDate,
                        EmployeeName = s.Employee.FullName,
                        SupplierName = s.Supplier != null ? GetSupplierName(s.Supplier) : "Không có",
                        TotalCost = s.TotalCost,
                        Status = "Hoàn thành",
                        ItemCount = s.StockInboundDetail.Count
                    })
                    .ToList();

                var viewModel = new StockInboundListViewModel
                {
                    InboundList = inboundList
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách phiếu nhập: " + ex.Message;
                return View(new StockInboundListViewModel());
            }
        }

        public ActionResult StockOutboundList()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var viewModel = new StockOutboundListViewModel
                {
                    OutboundList = new List<StockOutboundListItem>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách phiếu xuất: " + ex.Message;
                return View(new StockOutboundListViewModel());
            }
        }

        public ActionResult DamagedStock()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ViewBag.Ingredients = db.Ingredient.OrderBy(i => i.Name).ToList();

                ViewBag.Employees = db.Employee.Where(e => e.IsActive)
                    .Select(e => new
                    {
                        Id = e.Id,
                        FullName = e.FullName
                    }).ToList();

                return View(new DamagedStock { DamageDate = DateTime.Now });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Inventory");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DamagedStock(DamagedStock model, string submitType)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var ingredient = db.Ingredient.Find(model.IngredientId);
                    if (ingredient == null)
                    {
                        ModelState.AddModelError("IngredientId", "Không tìm thấy nguyên liệu.");
                    }
                    else if (ingredient.AvailableStock < model.Quantity)
                    {
                        ModelState.AddModelError("Quantity", $"Số lượng báo cáo vượt quá tồn kho hiện có ({ingredient.AvailableStock} {ingredient.Unit}).");
                    }
                    else
                    {
                        db.DamagedStock.Add(model);

                        if (submitType == "submit")
                        {
                            ingredient.AvailableStock -= model.Quantity;
                        }

                        db.SaveChanges();

                        string message = submitType == "draft"
                            ? $"Đã lưu nháp báo cáo hàng hỏng: {model.Quantity} {ingredient.Unit} {ingredient.Name}"
                            : $"Đã gửi báo cáo hàng hỏng thành công: {model.Quantity} {ingredient.Unit} {ingredient.Name}. Tồn kho đã được cập nhật.";

                        TempData["Success"] = message;
                        return RedirectToAction("DamagedStockList");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tạo báo cáo: " + ex.Message;
            }

            ViewBag.Ingredients = db.Ingredient.OrderBy(i => i.Name).ToList();
            ViewBag.Employees = db.Employee.Where(e => e.IsActive)
                .Select(e => new
                {
                    Id = e.Id,
                    FullName = e.FullName
                }).ToList();

            return View(model);
        }

        public ActionResult DamagedStockList()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var damagedStocks = db.DamagedStock
                    .Include("Ingredient")
                    .Include("Employee")
                    .OrderByDescending(d => d.DamageDate)
                    .ToList();

                return View(damagedStocks);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách báo cáo: " + ex.Message;
                return View(new List<DamagedStock>());
            }
        }

        #endregion

        #region Quick Stock Operations

        [HttpPost]
        public ActionResult QuickStockIn(int ingredientId, decimal quantity, decimal unitPrice, string notes)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                if (quantity <= 0)
                {
                    return Json(new { success = false, message = "Số lượng phải lớn hơn 0!" });
                }

                var ingredient = db.Ingredient.Find(ingredientId);
                if (ingredient == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy nguyên liệu!" });
                }

                var stockInbound = new StockInbound
                {
                    SupplierId = null,
                    EmployeeId = GetCurrentEmployeeIdFromSession(),
                    InboundDate = DateTime.Now,
                    TotalCost = quantity * unitPrice,
                    Notes = notes ?? "Nhập kho nhanh"
                };

                db.StockInbound.Add(stockInbound);
                db.SaveChanges();

                var inboundDetail = new StockInboundDetail
                {
                    StockInboundId = stockInbound.Id,
                    IngredientId = ingredientId,
                    Quantity = quantity,
                    UnitPrice = unitPrice
                };

                db.StockInboundDetail.Add(inboundDetail);

                ingredient.AvailableStock += quantity;

                if (unitPrice > 0)
                {
                    ingredient.EstimatedCost = unitPrice;
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Nhập kho thành công {quantity} {ingredient.Unit} {ingredient.Name}!",
                    newStock = ingredient.AvailableStock,
                    totalCost = quantity * unitPrice
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult QuickStockOut(int ingredientId, decimal quantity, string purpose, string notes)
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                if (quantity <= 0)
                {
                    return Json(new { success = false, message = "Số lượng phải lớn hơn 0!" });
                }

                var ingredient = db.Ingredient.Find(ingredientId);
                if (ingredient == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy nguyên liệu!" });
                }

                if (ingredient.AvailableStock < quantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không đủ tồn kho! Chỉ còn {ingredient.AvailableStock} {ingredient.Unit}."
                    });
                }

                if (purpose == "Hỏng hóc" || purpose == "Hết hạn")
                {
                    var damagedStock = new DamagedStock
                    {
                        IngredientId = ingredientId,
                        Quantity = quantity,
                        DamageDate = DateTime.Now,
                        Reason = purpose + (string.IsNullOrEmpty(notes) ? "" : " - " + notes),
                        ReportedByEmployeeId = GetCurrentEmployeeIdFromSession()
                    };

                    db.DamagedStock.Add(damagedStock);
                }

                ingredient.AvailableStock -= quantity;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Xuất kho thành công {quantity} {ingredient.Unit} {ingredient.Name}!",
                    newStock = ingredient.AvailableStock,
                    reason = purpose
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        private int GetCurrentEmployeeIdFromSession()
        {
            if (Session["EmployeeId"] != null)
            {
                return (int)Session["EmployeeId"];
            }

            var adminEmployee = db.Employee.FirstOrDefault(e => e.Role.RoleName == "Admin" && e.IsActive);
            return adminEmployee?.Id ?? 1;
        }

        #endregion

        #region Sample Data Creation

        [HttpPost]
        public ActionResult CreateSampleIngredients()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                if (db.Ingredient.Any())
                {
                    return Json(new { success = false, message = "Đã có dữ liệu nguyên liệu trong hệ thống!" });
                }

                var sampleIngredients = new List<Ingredient>
                {
                    new Ingredient { Name = "Thịt bò", Unit = "kg", AvailableStock = 15.5m, EstimatedCost = 200000m, LowStockThreshold = 5m },
                    new Ingredient { Name = "Thịt heo", Unit = "kg", AvailableStock = 12.0m, EstimatedCost = 150000m, LowStockThreshold = 3m },
                    new Ingredient { Name = "Thịt gà", Unit = "kg", AvailableStock = 8.5m, EstimatedCost = 120000m, LowStockThreshold = 2m },
                    new Ingredient { Name = "Tôm", Unit = "kg", AvailableStock = 3.2m, EstimatedCost = 300000m, LowStockThreshold = 1m },
                    new Ingredient { Name = "Cá", Unit = "kg", AvailableStock = 4.8m, EstimatedCost = 180000m, LowStockThreshold = 2m },
                    new Ingredient { Name = "Cà chua", Unit = "kg", AvailableStock = 6.0m, EstimatedCost = 25000m, LowStockThreshold = 2m },
                    new Ingredient { Name = "Hành tây", Unit = "kg", AvailableStock = 8.5m, EstimatedCost = 20000m, LowStockThreshold = 3m },
                    new Ingredient { Name = "Tỏi", Unit = "kg", AvailableStock = 2.1m, EstimatedCost = 45000m, LowStockThreshold = 1m },
                    new Ingredient { Name = "Gừng", Unit = "kg", AvailableStock = 1.5m, EstimatedCost = 35000m, LowStockThreshold = 0.5m },
                    new Ingredient { Name = "Ớt", Unit = "kg", AvailableStock = 0.8m, EstimatedCost = 40000m, LowStockThreshold = 0.3m },
                    new Ingredient { Name = "Gạo", Unit = "kg", AvailableStock = 25.0m, EstimatedCost = 22000m, LowStockThreshold = 10m },
                    new Ingredient { Name = "Mì", Unit = "gói", AvailableStock = 50m, EstimatedCost = 15000m, LowStockThreshold = 20m },
                    new Ingredient { Name = "Dầu ăn", Unit = "lít", AvailableStock = 8.5m, EstimatedCost = 35000m, LowStockThreshold = 3m },
                    new Ingredient { Name = "Muối", Unit = "kg", AvailableStock = 5.0m, EstimatedCost = 8000m, LowStockThreshold = 2m },
                    new Ingredient { Name = "Đường", Unit = "kg", AvailableStock = 4.2m, EstimatedCost = 18000m, LowStockThreshold = 2m },
                    new Ingredient { Name = "Nước mắm", Unit = "chai", AvailableStock = 12m, EstimatedCost = 25000m, LowStockThreshold = 5m },
                    new Ingredient { Name = "Nước ngọt", Unit = "chai", AvailableStock = 48m, EstimatedCost = 12000m, LowStockThreshold = 20m },
                    new Ingredient { Name = "Bia", Unit = "chai", AvailableStock = 36m, EstimatedCost = 18000m, LowStockThreshold = 15m },
                    new Ingredient { Name = "Nấm", Unit = "kg", AvailableStock = 0.5m, EstimatedCost = 60000m, LowStockThreshold = 2m },
                    new Ingredient { Name = "Rau cải", Unit = "kg", AvailableStock = 0m, EstimatedCost = 15000m, LowStockThreshold = 3m }
                };

                foreach (var ingredient in sampleIngredients)
                {
                    db.Ingredient.Add(ingredient);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã tạo thành công {sampleIngredients.Count} nguyên liệu mẫu!",
                    count = sampleIngredients.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi tạo dữ liệu mẫu: " + ex.Message
                });
            }
        }

        #endregion

        #region Sample Data Creation - Employees

        [HttpPost]
        public ActionResult CreateSampleEmployees()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return Json(new { success = false, message = "Không có quyền truy cập!" });
            }

            try
            {
                if (db.Employee.Any())
                {
                    return Json(new { success = false, message = "Đã có nhân viên trong hệ thống!" });
                }

                var roles = new List<Role>();
                if (!db.Role.Any(r => r.RoleName == "Admin")) roles.Add(new Role { RoleName = "Admin" });
                if (!db.Role.Any(r => r.RoleName == "Manager")) roles.Add(new Role { RoleName = "Manager" });
                if (!db.Role.Any(r => r.RoleName == "Cashier")) roles.Add(new Role { RoleName = "Cashier" });
                if (!db.Role.Any(r => r.RoleName == "Staff")) roles.Add(new Role { RoleName = "Staff" });
                if (!db.Role.Any(r => r.RoleName == "Kitchen")) roles.Add(new Role { RoleName = "Kitchen" });

                foreach (var role in roles)
                {
                    db.Role.Add(role);
                }
                db.SaveChanges();

                var adminRole = db.Role.FirstOrDefault(r => r.RoleName == "Admin");
                var managerRole = db.Role.FirstOrDefault(r => r.RoleName == "Manager");
                var cashierRole = db.Role.FirstOrDefault(r => r.RoleName == "Cashier");
                var staffRole = db.Role.FirstOrDefault(r => r.RoleName == "Staff");
                var kitchenRole = db.Role.FirstOrDefault(r => r.RoleName == "Kitchen");

                var sampleEmployees = new List<Employee>
                {
                    new Employee { FullName = "Nguyễn Văn Admin", UserName = "admin", PasswordHash = HashPassword("admin123"), Email = "admin@nhahang.com", PhoneNumber = "0901234567", RoleId = adminRole?.Id ?? 1, HireDate = DateTime.Now.AddMonths(-12), IsActive = true },
                    new Employee { FullName = "Trần Thị Manager", UserName = "manager", PasswordHash = HashPassword("manager123"), Email = "manager@nhahang.com", PhoneNumber = "0902345678", RoleId = managerRole?.Id ?? 2, HireDate = DateTime.Now.AddMonths(-10), IsActive = true },
                    new Employee { FullName = "Lê Văn Thu Ngân", UserName = "cashier1", PasswordHash = HashPassword("cashier123"), Email = "cashier1@nhahang.com", PhoneNumber = "0903456789", RoleId = cashierRole?.Id ?? 3, HireDate = DateTime.Now.AddMonths(-8), IsActive = true },
                    new Employee { FullName = "Hoàng Thị Thu Ngân 2", UserName = "cashier2", PasswordHash = HashPassword("cashier123"), Email = "cashier2@nhahang.com", PhoneNumber = "0904567890", RoleId = cashierRole?.Id ?? 3, HireDate = DateTime.Now.AddMonths(-6), IsActive = true },
                    new Employee { FullName = "Phạm Văn Bếp Trưởng", UserName = "chef", PasswordHash = HashPassword("chef123"), Email = "chef@nhahang.com", PhoneNumber = "0905678901", RoleId = kitchenRole?.Id ?? 5, HireDate = DateTime.Now.AddMonths(-9), IsActive = true },
                    new Employee { FullName = "Vũ Thị Phụ Bếp", UserName = "kitchen1", PasswordHash = HashPassword("kitchen123"), Email = "kitchen1@nhahang.com", PhoneNumber = "0906789012", RoleId = kitchenRole?.Id ?? 5, HireDate = DateTime.Now.AddMonths(-4), IsActive = true },
                    new Employee { FullName = "Đỗ Văn Nhân Viên", UserName = "staff1", PasswordHash = HashPassword("staff123"), Email = "staff1@nhahang.com", PhoneNumber = "0907890123", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddMonths(-3), IsActive = true },
                    new Employee { FullName = "Bùi Thị Nhân Viên 2", UserName = "staff2", PasswordHash = HashPassword("staff123"), Email = "staff2@nhahang.com", PhoneNumber = "0908901234", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddMonths(-2), IsActive = true },
                    new Employee { FullName = "Lý Văn Phục Vụ", UserName = "waiter1", PasswordHash = HashPassword("waiter123"), Email = "waiter1@nhahang.com", PhoneNumber = "0909012345", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddMonths(-1), IsActive = true },
                    new Employee { FullName = "Mai Thị Phục Vụ 2", UserName = "waiter2", PasswordHash = HashPassword("waiter123"), Email = "waiter2@nhahang.com", PhoneNumber = "0900123456", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddDays(-15), IsActive = false }
                };

                foreach (var employee in sampleEmployees)
                {
                    db.Employee.Add(employee);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã tạo thành công {sampleEmployees.Count} nhân viên mẫu và {roles.Count} vai trò!",
                    employeeCount = sampleEmployees.Count,
                    roleCount = roles.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi tạo nhân viên mẫu: " + ex.Message
                });
            }
        }

        #endregion

        #region Reports

        public ActionResult Reports()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction("Dashboard", "ReportsManagement");
        }

        #endregion

        #region Menu Helper Methods

        private List<MenuItem> GetFilteredMenuItems(string search, string category, string status)
        {
            var query = db.MenuItem.AsQueryable();
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

        private List<MenuCombo> GetAllMenuCombos()
        {
            try
            {
                return db.MenuCombo
                   .Include(mc => mc.MenuComboItem.Select(mci => mci.MenuItem))
                   .ToList();
            }
            catch (Exception)
            {
                return new List<MenuCombo>();
            }
        }

        private MenuStatsViewModel GetMenuStats()
        {
            int comboCount = 0;
            try
            {
                comboCount = db.MenuCombo.Count();
            }
            catch (Exception) { }

            var stats = new MenuStatsViewModel
            {
                TotalItems = db.MenuItem.Count(),
                AvailableItems = db.MenuItem.Count(m => m.IsAvailable),
                UnavailableItems = db.MenuItem.Count(m => !m.IsAvailable),
                TotalCombos = comboCount
            };
            return stats;
        }

        private List<Ingredient> GetAllIngredients()
        {
            return db.Ingredient.OrderBy(i => i.Name).ToList();
        }

        private MenuItem GetMenuItemById(int id)
        {
            return db.MenuItem.Find(id);
        }

        private List<MenuItemIngredientViewModel> GetMenuItemIngredients(int menuItemId)
        {
            return db.MenuItemIngredient
               .Where(mii => mii.MenuItemId == menuItemId)
               .Include(mii => mii.Ingredient)
                        .Select(mii => new MenuItemIngredientViewModel
                        {
                            IngredientId = mii.IngredientId,
                            IngredientName = mii.Ingredient.Name,
                            RequiredQuantity = mii.RequiredQuantity,
                            Unit = mii.Unit
                        })
               .ToList();
        }

        private List<MenuItem> GetMenuModels()
        {
            return db.MenuItem.ToList();
        }

        #endregion

        #region Inventory Report

        public ActionResult InventoryReport()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var reportData = GetInventoryReportData();
                return View(reportData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải báo cáo kho: " + ex.Message;
                return RedirectToAction("Inventory");
            }
        }

        private InventoryReportViewModel GetInventoryReportData()
        {
            try
            {
                var ingredients = db.Ingredient.ToList();
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);

                var totalIngredients = ingredients.Count;
                var totalValue = ingredients.Sum(i => i.AvailableStock * i.EstimatedCost);
                var lowStockCount = ingredients.Count(i => i.LowStockThreshold.HasValue && i.AvailableStock <= i.LowStockThreshold.Value);
                var outOfStockCount = ingredients.Count(i => i.AvailableStock <= 0);

                var monthlyInbound = db.StockInbound
                    .Where(s => s.InboundDate >= startOfMonth)
                    .Sum(s => (decimal?)s.TotalCost) ?? 0;

                var monthlyDamage = db.DamagedStock
                    .Where(d => d.DamageDate >= startOfMonth)
                    .Join(db.Ingredient, d => d.IngredientId, i => i.Id, (d, i) => new { d.Quantity, i.EstimatedCost })
                    .Sum(x => (decimal?)(x.Quantity * x.EstimatedCost)) ?? 0;

                var highValueIngredients = ingredients
                    .Select(i => new InventoryItemReportModel
                    {
                        IngredientName = i.Name,
                        Unit = i.Unit,
                        AvailableStock = i.AvailableStock,
                        EstimatedCost = i.EstimatedCost,
                        TotalValue = i.AvailableStock * i.EstimatedCost,
                        LowStockThreshold = i.LowStockThreshold ?? 0,
                        StockStatus = GetStockStatus(i)
                    })
                    .OrderByDescending(i => i.TotalValue)
                    .Take(10)
                    .ToList();

                var alertIngredients = ingredients
                    .Where(i => i.LowStockThreshold.HasValue && i.AvailableStock <= i.LowStockThreshold.Value)
                    .Select(i => new InventoryItemReportModel
                    {
                        IngredientName = i.Name,
                        Unit = i.Unit,
                        AvailableStock = i.AvailableStock,
                        EstimatedCost = i.EstimatedCost,
                        TotalValue = i.AvailableStock * i.EstimatedCost,
                        LowStockThreshold = i.LowStockThreshold ?? 0,
                        StockStatus = GetStockStatus(i)
                    })
                    .OrderBy(i => i.AvailableStock)
                    .ToList();

                var recentInbounds = db.StockInbound
                    .Include("Employee")
                    .Include("Supplier")
                    .OrderByDescending(s => s.InboundDate)
                    .Take(10)
                    .ToList()
                    .Select(s => new InboundActivityModel
                    {
                        InboundDate = s.InboundDate,
                        EmployeeName = s.Employee?.FullName ?? "N/A",
                        SupplierName = s.Supplier != null ? GetSupplierName(s.Supplier) : "Không có",
                        TotalCost = s.TotalCost,
                        ItemCount = s.StockInboundDetail?.Count ?? 0
                    })
                    .ToList();

                var recentDamages = db.DamagedStock
                    .Include("Ingredient")
                    .Include("Employee")
                    .OrderByDescending(d => d.DamageDate)
                    .Take(10)
                    .ToList()
                    .Select(d => new DamageReportModel
                    {
                        DamageDate = d.DamageDate,
                        IngredientName = d.Ingredient?.Name ?? "N/A",
                        Quantity = d.Quantity,
                        Unit = d.Ingredient?.Unit ?? "",
                        Reason = d.Reason ?? "Không có lý do",
                        ReportedBy = d.Employee?.FullName ?? "N/A",
                        EstimatedLoss = d.Quantity * (d.Ingredient?.EstimatedCost ?? 0)
                    })
                    .ToList();

                return new InventoryReportViewModel
                {
                    TotalIngredients = totalIngredients,
                    TotalInventoryValue = totalValue,
                    LowStockCount = lowStockCount,
                    OutOfStockCount = outOfStockCount,
                    MonthlyInboundValue = monthlyInbound,
                    MonthlyDamageValue = monthlyDamage,
                    HighValueIngredients = highValueIngredients,
                    AlertIngredients = alertIngredients,
                    RecentInbounds = recentInbounds,
                    RecentDamages = recentDamages,
                    ReportGeneratedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetInventoryReportData: {ex.Message}");
                return new InventoryReportViewModel();
            }
        }

        private string GetStockStatus(Ingredient ingredient)
        {
            if (ingredient.AvailableStock <= 0)
                return "out-of-stock";

            if (ingredient.LowStockThreshold.HasValue && ingredient.AvailableStock <= ingredient.LowStockThreshold.Value)
                return "low-stock";

            return "in-stock";
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private string GetSupplierName(Supplier supplier)
        {
            if (supplier == null)
                return "Chưa có nhà cung cấp";

            try
            {
                var supplierType = supplier.GetType();

                var nameProperty = supplierType.GetProperty("Name") ??
                                   supplierType.GetProperty("SupplierName") ??
                                   supplierType.GetProperty("CompanyName") ??
                                   supplierType.GetProperty("BusinessName");

                if (nameProperty != null)
                {
                    var nameValue = nameProperty.GetValue(supplier)?.ToString();
                    if (!string.IsNullOrWhiteSpace(nameValue))
                        return nameValue;
                }

                var idProperty = supplierType.GetProperty("Id") ??
                                 supplierType.GetProperty("SupplierId");

                var idValue = idProperty?.GetValue(supplier)?.ToString();

                return idValue != null
                    ? $"Nhà cung cấp {idValue}"
                    : "Nhà cung cấp không xác định";
            }
            catch
            {
                return "Nhà cung cấp không xác định";
            }
        }

        #region Ingredient Detail and History

        public ActionResult EditIngredient(int id)
        {
            var ingredient = db.Ingredient.Find(id);

            if (ingredient == null)
            {
                return HttpNotFound();
            }
            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditIngredient([Bind(Include = "Id, Name, Unit, EstimatedCost, LowStockThreshold, AvailableStock")] Ingredient formData)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var ingredientInDb = db.Ingredient.Find(formData.Id);

                    if (ingredientInDb == null)
                    {
                        return HttpNotFound();
                    }

                    ingredientInDb.Name = formData.Name;
                    ingredientInDb.Unit = formData.Unit;
                    ingredientInDb.AvailableStock = formData.AvailableStock;
                    ingredientInDb.LowStockThreshold = formData.LowStockThreshold;
                    ingredientInDb.EstimatedCost = formData.EstimatedCost;

                    db.SaveChanges();

                    return RedirectToAction("IngredientList");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi khi lưu dữ liệu: " + ex.Message);
                }
            }
            return View(formData);
        }

        [HttpGet]
        public JsonResult GetIngredientDetail(int id)
        {
            try
            {
                var ingredient = db.Ingredient.Find(id);
                if (ingredient == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy nguyên liệu!" }, JsonRequestBehavior.AllowGet);
                }

                var latestInbound = db.StockInboundDetail
                    .Where(d => d.IngredientId == id)
                    .OrderByDescending(d => d.StockInbound.InboundDate)
                    .FirstOrDefault();

                string latestSupplier = null;
                if (latestInbound != null && latestInbound.StockInbound?.Supplier != null)
                {
                    latestSupplier = latestInbound.StockInbound.Supplier.Name;
                }

                string statusBadge;
                if (ingredient.AvailableStock <= 0)
                {
                    statusBadge = "<span class='badge bg-danger'>Hết hàng</span>";
                }
                else if (ingredient.LowStockThreshold.HasValue && ingredient.AvailableStock <= ingredient.LowStockThreshold.Value)
                {
                    statusBadge = "<span class='badge bg-warning text-dark'>Sắp hết</span>";
                }
                else
                {
                    statusBadge = "<span class='badge bg-success'>Đủ hàng</span>";
                }

                var recentTransactions = new List<dynamic>();

                var recentInbound = db.StockInboundDetail
                    .Where(d => d.IngredientId == id)
                    .OrderByDescending(d => d.StockInbound.InboundDate)
                    .Take(5)
                    .ToList()
                    .Select(d => new
                    {
                        date = d.StockInbound.InboundDate,
                        type = "in",
                        typeText = "Nhập kho",
                        quantity = d.Quantity,
                        notes = d.StockInbound.Notes ?? "Không có ghi chú"
                    });

                recentTransactions.AddRange(recentInbound);

                var recentDamaged = db.DamagedStock
                    .Where(d => d.IngredientId == id)
                    .OrderByDescending(d => d.DamageDate)
                    .Take(5)
                    .ToList()
                    .Select(d => new
                    {
                        date = d.DamageDate,
                        type = "out",
                        typeText = "Hỏng hóc",
                        quantity = d.Quantity,
                        notes = d.Reason ?? "Không có lý do"
                    });

                recentTransactions.AddRange(recentDamaged);

                var sortedTransactions = recentTransactions
                    .OrderByDescending(t => t.date)
                    .Take(10)
                    .ToList();

                return Json(new
                {
                    success = true,
                    ingredient = new
                    {
                        id = ingredient.Id,
                        name = ingredient.Name,
                        unit = ingredient.Unit,
                        availableStock = ingredient.AvailableStock,
                        estimatedCost = ingredient.EstimatedCost,
                        lowStockThreshold = ingredient.LowStockThreshold,
                        latestSupplier = latestSupplier,
                        statusBadge = statusBadge,
                        totalValue = ingredient.AvailableStock * ingredient.EstimatedCost
                    },
                    recentTransactions = sortedTransactions
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult DeleteIngredient(int id)
        {
            try
            {
                var ingredient = db.Ingredient.Find(id);
                if (ingredient == null)
                    return Json(new { success = false, message = "Không tìm thấy nguyên liệu." });

                bool isUsed = db.StockInboundDetail.Any(d => d.IngredientId == id);

                if (isUsed)
                    return Json(new { success = false, message = "Không thể xóa: Nguyên liệu đang được sử dụng." });

                db.Ingredient.Remove(ingredient);
                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        public ActionResult IngredientHistory(int id)
        {
            var role = Session["UserRole"]?.ToString().ToLower();
            if (role != "admin" && role != "manager")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var ingredient = db.Ingredient.Find(id);
                if (ingredient == null)
                {
                    TempData["Error"] = "Không tìm thấy nguyên liệu!";
                    return RedirectToAction("IngredientList");
                }

                var viewModel = new IngredientHistoryViewModel
                {
                    Ingredient = ingredient,
                    InboundHistory = db.StockInboundDetail
                        .Include("StockInbound")
                        .Include("StockInbound.Supplier")
                        .Where(d => d.IngredientId == id)
                        .OrderByDescending(d => d.StockInbound.InboundDate)
                        .ToList(),

                    DamagedHistory = db.DamagedStock
                        .Where(d => d.IngredientId == id)
                        .OrderByDescending(d => d.DamageDate)
                        .ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải lịch sử: " + ex.Message;
                return RedirectToAction("IngredientList");
            }
        }

        #endregion

        private ActionResult ReloadViewOnError(StockInboundViewModel model)
        {
            model.AvailableIngredients = db.Ingredient.OrderBy(i => i.Name).ToList();

            model.AvailableEmployees = db.Employee
                                        .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                                        .ToList();

            model.AvailableSuppliers = db.Supplier
                                        .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                                        .ToList();

            return View("StockInbound", model);
        }
    }
}