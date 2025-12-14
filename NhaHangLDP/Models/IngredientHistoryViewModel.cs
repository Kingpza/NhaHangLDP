using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaHangLDP.Models
{
    public class IngredientHistoryViewModel
    {
        public Ingredient Ingredient { get; set; }
        public List<StockInboundDetail> InboundHistory { get; set; }
        public List<DamagedStock> DamagedHistory { get; set; }

        public IngredientHistoryViewModel()
        {
            InboundHistory = new List<StockInboundDetail>();
            DamagedHistory = new List<DamagedStock>();
        }
    }
}