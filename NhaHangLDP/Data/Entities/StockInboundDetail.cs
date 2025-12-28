using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class StockInboundDetail
{
    public int Id { get; set; }

    public int StockInboundId { get; set; }

    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string BatchNumber { get; set; }

    public virtual Ingredient Ingredient { get; set; }

    public virtual StockInbound StockInbound { get; set; }
}
