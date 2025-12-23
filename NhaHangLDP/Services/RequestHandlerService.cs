using System;
using System.Web;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class RequestHandlerService
    {
        public bool TryParseTableId(HttpRequestBase request, out int tableId, out string errorMessage)
        {
            var tableIdStr = request.Form["tableId"];
            if (!int.TryParse(tableIdStr, out tableId))
            {
                errorMessage = "Mã bàn không hợp lệ!";
                return false;
            }
            errorMessage = null;
            return true;
        }

        public bool TryParseOrderId(HttpRequestBase request, out int orderId, out string errorMessage)
        {
            var orderIdStr = request.Form["orderId"];
            if (string.IsNullOrEmpty(orderIdStr))
            {
                errorMessage = "Thiếu thông tin đơn hàng!";
                orderId = 0;
                return false;
            }

            if (!int.TryParse(orderIdStr, out orderId))
            {
                errorMessage = "Mã đơn hàng không hợp lệ!";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryParsePaymentRequest(HttpRequestBase request, out int orderId, out string paymentMethod, 
            out decimal receivedAmount, out string errorMessage)
        {
            var orderIdStr = request.Form["orderId"];
            paymentMethod = request.Form["paymentMethod"];
            var receivedAmountStr = request.Form["receivedAmount"];

            if (string.IsNullOrEmpty(orderIdStr) || string.IsNullOrEmpty(paymentMethod) || string.IsNullOrEmpty(receivedAmountStr))
            {
                errorMessage = "Thiếu thông tin thanh toán!";
                orderId = 0;
                receivedAmount = 0;
                return false;
            }

            if (!int.TryParse(orderIdStr, out orderId))
            {
                errorMessage = "Mã đơn hàng không hợp lệ!";
                receivedAmount = 0;
                return false;
            }

            if (!decimal.TryParse(receivedAmountStr, out receivedAmount))
            {
                errorMessage = "Số tiền nhận không hợp lệ!";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryParseTableInfoRequest(HttpRequestBase request, out TableInfoRequest tableInfo, out string errorMessage)
        {
            var tableIdStr = request.Form["tableId"];
            var customersStr = request.Form["customers"];

            int tableId;
            int customers;

            if (!int.TryParse(tableIdStr, out tableId))
            {
                errorMessage = "Mã bàn không hợp lệ!";
                tableInfo = null;
                return false;
            }

            if (!int.TryParse(customersStr, out customers) || customers <= 0)
            {
                errorMessage = "Số khách không hợp lệ!";
                tableInfo = null;
                return false;
            }

            tableInfo = new TableInfoRequest
            {
                TableId = tableId,
                Customers = customers,
                Action = request.Form["action"],
                CustomerName = request.Form["customerName"] ?? "",
                CustomerPhone = request.Form["customerPhone"] ?? "",
                Notes = request.Form["notes"] ?? "",
                Time = request.Form["time"] ?? ""
            };

            errorMessage = null;
            return true;
        }

        public class TableInfoRequest
        {
            public int TableId { get; set; }
            public string Action { get; set; }
            public int Customers { get; set; }
            public string CustomerName { get; set; }
            public string CustomerPhone { get; set; }
            public string Notes { get; set; }
            public string Time { get; set; }
        }
    }
}
