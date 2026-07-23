using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure CORS so our local HTML file can fetch data
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 2. Configure Entity Framework Core with PostgreSQL
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<GrapholoDbContext>(options =>
    options.UseNpgsql(connString));

// 3. Configure HTTP Client to talk to the Python Math Engine inside the Docker network
builder.Services.AddHttpClient("MathEngine", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MathEngineUrl"] ?? "http://math-engine:5000");
});

var app = builder.Build();

// Apply CORS policy
app.UseCors("AllowAll");


// --- API Endpoints ---

// Get today's puzzle
app.MapGet("/api/puzzles/today", async (GrapholoDbContext db) =>
{
    // Get the current date (using UTC to avoid timezone edge cases)
    var today = DateTime.UtcNow.Date;

    // Fetch the specific puzzle meant for today
    var puzzle = await db.Puzzles.FirstOrDefaultAsync(p => p.PuzzleDate == today);
    
    // Fallback for local development: if no puzzle for today exists, get the most recent one
    if (puzzle == null)
    {
        puzzle = await db.Puzzles.OrderByDescending(p => p.PuzzleDate).FirstOrDefaultAsync();
    }

    if (puzzle == null) return Results.NotFound(new { message = "No puzzles found in the database." });
    
    // Convert the JSONB string from Postgres into a usable JSON object for the frontend
    var points = JsonSerializer.Deserialize<JsonElement>(puzzle.TargetPoints);
    
    return Results.Ok(new { 
        id = puzzle.Id, 
        date = puzzle.PuzzleDate, 
        points = points, 
        hint = puzzle.Hint 
    });
});

// Submit a winning equation
app.MapPost("/api/submissions", async (SubmissionRequest req, GrapholoDbContext db, IHttpClientFactory httpFactory) =>
{
    // 1. Verify the puzzle exists in the DB
    var puzzle = await db.Puzzles.FindAsync(req.PuzzleId);
    if (puzzle == null) return Results.BadRequest("Invalid puzzle ID");

    // 2. Ask the Python microservice if the equation actually hits the targets
    var client = httpFactory.CreateClient("MathEngine");
    var pythonPayload = new {
        equation = req.Equation,
        targets = JsonSerializer.Deserialize<JsonElement>(puzzle.TargetPoints)
    };

    var response = await client.PostAsJsonAsync("/validate", pythonPayload);
    
    if (!response.IsSuccessStatusCode) {
        return Results.BadRequest(new { message = "Invalid mathematical expression" });
    }

    var validationResult = await response.Content.ReadFromJsonAsync<ValidationResult>();

    // 3. Save the submission to the PostgreSQL database
    var submission = new Submission {
        PuzzleId = puzzle.Id,
        EquationUsed = req.Equation,
        IsSuccessful = validationResult?.IsValid ?? false
    };
    
    db.Submissions.Add(submission);
    await db.SaveChangesAsync();

    // 4. Return the Python engine's verdict back to the frontend
    return Results.Ok(validationResult);
});

app.Run();


// --- Database Models & Context ---

public class GrapholoDbContext : DbContext
{
    public GrapholoDbContext(DbContextOptions<GrapholoDbContext> options) : base(options) { }
    public DbSet<Puzzle> Puzzles => Set<Puzzle>();
    public DbSet<Submission> Submissions => Set<Submission>();
}

// Maps precisely to the columns we created in db-init/init.sql
[Table("puzzles")]
public class Puzzle
{
    [Column("id")] public int Id { get; set; }
    [Column("puzzle_date")] public DateTime PuzzleDate { get; set; }
    [Column("target_points")] public string TargetPoints { get; set; } = "[]";
    [Column("hint")] public string? Hint { get; set; }
}

[Table("submissions")]
public class Submission
{
    [Column("id")] public int Id { get; set; }
    [Column("puzzle_id")] public int PuzzleId { get; set; }
    [Column("equation_used")] public string EquationUsed { get; set; } = "";
    [Column("is_successful")] public bool IsSuccessful { get; set; }
}

// --- DTOs (Data Transfer Objects) ---
public record SubmissionRequest(int PuzzleId, string Equation);
public record ValidationResult(bool IsValid, string Message);