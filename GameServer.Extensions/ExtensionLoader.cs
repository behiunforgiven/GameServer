using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using GameServer.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace GameServer.Extensions
{
    public class ExtensionLoader
    {
        private readonly ILogger<ExtensionLoader> _logger;
        private readonly Dictionary<string, IGameExtension> _extensions = new Dictionary<string, IGameExtension>();
        private readonly string _extensionsPath;

        public ExtensionLoader(ILogger<ExtensionLoader> logger, string extensionsPath)
        {
            _logger = logger;
            _extensionsPath = extensionsPath;
        }

        public Dictionary<string, IGameExtension> LoadExtensions()
        {
            _logger.LogInformation("Loading game extensions from: {Path}", _extensionsPath);
            
            if (!Directory.Exists(_extensionsPath))
            {
                _logger.LogWarning("Extensions directory not found: {Path}", _extensionsPath);
                Directory.CreateDirectory(_extensionsPath);
                return _extensions;
            }

            foreach (var dllPath in Directory.GetFiles(_extensionsPath, "*.dll"))
            {
                try
                {
                    LoadExtensionFromFile(dllPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load extension from {Path}", dllPath);
                }
            }

            _logger.LogInformation("Loaded {Count} game extensions", _extensions.Count);
            return _extensions;
        }

        private void LoadExtensionFromFile(string dllPath)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(dllPath);
            _logger.LogDebug("Loading extension: {Name}", assemblyName);

            var loadContext = new AssemblyLoadContext(assemblyName, true);
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
            var assembly = loadContext.LoadFromStream(stream);

            var extensionTypes = assembly.GetTypes()
                .Where(t => typeof(IGameExtension).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var extensionType in extensionTypes)
            {
                try
                {
                    var extension = (IGameExtension)Activator.CreateInstance(extensionType);
                    _extensions[extension.GameType] = extension;
                    _logger.LogInformation("Loaded game extension: {GameType} from {Assembly}", 
                        extension.GameType, assemblyName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to instantiate extension type: {Type}", extensionType.FullName);
                }
            }
        }

        public IGameExtension GetExtension(string gameType)
        {
            if (_extensions.TryGetValue(gameType, out var extension))
            {
                return extension;
            }
            
            _logger.LogWarning("Extension not found for game type: {GameType}", gameType);
            return null;
        }
    }
}