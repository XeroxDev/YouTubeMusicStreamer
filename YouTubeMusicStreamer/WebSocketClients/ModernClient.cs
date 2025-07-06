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
public class ModernClient(IServiceProvider services) : WebSocketClientBase(services, "Modern", "/images/clients/modern.gif")
{
    public override string GetHtml() =>
        """
        <div class="widget">
            <div class="image-container">
                <img id="image" src="" alt="Placeholder image" referrerpolicy="no-referrer">
            </div>
        
            <div class="info">
                <div class="card">
                    <div class="quote">
                        <div class="quote-text">
                            <h1 id="title"></h1>
                            <h2 id="album"></h2>
                        </div>
                        <div class="quote-author">
                            <p id="author"></p>
                        </div>
                    </div>
                </div>
        
                <div class="card">
                    <div id="progress" data-value="50"></div>
                    <div id="track-state">
                        <span class="timer" id="current">00:00</span>
                        <canvas id="visualizer"></canvas>
                        <span class="timer" id="duration">00:00</span>
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
            justify-content: space-between;
        }

        .card {
            background: #333333bb;
            padding: 0.75rem;
            position: relative;
        }

        .card:first-child {
            border-top-left-radius: 1rem;
            border-top-right-radius: 1rem;
        }

        .card:last-child {
            border-bottom-left-radius: 1rem;
            border-bottom-right-radius: 1rem;
        }

        .quote {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
        }

        .quote-text {
            display: flex;
            flex-direction: column;
            border-left: 0.25rem solid #c25;
            padding-left: 0.5rem;
        }

        .quote-author {
            font-size: 1.25rem;
            position: relative;
            padding-left: 1.5rem;
            font-weight: bold;
            font-style: italic;
        }

        .quote-author::before {
            content: '— ';
            position: absolute;
            left: 0;
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
            position: relative;
        }

        .timer {
            flex-shrink: 0;
        }

        #visualizer {
            position: absolute;
            left: 15%;
            width: 70%;
            height: 2rem;
        }

        #progress {
            --value: 0%;
        
            position: absolute;
            height: 0.2rem;
            background: #333;
            overflow: hidden;
            left: 0;
            right: 0;
            top: 0;
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
            if (typeof event.data === 'string') {
                const data = JSON.parse(event.data);
                switch (data.e) {
                    case 'TrackInfo':
                        return updateWidget(data.data);
                    case 'AudioInfo':
                        audioInfo = data.data;
                        break;
                }
            } else {
                // Binary message (Blob) with raw audio data
                if (!audioInfo) {
                    console.error("Audio metadata not received yet!");
                    return;
                }
                const arrayBuffer = await event.data.arrayBuffer();
                const floatData = new Float32Array(arrayBuffer);
        
                let monoData;
                if (audioInfo.Channels === 2) {
                    // For stereo data, average the left and right channels
                    const numSamples = floatData.length / 2;
                    monoData = new Float32Array(numSamples);
                    for (let i = 0; i < numSamples; i++) {
                        // Assuming interleaved stereo: left at index 2*i, right at index 2*i+1
                        monoData[i] = (floatData[2 * i] + floatData[2 * i + 1]) / 2;
                    }
                } else {
                    // If mono, no need to process further
                    monoData = floatData;
                }
                latestAudioData = monoData;
            }
        }
        """;

