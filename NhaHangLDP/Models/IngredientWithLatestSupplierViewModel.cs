using NhaHangLDP.Data.Entities;
using System;

namespace NhaHangLDP.Models
{
    public class IngredientWithLatestSupplierViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal AvailableStock { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal? LowStockThreshold { get; set; }
        public string LatestSupplier { get; set; }
        public DateTime? LastInboundDate { get; set; }
        public decimal? LastInboundQuantity { get; set; }
        public decimal? LastInboundUnitPrice { get; set; }
    }
}