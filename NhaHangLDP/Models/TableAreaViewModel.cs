using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    public class TableAreaViewModel
    {
      public int Id { get; set; }
        public string Name { get; set; }
      public List<TableViewModel> Tables { get; set; } = new List<TableViewModel>();
    }

    public class TableViewModel
    {
  public int Id { get; set; }
        public string TableNumber { get; set; }
     public int Capacity { get; set; }
        public string Status { get; set; }
        public int? ActiveOrderId { get; set; }
     public DateTime? OrderTime { get; set; }
        public string OrderTimeDisplay { get; set; }
      public int ItemCount { get; set; }
        public int CustomerCount { get; set; }
        public bool IsSelected { get; set; }
        public string StatusDisplay { get; set; }
  public string StatusClass { get; set; }
  }

    public class TableAreasViewModel
    {
    public List<TableAreaViewModel> TableAreas { get; set; } = new List<TableAreaViewModel>();
      public int TotalTables { get; set; }
        public int AvailableTables { get; set; }
     public int OccupiedTables { get; set; }
        public int ReservedTables { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}