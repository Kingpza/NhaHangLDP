using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class DeliverySetting
{
    public int Id { get; set; }

    public string SettingKey { get; set; }

    public string SettingValue { get; set; }

    public string Description { get; set; }

    public DateTime UpdatedDate { get; set; }
}
