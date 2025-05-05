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
    public class ExtensionLoader(ILogger<ExtensionLoader> logger, string extensionsPath)
    {
        private readonly Dictionary<string, IGameExtension> _extensions = new();

        public Dictionary<string, IGameExtension> LoadExtensions()
        {
            logger.LogInformation("Loading game extensions from: {Path}", extensionsPath);
            
            if (!Directory.Exists(extensionsPath))
            {
                logger.LogWarning("Extensions directory not found: {Path}", extensionsPath);
                Directory.CreateDirectory(extensionsPath);
                return _extensions;
            }

            foreach (var dllPath in Directory.GetFiles(extensionsPath, "*.dll"))
            {
                try
                {
                    LoadExtensionFromFile(dllPath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load extension from {Path}", dllPath);
                }
            }

            logger.LogInformation("Loaded {Count} game extensions", _extensions.Count);
            return _extensions;
        }

        private void LoadExtensionFromFile(string dllPath)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(dllPath);
            logger.LogDebug("Loading extension: {Name}", assemblyName);

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
                    logger.LogInformation("Loaded game extension: {GameType} from {Assembly}", 
                        extension.GameType, assemblyName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to instantiate extension type: {Type}", extensionType.FullName);
                }
            }
        }

        public IGameExtension GetExtension(string gameType)
        {
            if (_extensions.TryGetValue(gameType, out var extension))
            {
                return extension;
            }
            
            logger.LogWarning("Extension not found for game type: {GameType}", gameType);
            return null;
        }
    }
}