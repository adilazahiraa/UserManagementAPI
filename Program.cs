using Serilog;
using UserManagementAPI.Services;
using System.Text.Json;
using System.Xml.Serialization;
using UserManagementAPI.Models;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpLogging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IMyService, MyService>();

var app = builder.Build();

var samplePerson = new Person
{
    Username = "Alice",
    UserAge = 30
};

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseHttpLogging();

// Global Error Handling Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Global exception caught");

        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Unexpected error occurred. Please try again later.");
    }
});

// Request Timing Middleware
app.Use(async (context, next) =>
{
    var startTime = DateTime.UtcNow;

    await next.Invoke();

    var duration = DateTime.UtcNow - startTime;

    Log.Information(
        "Request {Method} {Path} took {Duration} ms",
        context.Request.Method,
        context.Request.Path,
        duration.TotalMilliseconds
    );
});

// API Key Middleware for POST, PUT, DELETE
app.Use(async (context, next) =>
{
    if (context.Request.Method != "GET")
    {
        var apiKey = context.Request.Headers["XAPIKey"];

        if (apiKey != "thisIsABadPassword")
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid API key");
            return;
        }
    }

    await next.Invoke();
});

// Simple Secret Key Middleware
app.Use(async (context, next) =>
{
    var secret = context.Request.Headers["secret-key"];

    if (secret != "adila123")
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    await next.Invoke();
});

// DI Service Middleware
app.Use(async (context, next) =>
{
    var myService = context.RequestServices.GetRequiredService<IMyService>();

    myService.LogCreation("DI Middleware");

    await next.Invoke();
});

// Test error endpoint
app.MapGet("/error", () =>
{
    throw new Exception("Test error");
});

// Serialization examples
app.MapGet("/manual-json", () =>
{
    var jsonString = JsonSerializer.Serialize(samplePerson);

    return TypedResults.Text(jsonString, "application/json");
});

app.MapGet("/json", () =>
{
    return TypedResults.Json(samplePerson);
});

app.MapGet("/auto", () =>
{
    return samplePerson;
});

app.MapGet("/xml", () =>
{
    var xmlSerializer = new XmlSerializer(typeof(Person));
    var stringWriter = new StringWriter();

    xmlSerializer.Serialize(stringWriter, samplePerson);

    return TypedResults.Text(stringWriter.ToString(), "application/xml");
});

// Serialization to files
app.MapGet("/save-person-files", () =>
{
    var jsonData = JsonSerializer.Serialize(samplePerson);
    File.WriteAllText("person.json", jsonData);

    var xmlSerializer = new XmlSerializer(typeof(Person));
    using (var writer = new StreamWriter("person.xml"))
    {
        xmlSerializer.Serialize(writer, samplePerson);
    }

    using (var fs = new FileStream("person.dat", FileMode.Create))
    using (var binaryWriter = new BinaryWriter(fs))
    {
        binaryWriter.Write(samplePerson.Username);
        binaryWriter.Write(samplePerson.UserAge);
    }

    return Results.Ok("Files created: person.json, person.xml, person.dat");
});

// Deserialization from files
app.MapGet("/read-person-files", () =>
{
    var jsonData = File.ReadAllText("person.json");
    var personFromJson = JsonSerializer.Deserialize<Person>(jsonData);

    var xmlSerializer = new XmlSerializer(typeof(Person));

    Person? personFromXml;
    using (var reader = new StreamReader("person.xml"))
    {
        personFromXml = xmlSerializer.Deserialize(reader) as Person;
    }

    Person personFromBinary;
    using (var fs = new FileStream("person.dat", FileMode.Open))
    using (var binaryReader = new BinaryReader(fs))
    {
        personFromBinary = new Person
        {
            Username = binaryReader.ReadString(),
            UserAge = binaryReader.ReadInt32()
        };
    }

    return Results.Ok(new
    {
        FromJson = personFromJson,
        FromXml = personFromXml,
        FromBinary = personFromBinary
    });
});

// Secure serialization example
app.MapGet("/secure-serialize-user", () =>
{
    var user = new SecureUser
    {
        Name = "Alice",
        Email = "alice@example.com",
        Password = "password123"
    };

    var serializedData = SerializeUserData(user);
    var hash = user.GenerateHash();

    return Results.Ok(new
    {
        SerializedData = serializedData,
        Hash = hash
    });
});

// Secure deserialization example
app.MapPost("/secure-deserialize-user", async (HttpContext context) =>
{
    var isTrustedSource = context.Request.Headers["trusted-source"] == "true";

    if (!isTrustedSource)
    {
        return Results.BadRequest("Untrusted source. Deserialization blocked.");
    }

    var user = await context.Request.ReadFromJsonAsync<SecureUser>();

    if (
        user == null ||
        string.IsNullOrWhiteSpace(user.Name) ||
        string.IsNullOrWhiteSpace(user.Email) ||
        string.IsNullOrWhiteSpace(user.Password)
    )
    {
        return Results.BadRequest("Invalid user data");
    }

    return Results.Ok(new
    {
        Message = "Secure deserialization success",
        User = user
    });
});

app.MapControllers();

app.Run();

string SerializeUserData(SecureUser user)
{
    if (
        string.IsNullOrWhiteSpace(user.Name) ||
        string.IsNullOrWhiteSpace(user.Email) ||
        string.IsNullOrWhiteSpace(user.Password)
    )
    {
        return "Invalid user data";
    }

    user.EncryptData();

    return JsonSerializer.Serialize(user);
}

public class SecureUser
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";

    public void EncryptData()
    {
        Password = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(Password)
        );
    }

    public string GenerateHash()
    {
        using var sha256 = SHA256.Create();

        var rawData = $"{Name}|{Email}|{Password}";
        var bytes = Encoding.UTF8.GetBytes(rawData);
        var hashBytes = sha256.ComputeHash(bytes);

        return Convert.ToBase64String(hashBytes);
    }
}