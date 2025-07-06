// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version (the "AGPLv3").
// 
// YouTubeMusicStreamer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// For full license text, see the LICENSE file in the project’s root directory.
// 
// You should have received a copy of the GNU Affero General Public License
// along with YouTubeMusicStreamer. If not, see <https://www.gnu.org/licenses/>.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Utils;

namespace YouTubeMusicStreamer.Services.App;

public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _appSettingsFilePath;

    private readonly AppSettings _appSettings;
    private readonly SensitiveSettings _secureData;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /**
     * Method to get the settings
     */
    public AppSettings GetAppSettings() => _appSettings;

    public event Action<AppSettings> AppSettingsChanged = delegate { };

    /**
     * Method to get the sensitive settings
     */
    public SensitiveSettings GetSensitiveSettings() => _secureData;

    /**
     * Constructor to initialize the settings service
     */
    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _appSettingsFilePath = Path.Combine(AppUtils.FilePath, "AppSettings.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_appSettingsFilePath)!);

        // Load settings at startup
        _appSettings = LoadAppSettings();
        _secureData = LoadSensitiveSettings();
    }

    /**
     * Method to save a specific setting for regular settings
     */
    public async Task SaveAppSettingAsync(Action<AppSettings> update)
    {
        update(_appSettings);
        await SaveAppSettingsToFileAsync();
        AppSettingsChanged(_appSettings);
    }

    public async Task SaveSensitiveSettingAsync(Action<SensitiveSettings> update)
    {
        update(_secureData);
        await SaveSensitiveSettingsToFileAsync();
    }

    /**
     * Helper method to save regular settings to JSON
     */
    private async Task SaveAppSettingsToFileAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_appSettings, _jsonSerializerOptions);
            await File.WriteAllTextAsync(_appSettingsFilePath, json);
        }
        catch
        {
            _logger.LogError("Failed to save app settings");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveSensitiveSettingsToFileAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            foreach (var property in typeof(SensitiveSettings).GetProperties())
            {
                var value = property.GetValue(_secureData);
                if (value is null)
                {
                    SecureStorage.Default.Remove(property.Name);
                    continue;
                }

                var json = JsonSerializer.Serialize(value, _jsonSerializerOptions);
                await SecureStorage.Default.SetAsync(property.Name, json);
            }
        }
        catch
        {
            _logger.LogError("Failed to save sensitive data");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /**
     * Helper method to load regular settings from JSON
     */
    private AppSettings LoadAppSettings()
    {
        if (!File.Exists(_appSettingsFilePath))
        {
            _logger.LogWarning("App settings file does not exist. Creating new settings file");
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_appSettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            _logger.LogError("Failed to load app settings");
        }

        return new AppSettings();
    }

    private SensitiveSettings LoadSensitiveSettings()
    {
        try
        {
            var sensitiveSettings = new SensitiveSettings();
            foreach (var property in typeof(SensitiveSettings).GetProperties())
            {
                var json = SecureStorage.Default.GetAsync(property.Name).Result;
                if (json is null) continue;

                var value = JsonSerializer.Deserialize(json, property.PropertyType, _jsonSerializerOptions);
                property.SetValue(sensitiveSettings, value);
            }

            return sensitiveSettings;
        }
        catch
        {
            _logger.LogError("Failed to load sensitive data");
        }

        return new SensitiveSettings();
    }
}