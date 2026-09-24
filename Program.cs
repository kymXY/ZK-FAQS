using FaqCms.Components;
using FaqCms.Data;
using FaqCms.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with interactive server-side rendering.
// DetailedErrors is off by default (it can leak internals to end users), which is why
// an unhandled exception in an event handler — e.g. a failed upload — only ever shows
// as a generic "exception invoking 'NotifyChange'" in the browser console. Turning it
// on in Development surfaces the real .NET exception and stack trace there instead.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

// Raise the circuit's message size so admins can upload step images/video
// (InputFile streams the bytes over this same SignalR connection).
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 200 * 1024 * 1024; // 200 MB
});

// A circuit (one open browser tab) whose connection drops — closed laptop lid, flaky
// LAN, navigating away without a clean disconnect — is kept alive by default for 3
// minutes in case the browser reconnects, holding onto its scoped services (DbContext
// factory usage, the article editor's 20s autosave loop, etc.) the whole time. Cutting
// that to 1 minute means abandoned tabs let go of their DB-backed state sooner instead
// of piling up if several admins leave editor tabs open.
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);
});

// SQL Server database. Connection string is configurable via appsettings.json
// ("ConnectionStrings:Default") or an environment variable (ConnectionStrings__Default) —
// point it at LocalDB for dev, or a real SQL Server instance/Azure SQL when deployed.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Server=ZKPAYROLL-KYM\\SQLEXPRESS;Database=FaqCms;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True";
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        // Automatically retries a handful of known-transient SQL Server failures —
        // a deadlock victim (error 1205), a dropped connection, the server briefly
        // unreachable — instead of surfacing them as a user-facing error. Safe here
        // because nothing in this app opens its own manual transactions.
        sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        // Matches what was already the EF Core default (30s), made explicit so it's
        // obvious this is a deliberate choice, not an oversight, if it ever needs tuning.
        sql.CommandTimeout(30);
    }));

builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SchemaMaintenanceService>();
// Powers the shared "Are you sure?" confirmation modal (Components/Shared/ConfirmDialog.razor)
// used before every destructive action in the admin panel.
builder.Services.AddScoped<ConfirmService>();

// Cookie auth guarding the /admin CMS area. Credentials are read from config
// (see "AdminAuth" in appsettings.json) so they can be changed per-environment
// without touching code — e.g. via environment variables when deployed:
//   AdminAuth__Username=youradmin  AdminAuth__Password=your-strong-password
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "FaqCms.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // This is an internal LAN app: the office PC runs it over plain HTTP and
        // coworkers reach it from other PCs via http://<server-ip>:<port>, with no TLS
        // certificate involved. CookieSecurePolicy.Always (the previous setting outside
        // Development) marks the auth cookie "Secure", which browsers silently refuse to
        // store or send back over a non-HTTPS connection — login would succeed (the
        // cookie gets issued) but the very next request had no cookie to prove it, so
        // every other PC bounced straight back to the login page. SameAsRequest matches
        // the cookie's Secure flag to however the request actually arrived, so it works
        // the same over LAN HTTP as it does over HTTPS if you later put this behind a
        // reverse proxy with a real certificate.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Data Protection encrypts the auth cookie. Without this, its keys default to a
// location that isn't guaranteed to survive every hosting setup (e.g. a Windows
// service account without a loaded profile, a container, or certain IIS app pool
// configurations) — if the keys change, every signed-in cookie instantly becomes
// invalid and everyone gets bounced back to the login page, indistinguishable from
// the login itself being broken. Pinning a fixed folder next to the app guarantees
// the same keys survive every restart/redeploy, on this LAN test box and once this
// moves to a real host.
builder.Services.AddDataProtection()
    .SetApplicationName("FaqCms")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

var app = builder.Build();

