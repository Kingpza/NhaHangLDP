// Global using statements for NhaHangLDP - .NET 8 / ASP.NET Core
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Http;
global using Microsoft.EntityFrameworkCore;
global using NhaHangLDP.Helpers;
global using NhaHangLDP.Data;
global using NhaHangLDP.Data.Entities;

// Type aliases cho backward compatibility với EF6 naming
global using QROrder = NhaHangLDP.Data.Entities.Qrorder;
global using QROrderDetail = NhaHangLDP.Data.Entities.QrorderDetail;
