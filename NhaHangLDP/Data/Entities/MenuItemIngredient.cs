using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class MenuItemIngredient
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }

    public int IngredientId { get; set; }

    public decimal RequiredQuantity { get; set; }

    public string Unit { get; set; }

    public virtual Ingredient Ingredient { get; set; }

    public virtual MenuItem MenuItem { get; set; }
}
