using System;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// Mo rong DeliveryAssignment de ho tro giao hang ben thu 3 va nhan vien shop
    /// </summary>
    public partial class DeliveryAssignment
    {
        /// <summary>
        /// Kiem tra day co phai don giao boi ben thu 3 khong
        /// </summary>
        public bool IsThirdPartyDelivery
        {
            get
            {
                return ShipperId == 0 ||
                       Status == "AssignedToThirdParty" ||
                       GetThirdPartyNameFromNotes() != null;
            }
        }

        /// <summary>
        /// Lay ten hang giao hang tu Notes
        /// Format moi: [Hang:Grab] hoac fallback kiem tra truc tiep
        /// </summary>
        public string GetThirdPartyNameFromNotes()
        {
            if (string.IsNullOrEmpty(Notes)) return null;

            // Parse format: [Hang:Grab]
            var result = ParseNoteTag("[Hang:");
            if (result != null) return result;

            // Fallback: kiem tra truc tiep ten hang trong Notes
            var thirdParties = new[] { "Grab", "ShopeeFood", "GoFood", "Baemin", "AhaMove" };
            foreach (var name in thirdParties)
            {
                if (Notes.Contains(name)) return name;
            }
            return null;
        }

        /// <summary>
        /// Lay ma don ben thu 3 tu Notes
        /// </summary>
        public string GetThirdPartyOrderCodeFromNotes()
        {
            return ParseNoteTag("[Ma don:") ?? ParseNoteField("Ma don:");
        }

        /// <summary>
        /// Lay ten shipper ben thu 3 tu Notes
        /// </summary>
        public string GetThirdPartyShipperNameFromNotes()
        {
            return ParseNoteTag("[Shipper:") ?? ParseNoteField("Shipper:");
        }

        /// <summary>
        /// Lay SDT shipper ben thu 3 tu Notes
        /// </summary>
        public string GetThirdPartyShipperPhoneFromNotes()
        {
            return ParseNoteTag("[SDT:") ?? ParseNoteField("SDT:");
        }

        /// <summary>
        /// Parse gia tri tu tag dang [Key:Value]
        /// </summary>
        private string ParseNoteTag(string tag)
        {
            if (string.IsNullOrEmpty(Notes)) return null;
            var idx = Notes.IndexOf(tag, StringComparison.Ordinal);
            if (idx < 0) return null;
            var start = idx + tag.Length;
            var end = Notes.IndexOf(']', start);
            if (end <= start) return null;
            return Notes.Substring(start, end - start).Trim();
        }

        /// <summary>
        /// Parse gia tri tu field dang "Key: Value," (fallback cho Notes cu)
        /// </summary>
        private string ParseNoteField(string fieldName)
        {
            if (string.IsNullOrEmpty(Notes)) return null;
            var idx = Notes.IndexOf(fieldName, StringComparison.Ordinal);
            if (idx < 0) return null;
            var start = idx + fieldName.Length;
            var end = Notes.IndexOfAny(new[] { ',', '\n' }, start);
            if (end < 0) end = Notes.Length;
            var value = Notes.Substring(start, end - start).Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Lay khoang cach dang text
        /// </summary>
        public string GetDistanceRangeText()
        {
            if (!ActualDistance.HasValue) return "Chua xac dinh";

            var distance = ActualDistance.Value;
            if (distance < 10) return "Duoi 10km";
            if (distance <= 20) return "10km - 20km";
            return "Tren 20km";
        }

        /// <summary>
        /// Tinh thoi gian giao hang (phut)
        /// </summary>
        public int? GetDeliveryDurationMinutes()
        {
            if (!PickupTime.HasValue || !DeliveryTime.HasValue) return null;
            return (int)(DeliveryTime.Value - PickupTime.Value).TotalMinutes;
        }
    }
}
