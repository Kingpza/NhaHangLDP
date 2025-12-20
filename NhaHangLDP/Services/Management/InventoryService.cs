using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Management
{
    public class InventoryService
    {
        private readonly NhaHangLDPEntities db;

        public InventoryService(NhaHangLDPEntities context)
        {
            db = context;
        }

        #region Inventory Stats

        public InventoryStatsViewModel GetInventoryStats()
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

        #endregion

        #region Ingredient Management

        public List<IngredientWithLatestSupplierViewModel> GetIngredientsWithSupplier()
        {
            return (from i in db.Ingredient
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
        }

        public Ingredient GetIngredientById(int id)
        {
            return db.Ingredient.Find(id);
        }

        public bool CreateIngredient(Ingredient ingredient, out string errorMessage)
        {
            try
            {
                var existingIngredient = db.Ingredient.FirstOrDefault(i => i.Name == ingredient.Name);
                if (existingIngredient != null)
                {
                    errorMessage = "Tên nguyên liệu đã tồn tại.";
                    return false;
                }

                ingredient.AvailableStock = 0;
                if (!ingredient.LowStockThreshold.HasValue)
                    ingredient.LowStockThreshold = 10;

                db.Ingredient.Add(ingredient);
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
                var ingredientInDb = db.Ingredient.Find(formData.Id);
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
                var ingredient = db.Ingredient.Find(id);
                if (ingredient == null)
                {
                    errorMessage = "Không tìm thấy nguyên liệu.";
                    return false;
                }

                bool isUsed = db.StockInboundDetail.Any(d => d.IngredientId == id);
                if (isUsed)
                {
                    errorMessage = "Không thể xóa: Nguyên liệu đang được sử dụng.";
                    return false;
                }

                db.Ingredient.Remove(ingredient);
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
            var ingredient = db.Ingredient.Find(id);
            if (ingredient == null)
                return null;

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

            var recentTransactions = new List<object>();

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
            var ingredient = db.Ingredient.Find(id);
            if (ingredient == null)
                return null;

            return new IngredientHistoryViewModel
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
        }

        public bool CreateSampleIngredients(out string errorMessage, out int count)
        {
            try
            {
                if (db.Ingredient.Any())
                {
                    errorMessage = "Đã có dữ liệu nguyên liệu trong hệ thống!";
                    count = 0;
                    return false;
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

        #endregion

        #region Stock Inbound

        public List<Ingredient> GetAllIngredients()
        {
            return db.Ingredient.OrderBy(i => i.Name).ToList();
        }

        public List<Employee> GetActiveEmployees()
        {
            return db.Employee.Where(e => e.IsActive).ToList();
        }

        public List<Supplier> GetAllSuppliers()
        {
            return db.Supplier.ToList();
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
            var inboundListRaw = db.StockInbound
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
                EmployeeName = s.Employee != null ? s.Employee.FullName : "N/A",
                SupplierName = s.Supplier != null ? GetSupplierName(s.Supplier) : "Không có",
                TotalCost = s.TotalCost,
                Status = "Hoàn thành",
                ItemCount = s.StockInboundDetail != null ? s.StockInboundDetail.Count : 0,
                Details = s.StockInboundDetail != null ? s.StockInboundDetail.Select(d => new StockInboundDetailItem
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
            var inbound = db.StockInbound
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
                EmployeeName = inbound.Employee?.FullName ?? "N/A",
                SupplierName = inbound.Supplier != null ? GetSupplierName(inbound.Supplier) : "Không có",
                Notes = inbound.Notes,
                Status = inbound.Status ?? "Hoàn thành",
                TotalCost = inbound.TotalCost,
                Details = inbound.StockInboundDetail?.Select(d => new StockInboundDetailItem
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
            return db.Ingredient.Where(i => i.AvailableStock > 0).OrderBy(i => i.Name).ToList();
        }

        public bool ProcessStockOutbound(int ingredientId, decimal quantity, string purpose, string notes, int employeeId, out string errorMessage)
        {
            try
            {
                var ingredient = db.Ingredient.Find(ingredientId);
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

                    db.DamagedStock.Add(damagedStock);
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
            var outboundListRaw = db.DamagedStock
                .Include("Ingredient")
                .Include("Employee")
                .OrderByDescending(d => d.DamageDate)
                .ToList();

            var outboundList = outboundListRaw.Select(d => new StockOutboundListItem
            {
                Id = d.Id,
                OutboundCode = "OUT" + d.Id.ToString().PadLeft(6, '0'),
                OutboundDate = d.DamageDate,
                EmployeeName = d.Employee != null ? d.Employee.FullName : "N/A",
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
            var outbound = db.DamagedStock
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
                EmployeeName = outbound.Employee?.FullName ?? "N/A",
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
            return db.DamagedStock
                .Include("Ingredient")
                .Include("Employee")
                .OrderByDescending(d => d.DamageDate)
                .ToList();
        }

        public bool ProcessDamagedStock(DamagedStock model, string submitType, out string errorMessage)
        {
            try
            {
                var ingredient = db.Ingredient.Find(model.IngredientId);
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

                db.DamagedStock.Add(model);

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

                var ingredient = db.Ingredient.Find(ingredientId);
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

                var ingredient = db.Ingredient.Find(ingredientId);
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

                    db.DamagedStock.Add(damagedStock);
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
