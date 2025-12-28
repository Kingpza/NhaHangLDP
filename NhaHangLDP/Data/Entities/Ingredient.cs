using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Ingredient
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Unit { get; set; }

    public decimal AvailableStock { get; set; }

    public decimal EstimatedCost { get; set; }

    public decimal? LowStockThreshold { get; set; }

    public string Category { get; set; }

    public string Description { get; set; }

    public virtual ICollection<DamagedStock> DamagedStocks { get; set; } = new List<DamagedStock>();

    public virtual ICollection<MenuItemIngredient> MenuItemIngredients { get; set; } = new List<MenuItemIngredient>();

    public virtual ICollection<StockInboundDetail> StockInboundDetails { get; set; } = new List<StockInboundDetail>();
}
