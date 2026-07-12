# Changelog

## [1.1.0](https://github.com/XeroxDev/YouTubeMusicStreamer/compare/v1.0.2...v1.1.0) (2026-07-12)


### Features

* **app:** v1.1.0 "The Autopilot Update" - complete overhaul. See [The-Autopilot-Update](named_releases/v1.1-The-Autopilot-Update.md) for full details. ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **quality:** hardened command runtime against pathological input and added 520+ automated tests ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **settings:** migrated to industrial-grade SQLite + EF Core persistence with automated legacy import ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **startup:** phased, observable boot sequence with zombie-process recovery ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **twitch:** support for dedicated Bot accounts and autonomous Channel Point reward moderation (Auto-Approve/Refund) ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **ui:** new in-app Health Center and detailed persistent diagnostic logging ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **widgets:** decoupled widget server that remains alive independently of the music player ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))


### Bug Fixes

* **app:** startup failure recovery when AppData folder is missing ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **queue:** order persistence race when closing app and safer auto-advance confirmation logic ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **twitch:** off-by-one error in EventSub reconnect loops and improved auth cancellation handling ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))
* **ui:** race conditions in toast notifications and stale update-available indicators ([77a7f1d](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/77a7f1d0d141c69fb9f8911c5434d8657669e691))

## [1.0.2](https://github.com/XeroxDev/YouTubeMusicStreamer/compare/v1.0.1...v1.0.2) (2026-01-16)


### Bug Fixes

* **commands:** semicolon breaks ci/cd ([834e7cd](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/834e7cd99f3dce9fc0d93a5ae5ab014c816d8ef9))
* **commands:** use canonical YouTube watch URL for {url} placeholder ([75a6255](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/75a62552b690b9534fc03af217008f7503e1f277))

## [1.0.1](https://github.com/XeroxDev/YouTubeMusicStreamer/compare/v1.0.0...v1.0.1) (2025-11-10)


### Bug Fixes

* **parser:** correctly remove prefix + trigger before tokenization ([954d2db](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/954d2db96f09c2f5e6274575605e269928cf5eb7))

## [1.0.0](https://github.com/XeroxDev/YouTubeMusicStreamer/compare/v0.0.1...v1.0.0) (2025-07-06)


### Miscellaneous Chores

* **init:** initial commit ([000c4be](https://github.com/XeroxDev/YouTubeMusicStreamer/commit/000c4bed9d7b37ef2e5fae8f437b6b99986c9e20))
