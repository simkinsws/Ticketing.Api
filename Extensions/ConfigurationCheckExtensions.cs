using System.Diagnostics.CodeAnalysis;

namespace Ticketing.Api.Extensions;

public static class ConfigurationCheckExtensions
{
    public static void LogConfigurationCheck(this WebApplication app)
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
}
