using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Transactions;
using System.Threading.Tasks;
using ManbaaELWaddi.Data;
using ManbaaELWaddi.ViewModels.Order;
using ManbaaELWaddi.Models;
using Gridify;
using Gridify.EntityFramework;

namespace ManbaaELWaddi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Create Order
        [HttpPost("addorder")]
        public async Task<ActionResult<string>> AddOrder([FromForm] AddOrderVM addOrderVM)
        {
            if (ModelState.IsValid)
            {
                using (var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Retrieve the user based on the provided user ID
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == addOrderVM.FkUserId);
                        if (user == null)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "User not found.");
                        }

                        // Retrieve the client based on the provided client ID
                        var client = await _context.Clients.FirstOrDefaultAsync(c => c.ClientId == addOrderVM.FkClientId);
                        if (client == null)
                        {
                            return StatusCode(StatusCodes.Status500InternalServerError, "Client not found.");
                        }

                        // Parse the order quantity
                        int orderQuantity = int.TryParse(addOrderVM.OrderQuantity, out int parsedQuantity) ? parsedQuantity : 0;

                        // Validate that the user's full quantity is greater than or equal to the order quantity
                        if (user.UserQuantityFull < orderQuantity)
                        {
                            return BadRequest(new { Message = "ليس لديك قوارير كافية " });
                        }

                        // Validate that the order quantity matches the client's quantity
                        if (client.ClientQuantity < orderQuantity)
                        {
                            return BadRequest(new { Message = "خطأ عهدة العميل أقل من كده " });
                        }

                        // Retrieve the latest order number and increment it
                        var latestOrder = await _context.Orders.OrderByDescending(o => o.OrderNumber).FirstOrDefaultAsync();
                        int newOrderNumber = latestOrder != null && int.TryParse(latestOrder.OrderNumber, out int latestOrderNum) ? latestOrderNum + 1 : 1;

                        // Create a new order
                        Order newOrder = new Order
                        {
                            OrderNumber = newOrderNumber.ToString(),
                            OrderQuantity = orderQuantity,
                            OrderDate = DateTime.Now,
                            FkUserId = addOrderVM.FkUserId,
                            FkClientId = addOrderVM.FkClientId,
                        };

                        _context.Orders.Add(newOrder);

                        // Update inventory quantity
                        var inventory = await _context.Inventories.FirstOrDefaultAsync();
                        if (inventory != null)
                        {
                            inventory.InventoryQuantityFull = (inventory.InventoryQuantityFull ?? 0) - orderQuantity;
                            inventory.InventoryQuantityEmptyWithUsers = (inventory.InventoryQuantityEmptyWithUsers ?? 0) + orderQuantity;
                            _context.Inventories.Update(inventory);

                            // Adjust user quantity
                            user.UserQuantityFull = (user.UserQuantityFull ?? 0) - orderQuantity;
                            user.UserQuantityEmpty = (user.UserQuantityEmpty ?? 0) + orderQuantity;
                            _context.Users.Update(user);
                            var userQuoteCalc = new CalcQuot
                            {
                                FkUserId = user.UserId,
                                UserQuantityOutForClients = orderQuantity, // Track quantity out for client
                                ClientQuantityOut = orderQuantity, // Track quantity out for client
                                QuantityOutDate = DateTime.Now // Track the date
                            };
                            _context.CalcQuots.Add(userQuoteCalc);
                        }

                        // No changes to client quantity here
                        await _context.SaveChangesAsync();
                        transactionScope.Complete();

                        return Ok(new { Message = "تم الطلب بنجاح " });
                    }
                    catch (Exception exc)
                    {
                        transactionScope.Dispose();
                        return StatusCode(StatusCodes.Status500InternalServerError, $"خطأ ضع الكمية : {exc.Message}");
                    }
                }
            }
            return BadRequest(ModelState);
        }


        // Get ordre List
        [HttpGet("getorderslist")]
        public async Task<ActionResult<Paging<GetOrderVm>>> GetOrdersList( [FromQuery] GridifyQuery query)
        {
           
            Paging<GetOrderVm> ordersList = new Paging<GetOrderVm>();

            try
            {
                IQueryable<Order> querable = _context.Orders.Include(o => o.FkUser).AsQueryable();

           

                ordersList = await querable.Select(o => new GetOrderVm
                {
                    OrderId = o.OrderId,
                    OrderNumber = o.OrderNumber,
                    OrderQuantity = o.OrderQuantity.ToString(),
                    OrderDate = o.OrderDate,
                }).GridifyAsync(query);

                return Ok(ordersList);
            }
            catch (Exception exp)
            {
                return BadRequest("Something went wrong: " + exp.Message);
            }
        }


        // Get Number Of Orders monthly
        [HttpGet("orders/monthly")]
        public async Task<ActionResult<IEnumerable<OrderMonthlyStatsVM>>> GetOrdersCountByMonth()
        {
            var monthlyOrders = await _context.Orders
                .GroupBy(o => new
                {
                    Year = o.OrderDate.Year,
                    Month = o.OrderDate.Month
                })
                .Select(group => new OrderMonthlyStatsVM
                {
                    Year = group.Key.Year,
                    Month = group.Key.Month,
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .ToListAsync();

            return Ok(monthlyOrders);
        }


        // Get Last Date For the Last Order
        [HttpGet("orders/last-date")]
        public async Task<ActionResult<DateTime?>> GetLastOrderDate()
        {
            var lastOrderDate = await _context.Orders
                .MaxAsync(o => (DateTime?)o.OrderDate); // Use nullable DateTime to handle cases where there are no orders

            if (lastOrderDate == null)
            {
                return NotFound("No orders found.");
            }

            return Ok(lastOrderDate);
        }


        // Get Quantity Bottles for all orders Monthly
        [HttpGet("monthly-quantities")]
        public async Task<ActionResult<List<MonthlyOrderQuantityVM>>> GetMonthlyOrderQuantities()
        {
            var monthlyQuantities = await _context.Orders
                .Where(o => o.OrderQuantity.HasValue) // Ensure there are quantities to sum up
                .GroupBy(o => new
                {
                    Year = o.OrderDate.Year,
                    Month = o.OrderDate.Month
                })
                .Select(g => new MonthlyOrderQuantityVM
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalQuantity = g.Sum(o => o.OrderQuantity.Value) // Sum the quantities for each group
                })
                .OrderByDescending(y => y.Year).ThenByDescending(m => m.Month)
                .ToListAsync();

            return Ok(monthlyQuantities);
        }

        // Get Quantity for every user Monthly that delivered for client

        [HttpGet("monthly-order-quantities")]
        public async Task<ActionResult<List<UserMonthlyOrderQuantityVM>>> GetUserMonthlyOrderQuantities()
        {

            var monthlyUserOrders = await _context.Orders
                .Include(o => o.FkUser)  // Ensure the User is eagerly loaded
                .Where(o => o.OrderQuantity.HasValue)
                .GroupBy(o => new
                {
                    o.FkUserId,
                    Year = o.OrderDate.Year,
                    Month = o.OrderDate.Month,
                    o.FkUser.UserName
                })
                .Select(g => new UserMonthlyOrderQuantityVM
                {
                    UserId = g.Key.FkUserId,
                    UserName = g.Key.UserName,
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalOrderQuantity = g.Sum(o => o.OrderQuantity.Value)
                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToListAsync();

            return Ok(monthlyUserOrders);
        }
        // Get Quantity Bottles for all orders 3Monthly Before
        [HttpGet("3monthesbefore")]
        public async Task<ActionResult<List<MonthlyOrderQuantityVM>>> Get3MonthlyOrderQuantitiesbefore()
        {
            // Calculate the date three months ago from today
            var threeMonthsAgo = DateTime.Today.AddMonths(-3);

            var monthlyQuantities = await _context.Orders
                .Where(o => o.OrderQuantity.HasValue && o.OrderDate >= threeMonthsAgo) // Filter to include only the last three months
                .GroupBy(o => new
                {
                    Year = o.OrderDate.Year,
                    Month = o.OrderDate.Month
                })
                .Select(g => new MonthlyOrderQuantityVM
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalQuantity = g.Sum(o => o.OrderQuantity.Value) // Sum the quantities for each group
                })
                .OrderByDescending(y => y.Year).ThenByDescending(m => m.Month)
                .ToListAsync();

            return Ok(monthlyQuantities);
        }

        // Get Quantity every Day for users that he delivered
        [HttpGet("daily-quantities")]
        public async Task<ActionResult<List<DailyOrderQuantityVM>>> GetDailyOrderQuantities()
        {
            var dailyQuantities = await _context.Orders
                .Where(o => o.OrderQuantity.HasValue) // Ensure there are quantities to sum up
                .GroupBy(o => new
                {
                    Date = o.OrderDate.Date // Group by the date part only
                })
                .Select(g => new DailyOrderQuantityVM
                {
                    Date = g.Key.Date,
                    TotalQuantity = g.Sum(o => o.OrderQuantity.Value) // Sum the quantities for each day
                })
                .OrderByDescending(d => d.Date) // Order by date descending
                .ToListAsync();

            return Ok(dailyQuantities);
        }


        // Get Quantity Daily for  users that he delivered
        [HttpGet("daily-quantities-today")]
        public async Task<ActionResult<List<DailyOrderQuantityVM>>> GetTodayOrderQuantities()
        {
            var today = DateTime.Today; // Get today's date

            var todayQuantities = await _context.Orders
                .Where(o => o.OrderQuantity.HasValue && o.OrderDate.Date == today) // Filter for today's date
                .GroupBy(o => o.OrderDate.Date) // Group by the date part only
                .Select(g => new DailyOrderQuantityVM
                {
                    Date = g.Key,
                    TotalQuantity = g.Sum(o => o.OrderQuantity.Value) // Sum the quantities for today
                })
                .ToListAsync(); // Only one group expected, for today

            return Ok(todayQuantities);
        }


        // Get all quantity order monthly for all users 
        [HttpGet("monthly-quantities-summary")]
        public async Task<ActionResult<List<MonthlyOrderSummaryVM>>> GetSumMonthlyOrderQuantities()
        {
            var monthlyQuantities = await _context.Orders
                .Where(o => o.OrderQuantity.HasValue) // Ensure there are quantities to sum up
                .GroupBy(o => new
                {
                    Year = o.OrderDate.Year,
                    Month = o.OrderDate.Month
                })
                .Select(g => new MonthlyOrderSummaryVM
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalQuantity = g.Sum(o => o.OrderQuantity.Value) // Sum the quantities for each month
                })
                .OrderByDescending(y => y.Year).ThenByDescending(m => m.Month)
                .ToListAsync();

            return Ok(monthlyQuantities);
        }

    }
}



