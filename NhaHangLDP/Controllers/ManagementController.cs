using NhaHangLDP.Models;
using NhaHangLDP.Services.Management;
using NhaHangLDP.Services;
using NhaHangLDP.Helpers;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NhaHangLDP.Controllers
{
    public class ManagementController : Controller
    {
        private readonly NhaHangLDPEntities db = new NhaHangLDPEntities();
        private readonly TableAdminService tableService;
        private readonly EmployeeAdminService employeeService;
        private readonly MenuAdminService menuService;
        private readonly InventoryService inventoryService;
        private readonly ManagementDashboardDataService dashboardService;
        private readonly OrderQueryService orderQueryService;

        public ManagementController()
        {
            tableService = new TableAdminService(db);
            employeeService = new EmployeeAdminService(db);
            menuService = new MenuAdminService(db);
            inventoryService = new InventoryService(db);
            dashboardService = new ManagementDashboardDataService(db);
            orderQueryService = new OrderQueryService(db);
        }

        #region Authorization Helper

        private bool IsAuthorized()
        {
            return AuthorizationHelper.IsAdminOrManager(Session);
        }

        private ActionResult RedirectUnauthorized()
        {
            return RedirectToAction("Login", "Account");
        }

        #endregion

        #region Dashboard

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
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var viewModel = dashboardService.GetDashboardData();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải dữ liệu Dashboard: " + ex.Message;
                return View(new ManagementDashboardViewModel());
            }
        }

        #endregion

        #region Table Management

        public ActionResult TableManagement()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var tables = tableService.GetAllTables();
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
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.TableAreaId = new SelectList(tableService.GetAllTableAreas(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTable(RestaurantTable table)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (tableService.CreateTable(table, out errorMessage))
                {
                    TempData["Success"] = "Thêm bàn thành công!";
                    return RedirectToAction("TableManagement");
                }
                ModelState.AddModelError("TableNumber", errorMessage);
            }

            ViewBag.TableAreaId = new SelectList(tableService.GetAllTableAreas(), "Id", "Name", table.TableAreaId);
            return View(table);
        }

        public ActionResult EditTable(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var table = tableService.GetTableById(id);
            if (table == null)
            {
                TempData["Error"] = "Không tìm thấy bàn.";
                return RedirectToAction("TableManagement");
            }

            ViewBag.TableAreaId = new SelectList(tableService.GetAllTableAreas(), "Id", "Name", table.TableAreaId);
            return View(table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTable(RestaurantTable table)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (tableService.UpdateTable(table, out errorMessage))
                {
                    TempData["Success"] = "Cập nhật bàn thành công!";
                    return RedirectToAction("TableManagement");
                }
                ModelState.AddModelError("TableNumber", errorMessage);
            }

            ViewBag.TableAreaId = new SelectList(tableService.GetAllTableAreas(), "Id", "Name", table.TableAreaId);
            return View(table);
        }

        public JsonResult DeleteTable(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập." });

            string errorMessage;
            if (tableService.DeleteTable(id, out errorMessage))
                return Json(new { success = true, message = "Xóa bàn thành công!" });

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Employee Management

        public ActionResult EmployeeManagement()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var employees = employeeService.GetAllEmployees();
                return View(employees);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách nhân viên: " + ex.Message;
                return View(new List<Employee>());
            }
        }

        public ActionResult CreateEmployee()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.RoleId = new SelectList(employeeService.GetAllRoles(), "Id", "RoleName");
            return View(new Employee());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateEmployee(Employee employee, string ConfirmPassword)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (employee.PasswordHash != ConfirmPassword)
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");

            if (employeeService.IsUsernameExists(employee.UserName))
                ModelState.AddModelError("UserName", "Tên đăng nhập này đã tồn tại.");

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (employeeService.CreateEmployee(employee, out errorMessage))
                {
                    TempData["Success"] = "Đã thêm nhân viên mới thành công!";
                    return RedirectToAction("EmployeeManagement");
                }
                TempData["Error"] = errorMessage;
            }

            ViewBag.RoleId = new SelectList(employeeService.GetAllRoles(), "Id", "RoleName", employee.RoleId);
            return View(employee);
        }

        public ActionResult EditEmployee(int? id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (id == null)
                return new StatusCodeResult((int)HttpStatusCode.BadRequest);

            Employee employee = employeeService.GetEmployeeById(id.Value);
            if (employee == null)
                return NotFound("Không tìm thấy nhân viên này.");

            ViewBag.RoleId = new SelectList(employeeService.GetAllRoles(), "Id", "RoleName", employee.RoleId);
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditEmployee(Employee employee, string NewPassword, string ConfirmPassword)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (employeeService.IsUsernameExists(employee.UserName, employee.Id))
                ModelState.AddModelError("UserName", "Tên đăng nhập này đã tồn tại.");

            string validatedPassword = null;
            if (!string.IsNullOrEmpty(NewPassword))
            {
                if (NewPassword.Length < 6)
                    ModelState.AddModelError("NewPassword", "Mật khẩu mới phải có ít nhất 6 ký tự.");
                else if (NewPassword != ConfirmPassword)
                    ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
                else
                    validatedPassword = NewPassword;
            }

            if (ModelState.ContainsKey("PasswordHash"))
                ModelState.Remove("PasswordHash");

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (employeeService.UpdateEmployee(employee, validatedPassword, out errorMessage))
                {
                    TempData["Success"] = "Cập nhật thông tin nhân viên thành công!";
                    return RedirectToAction("EmployeeManagement");
                }
                TempData["Error"] = errorMessage;
            }

            ViewBag.RoleId = new SelectList(employeeService.GetAllRoles(), "Id", "RoleName", employee.RoleId);
            return View(employee);
        }

        [HttpPost]
        public ActionResult ToggleEmployeeStatus(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

            string errorMessage;
            bool newStatus;
            string currentUserName = Session["UserName"]?.ToString();

            if (employeeService.ToggleEmployeeStatus(id, currentUserName, out errorMessage, out newStatus))
            {
                string statusMessage = newStatus ? "kích hoạt" : "khóa";
                return Json(new { success = true, message = $"Đã {statusMessage} tài khoản nhân viên thành công." });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult CreateSampleEmployees()
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" });

            string errorMessage;
            int employeeCount, roleCount;
            if (employeeService.CreateSampleEmployees(out errorMessage, out employeeCount, out roleCount))
            {
                return Json(new
                {
                    success = true,
                    message = $"Đã tạo thành công {employeeCount} nhân viên mẫu và {roleCount} vai trò!",
                    employeeCount,
                    roleCount
                });
            }

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Menu Management

        public ActionResult MenuManagement(string search = "", string category = "", string status = "")
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var viewModel = new MenuManagementViewModel
            {
                MenuItems = menuService.GetFilteredMenuItems(search, category, status),
                MenuCombos = menuService.GetAllMenuCombos(),
                Stats = menuService.GetMenuStats(),
                SearchTerm = search,
                CategoryFilter = category,
                StatusFilter = status
            };
            return View(viewModel);
        }

        public ActionResult CreateMenuItem()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var viewModel = new MenuItemFormViewModel
            {
                MenuItem = new MenuItem { IsAvailable = true },
                IsEdit = false,
                AvailableIngredients = menuService.GetAllIngredients()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateMenuItem(MenuItemFormViewModel model)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                    string serverPath = Path.Combine(Server.MapPath("~/images/menu/"), fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(serverPath));
                    model.ImageFile.SaveAs(serverPath);
                    model.MenuItem.ImageUrl = fileName;
                }

                string errorMessage;
                if (menuService.CreateMenuItem(model.MenuItem, model.SelectedIngredients, Session["Username"]?.ToString() ?? "Admin", out errorMessage))
                {
                    TempData["Success"] = "Thêm món ăn thành công!";
                    return RedirectToAction("MenuManagement");
                }
                TempData["Error"] = errorMessage;
            }

            model.AvailableIngredients = menuService.GetAllIngredients();
            return View(model);
        }

        public ActionResult EditMenuItem(int? id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (id == null)
                return new StatusCodeResult((int)HttpStatusCode.BadRequest);

            MenuItem menuItem = menuService.GetMenuItemById(id.Value);
            if (menuItem == null)
                return NotFound();

            var viewModel = new MenuItemFormViewModel
            {
                MenuItem = menuItem,
                IsEdit = true,
                AvailableIngredients = menuService.GetAllIngredients(),
                SelectedIngredients = menuService.GetMenuItemIngredients(id.Value)
            };

            return View("CreateMenuItem", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMenuItem(MenuItemFormViewModel model)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.ContainsKey("ImageFile"))
                ModelState["ImageFile"].Errors.Clear();

            if (ModelState.IsValid)
            {
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                    string serverPath = Path.Combine(Server.MapPath("~/images/menu/"), fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(serverPath));
                    model.ImageFile.SaveAs(serverPath);
                    model.MenuItem.ImageUrl = fileName;
                }

                string errorMessage;
                if (menuService.UpdateMenuItem(model.MenuItem, model.SelectedIngredients, out errorMessage))
                {
                    TempData["Success"] = "Cập nhật món ăn thành công!";
                    return RedirectToAction("MenuManagement");
                }
                TempData["Error"] = errorMessage;
            }

            model.AvailableIngredients = menuService.GetAllIngredients();
            return View("CreateMenuItem", model);
        }

        [HttpPost]
        public JsonResult ToggleMenuItemStatus(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập." });

            bool newStatus;
            string errorMessage;
            if (menuService.ToggleMenuItemStatus(id, out newStatus, out errorMessage))
                return Json(new { success = true, isAvailable = newStatus });

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult DeleteMenuItem(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập." });

            string errorMessage;
            if (menuService.DeleteMenuItem(id, out errorMessage))
                return Json(new { success = true, message = "Xóa món ăn thành công!" });

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Combo Management

        public ActionResult CreateCombo()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.AvailableMenuItems = menuService.GetAvailableMenuItems();
            return View(new MenuCombo());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCombo([Bind("Name", "Description", "ComboPrice")] MenuCombo combo, List<int> selectedMenuItems)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (selectedMenuItems == null || selectedMenuItems.Count < 2)
                ModelState.AddModelError("", "Bạn phải chọn ít nhất 2 món ăn cho combo.");

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (menuService.CreateCombo(combo, selectedMenuItems, out errorMessage))
                {
                    TempData["Success"] = "Tạo combo '" + combo.Name + "' thành công!";
                    return RedirectToAction("MenuManagement");
                }
                TempData["Error"] = errorMessage;
            }

            ViewBag.AvailableMenuItems = menuService.GetAvailableMenuItems();
            return View(combo);
        }

        public ActionResult EditCombo(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var combo = menuService.GetComboById(id);
            if (combo == null)
                return NotFound();

            ViewBag.AvailableMenuItems = menuService.GetAvailableMenuItems();
            return View("EditCombo", combo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCombo([Bind("Id", "Name", "Description", "ComboPrice")] MenuCombo combo, List<int> selectedMenuItems)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (selectedMenuItems == null || selectedMenuItems.Count < 2)
                ModelState.AddModelError("", "Bạn phải chọn ít nhất 2 món ăn cho combo.");

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (menuService.UpdateCombo(combo, selectedMenuItems, out errorMessage))
                {
                    TempData["Success"] = "Cập nhật combo '" + combo.Name + "' thành công!";
                    return RedirectToAction("MenuManagement");
                }
                TempData["Error"] = errorMessage;
            }

            ViewBag.AvailableMenuItems = menuService.GetAvailableMenuItems();
            return View("EditCombo", combo);
        }

        [HttpPost]
        public JsonResult DeleteCombo(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập." });

            string errorMessage;
            if (menuService.DeleteCombo(id, out errorMessage))
                return Json(new { success = true, message = "Xóa combo thành công!" });

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Inventory Management

        public ActionResult Inventory()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var inventoryStats = inventoryService.GetInventoryStats();
                return View(inventoryStats);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải thông tin kho: " + ex.Message;
                return View(new InventoryStatsViewModel());
            }
        }

        public ActionResult IngredientList()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var ingredients = inventoryService.GetIngredientsWithSupplier();
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
            if (!IsAuthorized())
                return RedirectUnauthorized();

            return View(new Ingredient());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateIngredient(Ingredient ingredient)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (inventoryService.CreateIngredient(ingredient, out errorMessage))
                {
                    TempData["Success"] = "Thêm nguyên liệu thành công!";
                    return RedirectToAction("IngredientList");
                }
                ModelState.AddModelError("Name", errorMessage);
            }

            return View(ingredient);
        }

        public ActionResult EditIngredient(int id)
        {
            var ingredient = inventoryService.GetIngredientById(id);
            if (ingredient == null)
                return NotFound();

            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditIngredient([Bind("Id", " Name", " Unit", " EstimatedCost", " LowStockThreshold", " AvailableStock")] Ingredient formData)
        {
            if (ModelState.IsValid)
            {
                string errorMessage;
                if (inventoryService.UpdateIngredient(formData, out errorMessage))
                    return RedirectToAction("IngredientList");

                ModelState.AddModelError("", errorMessage);
            }
            return View(formData);
        }

        [HttpGet]
        public JsonResult GetIngredientDetail(int id)
        {
            try
            {
                var result = inventoryService.GetIngredientDetail(id);
                if (result == null)
                    return Json(new { success = false, message = "Không tìm thấy nguyên liệu!" }, JsonRequestBehavior.AllowGet);

                return Json(new { success = true, ingredient = ((dynamic)result).ingredient, recentTransactions = ((dynamic)result).recentTransactions }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult DeleteIngredient(int id)
        {
            string errorMessage;
            if (inventoryService.DeleteIngredient(id, out errorMessage))
                return Json(new { success = true });

            return Json(new { success = false, message = errorMessage });
        }

        public ActionResult IngredientHistory(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var viewModel = inventoryService.GetIngredientHistory(id);
                if (viewModel == null)
                {
                    TempData["Error"] = "Không tìm thấy nguyên liệu!";
                    return RedirectToAction("IngredientList");
                }
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải lịch sử: " + ex.Message;
                return RedirectToAction("IngredientList");
            }
        }

        [HttpPost]
        public ActionResult CreateSampleIngredients()
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" });

            string errorMessage;
            int count;
            if (inventoryService.CreateSampleIngredients(out errorMessage, out count))
                return Json(new { success = true, message = $"Đã tạo thành công {count} nguyên liệu mẫu!", count });

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Stock Inbound

        public ActionResult StockInbound()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

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
                    AvailableIngredients = inventoryService.GetAllIngredients()
                };

                ViewBag.Employees = inventoryService.GetActiveEmployees()
                    .Select(e => new { Id = e.Id, FullName = e.FullName }).ToList();

                ViewBag.Suppliers = inventoryService.GetAllSuppliers()
                    .Select(s => new { Id = s.Id, Name = s.Name }).ToList();

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
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (model.Details == null || !model.Details.Any())
            {
                TempData["Error"] = "Vui lòng nhập ít nhất một nguyên liệu.";
                return ReloadStockInboundView(model);
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu nhập không hợp lệ, vui lòng kiểm tra lại.";
                return ReloadStockInboundView(model);
            }

            string errorMessage;
            if (inventoryService.ProcessStockInbound(model, submitType, Session["Username"]?.ToString(), out errorMessage))
            {
                TempData["Success"] = submitType == "draft" ? "Đã lưu nháp phiếu nhập kho!" : "Nhập kho thành công!";
                return RedirectToAction("IngredientList");
            }

            TempData["Error"] = errorMessage;
            return ReloadStockInboundView(model);
        }

        public ActionResult StockInboundList()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var viewModel = inventoryService.GetStockInboundList();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách phiếu nhập: " + ex.Message;
                return View(new StockInboundListViewModel());
            }
        }

        public ActionResult StockInboundDetail(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var viewModel = inventoryService.GetStockInboundDetail(id);
                if (viewModel == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu nhập kho.";
                    return RedirectToAction("StockInboundList");
                }
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("StockInboundList");
            }
        }

        private ActionResult ReloadStockInboundView(StockInboundViewModel model)
        {
            model.AvailableIngredients = inventoryService.GetAllIngredients();
            model.AvailableEmployees = inventoryService.GetActiveEmployees()
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName }).ToList();
            model.AvailableSuppliers = inventoryService.GetAllSuppliers()
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList();
            return View("StockInbound", model);
        }

        #endregion

        #region Stock Outbound

        public ActionResult StockOutbound()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

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
                    AvailableIngredients = inventoryService.GetIngredientsWithStock()
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
            if (!IsAuthorized())
                return RedirectUnauthorized();

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

            string errorMessage;
            if (inventoryService.ProcessStockOutbound(ingredientId.Value, quantity.Value, purpose, notes, GetCurrentEmployeeId(), out errorMessage))
            {
                var ingredient = inventoryService.GetIngredientById(ingredientId.Value);
                TempData["Success"] = $"Xuất kho thành công! Đã xuất {quantity.Value} {ingredient.Unit} {ingredient.Name}";
                return RedirectToAction("Inventory");
            }

            TempData["Error"] = errorMessage;
            return RedirectToAction("StockOutbound");
        }

        public ActionResult StockOutboundList()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var viewModel = inventoryService.GetStockOutboundList();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách phiếu xuất: " + ex.Message;
                return View(new StockOutboundListViewModel());
            }
        }

        public ActionResult StockOutboundDetail(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var viewModel = inventoryService.GetStockOutboundDetail(id);
                if (viewModel == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu xuất kho.";
                    return RedirectToAction("StockOutboundList");
                }
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("StockOutboundList");
            }
        }

        #endregion

        #region Damaged Stock

        public ActionResult DamagedStock()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.Ingredients = inventoryService.GetAllIngredients();
            ViewBag.Employees = inventoryService.GetActiveEmployees()
                .Select(e => new { Id = e.Id, FullName = e.FullName }).ToList();
            return View(new DamagedStock { DamageDate = DateTime.Now });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DamagedStock(DamagedStock model, string submitType)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (inventoryService.ProcessDamagedStock(model, submitType, out errorMessage))
                {
                    var ingredient = inventoryService.GetIngredientById(model.IngredientId);
                    string message = submitType == "draft"
                        ? $"Đã lưu nháp báo cáo hàng hỏng: {model.Quantity} {ingredient.Unit} {ingredient.Name}"
                        : $"Đã gửi báo cáo hàng hỏng thành công: {model.Quantity} {ingredient.Unit} {ingredient.Name}. Tồn kho đã được cập nhật.";
                    TempData["Success"] = message;
                    return RedirectToAction("DamagedStockList");
                }
                ModelState.AddModelError("", errorMessage);
            }

            ViewBag.Ingredients = inventoryService.GetAllIngredients();
            ViewBag.Employees = inventoryService.GetActiveEmployees()
                .Select(e => new { Id = e.Id, FullName = e.FullName }).ToList();
            return View(model);
        }

        public ActionResult DamagedStockList()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var damagedStocks = inventoryService.GetDamagedStockList();
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
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" });

            string errorMessage;
            decimal newStock;
            if (inventoryService.QuickStockIn(ingredientId, quantity, unitPrice, notes, GetCurrentEmployeeId(), out errorMessage, out newStock))
            {
                var ingredient = inventoryService.GetIngredientById(ingredientId);
                return Json(new
                {
                    success = true,
                    message = $"Nhập kho thành công {quantity} {ingredient.Unit} {ingredient.Name}!",
                    newStock,
                    totalCost = quantity * unitPrice
                });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult QuickStockOut(int ingredientId, decimal quantity, string purpose, string notes)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" });

            string errorMessage;
            decimal newStock;
            if (inventoryService.QuickStockOut(ingredientId, quantity, purpose, notes, GetCurrentEmployeeId(), out errorMessage, out newStock))
            {
                var ingredient = inventoryService.GetIngredientById(ingredientId);
                return Json(new
                {
                    success = true,
                    message = $"Xuất kho thành công {quantity} {ingredient.Unit} {ingredient.Name}!",
                    newStock,
                    reason = purpose
                });
            }

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Reports

        public ActionResult InventoryReport(string fromDate = "", string toDate = "")
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                DateTime? from = null;
                DateTime? to = null;

                if (!string.IsNullOrEmpty(fromDate))
                {
                    DateTime parsedFrom;
                    if (DateTime.TryParse(fromDate, out parsedFrom))
                        from = parsedFrom;
                }

                if (!string.IsNullOrEmpty(toDate))
                {
                    DateTime parsedTo;
                    if (DateTime.TryParse(toDate, out parsedTo))
                        to = parsedTo;
                }

                var reportData = inventoryService.GetInventoryReportData(from, to);

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;

                return View(reportData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải báo cáo kho: " + ex.Message;
                return RedirectToAction("Inventory");
            }
        }

        /// <summary>
        /// Xuất báo cáo kho ra file CSV
        /// </summary>
        public ActionResult ExportInventoryReport()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var csvContent = inventoryService.ExportInventoryToCsv();
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                var fileName = $"BaoCaoKho_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                // Thêm BOM để Excel hiển thị đúng tiếng Việt
                var bom = new byte[] { 0xEF, 0xBB, 0xBF };
                var result = new byte[bom.Length + bytes.Length];
                bom.CopyTo(result, 0);
                bytes.CopyTo(result, bom.Length);

                return File(result, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xuất báo cáo: " + ex.Message;
                return RedirectToAction("InventoryReport");
            }
        }

        /// <summary>
        /// Lấy dữ liệu biểu đồ cho báo cáo kho (AJAX)
        /// </summary>
        [HttpGet]
        public JsonResult GetInventoryChartData(string period = "month")
        {
            if (!IsAuthorized())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            try
            {
                DateTime from;
                switch (period.ToLower())
                {
                    case "week":
                        from = DateTime.Today.AddDays(-7);
                        break;
                    case "month":
                        from = DateTime.Today.AddDays(-30);
                        break;
                    case "quarter":
                        from = DateTime.Today.AddDays(-90);
                        break;
                    case "year":
                        from = DateTime.Today.AddDays(-365);
                        break;
                    default:
                        from = DateTime.Today.AddDays(-30);
                        break;
                }

                var reportData = inventoryService.GetInventoryReportData(from, DateTime.Today);

                return Json(new
                {
                    success = true,
                    labels = reportData.DailyTrends.Select(d => d.DateText).ToArray(),
                    inboundData = reportData.DailyTrends.Select(d => d.InboundValue).ToArray(),
                    outboundData = reportData.DailyTrends.Select(d => d.OutboundValue).ToArray(),
                    categoryLabels = reportData.CategoryStats.Select(c => c.CategoryName).ToArray(),
                    categoryValues = reportData.CategoryStats.Select(c => c.TotalValue).ToArray(),
                    summary = new
                    {
                        totalInbound = reportData.TotalInboundValue,
                        totalOutbound = reportData.TotalOutboundValue,
                        netValue = reportData.NetStockValue,
                        lowStockCount = reportData.LowStockCount,
                        outOfStockCount = reportData.OutOfStockCount
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Online Orders Management (for Manager)

        /// <summary>
        /// Trang quản lý đơn hàng online dành cho quản lý
        /// </summary>
        public ActionResult OnlineOrders(string status = "all", string orderType = "all", string dateFrom = "", string dateTo = "")
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.StatusCounts = orderQueryService.GetOnlineOrderCounts();
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedOrderType = orderType;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            var orders = orderQueryService.GetOnlineOrders(status, orderType);

            // Filter by date range if provided
            if (!string.IsNullOrEmpty(dateFrom))
            {
                DateTime fromDate;
                if (DateTime.TryParse(dateFrom, out fromDate))
                {
                    orders = orders.Where(o => o.OrderDate >= fromDate).ToList();
                }
            }
            if (!string.IsNullOrEmpty(dateTo))
            {
                DateTime toDate;
                if (DateTime.TryParse(dateTo, out toDate))
                {
                    orders = orders.Where(o => o.OrderDate <= toDate.AddDays(1)).ToList();
                }
            }

            // Calculate statistics for management
            ViewBag.TotalRevenue = orders.Where(o => o.Status == "Completed").Sum(o => o.TotalAmount);
            ViewBag.TotalOrders = orders.Count;
            ViewBag.CompletedOrders = orders.Count(o => o.Status == "Completed");
            ViewBag.CancelledOrders = orders.Count(o => o.Status == "Cancelled");
            ViewBag.DeliveryOrders = orders.Count(o => o.OrderType == "Delivery");
            ViewBag.PickupOrders = orders.Count(o => o.OrderType == "Pickup" || o.OrderType == "TakeAway");
            ViewBag.DineInOrders = orders.Count(o => o.OrderType == "DineIn");

            return View(orders);
        }

        /// <summary>
        /// API lấy chi tiết đơn hàng online cho quản lý
        /// </summary>
        [HttpGet]
        public JsonResult GetOnlineOrderDetail(int orderId)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" }, JsonRequestBehavior.AllowGet);

            try
            {
                var order = orderQueryService.GetOnlineOrderDetail(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" }, JsonRequestBehavior.AllowGet);

                return Json(new { success = true, order }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Xuất báo cáo đơn hàng online
        /// </summary>
        public ActionResult ExportOnlineOrdersReport(string dateFrom, string dateTo)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var orders = orderQueryService.GetOnlineOrders("all", "all");

            if (!string.IsNullOrEmpty(dateFrom))
            {
                DateTime fromDate;
                if (DateTime.TryParse(dateFrom, out fromDate))
                {
                    orders = orders.Where(o => o.OrderDate >= fromDate).ToList();
                }
            }
            if (!string.IsNullOrEmpty(dateTo))
            {
                DateTime toDate;
                if (DateTime.TryParse(dateTo, out toDate))
                {
                    orders = orders.Where(o => o.OrderDate <= toDate.AddDays(1)).ToList();
                }
            }

            // Generate CSV content
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Mã đơn,Ngày đặt,Khách hàng,SĐT,Loại,Trạng thái,Thanh toán,Tổng tiền");

            foreach (var order in orders)
            {
                csv.AppendLine($"{order.OrderCode},{order.OrderDateText},{order.CustomerName},{order.CustomerPhone},{order.OrderTypeText},{order.StatusText},{order.PaymentStatusText},{order.TotalAmount}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"DonHangOnline_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Thống kê đơn hàng online theo thời gian
        /// </summary>
        [HttpGet]
        public JsonResult GetOnlineOrderStats(string period = "week")
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" }, JsonRequestBehavior.AllowGet);

            try
            {
                var startDate = DateTime.Today;
                switch (period.ToLower())
                {
                    case "today":
                        startDate = DateTime.Today;
                        break;
                    case "week":
                        startDate = DateTime.Today.AddDays(-7);
                        break;
                    case "month":
                        startDate = DateTime.Today.AddDays(-30);
                        break;
                    case "year":
                        startDate = DateTime.Today.AddDays(-365);
                        break;
                }

                var orders = db.CustomerOrder
                    .Where(o => o.OrderDate >= startDate)
                    .ToList();

                var stats = new
                {
                    totalOrders = orders.Count,
                    completedOrders = orders.Count(o => o.Status == "Completed"),
                    cancelledOrders = orders.Count(o => o.Status == "Cancelled"),
                    totalRevenue = orders.Where(o => o.Status == "Completed").Sum(o => o.TotalAmount),
                    averageOrderValue = orders.Where(o => o.Status == "Completed").Any()
                        ? orders.Where(o => o.Status == "Completed").Average(o => o.TotalAmount)
                        : 0,
                    byType = new
                    {
                        delivery = orders.Count(o => o.OrderType == "Delivery"),
                        pickup = orders.Count(o => o.OrderType == "Pickup" || o.OrderType == "TakeAway"),
                        dineIn = orders.Count(o => o.OrderType == "DineIn")
                    },
                    byStatus = new
                    {
                        pending = orders.Count(o => o.Status == "Pending"),
                        confirmed = orders.Count(o => o.Status == "Confirmed"),
                        preparing = orders.Count(o => o.Status == "Preparing"),
                        ready = orders.Count(o => o.Status == "Ready"),
                        delivering = orders.Count(o => o.Status == "Delivering"),
                        completed = orders.Count(o => o.Status == "Completed"),
                        cancelled = orders.Count(o => o.Status == "Cancelled")
                    }
                };

                return Json(new { success = true, stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Supplier Management

        public ActionResult SupplierList()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var suppliers = inventoryService.GetSupplierList();
                return View(suppliers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách nhà cung cấp: " + ex.Message;
                return View(new List<Supplier>());
            }
        }

        public ActionResult CreateSupplier()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            return View(new Supplier());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateSupplier(Supplier supplier)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (inventoryService.CreateSupplier(supplier, out errorMessage))
                {
                    TempData["Success"] = "Thêm nhà cung cấp thành công!";
                    return RedirectToAction("SupplierList");
                }
                ModelState.AddModelError("Name", errorMessage);
            }

            return View(supplier);
        }

        public ActionResult EditSupplier(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var supplier = inventoryService.GetSupplierById(id);
            if (supplier == null)
            {
                TempData["Error"] = "Không tìm thấy nhà cung cấp.";
                return RedirectToAction("SupplierList");
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditSupplier(Supplier supplier)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                string errorMessage;
                if (inventoryService.UpdateSupplier(supplier, out errorMessage))
                {
                    TempData["Success"] = "Cập nhật nhà cung cấp thành công!";
                    return RedirectToAction("SupplierList");
                }
                ModelState.AddModelError("", errorMessage);
            }

            return View(supplier);
        }

        [HttpPost]
        public JsonResult DeleteSupplier(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập." });

            string errorMessage;
            if (inventoryService.DeleteSupplier(id, out errorMessage))
                return Json(new { success = true, message = "Xóa nhà cung cấp thành công!" });

            return Json(new { success = false, message = errorMessage });
        }

        [HttpGet]
        public JsonResult GetSupplierDetail(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" }, JsonRequestBehavior.AllowGet);

            try
            {
                var detail = inventoryService.GetSupplierDetail(id);
                if (detail == null)
                    return Json(new { success = false, message = "Không tìm thấy nhà cung cấp!" }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    supplier = new
                    {
                        id = detail.Supplier.Id,
                        name = detail.Supplier.Name,
                        contactPerson = detail.Supplier.ContactPerson,
                        phoneNumber = detail.Supplier.PhoneNumber,
                        address = detail.Supplier.Address,
                        totalOrders = detail.TotalOrders,
                        totalValue = detail.TotalValue
                    },
                    recentInbounds = detail.RecentInbounds
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult CreateSampleSuppliers()
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền truy cập!" });

            string errorMessage;
            int count;
            if (inventoryService.CreateSampleSuppliers(out errorMessage, out count))
                return Json(new { success = true, message = $"Đã tạo thành công {count} nhà cung cấp mẫu!", count });

            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Expiring Items Alert

        public ActionResult ExpiringItems()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                var expiringItems = inventoryService.GetExpiringItems(30); // 30 ngày
                return View(expiringItems);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(new List<ExpiringItem>());
            }
        }

        [HttpGet]
        public JsonResult GetExpiringItemsAlert()
        {
            if (!IsAuthorized())
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            try
            {
                var expiringItems = inventoryService.GetExpiringItems(7); // 7 ngày
                return Json(new
                {
                    success = true,
                    count = expiringItems.Count,
                    items = expiringItems.Take(5).Select(e => new
                    {
                        name = e.IngredientName,
                        quantity = e.Quantity,
                        unit = e.Unit,
                        daysLeft = e.DaysUntilExpiry,
                        expiryDate = e.ExpiryDate.ToString("dd/MM/yyyy")
                    })
                }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { success = false, count = 0 }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Helper Methods

        private int GetCurrentEmployeeId()
        {
            if (Session["EmployeeId"] != null)
                return (int)Session["EmployeeId"];

            var adminEmployee = db.Employee.FirstOrDefault(e => e.Role.RoleName == "Admin" && e.IsActive);
            return adminEmployee?.Id ?? 1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}