using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TodoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;

        public TodoController(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // 1. GET /api/todo - Retrieve all ToDo items
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodos()
        {
            var items = await _context.TodoItems.ToListAsync();
            return Ok(items);
        }

        // 2. POST /api/todo - Create a new ToDo item
        [HttpPost]
        public async Task<ActionResult<TodoItem>> CreateTodo(TodoItem item)
        {
            item.CreatedAt = DateTime.UtcNow;
            _context.TodoItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTodos), new { id = item.Id }, item);
        }

        // 3. GET /api/todo/{id}/external-verify - Retrieve ToDo item & make outgoing HttpClient call
        [HttpGet("{id}/external-verify")]
        public async Task<IActionResult> ExternalVerify(int id)
        {
            // Fetch item from SQLite (Generates EF Core DB Span)
            var todo = await _context.TodoItems.FindAsync(id);
            if (todo == null)
            {
                return NotFound(new { Message = $"Todo item with ID {id} not found in database." });
            }

            // Outgoing HTTP request (Generates HttpClient Span)
            var externalUrl = $"https://jsonplaceholder.typicode.com/todos/{id}";
            var response = await _httpClient.GetAsync(externalUrl);

            object? externalData = null;
            if (response.IsSuccessStatusCode)
            {
                externalData = await response.Content.ReadFromJsonAsync<object>();
            }

            return Ok(new
            {
                LocalTodo = todo,
                ExternalVerification = new
                {
                    StatusCode = (int)response.StatusCode,
                    Data = externalData
                }
            });
        }
    }
}
