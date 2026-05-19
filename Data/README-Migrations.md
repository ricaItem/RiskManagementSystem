# Applying Platform migrations

If you see:

```text
Could not load assembly 'WEB_Sentro'. Ensure it is referenced by the startup project 'WEB_Sentro'.
```

do one of the following.

## Option A: Package Manager Console (PMC)

1. **Set startup project**  
   In Solution Explorer, right-click **WEB_Sentro** → **Set as Startup Project**.

2. **Set default project in PMC**  
   In Package Manager Console, open the **Default project** dropdown and select **WEB_Sentro** (the project that contains `Data/PlatformDbContext.cs` and migrations). Do **not** select WEB_Sentro.Tests.

3. Run:

   ```powershell
   Update-Database -Context PlatformDbContext
   ```

You can also try specifying the startup project explicitly:

```powershell
Update-Database -Context PlatformDbContext -Project WEB_Sentro -StartupProject WEB_Sentro
```

## Option B: dotnet EF CLI (recommended if PMC keeps failing)

From the **solution folder** (where `WEB_Sentro.sln` and `WEB_Sentro.csproj` are):

```powershell
dotnet ef database update --context PlatformDbContext --project WEB_Sentro --startup-project WEB_Sentro
```

If you are already in the project folder (same folder as `WEB_Sentro.csproj`):

```powershell
dotnet ef database update --context PlatformDbContext
```

Ensure the **EF Core tools** are installed once:

```powershell
dotnet tool install --global dotnet-ef
```

(If already installed, you can skip this.)
