using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Transactions;
using System.Threading.Tasks;
using Gridify.EntityFramework;
using Gridify;
using System.Linq;
using ManbaaELWaddi.Data;
using ManbaaELWaddi.ViewModels.Invoice;
using ManbaaELWaddi.Models;
using OfficeOpenXml;
using ManbaaELWaddi.ViewModels.User;
using ManbaaELWaddi.ViewModels.Inventory;

namespace ElWaddi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InvoiceController(ApplicationDbContext context)
        {
            _context = context;
        }


        [HttpPost("addinvoice")]
        public async Task<ActionResult<string>> AddInvoice([FromForm] AddInvoiceVM addInvoiceVM)
        {
            if (ModelState.IsValid)
            {
                using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Fetch the user by the provided UserId
                        var user = await _context.Users.FindAsync(addInvoiceVM.FkUserId);
                        if (user == null)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "User not found.");
                        }

                        // Check if InvoiceQuantity exceeds the user's current full quantity
                        if (addInvoiceVM.InvoiceQuantity > (user.UserQuantityEmpty ?? 0))
                        {
                            return BadRequest(new { Message = "كميتك أكبر من الكمية الفارغة " });
                        }


                        // Create the invoice with a temporary number or null
                        Invoice newInvoice = new Invoice
                        {
                            InvoiceNumber = addInvoiceVM.InvoiceNumber,
                            InvoiceQuantity = addInvoiceVM.InvoiceQuantity,
                            InvoiceDate = DateTime.Now,
                            FkUserId = user.UserId,
                        };

                        _context.Invoices.Add(newInvoice);
                        await _context.SaveChangesAsync(); // Save to get the ID

                        // Save the image if provided
                        if (addInvoiceVM.InvoiceImage != null)
                        {
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/UploadFiles/InvoiceImages");
                            var filePath = Path.Combine(uploadsFolder, $"{newInvoice.InvoiceId}{Path.GetExtension(addInvoiceVM.InvoiceImage.FileName)}");

                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await addInvoiceVM.InvoiceImage.CopyToAsync(stream);
                            }

                            newInvoice.InvoiceImage = $"{newInvoice.InvoiceId}{Path.GetExtension(addInvoiceVM.InvoiceImage.FileName)}";
                            _context.Invoices.Update(newInvoice);
                        }

                        // Update Inventory and User quantities as necessary
                        var inventory = await _context.Inventories.FirstOrDefaultAsync();
                        if (inventory != null && addInvoiceVM.InvoiceQuantity.HasValue)
                        {
                            // Deduct the invoice quantity from the user's full quantity
                            user.UserQuantityFull = (user.UserQuantityFull ?? 0) + addInvoiceVM.InvoiceQuantity.Value;
                            user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) - addInvoiceVM.InvoiceQuantity.Value;
                            // Add the invoice quantity to the inventory full quantity
                            inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) + addInvoiceVM.InvoiceQuantity.Value;
                            inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) - addInvoiceVM.InvoiceQuantity.Value;

                            // Update the user and inventory records
                            _context.Users.Update(user);
                            _context.Inventories.Update(inventory);

                            // Save changes
                            await _context.SaveChangesAsync();
                        }

                        transactionScope.Complete();

                        return Ok(new { Message = "تمت الإضافة بنجاح" });
                    }
                    catch (DbUpdateException dbEx)
                    {
                        // Log the detailed error
                        var innerExceptionMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                        return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ أثناء الإضافة: {innerExceptionMessage}");
                    }
                    catch (Exception exc)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ أثناء الإضافة: {exc.Message}");
                    }
                }
            }

            return BadRequest(ModelState);
        }


        [HttpGet("getinvoices")]
        public async Task<ActionResult<List<GetInvoiceVM>>> GetInvoices([FromQuery] GridifyQuery query, [FromQuery] string? search)
        {
            try
            {
                IQueryable<Invoice> queryable = _context.Invoices;

                if (!string.IsNullOrEmpty(search))
                {
                    queryable = queryable.Where(x =>
                       x.InvoiceId.ToString().Contains(search) ||
                       x.InvoiceNumber.Contains(search));
                }
                string imageBaseUrl = $"{Request.Scheme}://{Request.Host}/UploadFiles/InvoiceImages/";

                var invoices = await queryable.Select(invoice => new GetInvoiceVM
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceQuantity = invoice.InvoiceQuantity,
                    InvoiceDate = invoice.InvoiceDate,
                    InvoiceImage = !string.IsNullOrEmpty(invoice.InvoiceImage)
                                   ? $"{imageBaseUrl}{invoice.InvoiceImage}"
                                   : null, // Handle image path construction
                    FkUserId = invoice.FkUserId
                }).GridifyAsync(query);

                return Ok(invoices);

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while processing your request." });
            }
        }

        // GET: api/Invoice/getinvoicebyid/{id}
        [HttpGet("getinvoicebyid/{id}")]
        public async Task<ActionResult<GetInvoiceVM>> GetInvoiceById(int id)
        {
            try
            {
                // Find the invoice by ID
                var invoice = await _context.Invoices.FindAsync(id);

                if (invoice == null)
                {
                    return NotFound();
                }

                // Define the base URL or the relative path for the invoice images
                string imageBaseUrl = $"{Request.Scheme}://{Request.Host}/UploadFiles/InvoiceImages/";

                // Map the invoice details to the ViewModel
                var getInvoiceVM = new GetInvoiceVM
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceQuantity = invoice.InvoiceQuantity,
                    InvoiceDate = invoice.InvoiceDate,
                    InvoiceImage = imageBaseUrl, // Set the full image URL
                    FkUserId = invoice.FkUserId
                };

                return Ok(getInvoiceVM);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while processing your request." });
            }
        }


        // DELETE: api/Invoice/deleteinvoice/{id}
        [HttpDelete("deleteinvoice/{id}")]
        public async Task<ActionResult<string>> DeleteInvoice(int id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    return NotFound("Invoice not found");
                }

                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();

                return Ok("تم المسح");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while deleting the invoice." });
            }
        }

        // PUT: api/Invoice/editinvoice/{id}
        [HttpPut("editinvoice/{id}")]
        public async Task<ActionResult<string>> EditInvoice(int id, [FromForm] EditInvoiceVM editInvoiceVM)
        {
            if (ModelState.IsValid)
            {
                using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Find the invoice by ID
                        var invoice = await _context.Invoices.FindAsync(id);
                        if (invoice == null)
                        {
                            return NotFound("Invoice not found");
                        }

                        // Track the difference in quantity
                        int oldQuantity = invoice.InvoiceQuantity ?? 0;
                        int newQuantity = editInvoiceVM.InvoiceQuantity ?? 0;
                        int quantityDiff = newQuantity - oldQuantity;

                        // Update the invoice details
                        invoice.InvoiceNumber = editInvoiceVM.InvoiceNunmber;
                        invoice.InvoiceQuantity = newQuantity;
                        _context.Invoices.Update(invoice);

                        // Fetch the inventory
                        var inventory = await _context.Inventories.FirstOrDefaultAsync();
                        if (inventory == null)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "Inventory not found");
                        }

                        // Fetch the user associated with the invoice
                        var user = await _context.Users.FindAsync(invoice.FkUserId);
                        if (user == null)
                        {
                            return NotFound("User associated with this invoice not found");
                        }

                        // Log current user values before modification
                        Console.WriteLine($"Before Update - UserQuantityFull: {user.UserQuantityFull}, UserQuantityEmpty: {user.UserQuantityEmpty}");

                        // Adjust the inventory and user based on the quantity difference
                        if (quantityDiff > 0) // Quantity increased
                        {
                            // Adjust Inventory
                            inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) + quantityDiff;
                            inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) - quantityDiff;

                            // Adjust User Quantities
                            user.UserQuantityFull = (user.UserQuantityFull ?? 0) + quantityDiff;
                            user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) - quantityDiff;

                            // Ensure the user's changes are tracked
                            _context.Users.Update(user);
                        }
                        else if (quantityDiff < 0) // Quantity decreased
                        {
                            int positiveDiff = Math.Abs(quantityDiff);

                            // Adjust Inventory
                            inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) - positiveDiff;
                            inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) + positiveDiff;

                            // Adjust User Quantities
                            user.UserQuantityFull = (user.UserQuantityFull ?? 0) - positiveDiff;
                            user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) + positiveDiff;

                            // Ensure the user's changes are tracked
                            _context.Users.Update(user);
                        }

                        // Log current user values after modification
                        Console.WriteLine($"After Update - UserQuantityFull: {user.UserQuantityFull}, UserQuantityEmpty: {user.UserQuantityEmpty}");

                        // Update the inventory
                        _context.Inventories.Update(inventory);

                        // Save all changes
                        await _context.SaveChangesAsync();

                        // Commit transaction
                        transactionScope.Complete();

                        return Ok("تم التعديل بنجاح");
                    }
                    catch (Exception ex)
                    {
                        // Rollback the transaction in case of an error
                        transactionScope.Dispose();
                        return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ أثناء التعديل: {ex.Message}");
                    }
                }
            }

            return BadRequest(ModelState);
        }

        //  Excel Reports
        [HttpGet("download-invoices-from-date-range")]
        public async Task<IActionResult> DownloadInvoicesFromDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                // Set the license context to NonCommercial before creating any ExcelPackage objects
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                endDate = endDate.AddDays(1).AddTicks(-1);


                var invoices = await _context.Invoices
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .Include(i => i.FkUser) // Include the related User entity
                    .ToListAsync();

                if (invoices == null || invoices.Count == 0)
                {
                    return NotFound("No invoices found in the given date range.");
                }

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("الفواتير");

                    // Add header row in Arabic
                    worksheet.Cells[1, 1].Value = "رقم الفاتورة";
                    worksheet.Cells[1, 2].Value = "اسم المندوب";
                    worksheet.Cells[1, 3].Value = "تاريخ الفاتورة";
                    worksheet.Cells[1, 4].Value = "كمية الفاتورة";

                    // Add data rows
                    for (int i = 0; i < invoices.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = invoices[i].InvoiceNumber;
                        worksheet.Cells[i + 2, 2].Value = invoices[i].FkUser?.UserName ?? "غير معروف"; // Display user name or "Unknown"
                        worksheet.Cells[i + 2, 3].Value = invoices[i].InvoiceDate.ToString("yyyy-MM-dd");
                        worksheet.Cells[i + 2, 4].Value = invoices[i].InvoiceQuantity;
                    }

                    var excelData = package.GetAsByteArray();

                    // Create the file name using Arabic and formatted dates
                    string fileName = $"فواتير المشتريات من {startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}.xlsx";

                    return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
            }
        }


        [HttpGet("download-invoices-for-user/{userId}")]
        public async Task<IActionResult> DownloadInvoicesForUser(int userId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                // Set the license context to NonCommercial
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;


                // Fetch the user first to get the user's name
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound($"User with ID {userId} not found.");
                }

                // Query to get invoices for the user within the specified date range
                var invoicesQuery = _context.Invoices.Where(i => i.FkUserId == userId);

                if (startDate.HasValue)
                {
                    invoicesQuery = invoicesQuery.Where(i => i.InvoiceDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    endDate = endDate.Value.AddDays(1).AddTicks(-1); // Adjust end date to include the full day
                    invoicesQuery = invoicesQuery.Where(i => i.InvoiceDate <= endDate.Value);
                }

                // Log data to see if invoices exist
                var invoices = await invoicesQuery
                    .Include(i => i.FkUser) // Ensure User is included
                    .ToListAsync();

                if (invoices == null || invoices.Count == 0)
                {
                    return NotFound("No invoices found for the specified user and date range.");
                }

                // Proceed with generating Excel file
                using (var package = new ExcelPackage())
                {
                    // Concatenate the user's name with the Arabic phrase "فواتير"
                    var worksheetName = $"{user.UserName} - فواتير";
                    var worksheet = package.Workbook.Worksheets.Add(worksheetName);

                    // Add header row in Arabic
                    worksheet.Cells[1, 1].Value = "رقم الفاتورة";
                    worksheet.Cells[1, 2].Value = "اسم المندوب";
                    worksheet.Cells[1, 3].Value = "تاريخ الفاتورة";
                    worksheet.Cells[1, 4].Value = "كمية الفاتورة";

                    // Add data rows
                    for (int i = 0; i < invoices.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = invoices[i].InvoiceNumber;
                        worksheet.Cells[i + 2, 2].Value = invoices[i].FkUser?.UserName ?? "غير معروف"; // Display user name or "Unknown"
                        worksheet.Cells[i + 2, 3].Value = invoices[i].InvoiceDate.ToString("yyyy-MM-dd");
                        worksheet.Cells[i + 2, 4].Value = invoices[i].InvoiceQuantity;
                    }

                    var excelData = package.GetAsByteArray();
                    // Name the Excel file in Arabic
                    string fileName = $"فواتير مشتريات-{user.UserName} من {startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}.xlsx";

                    return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
            }
        }

        [HttpGet("download-total-quantity-out-for-users")]
        public async Task<IActionResult> DownloadTotalQuantityOutForUsers([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                // Adjust startDate and endDate to ensure the full date range is covered
                startDate = startDate.Date;
                endDate = endDate.Date.AddDays(1).AddTicks(-1);

                // Set the license context to NonCommercial before creating any ExcelPackage objects
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Fetch records without date filtering first to see if data exists
                var allRecords = await _context.CalcQuots.ToListAsync();

                // Query the CalcQuots model to get total quantity out for clients within the date range
                var usersOutQuantities = await _context.CalcQuots
                    .Where(u => u.QuantityOutDate != null && u.QuantityOutDate >= startDate && u.QuantityOutDate <= endDate)
                    .GroupBy(u => new { u.FkUser.UserName, u.FkUser.CarNumber })
                    .Select(g => new
                    {
                        UserName = g.Key.UserName,
                        CarNumber = g.Key.CarNumber,
                        TotalQuantityOutForClients = g.Sum(u => u.UserQuantityOutForClients) // Sum the quantity out for each user
                    })
                    .ToListAsync();

                if (!usersOutQuantities.Any())
                {
                    return NotFound("No records found for the specified date range.");
                }

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("كميات المسحوبات");

                    // Add header row in Arabic
                    worksheet.Cells[1, 1].Value = "اسم المندوب";
                    worksheet.Cells[1, 2].Value = "رقم السيارة";
                    worksheet.Cells[1, 3].Value = "إجمالي المسحوبات للعملاء";

                    // Add data rows
                    for (int i = 0; i < usersOutQuantities.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = usersOutQuantities[i].UserName ?? "غير معرف";
                        worksheet.Cells[i + 2, 2].Value = usersOutQuantities[i].CarNumber ?? "غير معروف";
                        worksheet.Cells[i + 2, 3].Value = usersOutQuantities[i].TotalQuantityOutForClients ?? 0;
                    }

                    var excelData = package.GetAsByteArray();
                    string fileName = $"مسحوبات المناديب للعملاء من {startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}.xlsx";

                    return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return  StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
            }
        }



        [HttpGet("download-clients")]
        public async Task<IActionResult> DownloadClientsWithQuantityOut(
   [FromQuery] string carNumber,  // Only allow one car number
   [FromQuery] string? district,  // Allow optional single district filtering
   [FromQuery] DateTime? startDate,
   [FromQuery] DateTime? endDate)
        {
            try
            {
                // تأكد من أن startDate و endDate يحتويان على قيم
                if (startDate.HasValue && endDate.HasValue)
                {
                    startDate = startDate.Value.Date;  // الحصول على اليوم بشكل آمن
                    endDate = endDate.Value.Date.AddDays(1).AddTicks(-1);  // إنهاء اليوم
                }

                // إعداد ExcelPackage
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // استعلام البيانات الأساسية للعملاء
                var query = _context.Clients
                    .Include(c => c.FkUser) // تضمين البيانات المتعلقة بالمندوب
                    .AsQueryable();

                // التصفية حسب CarNumber إذا تم تقديمها
                if (!string.IsNullOrEmpty(carNumber))
                {
                    query = query.Where(c => c.FkUser.CarNumber.Contains(carNumber));
                }

                // التصفية حسب district إذا تم تقديمها
                if (!string.IsNullOrEmpty(district))
                {
                    query = query.Where(c => c.ClientDistrict == district);
                }

                // التصفية حسب المدى الزمني
                if (startDate.HasValue && endDate.HasValue)
                {
                    query = query.Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate);
                }

                // حساب الكميات المسحوبة لكل عميل من جدول CalcQuots
                var clientsWithQuantities = await query
                    .Select(c => new
                    {
                        c.ClientName,
                        c.ClientNumber,
                        c.ClientPhone1,
                        c.ClientPhone2,
                        c.ClientPhone3,
                        c.ClientDistrict,
                        c.Description,
                        c.CreatedAt,
                        AddedByUserName = c.FkUser.UserName, // اسم المندوب الذي أضاف العميل
                        CarNumber = c.FkUser.CarNumber, // رقم سيارة المندوب
                        TotalOrderQuantity = _context.Orders
                            .Where(o => o.FkClientId == c.ClientId)
                            .Sum(o => o.OrderQuantity ?? 0), // حساب إجمالي الطلبات لكل عميل
                        TotalQuantityOutForClients = _context.CalcQuots
                            .Where(q => q.FkUserId == c.FkUser.UserId && q.QuantityOutDate >= startDate && q.QuantityOutDate <= endDate)
                            .Sum(q => q.UserQuantityOutForClients ?? 0) // حساب الكمية المسحوبة لكل عميل
                    })
                    .ToListAsync();

                // التحقق من وجود البيانات
                if (clientsWithQuantities == null || !clientsWithQuantities.Any())
                {
                    return NotFound("No clients found for the given filters.");
                }

                // إنشاء ملف Excel
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("العملاء مع الكميات المسحوبة");

                    // إضافة رأس الجدول
                    worksheet.Cells[1, 1].Value = "اسم العميل";
                    worksheet.Cells[1, 2].Value = "رقم العميل";
                    worksheet.Cells[1, 3].Value = "هاتف العميل 1";
                    worksheet.Cells[1, 4].Value = "هاتف العميل 2";
                    worksheet.Cells[1, 5].Value = "هاتف العميل 3";
                    worksheet.Cells[1, 6].Value = "اسم الحي";
                    worksheet.Cells[1, 7].Value = "الوصف";
                    worksheet.Cells[1, 8].Value = "تاريخ الإنشاء";
                    worksheet.Cells[1, 9].Value = "تمت الإضافة بواسطة";
                    worksheet.Cells[1, 10].Value = "رقم السيارة";
                    worksheet.Cells[1, 11].Value = "إجمالي الطلبات";
                    worksheet.Cells[1, 12].Value = "إجمالي الكمية المسحوبة";

                    // إضافة بيانات العملاء
                    for (int i = 0; i < clientsWithQuantities.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = clientsWithQuantities[i].ClientName;
                        worksheet.Cells[i + 2, 2].Value = clientsWithQuantities[i].ClientNumber;
                        worksheet.Cells[i + 2, 3].Value = clientsWithQuantities[i].ClientPhone1;
                        worksheet.Cells[i + 2, 4].Value = clientsWithQuantities[i].ClientPhone2;
                        worksheet.Cells[i + 2, 5].Value = clientsWithQuantities[i].ClientPhone3;
                        worksheet.Cells[i + 2, 6].Value = clientsWithQuantities[i].ClientDistrict;
                        worksheet.Cells[i + 2, 7].Value = clientsWithQuantities[i].Description;
                        worksheet.Cells[i + 2, 8].Value = clientsWithQuantities[i].CreatedAt.ToString("yyyy-MM-dd");
                        worksheet.Cells[i + 2, 9].Value = clientsWithQuantities[i].AddedByUserName;
                        worksheet.Cells[i + 2, 10].Value = clientsWithQuantities[i].CarNumber;
                        worksheet.Cells[i + 2, 11].Value = clientsWithQuantities[i].TotalOrderQuantity;
                        worksheet.Cells[i + 2, 12].Value = clientsWithQuantities[i].TotalQuantityOutForClients;
                    }

                    var excelData = package.GetAsByteArray();
                    string fileName = $"كمية مسحوبات العملاء مع الكميات من {startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}.xlsx";

                   return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
            }
        }






        [HttpGet("download-noactive-clients")]
        public async Task<IActionResult> DownloadClients(
    [FromQuery] string? district,  // اجعل district اختياريًا
    [FromQuery] string carNumber,
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate)
        {
            try
            {
                // تأكد من وجود startDate و endDate
                if (startDate.HasValue && endDate.HasValue)
                {
                    startDate = startDate.Value.Date;  // الوصول إلى التاريخ
                    endDate = endDate.Value.Date.AddDays(1).AddTicks(-1);  // إنهاء اليوم
                }

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // تضمين User للحصول على CarNumber والتصفية حسب الحي
                var query = _context.Clients
                    .Include(c => c.FkUser) // تضمين User للحصول على CarNumber
                    .AsQueryable();

                // التصفية حسب CarNumber
                if (!string.IsNullOrEmpty(carNumber))
                {
                    query = query.Where(c => c.FkUser.CarNumber.Contains(carNumber));
                }

                // التصفية حسب الحي إذا تم تقديم district ولم تكن "All"
                if (!string.IsNullOrEmpty(district) && district != "All")
                {
                    query = query.Where(c => c.ClientDistrict == district);
                }

                // التصفية حسب الفترة الزمنية إذا تم تقديمها
                if (startDate.HasValue && endDate.HasValue)
                {
                    query = query.Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate);
                }

                // جلب العملاء الذين لم يأخذوا أي كمية من جدول CalcQuots
                var clients = await query
                    .Select(c => new
                    {
                        c.ClientName,
                        c.ClientNumber,
                        c.ClientPhone1,
                        c.ClientPhone2,
                        c.ClientPhone3,
                        c.ClientDistrict,
                        c.Description,
                        c.CreatedAt,
                        AddedByUserName = c.FkUser.UserName, // المندوب الذي أضاف العميل
                        CarNumber = c.FkUser.CarNumber, // رقم السيارة
                        TotalQuantityOutFromCalcQuot = _context.CalcQuots
                            .Where(q => q.FkClientId == c.ClientId)
                            .Sum(q => q.ClientQuantityOut ?? 0) // إجمالي الكمية المسحوبة من CalcQuots
                    })
                    .Where(c => c.TotalQuantityOutFromCalcQuot == 0) // العملاء الذين لم يأخذوا أي كمية
                    .ToListAsync();

                if (!clients.Any())
                {
                    return NotFound("No clients found.");
                }

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("العملاء");

                    // إضافة رأس الجدول بالعربية
                    worksheet.Cells[1, 1].Value = "اسم العميل";
                    worksheet.Cells[1, 2].Value = "رقم العميل";
                    worksheet.Cells[1, 3].Value = "هاتف العميل 1";
                    worksheet.Cells[1, 4].Value = "هاتف العميل 2";
                    worksheet.Cells[1, 5].Value = "هاتف العميل 3";
                    worksheet.Cells[1, 6].Value = "اسم الحي"; // District
                    worksheet.Cells[1, 7].Value = "الوصف";
                    worksheet.Cells[1, 8].Value = "تاريخ الإنشاء";
                    worksheet.Cells[1, 9].Value = "تمت الإضافة بواسطة"; // المندوب الذي أضاف العميل
                    worksheet.Cells[1, 10].Value = "رقم السيارة"; // Car Number
                    worksheet.Cells[1, 11].Value = "إجمالي الكمية المسحوبة"; // Total Quantity Out

                    // إضافة البيانات لكل عميل
                    for (int i = 0; i < clients.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = clients[i].ClientName;
                        worksheet.Cells[i + 2, 2].Value = clients[i].ClientNumber;
                        worksheet.Cells[i + 2, 3].Value = clients[i].ClientPhone1;
                        worksheet.Cells[i + 2, 4].Value = clients[i].ClientPhone2;
                        worksheet.Cells[i + 2, 5].Value = clients[i].ClientPhone3;
                        worksheet.Cells[i + 2, 6].Value = clients[i].ClientDistrict;
                        worksheet.Cells[i + 2, 7].Value = clients[i].Description;
                        worksheet.Cells[i + 2, 8].Value = clients[i].CreatedAt.ToString("yyyy-MM-dd");
                        worksheet.Cells[i + 2, 9].Value = clients[i].AddedByUserName; // إضافة اسم المندوب
                        worksheet.Cells[i + 2, 10].Value = clients[i].CarNumber; // إضافة رقم السيارة
                        worksheet.Cells[i + 2, 11].Value = clients[i].TotalQuantityOutFromCalcQuot; // إضافة إجمالي الكمية
                    }

                    var excelData = package.GetAsByteArray();
                    string fileName = $"العملاء الذين لم يأخذوا أي كمية من {startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}.xlsx";

                    return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
            }
        }



        //[HttpGet("download-noactive-clients-before-date-by-15-day")]
        //public async Task<IActionResult> DownloadClientsBeforeDate(
        //  [FromQuery] string district,
        //  [FromQuery] string userName,
        //  [FromQuery] string carNumber,
        //  [FromQuery] DateTime? startDate)
        //{
        //    try
        //    {
        //        // Ensure startDate and endDate have values
        //        if (startDate.HasValue )
        //        {
        //            startDate = startDate.Value.Date;  // Access the Date property safely
        //        }
        //        if (!startDate.HasValue)
        //        {
        //            return BadRequest("Start date is required.");
        //        }

        //        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        //        // Define the 15-day period before the start date
        //        DateTime fifteenDaysBeforeStartDate = startDate.Value.AddDays(-60);

        //        // Build the base query including FkUser to get the UserName and CarNumber
        //        var query = _context.Clients
        //            .Include(c => c.FkUser) // Include the User entity to get UserName and CarNumber
        //            .AsQueryable();

        //        // Apply district filter if provided
        //        if (!string.IsNullOrEmpty(district))
        //        {
        //            query = query.Where(c => c.ClientDistrict == district);
        //        }

        //        // Apply carNumber filter if provided
        //        if (!string.IsNullOrEmpty(carNumber))
        //        {
        //            query = query.Where(c => c.FkUser.CarNumber.Contains(carNumber));
        //        }

        //        // Apply userName filter if provided
        //        if (!string.IsNullOrEmpty(userName))
        //        {
        //            query = query.Where(c => c.FkUser.UserName.Contains(userName));
        //        }

        //        // Filter clients who have no orders in the 15-day period before the start date
        //        var clients = await query
        //            .Select(c => new
        //            {
        //                c.ClientName,
        //                c.ClientNumber,
        //                c.ClientPhone1,
        //                c.ClientPhone2,
        //                c.ClientPhone3,
        //                c.ClientDistrict,
        //                c.Description,
        //                c.CreatedAt,
        //                AddedByUserName = c.FkUser.UserName, // Get the UserName from the User entity
        //                CarNumber = c.FkUser.CarNumber, // Get the CarNumber from the User entity
        //                TotalOrderQuantity = _context.Orders
        //                    .Where(o => o.FkClientId == c.ClientId && o.OrderDate >= fifteenDaysBeforeStartDate && o.OrderDate < startDate && c.ClientQuantity == 0)
        //                    .Sum(o => o.OrderQuantity ?? 0) // Sum the order quantities for each client before the start date
        //            })
        //            .Where(c => c.TotalOrderQuantity == 0) // Filter clients with total order quantity of 0 in the 15-day period before the start date
        //            .ToListAsync();

        //        // Generate Excel file
        //        using (var package = new ExcelPackage())
        //        {
        //            var worksheet = package.Workbook.Worksheets.Add("العملاء");

        //            // Add header row in Arabic
        //            worksheet.Cells[1, 1].Value = "اسم العميل";
        //            worksheet.Cells[1, 2].Value = "رقم العميل";
        //            worksheet.Cells[1, 3].Value = "هاتف العميل 1";
        //            worksheet.Cells[1, 4].Value = "هاتف العميل 2";
        //            worksheet.Cells[1, 5].Value = "هاتف العميل 3";
        //            worksheet.Cells[1, 6].Value = "منطقة العميل";
        //            worksheet.Cells[1, 7].Value = "الوصف";
        //            worksheet.Cells[1, 8].Value = "تاريخ الإنشاء";
        //            worksheet.Cells[1, 9].Value = "تمت الإضافة بواسطة"; // Added column for UserName
        //            worksheet.Cells[1, 10].Value = "رقم السيارة"; // Added column for Car Number
        //            worksheet.Cells[1, 11].Value = "إجمالي كمية الطلب"; // Added column for Total Order Quantity

        //            // Add data rows
        //            for (int i = 0; i < clients.Count; i++)
        //            {
        //                worksheet.Cells[i + 2, 1].Value = clients[i].ClientName;
        //                worksheet.Cells[i + 2, 2].Value = clients[i].ClientNumber;
        //                worksheet.Cells[i + 2, 3].Value = clients[i].ClientPhone1;
        //                worksheet.Cells[i + 2, 4].Value = clients[i].ClientPhone2;
        //                worksheet.Cells[i + 2, 5].Value = clients[i].ClientPhone3;
        //                worksheet.Cells[i + 2, 6].Value = clients[i].ClientDistrict;
        //                worksheet.Cells[i + 2, 7].Value = clients[i].Description;
        //                worksheet.Cells[i + 2, 8].Value = clients[i].CreatedAt.ToString("yyyy-MM-dd");
        //                worksheet.Cells[i + 2, 9].Value = clients[i].AddedByUserName; // Add UserName to Excel
        //                worksheet.Cells[i + 2, 10].Value = clients[i].CarNumber; // Add Car Number to Excel
        //                worksheet.Cells[i + 2, 11].Value = clients[i].TotalOrderQuantity; // Add Total Order Quantity to Excel
        //            }

        //            var excelData = package.GetAsByteArray();
        //            string fileName = $"العملاء_قبل_{startDate:yyyy-MM-dd}_بدون_طلبات_.xlsx";

        //            return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
        //    }
        //}

        [HttpGet("download-noactive-clients-before-date-by-15-day")]
        public async Task<IActionResult> DownloadClientsBeforeDate(
    [FromQuery] string? district, // جعل district اختياريًا
    [FromQuery] string carNumber,
    [FromQuery] DateTime? startDate)
        {
            try
            {
                // تأكد من وجود startDate
                if (!startDate.HasValue)
                {
                    return BadRequest("Start date is required.");
                }

                DateTime endDate = startDate.Value.Date.AddDays(-15);  // 15 يوماً قبل تاريخ البدء

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // بناء الاستعلام الأساسي مع تضمين FkUser للحصول على CarNumber
                var query = _context.Clients
                    .Include(c => c.FkUser) // تضمين FkUser للحصول على CarNumber
                    .AsQueryable();

                // التصفية حسب CarNumber
                if (!string.IsNullOrEmpty(carNumber))
                {
                    query = query.Where(c => c.FkUser.CarNumber.Contains(carNumber));
                }

                // التصفية حسب الحي إذا تم تقديم district ولم تكن "All"
                if (!string.IsNullOrEmpty(district) && district != "All")
                {
                    query = query.Where(c => c.ClientDistrict == district);
                }

                // جلب العملاء الذين لم يسحبوا أي كمية من جدول CalcQuots خلال فترة 15 يوماً قبل startDate
                var clients = await query
                    .Select(c => new
                    {
                        c.ClientName,
                        c.ClientNumber,
                        c.ClientPhone1,
                        c.ClientPhone2,
                        c.ClientPhone3,
                        c.ClientDistrict,
                        c.Description,
                        c.CreatedAt,
                        AddedByUserName = c.FkUser.UserName, // المندوب الذي أضاف العميل
                        CarNumber = c.FkUser.CarNumber, // رقم السيارة
                        TotalQuantityOutFromCalcQuot = _context.CalcQuots
                            .Where(q => q.FkClientId == c.ClientId && q.QuantityOutDate <= startDate.Value && q.QuantityOutDate >= endDate)
                            .Sum(q => q.ClientQuantityOut ?? 0) // إجمالي الكمية المسحوبة من CalcQuots
                    })
                    .Where(c => c.TotalQuantityOutFromCalcQuot == 0) // العملاء الذين لم يسحبوا أي كمية
                    .ToListAsync();

                if (!clients.Any())
                {
                    return NotFound("No clients found.");
                }

                // إنشاء ملف Excel
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("العملاء");

                    // إضافة رأس الجدول بالعربية
                    worksheet.Cells[1, 1].Value = "اسم العميل";
                    worksheet.Cells[1, 2].Value = "رقم العميل";
                    worksheet.Cells[1, 3].Value = "هاتف العميل 1";
                    worksheet.Cells[1, 4].Value = "هاتف العميل 2";
                    worksheet.Cells[1, 5].Value = "هاتف العميل 3";
                    worksheet.Cells[1, 6].Value = "اسم الحي";
                    worksheet.Cells[1, 7].Value = "الوصف";
                    worksheet.Cells[1, 8].Value = "تاريخ الإنشاء";
                    worksheet.Cells[1, 9].Value = "تمت الإضافة بواسطة"; // المندوب الذي أضاف العميل
                    worksheet.Cells[1, 10].Value = "رقم السيارة"; // رقم السيارة
                    worksheet.Cells[1, 11].Value = "إجمالي الكمية المسحوبة"; // إجمالي الكمية المسحوبة

                    // إضافة البيانات لكل عميل
                    for (int i = 0; i < clients.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = clients[i].ClientName;
                        worksheet.Cells[i + 2, 2].Value = clients[i].ClientNumber;
                        worksheet.Cells[i + 2, 3].Value = clients[i].ClientPhone1;
                        worksheet.Cells[i + 2, 4].Value = clients[i].ClientPhone2;
                        worksheet.Cells[i + 2, 5].Value = clients[i].ClientPhone3;
                        worksheet.Cells[i + 2, 6].Value = clients[i].ClientDistrict;
                        worksheet.Cells[i + 2, 7].Value = clients[i].Description;
                        worksheet.Cells[i + 2, 8].Value = clients[i].CreatedAt.ToString("yyyy-MM-dd");
                        worksheet.Cells[i + 2, 9].Value = clients[i].AddedByUserName; // المندوب الذي أضاف العميل
                        worksheet.Cells[i + 2, 10].Value = clients[i].CarNumber; // رقم السيارة
                        worksheet.Cells[i + 2, 11].Value = clients[i].TotalQuantityOutFromCalcQuot; // إجمالي الكمية
                    }

                    var excelData = package.GetAsByteArray();
                    string fileName = $"العملاء_الذين_لم_يسحبوا_أي_كمية_قبل_{startDate:yyyy-MM-dd}_بـ15_يوم.xlsx";

                    return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while generating the Excel file: {ex.Message}");
            }
        }




        [HttpGet("get-total-scrap")]
        public async Task<ActionResult<int>> GetAllScrap()
        {
            try
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync();

                if (inventory == null)
                    return NotFound("Inventory not found.");

                return Ok(inventory.AllScrap ?? 0);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while retrieving the total scrap.", Error = ex.Message });
            }
        }



        // API to edit AllScrap in inventory
        [HttpPut("edit-all-scrap")]
        public async Task<ActionResult<string>> EditAllScrap([FromBody] EditScrapVM model)
        {
            try
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync();
                if (inventory == null)
                {
                    return NotFound("Inventory not found");
                }

                inventory.AllScrap = model.NewAllScrap;
                _context.Inventories.Update(inventory);
                await _context.SaveChangesAsync();

                return Ok("تم التعدييل");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while updating AllScrap: {ex.Message}");
            }
        }

        [HttpGet("get-districts-dropdown")]
        public async Task<IActionResult> GetDistrictsDropdown()
        {
            try
            {
                // Fetch all districts from the database
                var districts = await _context.Districts
                    .Select(d => new DistrictDropdownVM
                    {
                        DistrictId = d.DistrictId,
                        DistrictName = d.DistrictName
                    })
                    .ToListAsync();

                // Return the list of districts as a JSON response
                return new JsonResult(new { data = districts, message = "Success", count = districts.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new JsonResult(new { Message = "An error occurred while retrieving the district data.", Error = ex.Message }));
            }
        }
    }

    }
