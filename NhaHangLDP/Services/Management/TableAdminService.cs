using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Management
{
    public class TableAdminService
    {
        private readonly MyDbContext db;

        public TableAdminService(MyDbContext context)
        {
            db = context;
        }

        public List<RestaurantTable> GetAllTables()
        {
            return db.RestaurantTables.Include("TableArea").ToList();
        }

        public RestaurantTable GetTableById(int id)
        {
            return db.RestaurantTables.Find(id);
        }

        public List<TableArea> GetAllTableAreas()
        {
            return db.TableAreas.ToList();
        }

        public bool CreateTable(RestaurantTable table, out string errorMessage)
        {
            try
            {
                var existingTable = db.RestaurantTables
                    .FirstOrDefault(t => t.TableNumber == table.TableNumber && t.TableAreaId == table.TableAreaId);

                if (existingTable != null)
                {
                    errorMessage = "Số bàn này đã tồn tại trong khu vực được chọn.";
                    return false;
                }

                table.Status = "Available";
                db.RestaurantTables.Add(table);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi thêm bàn: " + ex.Message;
                return false;
            }
        }

        public bool UpdateTable(RestaurantTable table, out string errorMessage)
        {
            try
            {
                var existingTable = db.RestaurantTables
                    .FirstOrDefault(t => t.TableNumber == table.TableNumber &&
                                        t.TableAreaId == table.TableAreaId &&
                                        t.Id != table.Id);

                if (existingTable != null)
                {
                    errorMessage = "Số bàn này đã tồn tại trong khu vực được chọn.";
                    return false;
                }

                db.Entry(table).State = EntityState.Modified;
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi cập nhật bàn: " + ex.Message;
                return false;
            }
        }

        public bool DeleteTable(int id, out string errorMessage)
        {
            try
            {
                var table = db.RestaurantTables.Find(id);
                if (table == null)
                {
                    errorMessage = "Không tìm thấy bàn.";
                    return false;
                }

                var hasActiveOrders = db.Orders.Any(o => o.TableId == id && (o.Status == "Pending" || o.Status == "Processing"));
                if (hasActiveOrders)
                {
                    errorMessage = "Không thể xóa bàn đang có khách hoặc đang phục vụ.";
                    return false;
                }

                db.RestaurantTables.Remove(table);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra: " + ex.Message;
                return false;
            }
        }
    }
}
