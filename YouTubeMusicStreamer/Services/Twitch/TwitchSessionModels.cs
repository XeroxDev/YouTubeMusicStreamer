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

namespace YouTubeMusicStreamer.Services.Twitch;

public enum TwitchAccountRole
{
    Broadcaster,
    Bot
}

public enum TwitchSessionStatus
{
    NotConfigured,
    ValidatingBroadcaster,
    ValidatingBot,
    BroadcasterReady,
    ConnectingChat,
    ConnectingEventSub,
    Ready,
    Degraded,
    LoggedOut,
    SwitchingAccount,
    AuthRequired,
    Error
}

public enum TwitchFailureKind
{
    AuthValidation,
    AuthCancelled,
    CallbackListenerBind,
    IdentityBootstrap,
    ChatTransport,
    EventSubTransport,
    EventSubSubscription,
    RewardLoad,
    AccountReset,
    Unknown
}

public sealed record TwitchIdentitySnapshot(
    string UserId,
    string Login,
    string DisplayName,
    string? ProfileImageUrl);

public sealed record TwitchAccountSession(
    TwitchAccountRole Role,
    bool IsConfigured,
    bool IsChatConnected,
    bool IsEventSubConnected,
    TwitchIdentitySnapshot? Identity);

public sealed record TwitchSessionFailure(
    TwitchFailureKind Kind,
    string Message,
    Exception? Exception = null);

public sealed record TwitchSessionState(
    TwitchSessionStatus Status,
    TwitchAccountSession Broadcaster,
    TwitchAccountSession? Bot,
    TwitchAccountRole EffectiveChatRole,
    IReadOnlyList<TwitchRewardSnapshot> Rewards,
    TwitchSessionFailure? LastFailure);
