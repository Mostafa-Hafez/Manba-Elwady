using ManbaaELWaddi.Data;
using ManbaaELWaddi.Services;
using ManbaaELWaddi.ViewModels.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;



namespace ManbaaELWaddi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;


        public AdminController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("loginintoadmin")]
        public IActionResult Login([FromForm] AddAdminVM login)
        {
            var admin = _context.Admins
                         .SingleOrDefault(a =>
                         a.Username == login.Username &&
                         a.Password == login.Password);

            if (admin == null)
            {
                return Unauthorized("Invalid username or password");
            }

            // You can implement token generation here if needed

            return Ok(new { Message = "Login successful", AdminID = admin.AdminID });
        }
        [HttpPut("update/{adminsignin}")]
        public IActionResult UpdateAdmin(int adminId, [FromForm] EditAdmin model)
        {
            var admin = _context.Admins.Find(adminId);
            if (admin == null)
            {
                return NotFound("Admin not found");
            }

            if (!string.IsNullOrWhiteSpace(model.NewUsername))
            {
                // Check if the new username is already taken
                if (_context.Admins.Any(a => a.Username == model.NewUsername && a.AdminID != adminId))
                {
                    return BadRequest("Username is already in use.");
                }
                admin.Username = model.NewUsername;
            }

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                // Hash the new password before saving it
                admin.Password = AdminService.HashPassword(model.NewPassword);
            }

            _context.SaveChanges();

            return Ok("Admin updated successfully");
        }
  

    }
}
