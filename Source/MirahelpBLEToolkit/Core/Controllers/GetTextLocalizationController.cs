using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Interfaces;
using NGettext;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public sealed class GetTextLocalizationController : ILocalizationControllerService
    {
        private readonly ConcurrentDictionary<String, ICatalog> catalogCache;

        public GetTextLocalizationController()
        {
            this.catalogCache = new ConcurrentDictionary<String, ICatalog>(StringComparer.Ordinal);
        }

        public String GetText(String domain, String key, String locale)
        {
            if (String.IsNullOrEmpty(key))
            {
                return String.Empty;
            }

            String effectiveDomain = String.IsNullOrWhiteSpace(domain) ? AppStrings.LocalizationDefaultDomain : domain;

            ICatalog catalog = GetOrCreateCatalog(effectiveDomain);

            String translatedValue = catalog.GetString(key);
            if (String.IsNullOrEmpty(translatedValue))
            {
                return key;
            }

            return translatedValue;
        }

        public String GetText(String key)
        {
            return GetText(AppStrings.LocalizationDefaultDomain, key, AppStrings.LocalizationLocaleEnUs);
        }

        private ICatalog GetOrCreateCatalog(String domain)
        {
            CultureInfo systemUserInterfaceCultureInfo = CultureInfo.CurrentUICulture;
            CultureInfo effectiveCultureInfo = ResolveEffectiveCultureInfo(domain, systemUserInterfaceCultureInfo);

            String cacheKey = domain + "|" + effectiveCultureInfo.Name;

            ICatalog catalog;
            if (!this.catalogCache.TryGetValue(cacheKey, out catalog))
            {
                catalog = CreateCatalog(domain, effectiveCultureInfo);
                this.catalogCache[cacheKey] = catalog;
            }

            return catalog;
        }

        private CultureInfo ResolveEffectiveCultureInfo(String domain, CultureInfo requestedCultureInfo)
        {
            String normalizedRequestedFolderName = ConvertCultureInfoToUnderscoreName(requestedCultureInfo);
            String requestedMoFilePath = BuildMoFilePath(domain, normalizedRequestedFolderName);

            if (File.Exists(requestedMoFilePath))
            {
                return requestedCultureInfo;
            }

            CultureInfo fallbackCultureInfo = CreateCultureInfoFromLocale(AppStrings.LocalizationLocaleEnUs);
            String normalizedFallbackFolderName = ConvertCultureInfoToUnderscoreName(fallbackCultureInfo);
            String fallbackMoFilePath = BuildMoFilePath(domain, normalizedFallbackFolderName);

            if (File.Exists(fallbackMoFilePath))
            {
                return fallbackCultureInfo;
            }

            return requestedCultureInfo;
        }

        private ICatalog CreateCatalog(String domain, CultureInfo cultureInfo)
        {
            String localesRootDirectoryPath = Path.Combine(AppContext.BaseDirectory, AppStrings.LocalizationFolderRoot);
            ICatalog catalog = new Catalog(domain, localesRootDirectoryPath, cultureInfo);
            return catalog;
        }

        private CultureInfo CreateCultureInfoFromLocale(String localeToken)
        {
            String cultureName = localeToken.Replace('_', '-');
            CultureInfo cultureInfo = new(cultureName);
            return cultureInfo;
        }

        private String ConvertCultureInfoToUnderscoreName(CultureInfo cultureInfo)
        {
            String underscoreName = cultureInfo.Name.Replace('-', '_');
            return underscoreName;
        }

        private String BuildMoFilePath(String domain, String normalizedLocaleFolderName)
        {
            String localesRootDirectoryPath = Path.Combine(AppContext.BaseDirectory, AppStrings.LocalizationFolderRoot);
            String moFileName = domain + ".mo";
            String moFilePath = Path.Combine(localesRootDirectoryPath, normalizedLocaleFolderName, AppStrings.LocalizationMessagesFolder, moFileName);
            return moFilePath;
        }
    }
}