using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Disable file watchers for configuration files in container environments to avoid inotify limits
foreach (var source in builder.Configuration.Sources.OfType<FileConfigurationSource>())
{
    source.ReloadOnChange = false;
}

// 1. Configure CORS to allow Vercel and any other frontend origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. Configure Entity Framework Core with PostgreSQL (supporting both URI and Key-Value formats)
var rawConnString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var connString = ParseNpgsqlConnectionString(rawConnString);

builder.Services.AddDbContext<GrapholoDbContext>(options =>
    options.UseNpgsql(connString));

// Helper to parse postgres:// or postgresql:// URI strings into Npgsql format
static string ParseNpgsqlConnectionString(string rawConnString)
{
    if (string.IsNullOrWhiteSpace(rawConnString)) return rawConnString;

    if (rawConnString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        rawConnString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(rawConnString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }

    return rawConnString;
}

// 3. Configure HTTP Client to talk to the Python Math Engine inside the Docker network
var rawMathUrl = builder.Configuration["MathEngineUrl"] ?? "http://math-engine:5000";
var mathUrl = rawMathUrl.EndsWith("/") ? rawMathUrl : rawMathUrl + "/";

builder.Services.AddHttpClient("MathEngine", client =>
{
    client.BaseAddress = new Uri(mathUrl);
});

var app = builder.Build();

// Apply CORS policy globally to all endpoints and preflight requests
app.UseCors("AllowAll");

// Auto-create database tables and seed initial puzzle if DB is brand new
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GrapholoDbContext>();
    db.Database.EnsureCreated();

    if (!db.Puzzles.Any())
    {
        db.Puzzles.Add(new Puzzle
        {
            PuzzleDate = DateTime.UtcNow.Date,
            TargetPoints = "[{\"x\": -3, \"y\": 0}, {\"x\": 0, \"y\": 3}, {\"x\": 3, \"y\": 0}]",
            Hint = "Think symmetrical, but flatter."
        });
        db.SaveChanges();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Database initialization notice: {ex.Message}");
}

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "Grapholo API Gateway Online" })).RequireCors("AllowAll");

// --- API Endpoints ---

// Get today's puzzle
app.MapGet("/api/puzzles/today", async (GrapholoDbContext db) =>
{
    try
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error fetching puzzle: {ex.Message}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}).RequireCors("AllowAll");

// Submit a winning equation
app.MapPost("/api/submissions", async (SubmissionRequest req, GrapholoDbContext db, IHttpClientFactory httpFactory) =>
{
    // 1. Verify the puzzle exists in the DB
    var puzzle = await db.Puzzles.FindAsync(req.PuzzleId);
    if (puzzle == null) return Results.BadRequest(new { message = "Invalid puzzle ID" });

    try
    {
        // 2. Ask the Python microservice if the equation actually hits the targets
        var client = httpFactory.CreateClient("MathEngine");
        var pythonPayload = new {
            equation = req.Equation,
            targets = JsonSerializer.Deserialize<JsonElement>(puzzle.TargetPoints)
        };

        var response = await client.PostAsJsonAsync("validate", pythonPayload);
        
        if (!response.IsSuccessStatusCode) {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MathEngine error ({response.StatusCode}): {errorContent}");
            return Results.BadRequest(new { message = $"Math Engine error ({response.StatusCode}): {errorContent}" });
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error communicating with MathEngine: {ex.Message}");
        return Results.BadRequest(new { message = $"Connection to Math Engine failed: {ex.Message}" });
    }
}).RequireCors("AllowAll");

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