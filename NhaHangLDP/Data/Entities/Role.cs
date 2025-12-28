using System;
using System.Collections.Generic;

namespace NhaHangLDP.Data.Entities;

public partial class Role
{
    public int Id { get; set; }

    public string RoleName { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
