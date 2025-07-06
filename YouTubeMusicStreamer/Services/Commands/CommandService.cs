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

using System.Reflection;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Enums;
using YouTubeMusicStreamer.Extensions;
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Models;
using YouTubeMusicStreamer.Services.App;

namespace YouTubeMusicStreamer.Services.Commands;

public class CommandService
{
    private readonly SettingsService _settingsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ICommand> _commands = new();
    private readonly Dictionary<string, string> _descriptions = new();

    public CommandService(SettingsService settingsService, IServiceProvider serviceProvider)
    {
        _settingsService = settingsService;
        _serviceProvider = serviceProvider;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var appSettings = _settingsService.GetAppSettings();

        var commandTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<CommandAttribute>() is not null && typeof(ICommand).IsAssignableFrom(t));

        foreach (var type in commandTypes)
        {
            var attribute = type.GetCustomAttribute<CommandAttribute>();
            if (attribute is null) continue;

            if (!appSettings.Commands.TryGetValue(type.Name, out var settings))
            {
                settings = new CommandSettings
                {
                    Trigger = type.Name.Replace("Command", string.Empty).ToLower(),
                    IsEnabled = attribute.DefaultEnabled,
                    Cooldown = attribute.DefaultCooldown,
                    Response = attribute.DefaultResponse,
                    RequiredBits = attribute.DefaultRequiredBits
                };
                appSettings.Commands[type.Name] = settings;
            }

            _descriptions[settings.Trigger] = attribute.Description;

            if (ActivatorUtilities.CreateInstance(_serviceProvider, type) is ICommand commandInstance) _commands[settings.Trigger] = commandInstance;
        }

        await _settingsService.SaveAppSettingAsync(app => app.Commands = appSettings.Commands);
    }


    private async Task<CommandResult> ExecuteCommandAsync(string trigger, ChannelChatMessage message)
    {
        if (!_commands.TryGetValue(trigger, out var command)) return CommandResult.NotFound;

        try
        {
            return await command.ExecuteAsync(message) ? CommandResult.Success : CommandResult.Failed;
        }
        catch (Exception)
        {
            return CommandResult.Failed;
        }
    }

    public IEnumerable<(string Trigger, string Description, bool IsEnabled)> ListCommands() =>
        _settingsService.GetAppSettings().Commands.Select(c => (c.Value.Trigger, _descriptions[c.Value.Trigger], c.Value.IsEnabled));

    public void ProcessInput(ChannelChatMessage message, Action<CommandResult>? callback = null)
    {
        var commandPrefix = _settingsService.GetAppSettings().TwitchCommandPrefix;

        var hasPrefix = false;
        var trigger = "";

        if (!string.IsNullOrEmpty(message.ChannelPointsCustomRewardId))
        {
            var rewardId = message.ChannelPointsCustomRewardId;
            trigger = GetTriggerByRewardId(rewardId);
            if (string.IsNullOrEmpty(trigger)) return;

            hasPrefix = true;
        }

        switch (hasPrefix)
        {
            case false when string.IsNullOrEmpty(message.Message.Text):
                return;
            case false:
            {
                foreach (var fragment in message.Message.Fragments.Where(f => f.Type == "text"))
                {
                    var text = fragment.Text.Trim();
                    if (!text.StartsWith(commandPrefix)) continue;

                    hasPrefix = true;
                    trigger = text[commandPrefix.Length..].Trim().Split(' ')[0].ToLower();
                    break;
                }

                break;
            }
        }

        if (!hasPrefix) return;

        ExecuteCommandAsync(trigger, message).FireAndAfter(result => callback?.Invoke(result));
    }

    public void ProcessInput(ChannelPointsCustomRewardRedemption reward, Action<CommandResult>? callback = null)
    {
        var trigger = GetTriggerByRewardId(reward.Reward.Id);

        if (string.IsNullOrEmpty(trigger)) return;

        ExecuteCommandAsync(trigger, new ChannelChatMessage
        {
            ChannelPointsCustomRewardId = reward.Reward.Id,
            BroadcasterUserId = reward.BroadcasterUserId,
            BroadcasterUserName = reward.BroadcasterUserName,
            BroadcasterUserLogin = reward.BroadcasterUserLogin,
            ChatterUserId = reward.UserId,
            ChatterUserName = reward.UserName,
            ChatterUserLogin = reward.UserLogin
        }).FireAndAfter(result => callback?.Invoke(result));
    }

    public IEnumerable<CommandInformation> GetAllCommandsInfo()
    {
        var appSettings = _settingsService.GetAppSettings();

        return _commands.Select(cmd =>
        {
            var commandType = cmd.Value.GetType();
            var commandAttribute = commandType.GetCustomAttribute<CommandAttribute>();
            var settings = appSettings.Commands.GetValueOrDefault(commandType.Name);
            var placeholders = cmd.Value.GetAvailablePlaceholders();

            if (commandAttribute is null || settings is null) return null;

            return new CommandInformation(_settingsService, commandType.Name, commandAttribute, settings, placeholders);
        }).Where(c => c is not null).Cast<CommandInformation>().OrderBy(i => i.Name);
    }

    private string GetTriggerByRewardId(string rewardId)
    {
        try
        {
            return _settingsService.GetAppSettings().Commands.FirstOrDefault(c => c.Value.RewardId == rewardId).Value.Trigger;
        }
        catch
        {
            // ignored
        }
        
        return string.Empty;
    }
}