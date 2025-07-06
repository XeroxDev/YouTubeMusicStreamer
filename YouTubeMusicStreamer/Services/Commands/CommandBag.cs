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

using YouTubeMusicStreamer.Services.App;
using YouTubeMusicStreamer.Services.Commands.ArgumentParser;
using YouTubeMusicStreamer.Services.Commands.Binding;
using YouTubeMusicStreamer.Services.Commands.Cooldowns;
using YouTubeMusicStreamer.Services.Commands.Formatting;
using YouTubeMusicStreamer.Services.Commands.Placeholders;
using YouTubeMusicStreamer.Services.Commands.PrerequisiteChecking;
using YouTubeMusicStreamer.Services.Twitch.Interfaces;
using YouTubeMusicStreamer.Services.YouTube;

namespace YouTubeMusicStreamer.Services.Commands;

public class CommandBag(
    SettingsService settings,
    ITwitchChatService twitch,
    YouTubeService yt,
    IArgumentParser parser,
    IPrerequisiteChecker prereqs,
    ICooldownManager cooldowns,
    IArgumentBinder binder,
    IResponseFormatter formatter,
    IPlaceholderProvider placeholders)
{
    public SettingsService SettingsService { get; } = settings;
    public ITwitchChatService TwitchChatService { get; } = twitch;
    public YouTubeService YouTubeService { get; } = yt;
    public IArgumentParser Parser { get; } = parser;
    public IPrerequisiteChecker Prerequisite { get; } = prereqs;
    public ICooldownManager Cooldown { get; } = cooldowns;
    public IArgumentBinder Binder { get; } = binder;
    public IResponseFormatter Formatter { get; } = formatter;
    public IPlaceholderProvider Placeholders { get; } = placeholders;
}