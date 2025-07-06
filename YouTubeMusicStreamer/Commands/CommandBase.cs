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
using YouTubeMusicStreamer.Interfaces;
using YouTubeMusicStreamer.Services.Commands;

namespace YouTubeMusicStreamer.Commands;

public abstract class CommandBase(CommandBag bag) : ICommand
{
    protected CommandBag Bag { get; } = bag;

    public Dictionary<string, string> GetAvailablePlaceholders() => Bag.Placeholders.GetPlaceholders(this).ToDictionary(kv => kv.Key, kv => kv.Value);

    public async Task<bool> ExecuteAsync(ChannelChatMessage msg)
    {
        var key = GetType().Name;
        var cfg = Bag.SettingsService.GetAppSettings().Commands[key];
        var bits = msg.Cheer?.Bits
                   ?? msg.Message.Fragments.Sum(f => f.Cheermote?.Bits ?? 0);

        if (!Bag.Prerequisite.CanRun(msg, cfg, bits)) return false;
        if (!Bag.Cooldown.TryStart(key, cfg.Cooldown, out var wait))
        {
            Bag.TwitchChatService.SendMessage(msg, $"Please wait {wait}s…");
            return false;
        }

        var tokens = Bag.Parser.Parse(msg, Bag.SettingsService.GetAppSettings().TwitchCommandPrefix.Trim(), cfg.Trigger.Trim());

        var bound = await Bag.Binder.BindAndInvokeAsync(this, msg, tokens, bits);
        if (!bound.Success) return false;

        var all = new Dictionary<string, string>
        {
            ["{username}"] = msg.ChatterUserName
        };

        foreach (var kv in bound.ArgValues)
            all[kv.Key] = kv.Value;

        if (bound.Data is not null)
        {
            foreach (var prop in bound.Data.GetType()
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propKey = "{" + prop.Name.ToLowerInvariant() + "}";
                var val = prop.GetValue(bound.Data)?.ToString() ?? string.Empty;
                all[propKey] = val;
            }
        }

        if (cfg.Response == null) return true;

        var text = Bag.Formatter.Format(cfg.Response, all);
        if (!string.IsNullOrWhiteSpace(text))
            Bag.TwitchChatService.SendMessage(msg, text);

        return true;
    }
}