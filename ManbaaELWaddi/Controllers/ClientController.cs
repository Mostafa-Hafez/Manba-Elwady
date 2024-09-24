using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Transactions;
using System.Threading.Tasks;
using Gridify.EntityFramework;
using Gridify;
using System.Linq;
using ManbaaELWaddi.Data;
using ManbaaELWaddi.ViewModels.Client;
using ManbaaELWaddi.Models;
using System.Net;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using ElWadManbaaELWaddidi.Models;

namespace ElWaddi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }


        [HttpPost("addclient")]
        public async Task<ActionResult<string>> AddClient([FromForm] AddClientVM addClientVM)
        {
            if (ModelState.IsValid)
            {
                using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {

                        // Check if the user exists
                        var user = await _context.Users.FindAsync(addClientVM.FkUserId);
                        if (user == null)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "User not found.");
                        }

                        // Generate a new client number
                        int newClientNumber = await GenerateNewClientNumber();

                        // Create the client with provided details
                        Client newClient = new Client
                        {
                            ClientName = addClientVM.ClientName,
                            ClientNumber = newClientNumber.ToString(),
                            ClientPhone1 = addClientVM.ClientPhone1,
                            ClientPhone2 = addClientVM.ClientPhone2,
                            ClientPhone3 = addClientVM.ClientPhone3,
                            ClientDistrict = addClientVM.ClientDistrict,
                            Description = addClientVM.Description,
                            ClientQuantity = addClientVM.InitialQuantity ?? 0, // Assuming you have this field in AddClientVM
                            CreatedAt = DateTime.Now,
                            FkUserId = user.UserId,
                            AddedBy = user.UserName,
                        };

                        _context.Clients.Add(newClient);
                        await _context.SaveChangesAsync(); // Save to get the ID

                        // Handle image upload if provided
                        if (addClientVM.ClientImage != null)
                        {
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/clients/");
                            var filePath = Path.Combine(uploadsFolder, $"{newClient.ClientId}{Path.GetExtension(addClientVM.ClientImage.FileName)}");

                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await addClientVM.ClientImage.CopyToAsync(stream);
                            }

                            newClient.ClientImage = $"{newClient.ClientId}{Path.GetExtension(addClientVM.ClientImage.FileName)}";
                            _context.Clients.Update(newClient);
                        }

                        // Update Inventory quantity based on initial client quantity
                        var inventory = await _context.Inventories.FirstOrDefaultAsync();
                        if (inventory != null && addClientVM.InitialQuantity.HasValue)
                        {
                            // Decrement the inventory's "Empty with Users" quantity
                            if ((inventory.InventoryQuantityFull ?? 0) >= addClientVM.InitialQuantity.Value)
                            {
                                inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) - addClientVM.InitialQuantity.Value;
                                inventory.InventoryQuantityEmptyWithClients = (inventory.InventoryQuantityEmptyWithClients ?? 0) + addClientVM.InitialQuantity.Value;
                                _context.Inventories.Update(inventory);
                            }
                            else
                            {
                                return BadRequest(new { Message = "كميتك غير كافية لإضافة العميل" });
                            }
                        }

                        // Decrement User's QuantityEmpty
                        if (addClientVM.InitialQuantity.HasValue)
                        {
                            if ((user.UserQuantityFull ?? 0) >= addClientVM.InitialQuantity.Value)
                            {
                                user.UserQuantityFull = (user.UserQuantityFull ?? 0) - addClientVM.InitialQuantity.Value;
                                user.UserQuantityEmptyWithClients = (user.UserQuantityEmptyWithClients ?? 0) + addClientVM.InitialQuantity.Value;

                                _context.Users.Update(user);
                                // Create a new CalcQuot record to track quantity out for the client
                                var userQuoteCalc = new CalcQuot
                                {
                                    FkUserId = user.UserId,
                                    UserQuantityOutForClients = addClientVM.InitialQuantity.Value, // Track quantity out for client
                                    ClientQuantityOut = addClientVM.InitialQuantity.Value, // Track quantity out for client
                                    QuantityOutDate = DateTime.Now // Track the date
                                };
                                _context.CalcQuots.Add(userQuoteCalc);
                            }

                            else
                            {
                                return BadRequest(new { Message = "قواريرك الممتلئة لا تكفي لإضافة عميل جديد" });
                            }
                        }


                        await _context.SaveChangesAsync();
                        transactionScope.Complete();

                        return Ok(new { Message = " تم إضافة العميل بنجاح" });
                    }
                    catch (DbUpdateException dbEx)
                    {
                        // Log the detailed error
                        var innerExceptionMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                        return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ في الإضافة: {innerExceptionMessage}");
                    }
                    catch (Exception exc)
                    {
                        transactionScope.Dispose();
                        return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ في الإضافة: {exc.Message}");
                    }
                }
            }

            return BadRequest(ModelState);
                }

        private async Task<int> GenerateNewClientNumber()
        {
            var lastClient = await _context.Clients.OrderByDescending(c => c.ClientNumber).FirstOrDefaultAsync();
            return lastClient != null && int.TryParse(lastClient.ClientNumber, out int lastNumber) ? lastNumber + 1 : 1;
        }
    

        [HttpPut("editclientquantity/{clientId}")]
        public async Task<ActionResult<string>> EditClientQuantity(int clientId, [FromForm] EditClientQuantityVM editClientQuantityVM)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using (var transactionScope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // Find the client by ID
                    var client = await _context.Clients.FindAsync(clientId);
                    if (client == null)
                    {
                        return NotFound($"Client with ID {clientId} not found.");
                    }

                    // Find the user who added the client
                    var user = await _context.Users.FindAsync(client.FkUserId);
                    if (user == null)
                    {
                        return NotFound($"User who added the client not found.");
                    }

                    // Track the difference in quantity
                    int oldQuantity = client.ClientQuantity ?? 0;
                    int newQuantity = editClientQuantityVM.NewQuantity;
                    int quantityDiff = newQuantity - oldQuantity;

                    // Update client quantity
                    client.ClientQuantity = newQuantity;

                    // Get the inventory
                    var inventory = await _context.Inventories.FirstOrDefaultAsync();
                    if (inventory == null)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found.");
                    }

                    if (newQuantity == oldQuantity)
                    {
                        // Reduce from user's empty quantity
                        user.UserQuantityFull = (user.UserQuantityFull ?? 0) - newQuantity;
                        user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) + newQuantity;
                        // Reduce from inventory's empty quantity with users
                        inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) - newQuantity;
                        inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) + newQuantity;

                        // Update inventory and user
                        _context.Users.Update(user);
                        _context.Inventories.Update(inventory);
                        // Create a new CalcQuot record to track quantity out for the client
                        var userQuoteCalc = new CalcQuot
                        {
                            FkUserId = user.UserId,
                            UserQuantityOutForClients = newQuantity, // Track quantity out for client
                            ClientQuantityOut = newQuantity, // Track quantity out for client
                            QuantityOutDate = DateTime.Now // Track the date
                        };
                        _context.CalcQuots.Add(userQuoteCalc);

                    }

                    // If quantity increases, reduce from user's empty quantity and inventory
                    if (quantityDiff > 0) // Quantity increased
                    {
                        if ((user.UserQuantityFull ?? 0) >= quantityDiff)
                        {
                            // Reduce from user's empty quantity
                            user.UserQuantityFull = (user.UserQuantityFull ?? 0) - quantityDiff;
                            user.UserQuantityEmptyWithClients = (user.UserQuantityEmptyWithClients ?? 0) + quantityDiff;
                            //user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) + quantityDiff;

                            // Reduce from inventory's empty quantity with users
                            inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) - quantityDiff;
                            inventory.InventoryQuantityEmptyWithClients = (inventory.InventoryQuantityEmptyWithClients ?? 0) + quantityDiff;

                            // Update inventory and user
                            _context.Users.Update(user);
                            _context.Inventories.Update(inventory);
                            // Create a new CalcQuot record to track quantity out for the client
                            var userQuoteCalc = new CalcQuot
                            {
                                FkUserId = user.UserId,
                                UserQuantityOutForClients = quantityDiff, // Track quantity out for client
                                QuantityOutDate = DateTime.Now // Track the date
                            };
                            _context.CalcQuots.Add(userQuoteCalc);
                        }
                        else
                        {
                            return BadRequest(new { Message = "قواريك الفارغة لا تكفي لنزويده" });
                        }
                    }
                    // If quantity decreases, add back to user's empty quantity and inventory
                    else if (quantityDiff < 0) // Quantity decreased
                    {
                        int positiveDiff = Math.Abs(quantityDiff); // Convert negative to positive

                        // Add back to user's empty quantity
                        //user.UserQuantityFull = (user.UserQuantityFull ?? 0) - positiveDiff;
                        user.UserQuantityEmptyWithClients = (user.UserQuantityEmptyWithClients ?? 0) - positiveDiff;
                        user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) + positiveDiff;

                        // Add back to inventory's empty quantity with users
                        inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) + positiveDiff;
                        inventory.InventoryQuantityEmptyWithClients = (inventory.InventoryQuantityEmptyWithClients ?? 0) - positiveDiff;

                        // Update inventory and user
                        _context.Users.Update(user);
                        _context.Inventories.Update(inventory);
                        // Create a new CalcQuot record to track quantity out for the client
                        var userQuoteCalc = new CalcQuot
                        {
                            FkUserId = user.UserId,
                            UserQuantityOutForClients = positiveDiff, // Track quantity out for client
                            QuantityOutDate = DateTime.Now // Track the date
                        };
                        _context.CalcQuots.Add(userQuoteCalc);
                    }

                    // Save changes to the client, user, and inventory
                    _context.Clients.Update(client);
                    await _context.SaveChangesAsync();

                    transactionScope.Complete();
                    return Ok(new { Message = "Client quantity updated successfully and inventory adjusted." });
                }
                catch (Exception exc)
                {
                    transactionScope.Dispose();
                    return StatusCode(StatusCodes.Status500InternalServerError, $"خطأ في التعديل: {exc.Message}");
                }
            }
        }


        [HttpGet("getclients")]
        public async Task<ActionResult<List<GetClientVM>>> GetClients([FromQuery] GridifyQuery query, [FromQuery] string? cusSearch)
        {
            try
            {
                // Check if cusSearch is null or empty
                if (string.IsNullOrWhiteSpace(cusSearch))
                {
                    return BadRequest(new { Message = "Search query cannot be empty." });
                }

                IQueryable<Client> queryable = _context.Clients;

                // Apply search filter
                cusSearch = cusSearch.ToLower();
                queryable = queryable.Where(x =>
                    x.ClientId.ToString().Contains(cusSearch) ||
                    (x.ClientName != null && x.ClientName.ToLower().Contains(cusSearch)) ||
                    (x.ClientNumber != null && x.ClientNumber.ToLower().Contains(cusSearch)) ||
                    (x.ClientPhone1 != null && x.ClientPhone1.ToLower().Contains(cusSearch)) ||
                    (x.ClientPhone2 != null && x.ClientPhone2.ToLower().Contains(cusSearch)) ||
                    (x.ClientPhone3 != null && x.ClientPhone3.ToLower().Contains(cusSearch)) ||
                    (x.ClientDistrict != null && x.ClientDistrict.ToLower().Contains(cusSearch)) ||
                    (x.Description != null && x.Description.ToLower().Contains(cusSearch)));
                string imageBaseUrl = $"{Request.Scheme}://{Request.Host}/images/clients/";


                var clients = await queryable.Select(client => new GetClientVM
                {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName,
                    ClientNumber = client.ClientNumber,
                    ClientPhone1 = client.ClientPhone1,
                    ClientQuantity = client.ClientQuantity,
                    ClientPhone2 = client.ClientPhone2,
                    ClientPhone3 = client.ClientPhone3,
                    ClientDistrict = client.ClientDistrict,
                    Description = client.Description,
                    CreatedAt = client.CreatedAt,
                    ClientImage = !string.IsNullOrEmpty(client.ClientImage) ? $"{imageBaseUrl}{client.ClientImage}" : null, // Handle image path construction
                    AddedBy = client.AddedBy
                }).GridifyAsync(query); // Using Gridify for pagination and sorting

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while processing your request." });
            }
        }

        [HttpGet("getclientswithsearch")]
        public async Task<ActionResult<List<GetClientVM>>> GetClientsWithSearch([FromQuery] GridifyQuery query, [FromQuery] string? cusSearch)

        {
            try
            {
                IQueryable<Client> queryable = _context.Clients;

                // Apply search filter only if cusSearch is not null or empty
                if (!string.IsNullOrEmpty(cusSearch))
                {
                    cusSearch = cusSearch.ToLower(); // Convert search string to lower case

                    queryable = queryable.Where(x =>
                        x.ClientId.ToString().Contains(cusSearch) ||
                        (x.ClientName != null && x.ClientName.ToLower().Contains(cusSearch)) ||
                        (x.ClientNumber != null && x.ClientNumber.ToLower().Contains(cusSearch)) ||
                        (x.ClientPhone1 != null && x.ClientPhone1.ToLower().Contains(cusSearch)) ||
                        (x.ClientPhone2 != null && x.ClientPhone2.ToLower().Contains(cusSearch)) ||
                        (x.ClientPhone3 != null && x.ClientPhone3.ToLower().Contains(cusSearch)) ||
                        (x.ClientDistrict != null && x.ClientDistrict.ToLower().Contains(cusSearch)) ||
                        (x.Description != null && x.Description.ToLower().Contains(cusSearch)));
                }
                string imageBaseUrl = $"{Request.Scheme}://{Request.Host}/images/clients/";


                // Project to GetClientVM and use Gridify for pagination and sorting
                var clients = await queryable.Select(client => new GetClientVM
                {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName,
                    ClientNumber = client.ClientNumber,
                    ClientPhone1 = client.ClientPhone1,
                    ClientQuantity = client.ClientQuantity,
                    ClientPhone2 = client.ClientPhone2,
                    ClientPhone3 = client.ClientPhone3,
                    ClientDistrict = client.ClientDistrict,
                    Description = client.Description,
                    CreatedAt = client.CreatedAt,
                    ClientImage = !string.IsNullOrEmpty(client.ClientImage) ? $"{imageBaseUrl}{client.ClientImage}" : null, // Handle image path construction
                    AddedBy = client.AddedBy,
                    CarNumber = client.FkUser.CarNumber
                }).GridifyAsync(query); // Using Gridify for pagination and sorting

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while processing your request.", Error = ex.Message });
            }
        }


        [HttpGet("getclientbyid")]
        public async Task<ActionResult<GetClientVM>> GetClientById(int id, [FromQuery] string? search)
        {
            try
            {
                // Retrieve the client by ID and include the related FkUser entity
                IQueryable<Client> queryable = _context.Clients
                    .Include(c => c.FkUser) // Ensure FkUser is included
                    .Where(c => c.ClientId == id);

                // Apply search filter if provided
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    queryable = queryable.Where(x =>
                        x.ClientId.ToString().Contains(search) ||
                        (x.ClientName != null && x.ClientName.ToLower().Contains(search)) ||
                        (x.ClientNumber != null && x.ClientNumber.ToLower().Contains(search)) ||
                        (x.ClientPhone1 != null && x.ClientPhone1.ToLower().Contains(search)) ||
                        (x.ClientPhone2 != null && x.ClientPhone2.ToLower().Contains(search)) ||
                        (x.ClientPhone3 != null && x.ClientPhone3.ToLower().Contains(search)) ||
                        (x.ClientDistrict != null && x.ClientDistrict.ToLower().Contains(search)) ||
                        (x.Description != null && x.Description.ToLower().Contains(search)));
                }
                string imageBaseUrl = $"{Request.Scheme}://{Request.Host}/images/clients/";

                var client = await queryable.FirstOrDefaultAsync();

                if (client == null)
                {
                    return NotFound(new { Message = "Client not found." });
                }

                if (client.FkUser == null)
                {
                    return NotFound(new { Message = "Related user not found for this client." });
                }

                // Map to ViewModel
                var clientVM = new GetClientVM
                {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName,
                    ClientNumber = client.ClientNumber,
                    ClientPhone1 = client.ClientPhone1,
                    ClientQuantity = client.ClientQuantity,
                    ClientPhone2 = client.ClientPhone2,
                    ClientPhone3 = client.ClientPhone3,
                    ClientDistrict = client.ClientDistrict,
                    Description = client.Description,
                    CreatedAt = client.CreatedAt,
                    AddedBy = client.AddedBy,
                    CarNumber = client.FkUser?.CarNumber, // Ensure this won't throw an exception
                    ClientImage = !string.IsNullOrEmpty(client.ClientImage) ? $"{imageBaseUrl}{client.ClientImage}" : null, // Handle image path construction
                };

                return Ok(clientVM);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while processing your request.", Error = ex.Message });
            }
        }


        // Drop Down List
        [HttpGet("dropdownlistclients")]
        public async Task<ActionResult<string>> GetClientsForDropdown()
        {
            var clients = await _context.Clients
                .Select(c => new GetDropdownlistClientsVM  // Use ViewModel here
                {
                    ClientId = c.ClientId,
                    ClientName = c.ClientName
                })
                .ToListAsync();

            return Ok(clients);
        }

        // Delete Client  

        [HttpDelete("deleteclientbyid")]
        public async Task<ActionResult<string>> DeleteClient(int id)
        {
            using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    var client = await _context.Clients.FindAsync(id);
                    if (client == null)
                    {
                        return NotFound("Client not found");
                    }

                    // Get the user associated with this client
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == client.FkUserId);
                    if (user == null)
                    {
                        return NotFound("User not found");
                    }

                    // Get the inventory
                    var inventory = await _context.Inventories.FirstOrDefaultAsync();
                    if (inventory == null)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found");
                    }

                    // Increase the user's UserQuantityEmpty by the client's quantity (assuming this is the logic)
                    if (client.ClientQuantity.HasValue)
                    {
                        user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) + client.ClientQuantity.Value;

                        // Increase InventoryQuantityEmptyWithUsers by the same amount
                        inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) + client.ClientQuantity.Value;

                        // Update the user and inventory
                        _context.Users.Update(user);
                        _context.Inventories.Update(inventory);
                    }

                    // Remove the client
                    _context.Clients.Remove(client);

                    // Save changes to the database
                    await _context.SaveChangesAsync();
                    transactionScope.Complete();

                    return Ok("Client deleted successfully, and quantities updated.");
                }
                catch (Exception ex)
                {
                    transactionScope.Dispose();
                    return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while deleting the client: {ex.Message}");
                }
            }
        }

        // Edit Client 
        [HttpPut("editclient/{clientId}")]
        public async Task<ActionResult<string>> EditClient(int clientId, [FromForm] EditClientVM editClientVM)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using (var transactionScope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // Find the client by ID
                    var client = await _context.Clients.FindAsync(clientId);
                    if (client == null)
                    {
                        return NotFound($"Client with ID {clientId} not found.");
                    }

                    // Find the user who added the client
                    var user = await _context.Users.FindAsync(client.FkUserId);
                    if (user == null)
                    {
                        return NotFound($"User who added the client not found.");
                    }

                    // Track the difference in quantity
                    int oldQuantity = client.ClientQuantity ?? 0;
                    int newQuantity = editClientVM.ClientQuantity ?? oldQuantity;
                    int quantityDiff = newQuantity - oldQuantity;

                    // Update basic client details
                    client.ClientName = editClientVM.ClientName;
                    client.ClientNumber = editClientVM.ClientNumber;
                    client.ClientPhone1 = editClientVM.ClientPhone1;
                    client.ClientPhone2 = editClientVM.ClientPhone2;
                    client.ClientPhone3 = editClientVM.ClientPhone3;
                    client.ClientDistrict = editClientVM.ClientDistrict;
                    client.Description = editClientVM.Description;
                    client.ClientQuantity = newQuantity; // Update the quantity to the new value

                    // Get the inventory
                    var inventory = await _context.Inventories.FirstOrDefaultAsync();
                    if (inventory == null)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found.");
                    }

                    // If quantity increases, reduce from user's empty quantity and inventory
                    if (quantityDiff > 0) // Quantity increased
                    {
                        if ((user.UserQuantityFull ?? 0) >= quantityDiff)
                        {
                            // Reduce from user's empty quantity
                            user.UserQuantityFull = (user.UserQuantityFull ?? 0) - quantityDiff;
                            user.UserQuantityEmptyWithClients = (user.UserQuantityEmptyWithClients ?? 0) + quantityDiff;

                            // Reduce from inventory's empty quantity with users
                            inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) - quantityDiff;
                            inventory.InventoryQuantityEmptyWithClients = (inventory.InventoryQuantityEmptyWithClients ?? 0) + quantityDiff;

                            // Update inventory and user
                            _context.Users.Update(user);
                            _context.Inventories.Update(inventory);
                        }
                        else
                        {
                            return BadRequest(new { Message = "قواريك الفارغة لا تكفي لنزويده" });
                        }
                    }
                    // If quantity decreases, add back to user's empty quantity and inventory
                    else if (quantityDiff < 0) // Quantity decreased
                    {
                        int positiveDiff = Math.Abs(quantityDiff); // Convert negative to positive

                        // Add back to user's empty quantity
                        user.UserQuantityFull = (user.UserQuantityFull ?? 0) + positiveDiff;
                        user.UserQuantityEmptyWithClients = (user.UserQuantityEmptyWithClients ?? 0) - quantityDiff;


                        // Add back to inventory's empty quantity with users
                        inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) + quantityDiff;
                        inventory.InventoryQuantityEmptyWithClients = (inventory.InventoryQuantityEmptyWithClients ?? 0) - quantityDiff;

                        // Update inventory and user
                        _context.Users.Update(user);
                        _context.Inventories.Update(inventory);
                    }

                    // Save changes to the client, user, and inventory
                    _context.Clients.Update(client);
                    await _context.SaveChangesAsync();

                    transactionScope.Complete();
                    return Ok(new { Message = "Client updated successfully, including quantity adjustments." });
                }
                catch (Exception exc)
                {
                    transactionScope.Dispose();
                    return StatusCode(StatusCodes.Status500InternalServerError, $"خطأ في التعديل: {exc.Message}");
                }
            }
        }

        // Report clients with a total order quantity of 0 per 15-day period, filtered by the user who added the clients
        [HttpGet("inactive-clients-zero-bottles-per-15-day")]
        public async Task<ActionResult<List<ClientMonthlyOrderSummaryVM>>> GetInactiveClientsBy15Days([FromQuery] int? userId)
        {
            try
            {
                // Query to get all clients (with optional filtering by userId if provided)
                var clientsQuery = _context.Clients.AsQueryable();

                if (userId.HasValue)
                {
                    // Filter by the user who added the client if userId is provided
                    clientsQuery = clientsQuery.Where(c => c.FkUserId == userId.Value);
                }

                var clients = await clientsQuery
                    .Select(c => new
                    {
                        c.ClientId,
                        c.ClientName,
                        TotalOrderQuantity = _context.Orders
                            .Where(o => o.FkClientId == c.ClientId)
                            .Sum(o => o.OrderQuantity ?? 0), // Sum all orders, treat null as 0
                        CarNumber = _context.Users
                            .Where(u => u.UserId == c.FkUserId)
                            .Select(u => u.CarNumber)
                            .FirstOrDefault() // Retrieve the car number
                    })
                    .Where(c => c.TotalOrderQuantity == 0) // Filter clients with total orders = 0
                    .ToListAsync();

                return Ok(new { Data = clients, Message = "Success", Count = clients.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }

        // Get Total Quantity for all clients
        [HttpGet("get-total-client-quantity")]
        public async Task<IActionResult> GetTotalClientQuantity()
        {
            try
            {
                // Retrieve the InventoryQuantityEmptyWithClients from the first inventory entry
                var totalClientQuantity = await _context.Inventories
                    .Select(i => i.InventoryQuantityEmptyWithClients)
                    .FirstOrDefaultAsync();

                // If the inventory is not found, return a 404 error
                if (totalClientQuantity == null)
                {
                    return NotFound("No inventory found or no quantity available.");
                }

                // Return the value of InventoryQuantityEmptyWithClients
                return Ok(new { TotalClientQuantity = totalClientQuantity, Message = "Success" });
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("get-total-client-quantity-by-user")]
        public async Task<IActionResult> GetTotalClientQuantityByUser([FromQuery] int userId)
        {
            // Retrieve the UserQuantityEmptyWithClients for the user by userId
            var userQuantityEmptyWithClients = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => u.UserQuantityEmptyWithClients)
                .FirstOrDefaultAsync();

            return Ok(new { TotalClientQuantity = userQuantityEmptyWithClients, Message = "Success" });


            // Return the UserQuantityEmptyWithClients value as JSON
            return Ok(new { UserId = userId, UserQuantityEmptyWithClients = userQuantityEmptyWithClients });
        }



    }

}
