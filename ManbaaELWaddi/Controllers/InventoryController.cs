using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManbaaELWaddi.Data;
using ManbaaELWaddi.ViewModels.Inventory;
using ElWadManbaaELWaddidi.Models;

namespace ElWaddi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }



        //// Add Empty Quantity to Inventory
        [HttpPost("addinventory")]
        public async Task<ActionResult<string>> AddInventory([FromForm] AddInventoryVM addInventoryVM)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Inventory newInventory = new Inventory
                    {
                        InventoryQuantityEmpty = addInventoryVM.InventoryQuantityEmpty,
                        AddedBy = "Admin", // Set appropriate user
                        AddedById = 1,     // Set appropriate user ID
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Inventories.Add(newInventory);
                    await _context.SaveChangesAsync();

                    return Ok("Inventory added successfully");
                }
                catch (Exception ex)
                {
                    // Capture both the main exception and inner exception details
                    var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while adding the inventory.", Error = errorMessage });
                }
            }
            return BadRequest(ModelState);
        }

        // Edit Empty Quantites
        [HttpPut("editinventory/{id}")]
        public async Task<ActionResult<string>> EditInventory(int id, [FromBody] EditInventoryVm editInventoryVM)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var inventory = await _context.Inventories.FindAsync(id);
                    if (inventory == null)
                    {
                        return NotFound($"Inventory with ID {id} not found.");
                    }

                    // Update the inventory
                    inventory.InventoryQuantityEmpty = editInventoryVM.InventoryQuantityEmpty;
                    inventory.UpdatedAt = DateTime.Now;  // Update the 'UpdatedAt' field

                    _context.Inventories.Update(inventory);
                    await _context.SaveChangesAsync();

                    return Ok($"Inventory ID {id} updated successfully.");
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { Message = $"An error occurred while updating the inventory: {ex.Message}" });
                }
            }
            return BadRequest(ModelState);
        }


        ////Get All Quantites 

        [HttpGet("inventory-quantities/full")]
        public async Task<ActionResult<List<GetInventoryQuantityVM>>> GetInventoryQuantityFull()
        {
            try
            {
                var inventoryQuantities = await _context.Inventories
                    .Select(i => new GetInventoryQuantityVM
                    {
                        InventoryId = i.InventoryId,
                        Quantity = i.InventoryQuantityFull
                    }).ToListAsync();

                return Ok(inventoryQuantities);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }
        [HttpGet("inventory-quantities/empty")]
        public async Task<ActionResult<List<GetInventoryQuantityVM>>> GetInventoryQuantityEmpty()
        {
            try
            {
                var inventoryQuantities = await _context.Inventories
                    .Select(i => new GetInventoryQuantityVM
                    {
                        InventoryId = i.InventoryId,
                        Quantity = i.InventoryQuantityEmpty
                    }).ToListAsync();

                return Ok(inventoryQuantities);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }
        [HttpGet("inventory-quantities/empty-with-users")]
        public async Task<ActionResult<List<GetInventoryQuantityVM>>> GetInventoryQuantityEmptyWithUsers()
        {
            try
            {
                var inventoryQuantities = await _context.Inventories
                    .Select(i => new GetInventoryQuantityVM
                    {
                        InventoryId = i.InventoryId,
                        Quantity = i.InventoryQuantityEmptyWithUsers
                    }).ToListAsync();

                return Ok(inventoryQuantities);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }

        //// Edit Empty Quantity
        [HttpGet("inventoryquantities/empty")]
        public async Task<ActionResult<List<InventoryEmptyQuantityVM>>> GetInventoryQuantityEmpty([FromQuery] int? inventoryId)
        {
            try
            {
                var query = _context.Inventories.AsQueryable();

                // Filter by inventory ID if provided
                if (inventoryId.HasValue)
                {
                    query = query.Where(i => i.InventoryId == inventoryId.Value);
                }

                // Projecting directly to the ViewModel within the query
                var inventoryQuantities = await query
                    .Select(i => new InventoryEmptyQuantityVM
                    {
                        InventoryId = i.InventoryId,
                        EmptyQuantity = i.InventoryQuantityEmpty
                    }).ToListAsync();

                return Ok(inventoryQuantities);
            }
            catch (Exception ex)
            {
                // More specific error logging can be useful here
                Console.WriteLine(ex.ToString());
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }


        [HttpPut("edit/{id}")]
        public async Task<IActionResult> EditInventory(int id, [FromForm] EditInventoryVM1 model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var inventory = await _context.Inventories.FindAsync(id);

            if (inventory == null)
            {
                return NotFound("Inventory not found.");
            }

            try
            {
                // Update the inventory details
                inventory.InventoryQuantityFull = model.InventoryQuantityFull;
                inventory.InventoryQuantityEmpty = model.InventoryQuantityEmpty;
                inventory.InventoryQuantityEmptyWithUsers = model.InventoryQuantityEmptyWithUsers;
                inventory.InventoryQuantityEmptyWithClients = model.InventoryQuantityEmptyWithClients;
                inventory.UpdatedAt = DateTime.Now;
          

                _context.Inventories.Update(inventory);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Inventory updated successfully.", Inventory = inventory });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while updating the inventory: {ex.Message}");
            }
        }
    }
}
