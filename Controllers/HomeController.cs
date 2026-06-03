using System.Diagnostics;
using ARInteriorDesignApp.Data;
using ARInteriorDesignApp.Models;
using ARInteriorDesignApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARInteriorDesignApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IAIRoomDesignService _aiRoomDesignService;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IAIRoomDesignService aiRoomDesignService)
        {
            _logger = logger;
            _context = context;
            _environment = environment;
            _aiRoomDesignService = aiRoomDesignService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalFurniture = await _context.Furnitures.CountAsync();
            ViewBag.TotalUsers = await _context.AppUsers.CountAsync();
            ViewBag.TotalUploads = await _context.RoomDesigns.CountAsync();
            ViewBag.TotalMessages = await _context.ContactMessages.CountAsync();

            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.IsLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

            return View();
        }

        public async Task<IActionResult> Furniture()
        {
            var furnitures = await _context.Furnitures
                .Include(f => f.Category)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.IsLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

            return View(furnitures);
        }

        public async Task<IActionResult> ARView(int? furnitureId)
        {
            var furnitures = await _context.Furnitures
                .Include(f => f.Category)
                .ToListAsync();

            ViewBag.Furnitures = furnitures;
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.IsLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

            if (furnitureId != null)
            {
                ViewBag.SelectedFurnitureId = furnitureId;
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateAIRoomDesign(
            string roomType,
            string style,
            string budget,
            string userInstruction,
            string generationMode,
            IFormFile? roomImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return Json(new
                {
                    success = false,
                    requiresLogin = true,
                    message = "Please login or create an account to use AI Room Designer."
                });
            }

            if (string.IsNullOrWhiteSpace(roomType) ||
                string.IsNullOrWhiteSpace(style) ||
                string.IsNullOrWhiteSpace(budget) ||
                string.IsNullOrWhiteSpace(userInstruction))
            {
                return Json(new
                {
                    success = false,
                    message = "Please select room type, style, budget and enter your design instruction."
                });
            }

            var model = new AIRoomDesignViewModel
            {
                RoomType = roomType,
                Style = style,
                Budget = budget,
                UserInstruction = userInstruction,
                RoomImage = roomImage
            };

            AIRoomDesignResult result;

            if (generationMode == "image" && roomImage != null)
            {
                string uploadedImagePath = await SaveAIRoomUploadedImage(roomImage);

                result = await _aiRoomDesignService.GenerateFromImageAsync(model, uploadedImagePath);
            }
            else
            {
                result = await _aiRoomDesignService.GenerateFromTextAsync(model);
            }

            return Json(new
            {
                success = result.Success,
                imageUrl = result.GeneratedImageUrl,
                suggestionText = result.SuggestionText,
                promptUsed = result.PromptUsed,
                message = result.Success ? "AI room design generated successfully." : result.ErrorMessage
            });
        }

        [HttpPost]
        public async Task<IActionResult> RegenerateAIRoomDesign(
            string roomType,
            string style,
            string budget,
            string userInstruction,
            string changeRequest)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return Json(new
                {
                    success = false,
                    requiresLogin = true,
                    message = "Please login or create an account to regenerate AI design."
                });
            }

            if (string.IsNullOrWhiteSpace(changeRequest))
            {
                return Json(new
                {
                    success = false,
                    message = "Please enter the change you want in the design."
                });
            }

            var model = new AIRoomDesignViewModel
            {
                RoomType = roomType,
                Style = style,
                Budget = budget,
                UserInstruction = userInstruction,
                ChangeRequest = changeRequest
            };

            var result = await _aiRoomDesignService.RegenerateWithChangesAsync(model);

            return Json(new
            {
                success = result.Success,
                imageUrl = result.GeneratedImageUrl,
                suggestionText = result.SuggestionText,
                promptUsed = result.PromptUsed,
                message = result.Success ? "AI room design updated successfully." : result.ErrorMessage
            });
        }

        private async Task<string> SaveAIRoomUploadedImage(IFormFile roomImage)
        {
            string uploadsFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "ai-room-inputs"
            );

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string extension = Path.GetExtension(roomImage.FileName);

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string fileName = "ai-room-input-" + Guid.NewGuid().ToString("N") + extension;

            string filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await roomImage.CopyToAsync(stream);
            }

            return "/uploads/ai-room-inputs/" + fileName;
        }

        [HttpPost]
        public async Task<IActionResult> SaveRoomDesign(
            string designImage,
            string selectedFurnitureName,
            string selectedFurnitureImage,
            string aiSuggestion)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return Json(new
                {
                    success = false,
                    requiresLogin = true,
                    redirectUrl = Url.Action("Login", "Home"),
                    message = "Please login or create an account to save and use AR Studio features."
                });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(designImage))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Design image is missing. Please create a design before saving."
                    });
                }

                string uploadsFolder = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "designs"
                );

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string fileName = "room-design-" + Guid.NewGuid().ToString("N") + ".png";
                string filePath = Path.Combine(uploadsFolder, fileName);

                string base64Data = designImage;

                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }

                byte[] imageBytes = Convert.FromBase64String(base64Data);

                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                string databasePath = "/uploads/designs/" + fileName;

                var roomDesign = new RoomDesign
                {
                    UserName = HttpContext.Session.GetString("UserName") ?? "Registered User",
                    UserEmail = HttpContext.Session.GetString("UserEmail") ?? "",
                    RoomImagePath = databasePath,
                    SelectedFurnitureName = selectedFurnitureName ?? "Custom Room Design",
                    SelectedFurnitureImage = selectedFurnitureImage ?? "",
                    AiSuggestion = aiSuggestion ?? "",
                    CreatedAt = DateTime.Now
                };

                _context.RoomDesigns.Add(roomDesign);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Room design saved successfully.",
                    imagePath = databasePath
                });
            }
            catch
            {
                return Json(new
                {
                    success = false,
                    message = "Design could not be saved. Please try again."
                });
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["RegisterError"] = "Please fill all required fields.";
                return RedirectToAction("Register");
            }

            string cleanEmail = email.Trim().ToLower();

            bool emailExists = await _context.AppUsers
                .AnyAsync(u => u.Email.ToLower() == cleanEmail);

            if (emailExists)
            {
                TempData["RegisterError"] = "This email is already registered.";
                return RedirectToAction("Register");
            }

            var appUser = new AppUser
            {
                FullName = fullName.Trim(),
                Email = cleanEmail,
                Password = password,
                Role = "User",
                CreatedAt = DateTime.Now
            };

            _context.AppUsers.Add(appUser);

            await _context.SaveChangesAsync();

            TempData["RegisterSuccess"] = "Account created successfully. Please login.";

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.LoginMessage = TempData["LoginMessage"];
            ViewBag.LoginError = TempData["LoginError"];
            ViewBag.LogoutMessage = TempData["LogoutMessage"];
            ViewBag.RegisterSuccess = TempData["RegisterSuccess"];

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["LoginError"] = "Please enter email and password.";
                return RedirectToAction("Login");
            }

            string cleanEmail = email.Trim().ToLower();

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == cleanEmail &&
                    u.Password == password);

            if (user == null)
            {
                TempData["LoginError"] = "Invalid email or password.";
                return RedirectToAction("Login");
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.Role);

            if (user.Role == "Admin")
            {
                return RedirectToAction("Admin");
            }

            return RedirectToAction("ARView");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            string email,
            string newPassword,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                TempData["ForgotError"] = "Please fill all fields.";
                return RedirectToAction("ForgotPassword");
            }

            if (newPassword != confirmPassword)
            {
                TempData["ForgotError"] = "New password and confirm password do not match.";
                return RedirectToAction("ForgotPassword");
            }

            string cleanEmail = email.Trim().ToLower();

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);

            if (user == null)
            {
                TempData["ForgotError"] = "No account found with this email.";
                return RedirectToAction("ForgotPassword");
            }

            user.Password = newPassword;

            await _context.SaveChangesAsync();

            TempData["RegisterSuccess"] = "Password reset successfully. Please login with your new password.";

            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            TempData["LogoutMessage"] = "You have been logged out successfully.";

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Contact()
        {
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.IsLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

            return View(new ContactMessage());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactMessage contactMessage)
        {
            if (!ModelState.IsValid)
            {
                TempData["ContactError"] = "Please fill all required fields correctly.";
                return View(contactMessage);
            }

            contactMessage.CreatedAt = DateTime.Now;
            contactMessage.IsRead = false;

            _context.ContactMessages.Add(contactMessage);

            await _context.SaveChangesAsync();

            TempData["ContactSuccess"] = "Your message has been sent successfully.";

            return RedirectToAction("Contact");
        }

        public async Task<IActionResult> Admin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null)
            {
                TempData["LoginError"] = "Please login first to access the admin dashboard.";
                return RedirectToAction("Login");
            }

            if (userRole != "Admin")
            {
                HttpContext.Session.Clear();

                TempData["LoginError"] = "Access denied. Admin login is required.";
                return RedirectToAction("Login");
            }

            ViewBag.TotalFurniture = await _context.Furnitures.CountAsync();
            ViewBag.TotalUsers = await _context.AppUsers.CountAsync();
            ViewBag.TotalUploads = await _context.RoomDesigns.CountAsync();
            ViewBag.TotalMessages = await _context.ContactMessages.CountAsync();

            ViewBag.RecentMessages = await _context.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentDesigns = await _context.RoomDesigns
                .OrderByDescending(d => d.CreatedAt)
                .Take(6)
                .ToListAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}