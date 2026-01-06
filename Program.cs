using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.Extensions;
using Ticketing.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .WriteTo.Console();

    var seqUrl = ctx.Configuration["Seq:ServerUrl"];

    if (Uri.TryCreate(seqUrl, UriKind.Absolute, out _))
    {
        lc.WriteTo.Seq(seqUrl!);
    }
});

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")))
        throw new InvalidOperationException("ConnectionStrings:Default is required in production.");

    if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
        throw new InvalidOperationException("Jwt:Key is required in production.");
}


builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.Password.RequiredLength = 8;
        opt.Password.RequireDigit = true;
        opt.Password.RequireUppercase = true;
        opt.Password.RequireLowercase = true;
        opt.Password.RequireNonAlphanumeric = true;

        opt.Lockout.AllowedForNewUsers = true;
        opt.Lockout.MaxFailedAccessAttempts = 5;
        opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                //TODO : Consider security implications of reading token from cookie or from header for production use!
                //if (!string.IsNullOrEmpty(context.Request.Headers["Authorization"]))
                //{
                //    return Task.CompletedTask;
                //}

                var token = context.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ITicketsService, TicketsService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token in the text input below.\r\n\r\nExample: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

{
    string? envSeqUrl = Environment.GetEnvironmentVariable("Seq__ServerUrl");
    string? envJwtKey = Environment.GetEnvironmentVariable("Jwt__Key");
    string? envSendGridKey = Environment.GetEnvironmentVariable("SendGridSettings__ApiKey");

    var cfgSeqUrl = app.Configuration["Seq:ServerUrl"];
    var cfgJwtKeyPresent = !string.IsNullOrWhiteSpace(app.Configuration["Jwt:Key"]);
    var cfgSendGridPresent = !string.IsNullOrWhiteSpace(app.Configuration["SendGridSettings:ApiKey"]);
    var cfgConnPresent = !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default"));

    string seqHost = "(missing/invalid)";
    if (Uri.TryCreate(cfgSeqUrl, UriKind.Absolute, out var seqUri))
        seqHost = seqUri.Host;

    bool seqFromEnv = !string.IsNullOrWhiteSpace(envSeqUrl) && string.Equals(envSeqUrl, cfgSeqUrl, StringComparison.Ordinal);
    bool jwtFromEnv = !string.IsNullOrWhiteSpace(envJwtKey); // don't compare values; just prove it exists in env
    bool sendGridFromEnv = !string.IsNullOrWhiteSpace(envSendGridKey);

    bool isAppService = !string.IsNullOrWhiteSpace(app.Configuration["WEBSITE_INSTANCE_ID"]);

    app.Logger.LogInformation(
        "CONFIG CHECK: Env={Env} IsAppService={IsAppService} SeqHost={SeqHost} SeqConfigured={SeqConfigured} SeqFromEnv={SeqFromEnv} JwtKeyPresent={JwtKeyPresent} JwtFromEnv={JwtFromEnv} SendGridKeyPresent={SendGridKeyPresent} SendGridFromEnv={SendGridFromEnv} ConnStringPresent={ConnStringPresent}",
        app.Environment.EnvironmentName,
        isAppService,
        seqHost,
        !string.IsNullOrWhiteSpace(cfgSeqUrl),
        seqFromEnv,
        cfgJwtKeyPresent,
        jwtFromEnv,
        cfgSendGridPresent,
        sendGridFromEnv,
        cfgConnPresent
    );
}

await app.ApplyMigrationsAsync(app.Environment);

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("DevCors"); 

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


if (app.Environment.IsDevelopment())
{
    await app.SeedAsync();
}

app.Run();
