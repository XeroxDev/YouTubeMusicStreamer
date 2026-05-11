// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2026 Dominic Ris
// 
// YouTubeMusicStreamer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
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
using Microsoft.Extensions.Logging;
using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Placeholders;

namespace YouTubeMusicStreamer.Services.Commands;

public sealed class CommandRegistry(
    SettingsService settingsService,
    IPlaceholderProvider placeholderProvider,
    ILogger<CommandRegistry> logger) : ICommandCatalog
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly Dictionary<string, CommandRegistryEntry> _entriesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _commandNamesByTrigger = new(StringComparer.OrdinalIgnoreCase);
    private Task? _initializationTask;

    public bool IsInitialized { get; private set; }

    public Task InitializeAsync() => _initializationTask ??= InitializeCoreAsync();

    public bool TryGetByName(string name, out CommandRegistryEntry entry) => _entriesByName.TryGetValue(name, out entry!);

    public bool TryGetByChatTrigger(string trigger, out CommandRegistryEntry entry)
    {
        entry = null!;
        return _commandNamesByTrigger.TryGetValue(trigger, out var commandName) &&
               _entriesByName.TryGetValue(commandName, out entry!);
    }

    public bool TryGetByRewardId(string rewardId, out CommandRegistryEntry entry)
    {
        entry = null!;
        var match = settingsService.GetCommandConfigurations()
            .FirstOrDefault(c => string.Equals(c.Value.RewardBinding?.RewardId, rewardId, StringComparison.Ordinal));

        return !string.IsNullOrWhiteSpace(match.Key) && _entriesByName.TryGetValue(match.Key, out entry!);
    }

    public IReadOnlyList<CommandDescriptor> GetCommandDescriptors()
    {
        var prefix = settingsService.GetTwitchSettings().CommandPrefix;
        var configurations = settingsService.GetCommandConfigurations();

        return _entriesByName.Values
            .Select(entry =>
            {
                var configuration = configurations.GetValueOrDefault(entry.Name);
                if (configuration is null)
                    return null;

                return CreateDescriptor(entry, configuration.Clone(), prefix);
            })
            .Where(descriptor => descriptor is not null)
            .Cast<CommandDescriptor>()
            .OrderBy(descriptor => descriptor.Name)
            .ToList();
    }

    public IReadOnlyList<string> GetEnabledCommandListing() =>
        GetCommandDescriptors()
            .Where(descriptor => descriptor.CommandConfiguration.IsEnabled)
            .Select(descriptor =>
            {
                var label = descriptor.Name.Replace("Command", string.Empty, StringComparison.Ordinal);
                var invocationText = string.Join(", ", descriptor.Invocations.Select(i => i.DisplayText));
                return $"{label} [{invocationText}]";
            })
            .ToList();

    public IEnumerable<(string Trigger, string Description, bool IsEnabled)> ListCommands() =>
        GetCommandDescriptors().Select(d => (d.CommandConfiguration.Trigger, d.Attribute.Description, d.CommandConfiguration.IsEnabled));

    public void RefreshTriggerMap()
    {
        _commandNamesByTrigger.Clear();
        foreach (var (name, configuration) in settingsService.GetCommandConfigurations())
        {
            if (_entriesByName.ContainsKey(name))
                _commandNamesByTrigger[configuration.Trigger] = name;
        }
    }

    private async Task InitializeCoreAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            if (IsInitialized)
                return;

            var commandConfigurations = settingsService.GetCommandConfigurations()
                .ToDictionary(x => x.Key, x => x.Value.Clone(), StringComparer.Ordinal);

            var commandTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => type.GetCustomAttribute<CommandAttribute>() is not null && type is { IsAbstract: false, IsClass: true })
                .OrderBy(type => type.Name, StringComparer.Ordinal);

            foreach (var type in commandTypes)
            {
                var attribute = type.GetCustomAttribute<CommandAttribute>()!;
                if (!CommandHandlerReflection.TryGetValidatedHandlerMethod(type, out var handlerMethod, out var validationError))
                {
                    logger.LogError("Invalid command contract for {CommandType}: {ValidationError}", type.Name, validationError);
                    continue;
                }

                if (!commandConfigurations.TryGetValue(type.Name, out var configuration))
                {
                    configuration = CreateDefaultConfiguration(type, attribute);
                    commandConfigurations[type.Name] = configuration;
                }

                var entry = new CommandRegistryEntry
                {
                    Name = type.Name,
                    DefaultTrigger = type.Name.Replace("Command", string.Empty, StringComparison.Ordinal).ToLowerInvariant(),
                    Attribute = attribute,
                    RequiresInput = handlerMethod!.GetParameters().Length > 1,
                    ImplementationType = type,
                    HandlerMethod = handlerMethod,
                    Placeholders = placeholderProvider.GetPlaceholders(type, handlerMethod!)
                };

                _entriesByName[type.Name] = entry;
                _commandNamesByTrigger[configuration.Trigger] = type.Name;
            }

            foreach (var configuration in commandConfigurations)
                await settingsService.SaveCommandConfigurationAsync(configuration.Key, configuration.Value);

            RefreshTriggerMap();
            IsInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static CommandConfigurationSnapshot CreateDefaultConfiguration(Type type, CommandAttribute attribute)
    {
        var configuration = new CommandConfigurationSnapshot
        {
            Trigger = type.Name.Replace("Command", string.Empty, StringComparison.Ordinal).ToLowerInvariant(),
            ChatTriggerMode = attribute.DefaultEnabled ? ChatTriggerMode.Chat : ChatTriggerMode.Disabled,
            RewardTriggerMode = RewardTriggerMode.Disabled,
            RequiredAccessLevel = attribute.DefaultAccess,
            CooldownScope = CommandCooldownScope.Global,
            BitsThreshold = attribute.DefaultRequiredBits,
            Cooldown = attribute.DefaultCooldown,
            Response = attribute.DefaultResponse
        };

        if (attribute.DefaultRequiredBits > 0)
            configuration.ChatTriggerMode = ChatTriggerMode.BitsOnly;

        return configuration;
    }

    private static CommandDescriptor CreateDescriptor(
        CommandRegistryEntry entry,
        CommandConfigurationSnapshot configuration,
        string commandPrefix)
    {
        var invocations = new List<CommandInvocationDescriptor>();
        if (configuration.ChatTriggerMode == ChatTriggerMode.Chat)
            invocations.Add(new CommandInvocationDescriptor(CommandInvocationKind.Chat, $"{commandPrefix}{configuration.Trigger}"));
        else if (configuration.ChatTriggerMode == ChatTriggerMode.BitsOnly)
            invocations.Add(new CommandInvocationDescriptor(CommandInvocationKind.Bits, $"{commandPrefix}{configuration.Trigger} ({configuration.BitsThreshold} bits)"));

        if (configuration.RewardTriggerMode != RewardTriggerMode.Disabled)
        {
            var rewardLabel = configuration.RewardBinding?.ManagedRewardName
                              ?? "linked reward";
            invocations.Add(new CommandInvocationDescriptor(CommandInvocationKind.Reward, $"reward \"{rewardLabel}\""));
        }

        return new CommandDescriptor
        {
            Name = entry.Name,
            Attribute = entry.Attribute,
            RequiresInput = entry.RequiresInput,
            CommandConfiguration = configuration,
            Placeholders = new Dictionary<string, string>(entry.Placeholders),
            Invocations = invocations
        };
    }
}
