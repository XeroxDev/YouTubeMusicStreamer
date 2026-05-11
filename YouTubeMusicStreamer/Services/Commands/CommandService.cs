// This file is part of YouTubeMusicStreamer.
// Copyright (C) 2025 Dominic Ris
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.App.Persistence;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;

namespace YouTubeMusicStreamer.Services.Commands;

public sealed class CommandService(
    SettingsService settingsService,
    CommandRegistry commandRegistry,
    IServiceProvider serviceProvider,
    ITwitchChatService twitchChatService,
    IArgumentParser argumentParser,
    IPrerequisiteChecker prerequisiteChecker,
    ICooldownManager cooldownManager,
    IArgumentBinder argumentBinder,
    IResponseFormatter responseFormatter,
    ILogger<CommandService> logger)
{
    public bool IsInitialized => commandRegistry.IsInitialized;

    public Task InitializeAsync() => commandRegistry.InitializeAsync();

    public IEnumerable<(string Trigger, string Description, bool IsEnabled)> ListCommands() =>
        commandRegistry.ListCommands();

    public IReadOnlyList<CommandDescriptor> GetAllCommandsInfo() =>
        commandRegistry.GetCommandDescriptors();

    public async Task<CommandExecutionOutcome> ProcessInputAsync(ChannelChatMessage message, CancellationToken cancellationToken = default)
    {
        if (!commandRegistry.IsInitialized)
            return CreateOutcome(string.Empty, string.Empty, CommandInvocationKind.Chat, CommandExecutionStatus.Skipped, CommandExecutionReason.NotInitialized, "Command registry is not initialized.");

        var resolved = ResolveCommand(message);
        if (resolved is null)
            return CreateOutcome(string.Empty, string.Empty, CommandInvocationKind.Chat, CommandExecutionStatus.Skipped, CommandExecutionReason.TriggerNotMatched, null);

        var (entry, invocationKind, trigger, rawInputText) = resolved.Value;
        var configuration = settingsService.GetCommandConfiguration(entry.Name);
        if (configuration is null)
            return CreateOutcome(entry.Name, trigger, invocationKind, CommandExecutionStatus.Skipped, CommandExecutionReason.InvalidConfiguration, $"No configuration found for command '{entry.Name}'.");

        var bits = GetEffectiveBits(message);
        var eligibility = prerequisiteChecker.Evaluate(message, configuration, bits, invocationKind);
        if (!eligibility.CanExecute)
        {
            if (eligibility.Status == CommandExecutionStatus.Blocked)
            {
                if (eligibility.Reason == CommandExecutionReason.AccessDenied)
                {
                    var accessDeniedMessage = configuration.AccessDeniedResponse;
                    if (!string.IsNullOrWhiteSpace(accessDeniedMessage))
                        await SendFeedbackAsync(message, responseFormatter.Format(accessDeniedMessage, new Dictionary<string, string> { ["{username}"] = message.ChatterUserName }));
                    else if (!string.IsNullOrWhiteSpace(eligibility.Message))
                        await SendFeedbackAsync(message, eligibility.Message);
                }
                else if (!string.IsNullOrWhiteSpace(eligibility.Message))
                {
                    await SendFeedbackAsync(message, eligibility.Message);
                }
            }

            return CreateOutcome(entry.Name, trigger, invocationKind, eligibility.Status, eligibility.Reason, eligibility.Message);
        }

        if (!cooldownManager.TryStart(entry.Name, message.ChatterUserId, configuration.CooldownScope, configuration.Cooldown, out var waitSeconds))
        {
            var cooldownMessage = $"Please wait {waitSeconds}s…";
            await SendFeedbackAsync(message, cooldownMessage);
            return CreateOutcome(entry.Name, trigger, invocationKind, CommandExecutionStatus.Blocked, CommandExecutionReason.CooldownActive, cooldownMessage, cooldownRemainingSeconds: waitSeconds);
        }

        using var scope = serviceProvider.CreateScope();

        try
        {
            var handler = ActivatorUtilities.CreateInstance(scope.ServiceProvider, entry.ImplementationType);
            var context = new CommandContext(
                invocationKind,
                entry.Name,
                trigger,
                configuration.Clone(),
                message,
                rawInputText,
                bits,
                message.ChannelPointsCustomRewardId,
                cancellationToken,
                twitchChatService);

            var tokens = argumentParser.Parse(message, settingsService.GetTwitchSettings().CommandPrefix.Trim(), trigger.Trim());
            var bound = await argumentBinder.BindAndInvokeAsync(handler, entry.HandlerMethod, context, tokens);
            var result = bound.Result;

            if ((result.Status == CommandExecutionStatus.Blocked || result.Status == CommandExecutionStatus.BadInput) &&
                !string.IsNullOrWhiteSpace(result.Message))
            {
                await SendFeedbackAsync(message, result.Message);
            }

            var placeholders = BuildPlaceholderValues(context, bound.ArgValues, result.Data);
            if (result.Status == CommandExecutionStatus.Fulfilled && !string.IsNullOrWhiteSpace(configuration.Response))
            {
                var text = responseFormatter.Format(configuration.Response, placeholders);
                if (!string.IsNullOrWhiteSpace(text))
                    await context.ReplyAsync(text);
            }

            return CreateOutcome(entry.Name, trigger, invocationKind, result.Status, result.Reason, result.Message, result.Data, placeholders);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command '{CommandName}' failed unexpectedly", entry.Name);
            return CreateOutcome(entry.Name, trigger, invocationKind, CommandExecutionStatus.Failed, CommandExecutionReason.UnexpectedException, "Command execution failed unexpectedly.", exception: ex);
        }
    }

    public Task<CommandExecutionOutcome> ProcessRewardInputAsync(
        string rewardId,
        string broadcasterUserId,
        string broadcasterUserName,
        string broadcasterUserLogin,
        string userId,
        string userName,
        string userLogin,
        CancellationToken cancellationToken = default) =>
        ProcessInputAsync(new ChannelChatMessage
        {
            ChannelPointsCustomRewardId = rewardId,
            BroadcasterUserId = broadcasterUserId,
            BroadcasterUserName = broadcasterUserName,
            BroadcasterUserLogin = broadcasterUserLogin,
            ChatterUserId = userId,
            ChatterUserName = userName,
            ChatterUserLogin = userLogin
        }, cancellationToken);

    public async Task SaveCommandConfigurationAsync(string commandName, CommandConfigurationSnapshot configuration)
    {
        await settingsService.SaveCommandConfigurationAsync(commandName, configuration);
        commandRegistry.RefreshTriggerMap();
    }

    private (CommandRegistryEntry Entry, CommandInvocationKind InvocationKind, string Trigger, string RawInputText)? ResolveCommand(ChannelChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.ChannelPointsCustomRewardId))
        {
            return commandRegistry.TryGetByRewardId(message.ChannelPointsCustomRewardId, out var rewardEntry)
                ? (rewardEntry, CommandInvocationKind.Reward, rewardEntry.DefaultTrigger, string.Empty)
                : null;
        }

        if (message.Message is null || string.IsNullOrWhiteSpace(message.Message.Text))
            return null;

        var commandPrefix = settingsService.GetTwitchSettings().CommandPrefix;
        foreach (var fragment in message.Message.Fragments.Where(f => f.Type == "text"))
        {
            var text = fragment.Text.Trim();
            if (!text.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var triggerParts = text[commandPrefix.Length..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (triggerParts.Length == 0)
                return null;

            var trigger = triggerParts[0].ToLowerInvariant();
            if (!commandRegistry.TryGetByChatTrigger(trigger, out var entry))
                return null;

            var totalBits = GetEffectiveBits(message);
            var invocationKind = totalBits > 0
                ? CommandInvocationKind.Bits
                : CommandInvocationKind.Chat;
            return (entry, invocationKind, trigger, text);
        }

        return null;
    }

    private static int GetEffectiveBits(ChannelChatMessage message)
    {
        var cheerBits = message.Cheer?.Bits ?? 0;
        var fragmentBits = message.Message?.Fragments.Sum(f => (long)(f.Cheermote?.Bits ?? 0)) ?? 0L;
        if (fragmentBits > int.MaxValue)
            fragmentBits = int.MaxValue;

        return (int)Math.Max(cheerBits, fragmentBits);
    }

    private static Dictionary<string, string> BuildPlaceholderValues(
        CommandContext context,
        IReadOnlyDictionary<string, string> boundArguments,
        object? data)
    {
        var placeholders = new Dictionary<string, string>(boundArguments)
        {
            ["{username}"] = context.ExecutorDisplayName,
            ["{bits}"] = context.Bits.ToString()
        };

        if (data is null)
            return placeholders;

        foreach (var property in data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            placeholders["{" + property.Name.ToLowerInvariant() + "}"] = property.GetValue(data)?.ToString() ?? string.Empty;

        return placeholders;
    }

    private Task SendFeedbackAsync(ChannelChatMessage message, string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return Task.CompletedTask;

        twitchChatService.SendMessage(message, messageText, asReply: !string.IsNullOrWhiteSpace(message.MessageId));
        return Task.CompletedTask;
    }

    private static CommandExecutionOutcome CreateOutcome(
        string commandName,
        string trigger,
        CommandInvocationKind invocationKind,
        CommandExecutionStatus status,
        CommandExecutionReason reason,
        string? message,
        object? data = null,
        IReadOnlyDictionary<string, string>? placeholders = null,
        int? cooldownRemainingSeconds = null,
        Exception? exception = null) =>
        new()
        {
            CommandName = commandName,
            Trigger = trigger,
            InvocationKind = invocationKind,
            Status = status,
            Reason = reason,
            Message = message,
            Data = data,
            Placeholders = placeholders ?? new Dictionary<string, string>(),
            CooldownRemainingSeconds = cooldownRemainingSeconds,
            Exception = exception
        };
}
