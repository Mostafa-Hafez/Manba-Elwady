using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gridify;
using System.Transactions;
using Gridify.EntityFramework;
using ManbaaELWaddi.ViewModels.User;
using ManbaaELWaddi.Data;
using ManbaaELWaddi.Models;
using ManbaaELWaddi.ViewModels.Admin;
using ManbaaELWaddi.Services;
using System.Runtime.ConstrainedExecution;
namespace ElWaddi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }


        // Add User

        [HttpPost("adduser")]
        public async Task<ActionResult<string>> AddUser([FromForm] AddUserVM addUserVM)
        {
            if (ModelState.IsValid)
            {
                using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Fetch the inventory to check the available empty quantity
                        var inventory = await _context.Inventories.FirstOrDefaultAsync();
                        if (inventory == null)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found.");
                        }

                        // Check if the available empty quantity is less than the quantity being assigned to the user
                        if (inventory.InventoryQuantityEmpty < addUserVM.UserQuantityEmpty)
                        {
                            return BadRequest(new { Message = "المخزن الكبير الفارغ مفيهوش الكمية دي " });
                        }

                        // Create new user with hashed password (if needed, add hashing logic)
                        var newUser = new User
                        {
                            UserName = addUserVM.UserName,
                            Password = addUserVM.Password, // Hash the password before storing if needed
                            UserPhoneNumber = addUserVM.UserPhoneNumber,
                            CarNumber = addUserVM.CarNumber,
                            UserQuantityEmpty = addUserVM.UserQuantityEmpty,
                            UserQuantityFull = 0,
                            UserQuantityEmptyWithClients = 0,
                        };

                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();

                        // Update Inventory after successfully adding the user
                        if (addUserVM.UserQuantityEmpty.HasValue)
                        {
                            int quantityEmpty = addUserVM.UserQuantityEmpty.Value;
                            inventory.InventoryQuantityEmpty = (inventory.InventoryQuantityEmpty ?? 0) - quantityEmpty;
                            inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) + quantityEmpty;
                            _context.Inventories.Update(inventory);
                            await _context.SaveChangesAsync();
                        }

                        transactionScope.Complete();
                        return Ok("تم إضافة المندوب بنجاح");
                    }
                    catch (Exception exc)
                    {
                        transactionScope.Dispose();
                        return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ أثناؤ الإضافة: {exc.Message}");
                    }
                }
            }

            return BadRequest(ModelState);
        }


        // Get Users 
        [HttpGet("getusersList")]
        public async Task<ActionResult<Paging<GetUserVM>>> GetUsers([FromQuery] GridifyQuery query, [FromQuery] string? cusSearch)
        {
            try
            {
                var usersQuery = _context.Users.AsQueryable();

                if (!string.IsNullOrEmpty(cusSearch))
                {
                    cusSearch = cusSearch.ToLower();
                    usersQuery = usersQuery.Where(u =>
                        u.UserId.ToString().Contains(cusSearch) ||
                        (u.UserName != null && u.UserName.ToLower().Contains(cusSearch)) ||
                        (u.UserPhoneNumber != null && u.UserPhoneNumber.ToLower().Contains(cusSearch)) ||
                        (u.CarNumber != null && u.CarNumber.ToLower().Contains(cusSearch))
                    );
                }

                var users = await usersQuery.Select(u => new GetUserVM
                {
                    UserId = u.UserId,
                    Password = u.Password,
                    UserName = u.UserName,
                    UserPhoneNumber = u.UserPhoneNumber,
                    CarNumber = u.CarNumber,
                    UserQuantityFull = u.UserQuantityFull,
                    UserQuantityEmpty = u.UserQuantityEmpty,
                    UserQuantityEmptyWithClients = u.UserQuantityEmptyWithClients,
                }).GridifyAsync(query);

                return Ok(users);
            }
            catch (Exception ex)
            {
                // Log the exception details for debugging
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = $"An error occurred while retrieving the users: {ex.Message}", Exception = ex.ToString() });
            }
        }

        [HttpGet("getuserbyid")]
        public async Task<ActionResult<GetUserVM>> GetUserById(int id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.UserId == id)
                    .Select(u => new GetUserVM
                    {
                        UserId = u.UserId,
                        UserName = u.UserName,
                        UserPhoneNumber = u.UserPhoneNumber,
                        CarNumber = u.CarNumber,
                        Password = u.Password,
                        UserQuantityFull = u.UserQuantityFull,
                        UserQuantityEmpty = u.UserQuantityEmpty,
                        UserQuantityEmptyWithClients = u.UserQuantityEmptyWithClients,

                    }).FirstOrDefaultAsync();

                if (user == null)
                    return NotFound($"User with ID {id} not found");

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while retrieving the user." });
            }
        }


        //Drop Down List
        [HttpGet("dropdown")]
        public async Task<IActionResult> GetUsersForDropdown()
        {
            var users = await _context.Users
                .Select(u => new GetDropDownListUsersVM
                {
                    UserId = u.UserId,
                    UserName = u.UserName
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("get-users-dropdown-by-car")]
        public async Task<ActionResult<List<UserCarDropdownVM>>> GetUsersDropdownByCar([FromQuery] string? searchCar)
        {
            try
            {
                // Query to fetch users and their car numbers
                var query = _context.Users.AsQueryable();

                // Optional: Filter users by car number if provided
                if (!string.IsNullOrEmpty(searchCar))
                {
                    searchCar = searchCar.ToLower();
                    query = query.Where(u => u.CarNumber.ToLower().Contains(searchCar));
                }

                var users = await query
                    .Select(u => new UserCarDropdownVM
                    {
                        UserId = u.UserId,
                        CarNumber = u.CarNumber
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while retrieving the data.", Error = ex.Message });
            }
        }




        // Edit User
        [HttpPut("edituserbyid")]
        public async Task<ActionResult<string>> EditUser(int id, [FromForm] EditUserVM editUserVM)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound($"User with ID {id} not found");

            using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // Track changes for user properties
                    bool userNameChanged = user.UserName != editUserVM.UserName;
                    bool quantityEmptyChanged = user.UserQuantityEmpty != editUserVM.UserQuantityEmpty;
                    bool quantityFullChanged = user.UserQuantityFull != editUserVM.UserQuantityFull;
                    bool quantityEmptyWithClientsChanged = user.UserQuantityEmptyWithClients != editUserVM.UserQuantityEmptyWithClients;

                    // Old and new values
                    int oldQuantityEmpty = user.UserQuantityEmpty ?? 0;
                    int newQuantityEmpty = editUserVM.UserQuantityEmpty ?? 0;

                    int oldQuantityFull = user.UserQuantityFull ?? 0;
                    int newQuantityFull = editUserVM.UserQuantityFull ?? 0;

                    int oldQuantityEmptyWithClients = user.UserQuantityEmptyWithClients ?? 0;
                    int newQuantityEmptyWithClients = editUserVM.UserQuantityEmptyWithClients ?? 0;

                    // Retrieve the inventory
                    var inventory = await _context.Inventories.FirstOrDefaultAsync();
                    if (inventory == null)
                        return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found.");

                    // Adjust inventory for UserQuantityEmpty change
                    if (quantityEmptyChanged)
                    {
                        int quantityDiffEmpty = newQuantityEmpty - oldQuantityEmpty;

                        if (quantityDiffEmpty > 0) // If user has more empty bottles
                        {
                            if (inventory.InventoryQuantityEmpty < quantityDiffEmpty)
                            {
                                return BadRequest("Not enough empty bottles in inventory.");
                            }
                            inventory.InventoryQuantityEmpty -= quantityDiffEmpty;
                            inventory.InventoryQuantityEmptyWithUsers += quantityDiffEmpty;
                        }
                        else if (quantityDiffEmpty < 0) // If user has returned empty bottles
                        {
                            int positiveDiffEmpty = Math.Abs(quantityDiffEmpty);
                            inventory.InventoryQuantityEmptyWithUsers -= positiveDiffEmpty;
                            inventory.AllScrap = (inventory.AllScrap ?? 0) + positiveDiffEmpty; // Add returned bottles to scrap
                        }
                    }

                    // Adjust inventory for UserQuantityFull change
                    if (quantityFullChanged)
                    {
                        int quantityDiffFull = newQuantityFull - oldQuantityFull;

                        if (quantityDiffFull > 0) // If user received full bottles
                        {
                            inventory.InventoryQuantityEmptyWithUsers -= quantityDiffFull;
                            inventory.InventoryQuantityFull += quantityDiffFull;
                            user.UserQuantityEmpty -= quantityDiffFull;
                        }
                        else if (quantityDiffFull < 0) // If user consumed full bottles
                        {
                            int positiveDiffFull = Math.Abs(quantityDiffFull);
                            inventory.InventoryQuantityEmptyWithUsers += positiveDiffFull;
                            inventory.InventoryQuantityFull -= positiveDiffFull;
                            user.UserQuantityEmpty += positiveDiffFull;
                        }
                    }

                    // Adjust inventory for UserQuantityEmptyWithClients change
                    if (quantityEmptyWithClientsChanged)
                    {
                        int quantityDiffEmptyWithClients = newQuantityEmptyWithClients - oldQuantityEmptyWithClients;

                        if (quantityDiffEmptyWithClients > 0) // If user gave more empty bottles to clients
                        {
                            if (inventory.InventoryQuantityFull < quantityDiffEmptyWithClients)
                            {
                                return BadRequest("Not enough full bottles in inventory.");
                            }
                            user.UserQuantityFull -= quantityDiffEmptyWithClients;
                            inventory.InventoryQuantityEmptyWithClients += quantityDiffEmptyWithClients;
                            inventory.InventoryQuantityFull -= quantityDiffEmptyWithClients;
                        }
                        else if (quantityDiffEmptyWithClients < 0) // If user received empty bottles from clients
                        {
                            int positiveDiffEmptyWithClients = Math.Abs(quantityDiffEmptyWithClients);
                            user.UserQuantityEmpty += positiveDiffEmptyWithClients;
                            inventory.InventoryQuantityEmptyWithClients -= positiveDiffEmptyWithClients;
                            inventory.InventoryQuantityEmptyWithUsers += positiveDiffEmptyWithClients;
                        }
                    }

                    // Update user details
                    user.UserName = editUserVM.UserName;
                    user.Password = editUserVM.Password;
                    user.UserPhoneNumber = editUserVM.UserPhoneNumber;
                    user.CarNumber = editUserVM.CarNumber;
                    user.UserQuantityEmpty = newQuantityEmpty;
                    user.UserQuantityFull = newQuantityFull;
                    user.UserQuantityEmptyWithClients = newQuantityEmptyWithClients;

                    // Update related clients if the username changed
                    if (userNameChanged)
                    {
                        var clients = _context.Clients.Where(c => c.FkUserId == id).ToList();
                        foreach (var client in clients)
                        {
                            client.AddedBy = user.UserName;
                        }
                    }

                    // Save changes to inventory and user
                    _context.Inventories.Update(inventory);
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    transactionScope.Complete();
                    return Ok("User updated successfully and inventory adjusted.");
                }
                catch (Exception exc)
                {
                    transactionScope.Dispose();
                    return BadRequest($"Updating user failed: {exc.Message}");
                }
            }
        }


        // Delete User

        [HttpDelete("deleteuserbyid/{id}")]
        public async Task<ActionResult<string>> DeleteUser(int id)
        {
            try
            {
                // Find the user by ID
                var user = await _context.Users.Include(u => u.Orders)
                                               .Include(u => u.Invoices)
                                               .Include(u => u.Clients)
                                               .Include(u => u.CalcQuots) 
                                               .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound($"User with ID {id} not found");
                }

                // Retrieve the inventory
                var inventory = await _context.Inventories.FirstOrDefaultAsync();
                if (inventory == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found.");
                }

                // Adjust the inventory quantities based on the user's full and empty quantities
                if (user.UserQuantityEmpty.HasValue)
                {
                    inventory.InventoryQuantityEmpty = (inventory.InventoryQuantityEmpty ?? 0) + user.UserQuantityEmpty.Value;
                    inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) - user.UserQuantityEmpty.Value;
                }

                // Update the inventory
                _context.Inventories.Update(inventory);

                // Remove related orders, invoices, and clients if necessary
                if (user.Orders != null && user.Orders.Any())
                {
                    _context.Orders.RemoveRange(user.Orders);
                }

                if (user.Invoices != null && user.Invoices.Any())
                {
                    _context.Invoices.RemoveRange(user.Invoices);
                }

                if (user.Clients != null && user.Clients.Any())
                {
                    _context.Clients.RemoveRange(user.Clients);
                }

                // Remove the user
                _context.Users.Remove(user);

                // Save changes to the database
                await _context.SaveChangesAsync();

                return Ok("تم المسح وتم تعديل المخزون");
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                return StatusCode(StatusCodes.Status500InternalServerError, $"Deleting user failed: {ex.Message}");
            }
        }





        // sign in user
        [HttpPost("signinuser")]
        public async Task<ActionResult<string>> SignIn([FromForm] SignInUserVM signIn)
        {
            var user = await _context.Users
                                     .FirstOrDefaultAsync(u => u.Password == signIn.Password && u.UserName.ToLower() == signIn.UserName.ToLower());

            if (user == null)
            {
                Console.WriteLine("User not found.");
                return Unauthorized("Invalid username or password");
            }

            // Logging for debugging: remove or secure this in production!
            Console.WriteLine($"DB Password: {user.Password}");
            Console.WriteLine($"Entered Password: {signIn.Password}");

            return Ok(new { Message = "Sign in successful", UserId = user.UserId });
        }


        [HttpPut("Editusersignin/{userId}")]
        public async Task<ActionResult<string>> UpdateSignIn(int userId, [FromForm] EditSignInUserVM updateModel)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Console.WriteLine($"UpdateSignIn: User ID '{userId}' not found.");
                return NotFound("User not found");
            }

            // Update username if provided and not empty
            if (!string.IsNullOrWhiteSpace(updateModel.NewUserName) && updateModel.NewUserName != user.UserName)
            {
                if (await _context.Users.AnyAsync(u => u.UserName.ToLower() == updateModel.NewUserName.ToLower() && u.UserId != userId))
                {
                    Console.WriteLine($"UpdateSignIn: Username '{updateModel.NewUserName}' already in use.");
                    return BadRequest("Username already in use");
                }
                user.UserName = updateModel.NewUserName;
                Console.WriteLine($"UpdateSignIn: Username updated for user ID '{userId}'.");
            }

            // Update password if provided and not empty
            if (!string.IsNullOrWhiteSpace(updateModel.NewPassword))
            {
                user.Password = updateModel.NewPassword;
                Console.WriteLine($"UpdateSignIn: Password updated for user ID '{userId}'.");
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"UpdateSignIn: User ID '{userId}' credentials updated successfully.");
            return Ok("User sign-in credentials updated successfully");
        }

    

        [HttpPost("check-user-credentials")]
        public async Task<ActionResult<UserCredentialResponse>> CheckUserCredentials([FromForm] string userName, [FromForm] string password)
        {
            // Check if user exists with matching username and password
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == userName && u.Password == password);

            // Create response object
            var response = new UserCredentialResponse();

            if (user != null)
            {
                response.IsValid = true;
                response.Message = "User credentials are valid.";
                return Ok(response); // Return JSON response with status 200 OK
            }
            else
            {
                response.IsValid = false;
                response.Message = "Invalid username or password.";
                return Ok(response); // Return JSON response with status 200 OK
            }
        }

    }
}
