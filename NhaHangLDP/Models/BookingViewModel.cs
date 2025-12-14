using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaHangLDP.Models
{
    public class BookingViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public DateTime BookingTime { get; set; }
        public int GuestCount { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public string TableNumber { get; set; }
    }
}