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

using YouTubeMusicStreamer.Attributes;
using YouTubeMusicStreamer.Interfaces;

namespace YouTubeMusicStreamer.WebSocketClients;

[WebSocketClient]
public class MinimalProgressiveClient(IServiceProvider services) : WebSocketClientBase(services, "Minimal Progressive", "/images/clients/minimal-progressive.gif")
{
    public override string GetHtml() =>
        """
        <div class="widget">
            <div class="image-container">
                <img id="image" src="" alt="Placeholder image" referrerpolicy="no-referrer">
            </div>
        
            <div class="info">
                <div class="card">
                    <h1 id="title"></h1>
                    <h2 id="album"></h2>
                    <p id="author"></p>
                </div>
        
                <div class="card">
                    <div id="track-state">
                        <span id="current">00:00</span>
                        <div id="progress" data-value="50"></div>
                        <span id="duration">00:00</span>
                    </div>
                </div>
            </div>
        </div>
        """;

    public override string GetCss() =>
        """
        html, body {
            margin: 0;
            padding: 0;
            overflow: hidden;
            width: 100vw;
            height: 100vh;
            font-size: min(3vw, 9vh);
        }

        body {
            background: #111;
            color: white;
            font-family: 'Segoe UI', sans-serif;
        }

        *, *::before, *::after {
            box-sizing: border-box;
            padding: 0;
            margin: 0;
            overflow: hidden;
        }

        h1, h2, p {
            white-space: nowrap;
            overflow: hidden;
            width: 100%;
            max-width: 100%;
        }

        .widget {
            width: min(100vw, calc(3 * 100vh));
            height: min(100vh, calc(100vw / 3));
            display: flex;
            gap: 1rem;
        }

        .image-container {
            height: 100%;
            aspect-ratio: 1 / 1;
            flex-shrink: 0;
            overflow: hidden;
            border-radius: 1rem;
        }

        .image-container img {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }

        .info {
            flex: 1;
            display: flex;
            flex-direction: column;
            justify-content: center; 
            gap: 1rem;
            padding: 1rem;
        }

        #title {
            font-size: 1.75rem;
        }

        #album {
            font-size: 1.25rem;
            font-weight: normal;
        }

        #track-state {
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 1rem;
        }

        #progress {
            --value: 0%;
        
            height: 1rem;
            border-radius: 0.5rem;
            background: #333;
            overflow: hidden;
            flex: 1;
        }

        #progress::before {
            content: '';
            display: block;
            height: 100%;
            background: #c25;
            width: var(--value, 0%);
            transition: width 0.5s;
        }

        .marquee-container {
            display: inline-flex;
            white-space: nowrap;
        }

        .marquee-container.scroll {
            animation: marquee linear infinite;
        }

        .marquee-text {
            flex-shrink: 0;
            margin-right: 1rem;
        }

        @keyframes marquee {
            0% {
                transform: translateX(0);
            }
            100% {
                transform: translateX(-50%);
            }
        }
        """;

    public override string GetOnMessageJs() =>
        """
        async (event) => {
            if (typeof event.data !== 'string') return;
            const se = JSON.parse(event.data);
            if (se.e !== 'TrackInfo') return;
        
            const imageElement = document.getElementById('image');
            const titleElement = document.getElementById('title');
            const authorElement = document.getElementById('author');
            const albumElement = document.getElementById('album');
            const progressElement = document.getElementById('progress');
            const currentElement = document.getElementById('current');
            const durationElement = document.getElementById('duration');
        
            const updateInnerText = (element, text) => {
                if (!element || element.dataset.lastText === text || !text) {
                    return;
                }
        
                element.dataset.lastText = text;
        
                element.innerText = text;
        
                if (element.scrollWidth <= element.clientWidth) {
                    return;
                }
        
                element.innerHTML = '';
        
                const marqueeContainer = document.createElement('div');
                marqueeContainer.classList.add('marquee-container');
        
                const span1 = document.createElement('span');
                span1.classList.add('marquee-text');
                span1.innerText = text;
        
                const span2 = document.createElement('span');
                span2.classList.add('marquee-text');
                span2.innerText = text;
        
                marqueeContainer.appendChild(span1);
                marqueeContainer.appendChild(span2);
                element.appendChild(marqueeContainer);
        
                requestAnimationFrame(() => {
                    if (span1.scrollWidth > element.clientWidth) {
                        marqueeContainer.classList.add('scroll');
                        const duration = span1.scrollWidth / 50;
                        marqueeContainer.style.animationDuration = `${duration}s`;
                    }
                });
            };
        
            const updateImage = (element, url) => {
                if (typeof isDebug !== 'undefined' && isDebug) url = 'https://placehold.co/544?text=No%20Image';
                if (element.src === url || !url) {
                    return;
                }
                element.src = url;
            };
        
            const updateTimer = (element, seconds) => {
                const minutes = Math.floor(seconds / 60);
                const remainingSeconds = Math.floor(seconds % 60);
        
                const formattedMinutes = minutes.toString().padStart(2, '0');
                const formattedSeconds = remainingSeconds.toString().padStart(2, '0');
        
                updateInnerText(element, `${formattedMinutes}:${formattedSeconds}`);
            };
        
            const updateProgress = (element, value) => {
                element.style.setProperty('--value', `${value}%`);
            };
        
            const data = se.data;
            if (typeof isDebug !== 'undefined' && isDebug) console.log(data);
        
            if (!data.Video || !data.Player) return;
        
            const thumbnail = data.Video.Thumbnails.reduce((prev, current) => {
                return (prev.Width > current.Width) ? prev : current
            });
        
            if (thumbnail) {
                updateImage(imageElement, thumbnail.Url);
            }
        
            updateInnerText(titleElement, data.Video.Title);
            updateInnerText(authorElement, data.Video.Author);
            updateInnerText(albumElement, data.Video.Album);
        
            const durationInSeconds = data.Video.DurationSeconds;
            const progressInSeconds = data.Player.VideoProgress;
        
            updateProgress(progressElement, (progressInSeconds / durationInSeconds) * 100);
            updateTimer(currentElement, progressInSeconds);
            updateTimer(durationElement, durationInSeconds);
        }
        """;
}