// Trust reverse-proxy headers (IIS/Nginx/Azure/most hosts sit in front of the app
// when deployed online) so HTTPS detection and redirects work correctly behind them.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Apply migrations on startup (creates DB on first run, updates schema on subsequent runs)
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    // Nothing in this app serves a single request until this block finishes — before
    // this change, if SQL Server was slow, unreachable, or a query here was blocked
    // waiting on a lock, this would wait forever with no error and no timeout. Every
    // request would queue behind a process that could never finish starting, and the
    // log would just go silent — indistinguishable from any other hang, and the only
    // way out was a manual reset. Capping it means that instead of an invisible hang,
    // you get a clear, timestamped log line saying exactly what was still stuck, and
    // the process exits so IIS/ANCM's own restart handling can take over cleanly.
    using var startupCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    try
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = dbFactory.CreateDbContext();
        await db.Database.MigrateAsync(startupCts.Token);

        // Seed the very first admin account from config, only if no admins exist yet.
        // Once it exists, manage accounts from /admin/users — the config values are
        // just a bootstrap, not the ongoing source of truth.
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        var seedUsername = app.Configuration["AdminAuth:Username"] ?? "admin";
        var seedPassword = app.Configuration["AdminAuth:Password"] ?? "admin";
        await userService.EnsureSeedAdminAsync(seedUsername, seedPassword).WaitAsync(startupCts.Token);

        // Create the role/permission matrix table and its default rows if this is
        // the first run against this database (see PermissionService for the table).
        var permissionService = scope.ServiceProvider.GetRequiredService<PermissionService>();
        await permissionService.EnsureSchemaAndDefaultsAsync().WaitAsync(startupCts.Token);

        // Create the Helpdesk dashboard settings table and its one default row if
        // this is the first run against this database (see SettingsService).
        var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
        await settingsService.EnsureSchemaAndDefaultsAsync().WaitAsync(startupCts.Token);

        // Adds the IsDeleted/DeletedAt/DeletedBy columns backing the Recycle Bin to
        // Categories, Tags and Articles if this database predates that feature.
        var schemaMaintenanceService = scope.ServiceProvider.GetRequiredService<SchemaMaintenanceService>();
        await schemaMaintenanceService.EnsureRecycleBinSchemaAsync().WaitAsync(startupCts.Token);
        await schemaMaintenanceService.EnsureSubCategorySchemaAsync().WaitAsync(startupCts.Token);
        await schemaMaintenanceService.EnsureRelatedArticlesSchemaAsync().WaitAsync(startupCts.Token);
    }
    catch (OperationCanceledException) when (startupCts.IsCancellationRequested)
    {
        startupLogger.LogCritical(
            "Startup database initialization did not finish within 90 seconds. SQL Server is likely slow, unreachable, or a query is blocked waiting on a lock held by something else. Exiting instead of hanging indefinitely.");
        throw;
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Startup database initialization failed.");
        throw;
    }

    // NOTE: there used to be a second, hardcoded "Admin123/Admin123" account here
    // that was recreated on every startup and could never be deleted from
    // /admin/users. That was a live backdoor (well-known, fixed credentials that
    // can log in to any deployment of this app). It has been removed.
    //
    // Lockout protection is still in place without needing a backdoor:
    // UserService.DeleteAsync refuses to delete the last remaining admin account,
    // so there's always at least one working login. If you ever do get locked out
    // (e.g. lost the only password), reset it directly in the database, or run the
    // app once with AdminAuth:Username / AdminAuth:Password set to values you know
    // against an empty AdminUsers table to reseed the first account.
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Uploaded photos/videos are saved under a random GUID filename that's never reused
// or overwritten (SaveUploadAsync always mints a new name) — so once a browser has
// one, it's safe to cache "forever". Without this, every visit to an article re-downloads
// every step photo from scratch, which is the single biggest cause of "photos take a
// long time to load": the browser has no reason to believe it can reuse yesterday's copy.
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.PhysicalPath?.Contains(Path.Combine("wwwroot", "uploads"), StringComparison.OrdinalIgnoreCase) == true
            || ctx.Context.Request.Path.StartsWithSegments("/uploads"))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=31536000,immutable";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Plain (non-Blazor) endpoints for signing in/out, since setting an auth cookie
