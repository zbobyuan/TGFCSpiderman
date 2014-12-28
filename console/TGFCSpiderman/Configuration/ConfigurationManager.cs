using System;
using System.Configuration;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    class ConfigurationManager
    {
        private const string SectionName = "tgfc";
        private readonly System.Configuration.Configuration _config;

        public ConfigurationManager()
        {
#if DEBUG
            var applicationName = Environment.GetCommandLineArgs()[0];
#else 
            var applicationName = Environment.GetCommandLineArgs()[0]+ ".exe";
#endif

            var exePath = System.IO.Path.Combine(Environment.CurrentDirectory, applicationName);
            _config = System.Configuration.ConfigurationManager.OpenExeConfiguration(exePath);
        }

        public IPageFetcherConfig GetPageFetcherConfig()
        {
            var section = (TGFCSpidermanSection)_config.GetSection(SectionName);
            return section != null ? section.PageFetcherElement : new PageFetcherConfigElement(3, 20, "Opera/9.80 (Windows NT 5.1) Presto/2.12.388 Version/12.16");
        }

        public void SavePageFetcherConfig(IPageFetcherConfig c)
        {
            var configSection = (TGFCSpidermanSection)_config.GetSection(SectionName);
            if (configSection == null)
            {
                configSection = new TGFCSpidermanSection
                {
                    PageFetcherElement =
                    {
                        SigninRetryTimes = c.SigninRetryTimes,
                        TimeoutInSeconds = c.TimeoutInSeconds,
                        UserAgent = c.UserAgent
                    }
                };
                _config.Sections.Add(SectionName, configSection);
            }
            else
            {
                configSection.PageFetcherElement.SigninRetryTimes = c.SigninRetryTimes;
                configSection.PageFetcherElement.TimeoutInSeconds = c.TimeoutInSeconds;
                configSection.PageFetcherElement.UserAgent = c.UserAgent;
            }

            _config.Save(ConfigurationSaveMode.Modified);
            System.Configuration.ConfigurationManager.RefreshSection(SectionName);
        }
    }
}
