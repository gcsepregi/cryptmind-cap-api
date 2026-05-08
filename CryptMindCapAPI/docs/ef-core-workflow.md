# EF Core Workflow

Migrations are applied automatically on startup via `MigrateAsync()`. You only need to generate the migration file — running the app does the rest.

All commands run from the project directory:
```bash
cd /Users/gcsepregi/Projects/CryptMindCapAPI/CryptMindCapAPI
```

---

## Adding a new model

### 1. Create the entity in `Core/Data/`

```csharp
// Core/Data/User.cs
namespace CryptMindCapAPI.Core.Data;

public class User
{
    public string Id { get; set; } = "";
    public string PublicKeyB64 { get; set; } = "";
    public long CreatedAt { get; set; }
}
```

### 2. Add it to the relevant DbContext with fluent configuration

Column names use `snake_case` to match MariaDB conventions.

```csharp
// Core/Data/FlagsDbContext.cs
public DbSet<User> Users => Set<User>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // existing config ...

    modelBuilder.Entity<User>(e =>
    {
        e.ToTable("users");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
        e.Property(x => x.PublicKeyB64).HasColumnName("public_key_b64");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
    });
}
```

### 3. Generate the migration

Name migrations descriptively in PascalCase.

```bash
dotnet ef migrations add AddUsers --context FlagsDbContext
```

This creates `Migrations/<timestamp>_AddUsers.cs`. Review it before committing — EF occasionally misreads intent (e.g. drop+recreate instead of rename).

### 4. Run the app

`MigrateAsync()` in `Program.cs` applies all pending migrations on startup. No manual step needed.

---

## Updating an existing model

### 1. Change the entity class

```csharp
// Adding a new column
public class User
{
    public string Id { get; set; } = "";
    public string PublicKeyB64 { get; set; } = "";
    public long CreatedAt { get; set; }
    public long? LastSeenAt { get; set; }   // new
}
```

### 2. Update the DbContext fluent config

```csharp
e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
```

### 3. Generate the migration

```bash
dotnet ef migrations add AddUserLastSeenAt --context FlagsDbContext
```

### 4. Run the app

Migrations apply on startup.

---

## Adding a second DbContext

When a new feature area warrants its own database (e.g. `cryptmind_storage`), follow this pattern:

### 1. Create the context

```csharp
// Core/Data/StorageDbContext.cs
public class StorageDbContext(DbContextOptions<StorageDbContext> options) : DbContext(options)
{
    // DbSets and OnModelCreating here
}
```

### 2. Register it in `Program.cs`

```csharp
builder.Services.AddDbContext<StorageDbContext>(options =>
    options.UseMySql(settings.MariaDb.StorageConnectionString,
        ServerVersion.AutoDetect(settings.MariaDb.StorageConnectionString)));
```

### 3. Add it to the startup migration block

```csharp
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<FlagsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<StorageDbContext>().Database.MigrateAsync();
}
```

### 4. Generate its first migration

```bash
dotnet ef migrations add InitialCreate --context StorageDbContext --output-dir Migrations/Storage
```

Use `--output-dir` to keep each context's migrations in a separate folder.

---

## Renaming a column

EF cannot detect renames — it will drop and recreate the column, losing data. Override the generated migration manually:

```csharp
// Generated (wrong — data loss):
migrationBuilder.DropColumn("old_name", "users");
migrationBuilder.AddColumn<string>("new_name", "users");

// Correct:
migrationBuilder.RenameColumn("old_name", "users", "new_name");
```

Always review generated migrations when renaming anything.

---

## Useful commands

```bash
# List applied and pending migrations
dotnet ef migrations list --context FlagsDbContext

# Remove the last migration (only if not yet applied to the DB)
dotnet ef migrations remove --context FlagsDbContext

# Apply migrations manually without starting the app
dotnet ef database update --context FlagsDbContext
```
