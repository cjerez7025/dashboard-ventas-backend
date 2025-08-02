// Controllers/TodoController.cs
using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TodoController : ControllerBase
    {
        // Lista en memoria (simula una DB)
        private static List<TodoTask> _tasks = new List<TodoTask>
        {
            new TodoTask { Id = 1, Title = "Aprender Angular", IsCompleted = false },
            new TodoTask { Id = 2, Title = "Crear API .NET", IsCompleted = true }
        };
        
        private static int _nextId = 3;

        [HttpGet]
        public ActionResult<List<TodoTask>> GetTasks()
        {
            return Ok(_tasks);
        }

        [HttpGet("{id}")]
        public ActionResult<TodoTask> GetTask(int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
                return NotFound();
            
            return Ok(task);
        }

        [HttpPost]
        public ActionResult<TodoTask> CreateTask(TodoTask task)
        {
            task.Id = _nextId++;
            task.CreatedDate = DateTime.Now;
            _tasks.Add(task);
            
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }

        [HttpPut("{id}")]
        public IActionResult UpdateTask(int id, TodoTask task)
        {
            var existingTask = _tasks.FirstOrDefault(t => t.Id == id);
            if (existingTask == null)
                return NotFound();

            existingTask.Title = task.Title;
            existingTask.IsCompleted = task.IsCompleted;
            
            return NoContent();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteTask(int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
                return NotFound();

            _tasks.Remove(task);
            return NoContent();
        }
    }
}