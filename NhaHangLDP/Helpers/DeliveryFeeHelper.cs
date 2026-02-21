using System;
using System.Collections.Generic;

namespace NhaHangLDP.Helpers
{
    /// <summary>
    /// Helper tính phí giao hàng theo kho?ng cách
    /// </summary>
    public static class DeliveryFeeHelper
    {
        /// <summary>
        /// C?u hình phí m?c ??nh theo kho?ng cách
        /// </summary>
        private static readonly Dictionary<string, decimal> DefaultFees = new Dictionary<string, decimal>
        {
            { "Under10Km", 15000 },
            { "10To20Km", 25000 },
            { "Over20Km", 40000 }
        };

        /// <summary>
        /// Tính phí giao hàng d?a trên kho?ng cách
        /// </summary>
        /// <param name="distanceKm">Kho?ng cách tính b?ng km</param>
        /// <param name="feeUnder10">Phí cho kho?ng cách d??i 10km</param>
        /// <param name="fee10To20">Phí cho kho?ng cách 10-20km</param>
        /// <param name="feeOver20">Phí cho kho?ng cách trên 20km</param>
        /// <returns>Phí giao hàng</returns>
        public static decimal CalculateFee(decimal distanceKm, 
            decimal? feeUnder10 = null, 
            decimal? fee10To20 = null, 
            decimal? feeOver20 = null)
        {
            var under10 = feeUnder10 ?? DefaultFees["Under10Km"];
            var from10To20 = fee10To20 ?? DefaultFees["10To20Km"];
            var over20 = feeOver20 ?? DefaultFees["Over20Km"];

            if (distanceKm < 10)
                return under10;
            else if (distanceKm <= 20)
                return from10To20;
            else
                return over20;
        }

        /// <summary>
        /// L?y kho?ng cách d?ng text
        /// </summary>
        public static string GetDistanceRangeText(decimal distanceKm)
        {
            if (distanceKm < 10)
                return "D??i 10km";
            else if (distanceKm <= 20)
                return "10km - 20km";
            else
                return "Trên 20km";
        }

        /// <summary>
        /// L?y màu CSS class cho kho?ng cách
        /// </summary>
        public static string GetDistanceColorClass(decimal distanceKm)
        {
            if (distanceKm < 10)
                return "text-green-600 bg-green-100";
            else if (distanceKm <= 20)
                return "text-yellow-600 bg-yellow-100";
            else
                return "text-red-600 bg-red-100";
        }

        /// <summary>
        /// ??c tính th?i gian giao hàng (phút)
        /// </summary>
        public static int EstimateDeliveryTime(decimal distanceKm)
        {
            // Gi? s? t?c ?? trung bình 25km/h trong thành ph?
            // C?ng thêm 15 phút chu?n b?
            var travelTime = (int)Math.Ceiling((double)distanceKm / 25 * 60);
            return Math.Max(travelTime + 15, 20); // T?i thi?u 20 phút
        }

        /// <summary>
        /// Ki?m tra có ???c mi?n phí giao hàng không
        /// </summary>
        public static bool IsFreeDelivery(decimal orderAmount, decimal minOrderForFree)
        {
            return minOrderForFree > 0 && orderAmount >= minOrderForFree;
        }

        /// <summary>
        /// Tính s? ti?n còn thi?u ?? ???c mi?n phí giao hàng
        /// </summary>
        public static decimal GetRemainingForFreeDelivery(decimal orderAmount, decimal minOrderForFree)
        {
            if (minOrderForFree <= 0 || orderAmount >= minOrderForFree)
                return 0;
            
            return minOrderForFree - orderAmount;
        }
    }
}
