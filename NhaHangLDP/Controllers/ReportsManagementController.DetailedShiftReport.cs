using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using NhaHangLDP.Models;
using NhaHangLDP.Services.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    public partial class ReportsManagementController
    {
        private DetailedShiftReportService _detailedShiftService;

        private DetailedShiftReportService DetailedShiftService
        {
            get
            {
                if (_detailedShiftService == null)
                    _detailedShiftService = new DetailedShiftReportService(db);
                return _detailedShiftService;
            }
        }

        // GET: ReportsManagement/DetailedShiftReport
        public ActionResult DetailedShiftReport(DateTime? shiftDate, string cashierName = "")
        {
            ViewBag.ShiftDate = shiftDate ?? DateTime.Today;
            ViewBag.CashierName = cashierName;
            ViewBag.CashierList = _shiftService.GetCashierNames();
            
            return View();
        }

        // GET: ReportsManagement/LiveShiftMonitor - Trang giám sát ca trực tiếp
        public ActionResult LiveShiftMonitor()
        {
            return View();
        }

        #region Detailed Shift Report API

        [HttpPost]
        public JsonResult GetDetailedShiftReportData(DateTime? shiftDate, string cashierName = "")
        {
            try
            {
                var targetDate = shiftDate ?? DateTime.Today;
                var data = DetailedShiftService.GetDetailedShiftReportData(targetDate, cashierName);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Link to Cashier System

        [HttpPost]
        public JsonResult GetShiftCashierLink(int shiftId)
        {
            try
            {
                var shiftInfo = _shiftService.GetShiftCashierLink(shiftId);
                if (shiftInfo == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc!" });
                }

                var cashierUrl = Url.Action("ShiftDetails", "Cashier", new { shiftId = shiftId });
                
                return Json(new 
                { 
                    success = true, 
                    cashierUrl = cashierUrl,
                    shiftInfo = shiftInfo
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }
}