    public override string GetJs() =>
        """
        
        //////////////////////////
        // Feel free to edit me //
        //////////////////////////
        const amplitudeMultiplier = 1;
        const FFT_SIZE = 1024; // Use a power-of-2 FFT size.
        
        // Define non-linear EQ parameters.
        // Left-most bar will be scaled by minEQ (shrunken down),
        // right-most bar by maxEQ (boosted more).
        const minEQ = 0.05;   // Left side scale factor
        const maxEQ = 6.0;    // Right side scale factor
        const exponent = 2;   // Use a quadratic mapping for a non-linear transition
        
        //////////////////////////
        // Do not edit below me //
        //////////////////////////
        
        function updateWidget(data) {
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
        
        let audioInfo = null;
        let latestAudioData = null;
        
        // Helper: Resize the canvas for high-DPI displays.
        function resizeCanvasToDisplaySize(canvas) {
            const width = canvas.clientWidth;
            const height = canvas.clientHeight;
            if (canvas.width !== width || canvas.height !== height) {
                canvas.width = width;
                canvas.height = height;
            }
            return 1;
        }
        
        function fft(re, im) {
            const n = re.length;
            if (n <= 1) return;
        
            const half = n >> 1;
            const evenRe = new Array(half),
                evenIm = new Array(half);
            const oddRe = new Array(half),
                oddIm = new Array(half);
        
            for (let i = 0; i < half; i++) {
                evenRe[i] = re[2 * i];
                evenIm[i] = im[2 * i];
                oddRe[i] = re[2 * i + 1];
                oddIm[i] = im[2 * i + 1];
            }
        
            fft(evenRe, evenIm);
            fft(oddRe, oddIm);
        
            for (let k = 0; k < half; k++) {
                const t = (-2 * Math.PI * k) / n;
                const cos = Math.cos(t);
                const sin = Math.sin(t);
                const oddReTemp = cos * oddRe[k] - sin * oddIm[k];
                const oddImTemp = sin * oddRe[k] + cos * oddIm[k];
        
                re[k] = evenRe[k] + oddReTemp;
                im[k] = evenIm[k] + oddImTemp;
                re[k + half] = evenRe[k] - oddReTemp;
                im[k + half] = evenIm[k] - oddImTemp;
            }
        }
        function computeFFT(samples) {
            const data = new Float32Array(FFT_SIZE);
            const len = Math.min(samples.length, FFT_SIZE);
            data.set(samples.subarray(0, len));
        
            const N = FFT_SIZE;
            const re = Array.from(data);
            const im = new Array(N).fill(0);
        
            fft(re, im);
        
            const half = N / 2;
            const magnitudes = new Array(half);
            for (let i = 0; i < half; i++) {
                magnitudes[i] = Math.sqrt(re[i] * re[i] + im[i] * im[i]);
            }
            return magnitudes;
        }
        
        function drawFrequencyBarsVisualizer() {
            const canvas = document.getElementById("visualizer");
            const ctx = canvas.getContext("2d");
        
            // Resize and clear the canvas.
            const dpr = resizeCanvasToDisplaySize(canvas);
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.save();
            ctx.scale(dpr, dpr);
        
            if (latestAudioData) {
                // Compute the frequency spectrum from the latest audio data.
                const spectrum = computeFFT(latestAudioData);
        
                const width = canvas.clientWidth;
                const height = canvas.clientHeight;
                const barCount = 64; // Typical count (change to 16 if desired)
                const barWidth = width / barCount;
                const groupSize = Math.floor(spectrum.length / barCount);
        
                for (let i = 0; i < barCount; i++) {
                    let sum = 0;
                    let count = 0;
                    const start = i * groupSize;
                    const end = start + groupSize;
                    for (let j = start; j < end && j < spectrum.length; j++) {
                        sum += spectrum[j];
                        count++;
                    }
                    const avgAmplitude = count > 0 ? sum / count : 0;
                    // Compute a non-linear EQ factor: lower on the left, higher on the right.
                    const eqFactor = minEQ + (maxEQ - minEQ) * Math.pow(i / (barCount - 1), exponent);
                    let amplitude = avgAmplitude * amplitudeMultiplier * eqFactor;
                    // Convert amplitude to a bar height.
                    let barHeight = amplitude * height;
                    // Ensure bars are always visible (minimum 5% of the canvas height).
                    barHeight = Math.max(barHeight, height * 0.05);
                    if (barHeight > height) barHeight = height;
        
                    // Draw the bar centered vertically.
                    const centerY = height / 2;
                    const y = centerY - barHeight / 2;
                    const x = i * barWidth;
                    ctx.fillStyle = "#C25";
                    // Use 80% of the available bar width to leave a gap between bars.
                    ctx.fillRect(x, y, barWidth * 0.8, barHeight);
                }
            }
        
            ctx.restore();
            requestAnimationFrame(drawFrequencyBarsVisualizer);
        }
        
        // Start the frequency bars visualizer loop.
        requestAnimationFrame(drawFrequencyBarsVisualizer);
        """;
}