using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class VwAppSetting
{
    public string SettingKey { get; set; }

    public string SettingValue { get; set; }

    public string Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string CreatedBy { get; set; }

    public string UpdatedBy { get; set; }
}
