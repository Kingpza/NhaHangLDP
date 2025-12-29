using NhaHangLDP.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Data.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

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
                        // Lưu thông tin vào Session với Role key chuẩn
                        HttpContext.Session.SetString("Role", employee.Role.RoleName); // KEY QUAN TRỌNG!
                        HttpContext.Session.SetString("UserRole", employee.Role.RoleName); // Backup key
                        HttpContext.Session.SetString("Username", employee.UserName);
                        HttpContext.Session.SetString("FullName", employee.FullName ?? "");
                        HttpContext.Session.SetInt32("UserId", employee.Id);
                        HttpContext.Session.SetInt32("RoleId", employee.RoleId);
                        HttpContext.Session.SetInt32("CashierId", employee.Id);
                        HttpContext.Session.SetInt32("EmployeeId", employee.Id);
                        HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString("o"));
                        HttpContext.Session.SetString("IsEmployee", "true");

                        // Log session info (for debugging)
                        System.Diagnostics.Debug.WriteLine($"Login Success - Employee: {employee.UserName}, Role: {employee.Role.RoleName}");

                        // Redirect based on role
                        switch (employee.Role.RoleName.ToLower())
                        {
                            case "admin":
                            case "manager":
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "Management");
                            case "cashier":
                            case "thu ngân":
                            case "thu_ngan":
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("OpenShift", "Cashier");
                            case "staff":
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("Menu", "Public");
                            default:
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("Index", "Home");
                        }
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
                        // Lưu thông tin vào Session với Role key chuẩn
                        HttpContext.Session.SetString("Role", account.Role.RoleName); // KEY QUAN TRỌNG!
                        HttpContext.Session.SetString("UserRole", account.Role.RoleName); // Backup key
                        HttpContext.Session.SetString("Username", account.Username);
                        HttpContext.Session.SetString("FullName", account.FullName ?? "");
                        HttpContext.Session.SetInt32("UserId", account.Id);
                        HttpContext.Session.SetInt32("RoleId", account.RoleId);
                        HttpContext.Session.SetInt32("CashierId", account.Id);
                        HttpContext.Session.SetInt32("EmployeeId", account.Id);
                        HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString("o"));
                        HttpContext.Session.SetString("IsEmployee", "false");

                        // Update last login
                        account.LastLoginDate = DateTime.Now;
                        db.SaveChanges();

                        // Log session info (for debugging)
                        System.Diagnostics.Debug.WriteLine($"Login Success - Account: {account.Username}, Role: {account.Role.RoleName}");
                        
                        // Redirect based on role
                        switch (account.Role.RoleName.ToLower())
                        {
                            case "admin":
                            case "manager":
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("Dashboard", "Management");
                            case "cashier":
                            case "thu ngân":
                            case "thu_ngan":
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("OpenShift", "Cashier");
                            case "staff":
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("Menu", "Public");
                            default:
                                return RedirectToLocal(returnUrl) ?? RedirectToAction("Index", "Home");
                        }
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

        // Helper method to redirect to return URL
        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return null;
        }

        // GET: /Account/Register (Giữ nguyên)
        [HttpGet]
        public ActionResult Register()
        {
            ViewBag.RoleId = new SelectList(db.Role.Where(r => r.RoleName != "Admin"), "Id", "RoleName");
            return View();
        }

        // POST: /Account/Register (Giữ nguyên)
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

        // POST: /Account/LogOut
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