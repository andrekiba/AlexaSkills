using Microsoft.Extensions.Configuration;

namespace KLabSkill.Configuration
{
    public static class AppSettings
    {
        public static string MeetupApiToken { get; }

        static readonly IConfigurationRoot config;

        static AppSettings()
        {
            config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            MeetupApiToken = config.GetValue<string>("MeetupApiToken");
        }

        public static string GetValue(string key) => config.GetValue<string>(key);
    }
}
