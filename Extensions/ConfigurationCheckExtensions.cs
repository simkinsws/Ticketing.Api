using System.Diagnostics.CodeAnalysis;

namespace Ticketing.Api.Extensions;

public static class ConfigurationCheckExtensions
{
    public static void LogConfigurationCheck(this WebApplication app)
    {
        string? envSeqUrl = Environment.GetEnvironmentVariable("Seq__ServerUrl");
        string? envJwtKey = Environment.GetEnvironmentVariable("Jwt__Key");
        string? envSendGridKey = Environment.GetEnvironmentVariable("SendGridSettings__ApiKey");
        string? envBaseUrl = Environment.GetEnvironmentVariable("EmailConfirmation__BaseUrl");
        string? envSeqEnabled = Environment.GetEnvironmentVariable("Seq__Enabled");

        var cfgSeqUrl = app.Configuration["Seq:ServerUrl"];
        var cfgSeqEnabled = app.Configuration.GetValue<bool>("Seq:Enabled", false);
        var cfgJwtKeyPresent = !string.IsNullOrWhiteSpace(app.Configuration["Jwt:Key"]);
        var cfgSendGridPresent = !string.IsNullOrWhiteSpace(app.Configuration["SendGridSettings:ApiKey"]);
        var cfgConnPresent = !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default"));
        var cfgBaseUrl = app.Configuration["EmailConfirmation:BaseUrl"];

        string seqHost = "(missing/invalid)";
        if (Uri.TryCreate(cfgSeqUrl, UriKind.Absolute, out var seqUri))
            seqHost = seqUri.Host;

        bool seqFromEnv = !string.IsNullOrWhiteSpace(envSeqUrl) && string.Equals(envSeqUrl, cfgSeqUrl, StringComparison.Ordinal);
        bool jwtFromEnv = !string.IsNullOrWhiteSpace(envJwtKey); // don't compare values; just prove it exists in env
        bool sendGridFromEnv = !string.IsNullOrWhiteSpace(envSendGridKey);
        bool baseUrlFromEnv = !string.IsNullOrWhiteSpace(envBaseUrl) && string.Equals(envBaseUrl, cfgBaseUrl, StringComparison.Ordinal);

        bool isAppService = !string.IsNullOrWhiteSpace(app.Configuration["WEBSITE_INSTANCE_ID"]);

        app.Logger.LogInformation(
            "CONFIG CHECK: Env={Env} IsAppService={IsAppService} SeqAppSettingsEnabled={SeqEnabled} SeqEnvEnabled={SeqProdEnvEnabled} SeqHost={SeqHost} SeqConfigured={SeqConfigured} SeqFromEnv={SeqFromEnv} JwtKeyPresent={JwtKeyPresent} JwtFromEnv={JwtFromEnv} SendGridKeyPresent={SendGridKeyPresent} SendGridFromEnv={SendGridFromEnv} ConnStringPresent={ConnStringPresent} BaseUrl={BaseUrl} BaseUrlFromEnv={BaseUrlFromEnv}",
            app.Environment.EnvironmentName,
            isAppService,
            cfgSeqEnabled,
            envSeqEnabled,
            seqHost,
            !string.IsNullOrWhiteSpace(cfgSeqUrl),
            seqFromEnv,
            cfgJwtKeyPresent,
            jwtFromEnv,
            cfgSendGridPresent,
            sendGridFromEnv,
            cfgConnPresent,
            cfgBaseUrl ?? "(not configured)",
            baseUrlFromEnv
        );
    }
}
