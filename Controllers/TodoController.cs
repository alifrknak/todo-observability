using System.Diagnostics;
using System.Diagnostics.Metrics;
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

        // Custom Meter for endpoint-level component timing
        public static readonly Meter CustomMeter = new Meter("TodoService.Custom");
        private static readonly Histogram<double> DbDuration = CustomMeter.CreateHistogram<double>(
            "todo.db.duration", "s", "Duration of EF Core database operations per endpoint.");
        private static readonly Histogram<double> ExternalHttpDuration = CustomMeter.CreateHistogram<double>(
            "todo.external_http.duration", "s", "Duration of outgoing external HTTP calls per endpoint.");

        public TodoController(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // 1. GET /api/todo - Retrieve all ToDo items
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodos()
        {
            var sw = Stopwatch.StartNew();
            var items = await _context.TodoItems.ToListAsync();
            sw.Stop();

            DbDuration.Record(sw.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("http.route", "api/Todo"));

            return Ok(items);
        }

        // 2. POST /api/todo - Create a new ToDo item
        [HttpPost]
        public async Task<ActionResult<TodoItem>> CreateTodo(TodoItem item)
        {
            item.CreatedAt = DateTime.UtcNow;
            _context.TodoItems.Add(item);

            var sw = Stopwatch.StartNew();
            await _context.SaveChangesAsync();
            sw.Stop();

            DbDuration.Record(sw.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("http.route", "api/Todo"));

            return CreatedAtAction(nameof(GetTodos), new { id = item.Id }, item);
        }

        // 3. GET /api/todo/{id}/external-verify - Retrieve ToDo item & make outgoing HttpClient call
        [HttpGet("{id}/external-verify")]
        public async Task<IActionResult> ExternalVerify(int id)
        {
            // Fetch item from SQLite (Measure DB time)
            var dbSw = Stopwatch.StartNew();
            var todo = await _context.TodoItems.FindAsync(id);
            dbSw.Stop();

            DbDuration.Record(dbSw.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("http.route", "api/Todo/{id}/external-verify"));

            if (todo == null)
            {
                return NotFound(new { Message = $"Todo item with ID {id} not found in database." });
            }

            // Outgoing HTTP request (Measure External Call time)
            var httpSw = Stopwatch.StartNew();
            var externalUrl = $"https://jsonplaceholder.typicode.com/todos/{id}";
            var response = await _httpClient.GetAsync(externalUrl);
            httpSw.Stop();

            ExternalHttpDuration.Record(httpSw.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("http.route", "api/Todo/{id}/external-verify"));

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
