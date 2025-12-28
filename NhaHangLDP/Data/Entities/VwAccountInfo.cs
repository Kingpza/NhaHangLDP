using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class VwAccountInfo
{
    public int Id { get; set; }

    public string Username { get; set; }

    public string FullName { get; set; }

    public string Email { get; set; }

    public string PhoneNumber { get; set; }

    public string RoleName { get; set; }

    public string StatusText { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CreatedBy { get; set; }
}
