using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Management
{
    public class InventoryService
    {
        private readonly MyDbContext db;

        public InventoryService(MyDbContext context)
        {
            db = context;
        }

        #region Inventory Stats

        public InventoryStatsViewModel GetInventoryStats()
        {
            try
            {
                var ingredients = db.Ingredients.ToList();

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

                // Tính nguyên liệu sắp hết hạn
                var expiringList = GetExpiringItems();
                var expiredItems = expiringList.Count(e => e.DaysUntilExpiry <= 0);

                // Tính giá trị hàng hỏng trong tháng
                var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var monthlyDamageValue = db.DamagedStocks
                    .Where(d => d.DamageDate >= startOfMonth)
                    .ToList()
                    .Sum(d => d.Quantity * (d.Ingredient?.EstimatedCost ?? 0));

                return new InventoryStatsViewModel
                {
                    TotalIngredients = totalIngredients,
                    LowStockItems = lowStockItems,
                    ExpiredItems = expiredItems,
                    TotalInventoryValue = totalInventoryValue,
                    LowStockList = lowStockList,
                    ExpiringList = expiringList,
                    MonthlyDamageValue = monthlyDamageValue
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetInventoryStats: {ex.Message}");
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

        /// <summary>
        /// Lấy danh sách nguyên liệu sắp hết hạn (trong 7 ngày tới)
        /// </summary>
        public List<ExpiringItem> GetExpiringItems(int daysThreshold = 7)
        {
            var today = DateTime.Today;
            var thresholdDate = today.AddDays(daysThreshold);

            var expiringItems = db.StockInboundDetails
                .Include("Ingredient")
                .Include("StockInbound")
                .Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value <= thresholdDate)
                .ToList()
                .GroupBy(d => d.IngredientId)
                .Select(g => {
                    var nearestExpiry = g.Where(d => d.ExpiryDate.HasValue)
                                          .OrderBy(d => d.ExpiryDate.Value)
                                          .FirstOrDefault();
                    var ingredient = g.First().Ingredient;
                    return new ExpiringItem
                    {
                        IngredientId = g.Key,
                        IngredientName = ingredient?.Name ?? "N/A",
                        Unit = ingredient?.Unit ?? "",
                        Quantity = g.Sum(d => d.Quantity),
                        ExpiryDate = nearestExpiry?.ExpiryDate ?? DateTime.MinValue,
                        DaysUntilExpiry = nearestExpiry?.ExpiryDate.HasValue == true 
                            ? (int)(nearestExpiry.ExpiryDate.Value - today).TotalDays 
                            : 0,
                        BatchNumber = nearestExpiry?.BatchNumber
                    };
                })
                .OrderBy(e => e.DaysUntilExpiry)
                .ToList();

            return expiringItems;
        }

        #endregion

        #region Supplier Management

        public List<Supplier> GetSupplierList()
        {
            return db.Suppliers.OrderBy(s => s.Name).ToList();
        }

        public Supplier GetSupplierById(int id)
        {
            return db.Suppliers.Find(id);
        }

        public bool CreateSupplier(Supplier supplier, out string errorMessage)
        {
            try
            {
                var existing = db.Suppliers.FirstOrDefault(s => s.Name == supplier.Name);
                if (existing != null)
                {
                    errorMessage = "Tên nhà cung cấp đã tồn tại.";
                    return false;
                }

                db.Suppliers.Add(supplier);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi thêm nhà cung cấp: " + ex.Message;
                return false;
            }
        }

        public bool UpdateSupplier(Supplier formData, out string errorMessage)
        {
            try
            {
                var supplierInDb = db.Suppliers.Find(formData.Id);
                if (supplierInDb == null)
                {
                    errorMessage = "Không tìm thấy nhà cung cấp.";
                    return false;
                }

                // Kiểm tra trùng tên
                var duplicate = db.Suppliers.FirstOrDefault(s => s.Name == formData.Name && s.Id != formData.Id);
                if (duplicate != null)
                {
                    errorMessage = "Tên nhà cung cấp đã tồn tại.";
                    return false;
                }

                supplierInDb.Name = formData.Name;
                supplierInDb.ContactPerson = formData.ContactPerson;
                supplierInDb.PhoneNumber = formData.PhoneNumber;
                supplierInDb.Address = formData.Address;

                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi khi lưu dữ liệu: " + ex.Message;
                return false;
            }
        }

        public bool DeleteSupplier(int id, out string errorMessage)
        {
            try
            {
                var supplier = db.Suppliers.Find(id);
                if (supplier == null)
                {
                    errorMessage = "Không tìm thấy nhà cung cấp.";
                    return false;
                }

                // Kiểm tra xem nhà cung cấp có đang được sử dụng không
                bool isUsed = db.StockInbounds.Any(s => s.SupplierId == id);
                if (isUsed)
                {
                    errorMessage = "Không thể xóa: Nhà cung cấp đang được sử dụng trong các phiếu nhập kho.";
                    return false;
                }

                db.Suppliers.Remove(supplier);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi: " + ex.Message;
                return false;
            }
        }

        public SupplierDetailViewModel GetSupplierDetail(int id)
        {
            var supplier = db.Suppliers.Find(id);
            if (supplier == null)
                return null;

            var inboundHistory = db.StockInbounds
                .Include("StockInboundDetail")
                .Include("StockInboundDetail.Ingredient")
                .Where(s => s.SupplierId == id)
                .OrderByDescending(s => s.InboundDate)
                .Take(10)
                .ToList();

            var totalOrders = db.StockInbounds.Count(s => s.SupplierId == id);
            var totalValue = db.StockInbounds
                .Where(s => s.SupplierId == id)
                .Sum(s => (decimal?)s.TotalCost) ?? 0;

            return new SupplierDetailViewModel
            {
                Supplier = supplier,
                TotalOrders = totalOrders,
                TotalValue = totalValue,
                RecentInbounds = inboundHistory.Select(s => new StockInboundListItem
                {
                    Id = s.Id,
                    InboundCode = "IN" + s.Id.ToString().PadLeft(6, '0'),
                    InboundDate = s.InboundDate,
                    TotalCost = s.TotalCost,
                    Status = s.Status ?? "Hoàn thành",
                    ItemCount = s.StockInboundDetails?.Count ?? 0
                }).ToList()
            };
        }

        public bool CreateSampleSuppliers(out string errorMessage, out int count)
        {
            try
            {
                if (db.Suppliers.Any())
                {
                    errorMessage = "Đã có dữ liệu nhà cung cấp trong hệ thống!";
                    count = 0;
                    return false;
                }

                var sampleSuppliers = new List<Supplier>
                {
                    new Supplier { Name = "Công ty TNHH Thực phẩm Sạch", ContactPerson = "Nguyễn Văn A", PhoneNumber = "0901234567", Address = "123 Lê Lợi, Q.1, TP.HCM" },
                    new Supplier { Name = "Trang trại Rau Hữu Cơ Đà Lạt", ContactPerson = "Trần Thị B", PhoneNumber = "0912345678", Address = "456 Nguyễn Văn Cừ, Đà Lạt" },
                    new Supplier { Name = "Đại lý Hải sản Vũng Tàu", ContactPerson = "Lê Văn C", PhoneNumber = "0923456789", Address = "789 Bãi Trước, Vũng Tàu" },
                    new Supplier { Name = "Công ty CP Thịt Vissan", ContactPerson = "Phạm Thị D", PhoneNumber = "0934567890", Address = "420 Nơ Trang Long, Q.Bình Thạnh, TP.HCM" },
                    new Supplier { Name = "Nhà phân phối Gia vị Việt", ContactPerson = "Hoàng Văn E", PhoneNumber = "0945678901", Address = "111 Hai Bà Trưng, Q.3, TP.HCM" },
                    new Supplier { Name = "Công ty Đồ uống Sài Gòn", ContactPerson = "Vũ Thị F", PhoneNumber = "0956789012", Address = "222 Võ Văn Tần, Q.3, TP.HCM" }
                };

                foreach (var supplier in sampleSuppliers)
                {
                    db.Suppliers.Add(supplier);
                }

                db.SaveChanges();

                errorMessage = null;
                count = sampleSuppliers.Count;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi tạo dữ liệu mẫu: " + ex.Message;
                count = 0;
                return false;
            }
        }

        #endregion

        #region Ingredient Management

        public List<IngredientWithLatestSupplierViewModel> GetIngredientsWithSupplier()
        {
            return (from i in db.Ingredients
                    let lastInbound = db.StockInboundDetails
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
                        LastInboundQuantity = lastInbound != null ? db.StockInboundDetails
                            .Where(d => d.IngredientId == i.Id && d.StockInbound.InboundDate == lastInbound.InboundDate)
                            .Sum(d => (decimal?)d.Quantity) : null,
                        LastInboundUnitPrice = lastInbound != null ? lastInbound.UnitPrice : (decimal?)null
                    })
                    .OrderByDescending(x => x.LastInboundDate)
                    .ToList();
        }

        public Ingredient GetIngredientById(int id)
        {
            return db.Ingredients.Find(id);
        }

        public bool CreateIngredient(Ingredient ingredient, out string errorMessage)
        {
            try
            {
                var existingIngredient = db.Ingredients.FirstOrDefault(i => i.Name == ingredient.Name);
                if (existingIngredient != null)
                {
                    errorMessage = "Tên nguyên liệu đã tồn tại.";
                    return false;
                }

                ingredient.AvailableStock = 0;
                if (!ingredient.LowStockThreshold.HasValue)
                    ingredient.LowStockThreshold = 10;

                db.Ingredients.Add(ingredient);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi thêm nguyên liệu: " + ex.Message;
                return false;
            }
        }

        public bool UpdateIngredient(Ingredient formData, out string errorMessage)
        {
            try
            {
                var ingredientInDb = db.Ingredients.Find(formData.Id);
                if (ingredientInDb == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu.";
                    return false;
                }

                ingredientInDb.Name = formData.Name;
                ingredientInDb.Unit = formData.Unit;
                ingredientInDb.AvailableStock = formData.AvailableStock;
                ingredientInDb.LowStockThreshold = formData.LowStockThreshold;
                ingredientInDb.EstimatedCost = formData.EstimatedCost;

                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi khi lưu dữ liệu: " + ex.Message;
                return false;
            }
        }

        public bool DeleteIngredient(int id, out string errorMessage)
        {
            try
            {
                var ingredient = db.Ingredients.Find(id);
                if (ingredient == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu.";
                    return false;
                }

                bool isUsed = db.StockInboundDetails.Any(d => d.IngredientId == id);
                if (isUsed)
                {
                    errorMessage = "Không thể xóa: Nguyên liệu đang được sử dụng.";
                    return false;
                }

                db.Ingredients.Remove(ingredient);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Lỗi: " + ex.Message;
                return false;
            }
        }

        public object GetIngredientDetail(int id)
        {
            var ingredient = db.Ingredients.Find(id);
            if (ingredient == null)
                return null;

            var latestInbound = db.StockInboundDetails
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

            var recentTransactions = new List<object>();

            var recentInbound = db.StockInboundDetails
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

            var recentDamaged = db.DamagedStocks
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

            return new
            {
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
                recentTransactions = recentTransactions.OrderByDescending(t => ((dynamic)t).date).Take(10).ToList()
            };
        }

        public IngredientHistoryViewModel GetIngredientHistory(int id)
        {
            var ingredient = db.Ingredients.Find(id);
            if (ingredient == null)
                return null;

            return new IngredientHistoryViewModel
            {
                Ingredient = ingredient,
                InboundHistory = db.StockInboundDetails
                    .Include("StockInbound")
                    .Include("StockInbound.Supplier")
                    .Where(d => d.IngredientId == id)
                    .OrderByDescending(d => d.StockInbound.InboundDate)
                    .ToList(),
                DamagedHistory = db.DamagedStocks
                    .Where(d => d.IngredientId == id)
                    .OrderByDescending(d => d.DamageDate)
                    .ToList()
            };
        }

        public bool CreateSampleIngredients(out string errorMessage, out int count)
        {
            try
            {
                if (db.Ingredients.Any())
                {
                    errorMessage = "Đã có dữ liệu nguyên liệu trong hệ thống!";
                    count = 0;
                    return false;
                }

                var sampleIngredients = new List<Ingredient>
                {
                    new Ingredient { Name = "Thịt bò", Unit = "kg", AvailableStock = 15.5m, EstimatedCost = 200000m, LowStockThreshold = 5m, Category = "Thịt" },
                    new Ingredient { Name = "Thịt heo", Unit = "kg", AvailableStock = 12.0m, EstimatedCost = 150000m, LowStockThreshold = 3m, Category = "Thịt" },
                    new Ingredient { Name = "Thịt gà", Unit = "kg", AvailableStock = 8.5m, EstimatedCost = 120000m, LowStockThreshold = 2m, Category = "Thịt" },
                    new Ingredient { Name = "Tôm", Unit = "kg", AvailableStock = 3.2m, EstimatedCost = 300000m, LowStockThreshold = 1m, Category = "Hải sản" },
                    new Ingredient { Name = "Cá", Unit = "kg", AvailableStock = 4.8m, EstimatedCost = 180000m, LowStockThreshold = 2m, Category = "Hải sản" },
                    new Ingredient { Name = "Cà chua", Unit = "kg", AvailableStock = 6.0m, EstimatedCost = 25000m, LowStockThreshold = 2m, Category = "Rau củ" },
                    new Ingredient { Name = "Hành tây", Unit = "kg", AvailableStock = 8.5m, EstimatedCost = 20000m, LowStockThreshold = 3m, Category = "Rau củ" },
                    new Ingredient { Name = "Tỏi", Unit = "kg", AvailableStock = 2.1m, EstimatedCost = 45000m, LowStockThreshold = 1m, Category = "Gia vị" },
                    new Ingredient { Name = "Gừng", Unit = "kg", AvailableStock = 1.5m, EstimatedCost = 35000m, LowStockThreshold = 0.5m, Category = "Gia vị" },
                    new Ingredient { Name = "Ớt", Unit = "kg", AvailableStock = 0.8m, EstimatedCost = 40000m, LowStockThreshold = 0.3m, Category = "Gia vị" },
                    new Ingredient { Name = "Gạo", Unit = "kg", AvailableStock = 25.0m, EstimatedCost = 22000m, LowStockThreshold = 10m, Category = "Ngũ cốc" },
                    new Ingredient { Name = "Mì", Unit = "gói", AvailableStock = 50m, EstimatedCost = 15000m, LowStockThreshold = 20m, Category = "Ngũ cốc" },
                    new Ingredient { Name = "Dầu ăn", Unit = "lít", AvailableStock = 8.5m, EstimatedCost = 35000m, LowStockThreshold = 3m, Category = "Gia vị" },
                    new Ingredient { Name = "Muối", Unit = "kg", AvailableStock = 5.0m, EstimatedCost = 8000m, LowStockThreshold = 2m, Category = "Gia vị" },
                    new Ingredient { Name = "Đường", Unit = "kg", AvailableStock = 4.2m, EstimatedCost = 18000m, LowStockThreshold = 2m, Category = "Gia vị" },
                    new Ingredient { Name = "Nước mắm", Unit = "chai", AvailableStock = 12m, EstimatedCost = 25000m, LowStockThreshold = 5m, Category = "Gia vị" },
                    new Ingredient { Name = "Nước ngọt", Unit = "chai", AvailableStock = 48m, EstimatedCost = 12000m, LowStockThreshold = 20m, Category = "Đồ uống" },
                    new Ingredient { Name = "Bia", Unit = "chai", AvailableStock = 36m, EstimatedCost = 18000m, LowStockThreshold = 15m, Category = "Đồ uống" },
                    new Ingredient { Name = "Nấm", Unit = "kg", AvailableStock = 0.5m, EstimatedCost = 60000m, LowStockThreshold = 2m, Category = "Rau củ" },
                    new Ingredient { Name = "Rau cải", Unit = "kg", AvailableStock = 0m, EstimatedCost = 15000m, LowStockThreshold = 3m, Category = "Rau củ" }
                };

                foreach (var ingredient in sampleIngredients)
                {
                    db.Ingredients.Add(ingredient);
                }

                db.SaveChanges();

                errorMessage = null;
                count = sampleIngredients.Count;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi tạo dữ liệu mẫu: " + ex.Message;
                count = 0;
                return false;
            }
        }

        /// <summary>
        /// Lấy danh sách danh mục nguyên liệu
        /// </summary>
        public List<string> GetIngredientCategories()
        {
            return db.Ingredients
                .Where(i => !string.IsNullOrEmpty(i.Category))
                .Select(i => i.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        #endregion

        #region Stock Inbound

        public List<Ingredient> GetAllIngredients()
        {
            return db.Ingredients.OrderBy(i => i.Name).ToList();
        }

        public List<Employee> GetActiveEmployees()
        {
            return db.Employees.Where(e => e.IsActive).ToList();
        }

        public List<Supplier> GetAllSuppliers()
        {
            return db.Suppliers.ToList();
        }

        public bool ProcessStockInbound(StockInboundViewModel model, string submitType, string createdBy, out string errorMessage)
        {
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
                        CreatedBy = createdBy,
                        Status = (submitType == "draft" ? "Draft" : "Completed")
                    };
                    db.StockInbounds.Add(inbound);
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
                        db.StockInboundDetails.Add(detail);

                        if (submitType != "draft")
                        {
                            var ing = db.Ingredients.Find(d.IngredientId);
                            if (ing != null)
                            {
                                ing.AvailableStock += d.Quantity;
                                ing.EstimatedCost = d.UnitPrice;
                            }
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    errorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    errorMessage = "Có lỗi xảy ra khi nhập kho: " + ex.Message;
                    return false;
                }
            }
        }

        public StockInboundListViewModel GetStockInboundList()
        {
            var inboundListRaw = db.StockInbounds
                .Include("Employee")
                .Include("Supplier")
                .Include("StockInboundDetail")
                .Include("StockInboundDetail.Ingredient")
                .OrderByDescending(s => s.InboundDate)
                .ToList();

            var inboundList = inboundListRaw.Select(s => new StockInboundListItem
            {
                Id = s.Id,
                InboundCode = "IN" + s.Id.ToString().PadLeft(6, '0'),
                InboundDate = s.InboundDate,
                EmployeeName = s.Cashier != null ? s.ReportedByEmployee?.FullName : "N/A",
                SupplierName = s.Supplier != null ? GetSupplierName(s.Supplier) : "Không có",
                TotalCost = s.TotalCost,
                Status = "Hoàn thành",
                ItemCount = s.StockInboundDetails != null ? s.StockInboundDetails.Count : 0,
                Details = s.StockInboundDetails != null ? s.StockInboundDetails.Select(d => new StockInboundDetailItem
                {
                    IngredientName = d.Ingredient != null ? d.Ingredient.Name : "N/A",
                    Quantity = d.Quantity,
                    Unit = d.Ingredient != null ? d.Ingredient.Unit : "",
                    UnitPrice = d.UnitPrice
                }).ToList() : new List<StockInboundDetailItem>()
            }).ToList();

            return new StockInboundListViewModel { InboundList = inboundList };
        }

        public StockInboundViewPageModel GetStockInboundDetail(int id)
        {
            var inbound = db.StockInbounds
                .Include("Employee")
                .Include("Supplier")
                .Include("StockInboundDetail")
                .Include("StockInboundDetail.Ingredient")
                .FirstOrDefault(s => s.Id == id);

            if (inbound == null)
                return null;

            return new StockInboundViewPageModel
            {
                Id = inbound.Id,
                InboundCode = "IN" + inbound.Id.ToString().PadLeft(6, '0'),
                InboundDate = inbound.InboundDate,
                EmployeeName = inbound.ReportedByEmployee?.FullName ?? "N/A",
                SupplierName = inbound.Supplier != null ? GetSupplierName(inbound.Supplier) : "Không có",
                Notes = inbound.Notes,
                Status = inbound.Status ?? "Hoàn thành",
                TotalCost = inbound.TotalCost,
                Details = inbound.StockInboundDetails?.Select(d => new StockInboundDetailItem
                {
                    IngredientName = d.Ingredient?.Name ?? "N/A",
                    Quantity = d.Quantity,
                    Unit = d.Ingredient?.Unit ?? "",
                    UnitPrice = d.UnitPrice,
                    ExpiryDate = d.ExpiryDate,
                    BatchNumber = d.BatchNumber
                }).ToList() ?? new List<StockInboundDetailItem>()
            };
        }

        #endregion

        #region Stock Outbound

        public List<Ingredient> GetIngredientsWithStock()
        {
            return db.Ingredients.Where(i => i.AvailableStock > 0).OrderBy(i => i.Name).ToList();
        }

        public bool ProcessStockOutbound(int ingredientId, decimal quantity, string purpose, string notes, int employeeId, out string errorMessage)
        {
            try
            {
                var ingredient = db.Ingredients.Find(ingredientId);
                if (ingredient == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu.";
                    return false;
                }

                if (ingredient.AvailableStock < quantity)
                {
                    errorMessage = $"Không đủ tồn kho! Chỉ còn {ingredient.AvailableStock} {ingredient.Unit}.";
                    return false;
                }

                ingredient.AvailableStock -= quantity;

                if (purpose == "Hỏng hóc" || purpose == "Hết hạn" || purpose == "Hư hỏng")
                {
                    var damagedStock = new DamagedStock
                    {
                        IngredientId = ingredientId,
                        Quantity = quantity,
                        DamageDate = DateTime.Now,
                        Reason = purpose + (string.IsNullOrEmpty(notes) ? "" : " - " + notes),
                        ReportedByEmployeeId = employeeId
                    };

                    db.DamagedStocks.Add(damagedStock);
                }

                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi xuất kho: " + ex.Message;
                return false;
            }
        }

        public StockOutboundListViewModel GetStockOutboundList()
        {
            var outboundListRaw = db.DamagedStocks
                .Include("Ingredient")
                .Include("Employee")
                .OrderByDescending(d => d.DamageDate)
                .ToList();

            var outboundList = outboundListRaw.Select(d => new StockOutboundListItem
            {
                Id = d.Id,
                OutboundCode = "OUT" + d.Id.ToString().PadLeft(6, '0'),
                OutboundDate = d.DamageDate,
                EmployeeName = d.Employee != null ? d.ReportedByEmployee?.FullName : "N/A",
                Purpose = d.Reason ?? "Xuất kho",
                TotalCost = d.Quantity * (d.Ingredient != null ? d.Ingredient.EstimatedCost : 0),
                Status = "Hoàn thành",
                ItemCount = 1,
                IngredientName = d.Ingredient != null ? d.Ingredient.Name : "N/A",
                Quantity = d.Quantity,
                Unit = d.Ingredient != null ? d.Ingredient.Unit : ""
            }).ToList();

            return new StockOutboundListViewModel { OutboundList = outboundList };
        }

        public StockOutboundViewPageModel GetStockOutboundDetail(int id)
        {
            var outbound = db.DamagedStocks
                .Include("Ingredient")
                .Include("Employee")
                .FirstOrDefault(d => d.Id == id);

            if (outbound == null)
                return null;

            return new StockOutboundViewPageModel
            {
                Id = outbound.Id,
                OutboundCode = "OUT" + outbound.Id.ToString().PadLeft(6, '0'),
                OutboundDate = outbound.DamageDate,
                EmployeeName = outbound.ReportedByEmployee?.FullName ?? "N/A",
                Reason = outbound.Reason ?? "Xuất kho",
                Status = "Hoàn thành",
                IngredientName = outbound.Ingredient?.Name ?? "N/A",
                Quantity = outbound.Quantity,
                Unit = outbound.Ingredient?.Unit ?? "",
                UnitPrice = outbound.Ingredient?.EstimatedCost ?? 0,
                TotalCost = outbound.Quantity * (outbound.Ingredient?.EstimatedCost ?? 0)
            };
        }

        #endregion

        #region Damaged Stock

        public List<DamagedStock> GetDamagedStockList()
        {
            return db.DamagedStocks
                .Include("Ingredient")
                .Include("Employee")
                .OrderByDescending(d => d.DamageDate)
                .ToList();
        }

        public bool ProcessDamagedStock(DamagedStock model, string submitType, out string errorMessage)
        {
            try
            {
                var ingredient = db.Ingredients.Find(model.IngredientId);
                if (ingredient == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu.";
                    return false;
                }

                if (ingredient.AvailableStock < model.Quantity)
                {
                    errorMessage = $"Số lượng báo cáo vượt quá tồn kho hiện có ({ingredient.AvailableStock} {ingredient.Unit}).";
                    return false;
                }

                db.DamagedStocks.Add(model);

                if (submitType == "submit")
                {
                    ingredient.AvailableStock -= model.Quantity;
                }

                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi tạo báo cáo: " + ex.Message;
                return false;
            }
        }

        #endregion

        #region Quick Stock Operations

        public bool QuickStockIn(int ingredientId, decimal quantity, decimal unitPrice, string notes, int employeeId, out string errorMessage, out decimal newStock)
        {
            try
            {
                if (quantity <= 0)
                {
                    errorMessage = "Số lượng phải lớn hơn 0!";
                    newStock = 0;
                    return false;
                }

                var ingredient = db.Ingredients.Find(ingredientId);
                if (ingredient == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu!";
                    newStock = 0;
                    return false;
                }

                var stockInbound = new StockInbound
                {
                    SupplierId = null,
                    EmployeeId = employeeId,
                    InboundDate = DateTime.Now,
                    TotalCost = quantity * unitPrice,
                    Notes = notes ?? "Nhập kho nhanh"
                };

                db.StockInbounds.Add(stockInbound);
                db.SaveChanges();

                var inboundDetail = new StockInboundDetail
                {
                    StockInboundId = stockInbound.Id,
                    IngredientId = ingredientId,
                    Quantity = quantity,
                    UnitPrice = unitPrice
                };

                db.StockInboundDetails.Add(inboundDetail);

                ingredient.AvailableStock += quantity;

                if (unitPrice > 0)
                {
                    ingredient.EstimatedCost = unitPrice;
                }

                db.SaveChanges();

                newStock = ingredient.AvailableStock;
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra: " + ex.Message;
                newStock = 0;
                return false;
            }
        }

        public bool QuickStockOut(int ingredientId, decimal quantity, string purpose, string notes, int employeeId, out string errorMessage, out decimal newStock)
        {
            try
            {
                if (quantity <= 0)
                {
                    errorMessage = "Số lượng phải lớn hơn 0!";
                    newStock = 0;
                    return false;
                }

                var ingredient = db.Ingredients.Find(ingredientId);
                if (ingredient == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu!";
                    newStock = 0;
                    return false;
                }

                if (ingredient.AvailableStock < quantity)
                {
                    errorMessage = $"Không đủ tồn kho! Chỉ còn {ingredient.AvailableStock} {ingredient.Unit}.";
                    newStock = ingredient.AvailableStock;
                    return false;
                }

                if (purpose == "Hỏng hóc" || purpose == "Hết hạn")
                {
                    var damagedStock = new DamagedStock
                    {
                        IngredientId = ingredientId,
                        Quantity = quantity,
                        DamageDate = DateTime.Now,
                        Reason = purpose + (string.IsNullOrEmpty(notes) ? "" : " - " + notes),
                        ReportedByEmployeeId = employeeId
                    };

                    db.DamagedStocks.Add(damagedStock);
                }

                ingredient.AvailableStock -= quantity;
                db.SaveChanges();

                newStock = ingredient.AvailableStock;
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra: " + ex.Message;
                newStock = 0;
                return false;
            }
        }

        #endregion

        #region Inventory Report

        /// <summary>
        /// Lấy dữ liệu báo cáo kho tổng hợp
        /// </summary>
        public InventoryReportViewModel GetInventoryReportData(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var from = fromDate ?? DateTime.Today.AddDays(-30);
            var to = toDate ?? DateTime.Today;

            var ingredients = db.Ingredients.ToList();

            // Thống kê nhập kho
            var inboundData = db.StockInboundDetails
                .Include("StockInbound")
                .Include("Ingredient")
                .Where(d => d.StockInbound.InboundDate >= from && d.StockInbound.InboundDate <= to)
                .ToList();

            var totalInboundValue = inboundData.Sum(d => d.Quantity * d.UnitPrice);
            var totalInboundQuantity = inboundData.Sum(d => d.Quantity);

            // Thống kê xuất kho/hỏng hóc
            var outboundData = db.DamagedStocks
                .Include("Ingredient")
                .Where(d => d.DamageDate >= from && d.DamageDate <= to)
                .ToList();

            var totalOutboundValue = outboundData.Sum(d => d.Quantity * (d.Ingredient?.EstimatedCost ?? 0));
            var totalOutboundQuantity = outboundData.Sum(d => d.Quantity);

            // Thống kê theo danh mục
            var categoryStats = ingredients
                .GroupBy(i => i.Category ?? "Khác")
                .Select(g => new CategoryStockSummary
                {
                    CategoryName = g.Key,
                    ItemCount = g.Count(),
                    TotalStock = g.Sum(i => i.AvailableStock),
                    TotalValue = g.Sum(i => i.AvailableStock * i.EstimatedCost),
                    LowStockCount = g.Count(i => i.AvailableStock <= i.LowStockThreshold)
                })
                .OrderByDescending(c => c.TotalValue)
                .ToList();

            // Top nguyên liệu nhập nhiều nhất
            var topInboundIngredients = inboundData
                .GroupBy(d => d.IngredientId)
                .Select(g => new TopIngredientItem
                {
                    IngredientId = g.Key,
                    IngredientName = g.First().Ingredient?.Name ?? "N/A",
                    Unit = g.First().Ingredient?.Unit ?? "",
                    TotalQuantity = g.Sum(d => d.Quantity),
                    TotalValue = g.Sum(d => d.Quantity * d.UnitPrice)
                })
                .OrderByDescending(i => i.TotalValue)
                .Take(10)
                .ToList();

            // Top nguyên liệu xuất/hỏng nhiều nhất
            var topOutboundIngredients = outboundData
                .GroupBy(d => d.IngredientId)
                .Select(g => new TopIngredientItem
                {
                    IngredientId = g.Key,
                    IngredientName = g.First().Ingredient?.Name ?? "N/A",
                    Unit = g.First().Ingredient?.Unit ?? "",
                    TotalQuantity = g.Sum(d => d.Quantity),
                    TotalValue = g.Sum(d => d.Quantity * (d.Ingredient?.EstimatedCost ?? 0))
                })
                .OrderByDescending(i => i.TotalValue)
                .Take(10)
                .ToList();

            // Xu hướng nhập/xuất theo ngày
            var dailyTrends = new List<DailyStockTrend>();
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var dayInbound = inboundData.Where(d => d.StockInbound.InboundDate.Date == date.Date).Sum(d => d.Quantity * d.UnitPrice);
                var dayOutbound = outboundData.Where(d => d.DamageDate.Date == date.Date).Sum(d => d.Quantity * (d.Ingredient?.EstimatedCost ?? 0));
                
                dailyTrends.Add(new DailyStockTrend
                {
                    Date = date,
                    InboundValue = dayInbound,
                    OutboundValue = dayOutbound
                });
            }

            return new InventoryReportViewModel
            {
                FromDate = from,
                ToDate = to,
                TotalIngredients = ingredients.Count,
                TotalInventoryValue = ingredients.Sum(i => i.AvailableStock * i.EstimatedCost),
                LowStockCount = ingredients.Count(i => i.AvailableStock <= i.LowStockThreshold),
                OutOfStockCount = ingredients.Count(i => i.AvailableStock <= 0),
                TotalInboundValue = totalInboundValue,
                TotalInboundQuantity = totalInboundQuantity,
                TotalOutboundValue = totalOutboundValue,
                TotalOutboundQuantity = totalOutboundQuantity,
                CategoryStats = categoryStats,
                TopInboundIngredients = topInboundIngredients,
                TopOutboundIngredients = topOutboundIngredients,
                DailyTrends = dailyTrends,
                ExpiringItems = GetExpiringItems()
            };
        }

        /// <summary>
        /// Xuất dữ liệu báo cáo kho ra CSV
        /// </summary>
        public string ExportInventoryToCsv()
        {
            var ingredients = GetIngredientsWithSupplier();
            var csv = new System.Text.StringBuilder();
            
            csv.AppendLine("STT,Tên nguyên liệu,Đơn vị,Tồn kho,Ngưỡng tối thiểu,Giá ước tính,Giá trị tồn,Nhà cung cấp,Trạng thái");

            int stt = 1;
            foreach (var item in ingredients)
            {
                var status = item.AvailableStock <= 0 ? "Hết hàng" 
                    : item.LowStockThreshold.HasValue && item.AvailableStock <= item.LowStockThreshold.Value ? "Sắp hết" 
                    : "Đủ hàng";
                
                var totalValue = item.AvailableStock * item.EstimatedCost;
                
                csv.AppendLine($"{stt},{item.Name},{item.Unit},{item.AvailableStock},{item.LowStockThreshold ?? 0},{item.EstimatedCost},{totalValue},{item.LatestSupplier ?? "Chưa có"},{status}");
                stt++;
            }

            return csv.ToString();
        }

        #endregion

        #region Helper Methods

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

        #endregion
    }
}