// requires a real HTTP response — a Blazor circuit can't do that mid-render.
// These live at /api/admin/... (not /admin/login) so they don't collide with the
// Login.razor page, which is also routed at /admin/login.
app.MapPost("/api/admin/login", async (HttpContext http, UserService userService, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("AdminLogin");
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    // Plain login (no deep link) now lands on the "Welcome to the Helpdesk"
    // page first, same as a guest, rather than dropping straight into the
    // articles table. A deep link — e.g. an admin bookmarked /admin/tags
    // and got bounced here to sign in first — still returns them there.
    if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
    {
        returnUrl = "/helpdesk";
    }

    var clientIp = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var usernameKey = $"user:{username.Trim().ToLowerInvariant()}";
    var ipKey = $"ip:{clientIp}";

    // Brute-force protection: without this, the login form had no limit at all on
    // repeated guesses — a script could try passwords as fast as the network allowed.
    // Locking out by username blocks credential-guessing against one account; locking
    // out by IP on top of that also slows someone rotating through many usernames from
    // the same machine. This is in-memory (see LoginThrottle below), which is the right
    // tradeoff for this app's single-instance deployment — it resets on restart and
    // isn't shared across instances, but needs no extra infrastructure to be useful.
    var isUserLocked = LoginThrottle.IsLocked(usernameKey, out var retryAfterUser);
    var isIpLocked = LoginThrottle.IsLocked(ipKey, out var retryAfterIp);
    if (isUserLocked || isIpLocked)
    {
        var retryAfter = retryAfterUser > retryAfterIp ? retryAfterUser : retryAfterIp;
        logger.LogWarning("Login attempt for {Username} from {Ip} blocked by throttle for another {Seconds}s.",
            username, clientIp, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return Results.Redirect($"/admin/login?error=3&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    // If the database is slow/unreachable, ValidateCredentialsAsync could otherwise
    // hang the request indefinitely — the page would look "stuck" with no error and
    // no way to tell why. Cap it so the person always gets a clear answer within a
    // few seconds, one way or the other.
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    bool valid;
    try
    {
        valid = await userService.ValidateCredentialsAsync(username, password).WaitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogError("Login timed out validating credentials for {Username} — the database may be slow or unreachable.", username);
        return Results.Redirect($"/admin/login?error=2&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login failed unexpectedly for {Username}.", username);
        return Results.Redirect($"/admin/login?error=2&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    if (valid)
    {
        LoginThrottle.RecordSuccess(usernameKey);
        LoginThrottle.RecordSuccess(ipKey);

        try
        {
            await userService.RecordLoginAsync(username,
                clientIp,
                http.Request.Headers.UserAgent.ToString()).WaitAsync(cts.Token);
        }
        catch (Exception ex)
        {
            // Non-fatal: don't block the person from getting in just because the
            // "last login" bookkeeping failed to write.
            logger.LogWarning(ex, "RecordLoginAsync failed for {Username}, continuing sign-in anyway.", username);
        }

        // Get user role for claims
        FaqCms.Models.AdminUser? user;
        try
        {
            user = await userService.GetUserByUsernameAsync(username).WaitAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed unexpectedly for {Username} while loading role.", username);
            return Results.Redirect($"/admin/login?error=2&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
        var role = user?.Role ?? "Viewer";
        
        var claims = new List<Claim> 
        { 
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Redirect(returnUrl);
    }

    LoginThrottle.RecordFailure(usernameKey);
    LoginThrottle.RecordFailure(ipKey);

    return Results.Redirect($"/admin/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
}).DisableAntiforgery();

app.MapPost("/api/admin/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>Simple in-memory brute-force guard for /api/admin/login. Tracks failed attempts
/// per key (a username or an IP, both are used — see the login endpoint above) and imposes a
/// temporary lockout once a threshold is hit. Deliberately minimal: no external cache/store,
/// which is the right tradeoff for this app's single-instance deployment. A restart clears all
/// state, and a multi-instance/load-balanced deployment would need a shared store (e.g. Redis)
/// instead for this to be effective — call that out if this app is ever scaled out that way.
public static class LoginThrottle
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private sealed class Attempt
    {
        public int Count;
        public DateTime LockedUntilUtc;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Attempt> _attempts = new();

    public static bool IsLocked(string key, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (_attempts.TryGetValue(key, out var attempt))
        {
            lock (attempt)
            {
                var remaining = attempt.LockedUntilUtc - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    retryAfter = remaining;
                    return true;
                }
            }
        }
        return false;
    }

    public static void RecordFailure(string key)
    {
        var attempt = _attempts.GetOrAdd(key, _ => new Attempt());
        lock (attempt)
        {
            // A lockout that already expired starts counting fresh rather than
            // immediately re-triggering on the next single failed attempt.
            if (attempt.LockedUntilUtc <= DateTime.UtcNow)
            {
                attempt.Count++;
            }
            if (attempt.Count >= MaxAttempts)
            {
                attempt.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                attempt.Count = 0;
            }
        }
    }

    public static void RecordSuccess(string key)
    {
        _attempts.TryRemove(key, out _);
    }
}
