using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class MenuCombo
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal ComboPrice { get; set; }

    public decimal TotalItemPrice { get; set; }

    public string ImageUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public virtual ICollection<MenuComboItem> MenuComboItems { get; set; } = new List<MenuComboItem>();
}
