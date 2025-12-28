using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NhaHangLDP.Data;

namespace NhaHangLDP.Controllers
{
    public class AccountController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        
        // GET: Account
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password, string returnUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("", "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");
                    return View();
                }

                string hashedPassword = HashPassword(password);
                var employee = db.Employee.Include(e => e.Role)
                                 .FirstOrDefault(e => e.UserName == username && e.PasswordHash == hashedPassword);

                if (employee != null)
                {
                    if (employee.IsActive)
                    {
                        // Lưu thông tin vào Session
                        HttpContext.Session.SetString("Role", employee.Role.RoleName);
                        HttpContext.Session.SetString("UserRole", employee.Role.RoleName);
                        HttpContext.Session.SetString("Username", employee.UserName);
                        HttpContext.Session.SetString("FullName", employee.FullName);
                        HttpContext.Session.SetString("UserId", employee.Id.ToString());
                        HttpContext.Session.SetString("RoleId", employee.RoleId.ToString());
                        HttpContext.Session.SetString("CashierId", employee.Id.ToString());
                        HttpContext.Session.SetString("EmployeeId", employee.Id.ToString());
                        HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString());
                        HttpContext.Session.SetString("IsEmployee", true.ToString());

                        // Redirect based on role
                        return RedirectByRole(employee.Role.RoleName, returnUrl);
                    }
                    else
                    {
                        ModelState.AddModelError("", "Tài khoản nhân viên này đã bị khóa.");
                        return View();
                    }
                }

                var account = db.Account.Include(a => a.Role)
                                .FirstOrDefault(a => a.Username == username && a.PasswordHash == hashedPassword);

                if (account != null)
                {
                    if (account.IsActive)
                    {
                        // Lưu thông tin vào Session
                        HttpContext.Session.SetString("Role", account.Role.RoleName);
                        HttpContext.Session.SetString("UserRole", account.Role.RoleName);
                        HttpContext.Session.SetString("Username", account.Username);
                        HttpContext.Session.SetString("FullName", account.FullName);
                        HttpContext.Session.SetString("UserId", account.Id.ToString());
                        HttpContext.Session.SetString("RoleId", account.RoleId.ToString());
                        HttpContext.Session.SetString("CashierId", account.Id.ToString());
                        HttpContext.Session.SetString("EmployeeId", account.Id.ToString());
                        HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString());
                        HttpContext.Session.SetString("IsEmployee", false.ToString());

                        // Update last login
                        account.LastLoginDate = DateTime.Now;
                        db.SaveChanges();

                        // Redirect based on role
                        return RedirectByRole(account.Role.RoleName, returnUrl);
                    }
                    else
                    {
                        ModelState.AddModelError("", "Tài khoản này đã bị khóa.");
                        return View();
                    }
                }

                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login Error: {ex.Message}");
                ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                return View();
            }
        }

        private ActionResult RedirectByRole(string roleName, string returnUrl)
        {
            var localRedirect = RedirectToLocal(returnUrl);
            if (localRedirect != null)
                return localRedirect;

            switch (roleName.ToLower())
            {
                case "admin":
                case "manager":
                    return RedirectToAction("Dashboard", "Management");
                case "cashier":
                case "thu ngân":
                case "thu_ngan":
                    return RedirectToAction("OpenShift", "Cashier");
                case "staff":
                    return RedirectToAction("Menu", "Public");
                default:
                    return RedirectToAction("Index", "Home");
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }

        [HttpGet]
        public ActionResult Register()
        {
            ViewBag.RoleId = new SelectList(db.Role.Where(r => r.RoleName != "Admin"), "Id", "RoleName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Account account, string ConfirmPassword)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (account.PasswordHash != ConfirmPassword)
                    {
                        ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp.");
                        ViewBag.RoleId = new SelectList(db.Role.Where(r => r.RoleName != "Admin"), "Id", "RoleName");
                        return View(account);
                    }
                    if (db.Account.Any(a => a.Username == account.Username))
                    {
                        ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
                        ViewBag.RoleId = new SelectList(db.Role.Where(r => r.RoleName != "Admin"), "Id", "RoleName");
                        return View(account);
                    }
                    if (!string.IsNullOrEmpty(account.Email) && db.Account.Any(a => a.Email == account.Email))
                    {
                        ModelState.AddModelError("Email", "Email đã được sử dụng.");
                        ViewBag.RoleId = new SelectList(db.Role.Where(r => r.RoleName != "Admin"), "Id", "RoleName");
                        return View(account);
                    }

                    account.PasswordHash = HashPassword(account.PasswordHash);
                    account.IsActive = true;
                    account.CreatedDate = DateTime.Now;
                    account.CreatedBy = "System";

                    if (account.RoleId == 0)
                    {
                        var staffRole = db.Role.FirstOrDefault(r => r.RoleName == "Staff");
                        if (staffRole != null)
                        {
                            account.RoleId = staffRole.Id;
                        }
                    }

                    db.Account.Add(account);
                    db.SaveChanges();

                    TempData["Success"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                    return RedirectToAction("Login");
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra trong quá trình đăng ký. Vui lòng thử lại.");
            }

            ViewBag.RoleId = new SelectList(db.Role.Where(r => r.RoleName != "Admin"), "Id", "RoleName");
            return View(account);
        }

        public ActionResult LogOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        public ActionResult Index()
        {
            return View();
        }
        
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}