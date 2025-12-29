using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class DamagedStock
{
    public int Id { get; set; }

    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime DamageDate { get; set; }

    public string Reason { get; set; }

    public int ReportedByEmployeeId { get; set; }

    public virtual Ingredient Ingredient { get; set; }

    public virtual Employee ReportedByEmployee { get; set; }
}
