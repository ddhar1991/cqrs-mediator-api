# CQRS + MediatR in ASP.NET Core 8 (Web API)

A clean, minimal example of applying the CQRS pattern with MediatR in an ASP.NET Core 8 Web API. It uses EF Core’s InMemory provider for quick start and vertical-slice organization for clarity and maintainability.

<hr>

## Key Features
1. Clear CQRS separation
   - Commands: Create, Update, Delete
   - Queries: Get by Id, List
2. MediatR handlers for orchestration (keeps endpoints/controllers slim)
3. EF Core InMemory provider (no external DB required to try it out)
4. Vertical Slice-style feature folders
5. Minimal APIs with Swagger

<hr>

## Tech Stack
- .NET 8
- ASP.NET Core 8 (Minimal APIs)
- MediatR
- EF Core + InMemory provider
- Swagger/Swashbuckle

<hr>

## Project Structure
'''text
cqrs-mediatr/
├── Domain/
│   └── Product.cs
├── Persistence/
│   └── AppDbContext.cs
├── Features/
│   └── Products/
│       ├── DTOs/
│       │   └── ProductDto.cs
│       ├── Commands/
│       │   ├── Create/
│       │   │   ├── CreateProductCommand.cs
│       │   │   └── CreateProductCommandHandler.cs
│       │   ├── Update/
│       │   │   ├── UpdateProductCommand.cs
│       │   │   └── UpdateProductCommandHandler.cs
│       │   └── Delete/
│       │       ├── DeleteProductCommand.cs
│       │       └── DeleteProductCommandHandler.cs
│       └── Queries/
│           ├── Get/
│           │   ├── GetProductQuery.cs
│           │   └── GetProductQueryHandler.cs
│           └── List/
│               ├── ListProductsQuery.cs
│               └── ListProductsQueryHandler.cs
├── Program.cs
└── README.md
'''

<hr>

## Getting Started

### Prerequisites
- .NET SDK 8.0+
- Optional: Visual Studio 2022 / VS Code

### Install packages (if not already)
### bash
- dotnet add package MediatR
- dotnet add package Microsoft.EntityFrameworkCore
- dotnet add package Microsoft.EntityFrameworkCore.InMemory
- dotnet add package Swashbuckle.AspNetCore

### Build and Run
- dotnet restore
- dotnet build
- dotnet run

### Prerequisites
- .NET SDK 8.0+
- Optional: Visual Studio 2022 / VS Code

### The app will print the listening URLs in the console (Swagger UI at /swagger in Development).
### How CQRS + MediatR Is Applied
- Commands (write side)
  Encapsulate an intent (e.g., CreateProductCommand).
- Handlers apply business logic and persist via EF Core.
- Queries (read side)
   Return read-optimized DTOs (e.g., ProductDto).
-Handlers project entities to DTOs (no write logic).

### Benefits
- Strong separation of concerns
- Testable handlers for each operation
- Read and write paths can evolve independently
- Scales nicely as features grow

### Core Code Snippets

### Domain Model and DTO
- Domain/Product.cs

```csharp
public class Product
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }

    private Product() { } // EF Core
    public Product(string name, string description, decimal price)
        => (Name, Description, Price) = (name, description, price);

    public void Update(string name, string description, decimal price)
        => (Name, Description, Price) = (name, description, price);
}
```
// Features/Products/DTOs/ProductDto.cs
```csharp
public record ProductDto(Guid Id, string Name, string Description, decimal Price);
```
### EF Core DbContext (InMemory)
- Persistence/AppDbContext.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Domain;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasKey(p => p.Id);
    }
}
```
### MediatR Requests (Queries + Commands)
```csharp
// Queries
public record ListProductsQuery : IRequest<List<ProductDto>>;
public record GetProductQuery(Guid Id) : IRequest<ProductDto?>;

// Commands
public record CreateProductCommand(string Name, string Description, decimal Price) : IRequest<Guid>;
public record UpdateProductCommand(Guid Id, string Name, string Description, decimal Price) : IRequest;
public record DeleteProductCommand(Guid Id) : IRequest;
```
### Handlers (Examples)
```csharp
// List Products
public class ListProductsQueryHandler : IRequestHandler<ListProductsQuery, List<ProductDto>>
{
    private readonly AppDbContext _db;
    public ListProductsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<ProductDto>> Handle(ListProductsQuery request, CancellationToken ct) =>
        await _db.Products
                 .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price))
                 .ToListAsync(ct);
}

// Create Product
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly AppDbContext _db;
    public CreateProductCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var product = new Product(cmd.Name, cmd.Description, cmd.Price);
        await _db.Products.AddAsync(product, ct);
        await _db.SaveChangesAsync(ct);
        return product.Id;
    }
}
```
### Minimal API + Registrations
```csharp
// Program.cs
using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core (InMemory)
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase("codewithmukesh"));

// MediatR (scan current assembly)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed demo data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Products.Any())
    {
        db.Products.AddRange(
            new Product("iPhone 15 Pro", "Apple flagship", 999.99m),
            new Product("Dell XPS 15", "High-performance laptop", 1899.99m),
            new Product("Sony WH-1000XM4", "Wireless noise-canceling headphones", 349.99m)
        );
        db.SaveChanges();
    }
}

// Swagger UI in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints
app.MapGet("/api/products", async (IMediator mediator) =>
{
    var items = await mediator.Send(new ListProductsQuery());
    return Results.Ok(items);
});

app.MapGet("/api/products/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var item = await mediator.Send(new GetProductQuery(id));
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/api/products", async (CreateProductCommand cmd, IMediator mediator) =>
{
    var id = await mediator.Send(cmd);
    return Results.Created($"/api/products/{id}", new { id });
});

app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductCommand body, IMediator mediator) =>
{
    if (id != body.Id) return Results.BadRequest("Route id and body id must match.");
    await mediator.Send(body);
    return Results.NoContent();
});

app.MapDelete("/api/products/{id:guid}", async (Guid id, IMediator mediator) =>
{
    await mediator.Send(new DeleteProductCommand(id));
    return Results.NoContent();
});

app.Run();
```
