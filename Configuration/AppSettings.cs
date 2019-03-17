using Microsoft.Extensions.Configuration;

namespace KLabSkill.Configuration
{
    public class AppSettings
    {
        public string MeetupApiToken { get; }

        readonly IConfigurationRoot config;

        public AppSettings()
        {
            config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            MeetupApiToken = config.GetValue<string>("MeetupApiToken");
        }

        public string GetValue(string key) => config.GetValue<string>(key);
    }
}
