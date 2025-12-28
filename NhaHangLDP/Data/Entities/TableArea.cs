using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class TableArea
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<RestaurantTable> RestaurantTables { get; set; } = new List<RestaurantTable>();
}
