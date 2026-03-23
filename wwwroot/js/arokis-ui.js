// ═══════════════════════════════════════════════════════
// AROKIS — WebView2 версия
// Полный порт из Photino: ротация, ApexCharts, сводка
// Interop: window.chrome.webview.hostObjects.arokis
// ═══════════════════════════════════════════════════════

// ── ПЕРЕКЛЮЧАТЕЛИ ────────────────────────────────────────
const enableStreamRotation = true;
const ROTATION_INTERVAL_MS = 3000;

// ── КОНСТАНТЫ ────────────────────────────────────────────
const LINE_COLORS    = ["#2fb8c4","#ff6b35","#7c3aed","#10b981","#f59e0b","#ef4444","#3b82f6","#ec4899"];
const STREAM_COLORS  = ["#ea5454","#3b82f6","#10b981","#f59e0b"];
const STREAM_NAMES   = ["Circle","Triangle","Square","Star"];
const TOTAL_STREAMS  = 4;
const VISIBLE_SLOTS  = 3;

function getLineColor(i) { return LINE_COLORS[i % LINE_COLORS.length]; }
function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }
async function waitForElement(id, tries = 300) {
    for (let i = 0; i < tries; i++) {
        const el = document.getElementById(id);
        if (el) return el;
        await sleep(20);
    }
    return null;
}

// ═══════════════════════════════════════════════════════
// INTEROP — обёртка над window.chrome.webview.hostObjects.arokis
// Все методы C# возвращают JSON-строки (кроме bool/double)
// ═══════════════════════════════════════════════════════
const api = {
    async getStreamCable(n) {
        try {
            const s = await window.chrome.webview.hostObjects.arokis.GetStreamCable(n);
            return s === "null" ? null : JSON.parse(s);
        } catch { return null; }
    },
    async getThicknessMm(n, angles) {
        try {
            const s = await window.chrome.webview.hostObjects.arokis.GetThicknessMm(n, JSON.stringify(angles));
            return JSON.parse(s);
        } catch { return []; }
    },
    async getAllStreamsThickness(angles) {
        try {
            const s = await window.chrome.webview.hostObjects.arokis.GetAllStreamsThickness(JSON.stringify(angles));
            return JSON.parse(s);
        } catch { return [[],[],[],[]]; }
    },
    async setK(idx, val) {
        try { await window.chrome.webview.hostObjects.arokis.SetK(idx, val); } catch {}
    },
    startAll() {
        try { window.chrome.webview.hostObjects.arokis.StartAllStreams(); } catch {}
    },
    async ping() {
        try { return !!(await window.chrome.webview.hostObjects.arokis.PingController()); }
        catch { return false; }
    }
};

// ═══════════════════════════════════════════════════════
// RotationManager — 3 слота, 4 стрима, ротация по кругу
// ═══════════════════════════════════════════════════════
class RotationManager {
    constructor() {
        this.slotStreams   = [1, 2, 3];  // slot1→stream1, slot2→stream2, slot3→stream3
        this.hiddenStream  = 4;
        this.rotationIndex = 0;
        this.intervalId    = null;
        this.onStreamChange = null;      // (slotNumber, newStream, oldStream)
    }
    start() {
        if (!enableStreamRotation) return;
        this.intervalId = setInterval(() => this.rotate(), ROTATION_INTERVAL_MS);
    }
    stop() { if (this.intervalId) clearInterval(this.intervalId); }
    getStreamForSlot(slot) { return this.slotStreams[slot - 1]; }
    rotate() {
        const slotIdx    = this.rotationIndex % VISIBLE_SLOTS;
        const slotNumber = slotIdx + 1;
        const outgoing   = this.slotStreams[slotIdx];
        const incoming   = this.hiddenStream;
        this.slotStreams[slotIdx] = incoming;
        this.hiddenStream = outgoing;
        this.rotationIndex++;
        if (this.onStreamChange) this.onStreamChange(slotNumber, incoming, outgoing);
    }
}

// ═══════════════════════════════════════════════════════
// ThicknessChart — ApexCharts линейный график толщины
// ═══════════════════════════════════════════════════════
class ThicknessChart {
    // Читаем текущую тему из data-theme на <html> при каждом создании графика.
    // Это нужно потому что apex-theme-patch.js вешает только MutationObserver
    // и не вызывает updateAllCharts() для уже существующих + только что созданных графиков.
    static getCurrentTheme() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    static getColors(mode) {
        return mode === 'dark'
            ? { bg: 'transparent', text: '#8b949e', grid: 'rgba(47,184,196,0.10)', legend: '#e6edf3' }
            : { bg: 'transparent', text: '#6b7280', grid: 'rgba(47,184,196,0.15)', legend: '#374151' };
    }

    constructor(containerId) {
        this.containerId  = containerId;
        this.maxPoints    = 30;
        this.series       = [];
        this.startTime    = Date.now();

        const mode   = ThicknessChart.getCurrentTheme();
        const colors = ThicknessChart.getColors(mode);

        this.chart = new ApexCharts(document.getElementById(containerId), {
            chart: {
                type: "line", height: "100%",
                animations: { enabled: false },
                toolbar:    { show: false },
                zoom:       { enabled: false },
                background: colors.bg
            },
            theme: { mode },
            series: [],
            xaxis: {
                type: "numeric",
                title: { text: "Время (сек)", style: { color: colors.text, fontSize: "11px" } },
                labels: { style: { colors: colors.text, fontSize: "10px" }, formatter: v => Math.round(v) + "s" }
            },
            yaxis: {
                title:  { text: "мм", style: { color: colors.text, fontSize: "11px" } },
                labels: { style: { colors: colors.text, fontSize: "10px" }, formatter: v => v.toFixed(2) },
                min: 0
            },
            stroke:  { curve: "smooth", width: 2 },
            legend:  { show: true, labels: { colors: colors.legend }, fontSize: "11px" },
            tooltip: { theme: mode, y: { formatter: v => v.toFixed(3) + " mm" } },
            grid:    { borderColor: colors.grid, strokeDashArray: 3 }
        });
        this.chart.render();
    }

    ensureSeriesCount(count) {
        while (this.series.length < count) {
            const i = this.series.length;
            this.series.push({ name: `Толщина ${i + 1}`, color: getLineColor(i), data: [] });
        }
        while (this.series.length > count) this.series.pop();
    }

    pushData(arr) {
        const t = (Date.now() - this.startTime) / 1000;
        this.ensureSeriesCount(arr.length);
        arr.forEach((v, i) => {
            if (Number.isFinite(v)) {
                this.series[i].data.push({ x: t, y: v });
                if (this.series[i].data.length > this.maxPoints) this.series[i].data.shift();
            }
        });
        this.chart.updateOptions({ colors: this.series.map(s => s.color) }, false, false);
        this.chart.updateSeries(this.series.map(s => ({ name: s.name, data: s.data })));
    }

    reset() { this.series = []; this.startTime = Date.now(); this.chart.updateSeries([]); }
}

// ═══════════════════════════════════════════════════════
// CircleDiagram — привязан к слоту, показывает любой стрим
// ═══════════════════════════════════════════════════════
class CircleDiagram {
    constructor(canvasId, slotNumber) {
        this.canvas       = document.getElementById(canvasId);
        this.ctx          = this.canvas.getContext("2d");
        this.slotNumber   = slotNumber;
        this.streamNumber = slotNumber;
        this.streamPoints = [];
        this.lines        = [];
        this.dragLineIndex = null;
        this.dragPointType = null;
        this.chart        = null;
        this.chartCounter = 0;

        this.resizeObserver = new ResizeObserver(() => this.updateCanvasSize());
        this.resizeObserver.observe(this.canvas.parentElement);
        this.updateCanvasSize();
        this.addLine();

        this.canvas.addEventListener("mousedown",  e => this.handleMouseDown(e));
        this.canvas.addEventListener("mousemove",  e => this.handleMouseMove(e));
        this.canvas.addEventListener("mouseup",    () => this.handleMouseUp());
        this.canvas.addEventListener("mouseleave", () => this.handleMouseUp());
        this.canvas.addEventListener("touchstart", e => this.handleTouchStart(e), { passive: false });
        this.canvas.addEventListener("touchmove",  e => this.handleTouchMove(e),  { passive: false });
        this.canvas.addEventListener("touchend",   () => this.handleTouchEnd());
    }

    // ── Переключение стрима с анимацией ──────────────────
    async switchStream(newStream) {
        await this.animateOut();
        this.streamNumber = newStream;
        this.streamPoints = [];
        this.updateHeader();
        await this.loadStreamData();
        await this.animateIn();
    }

    updateHeader() {
        const titleEl = document.getElementById("cardTitle" + this.slotNumber);
        const chipEl  = document.getElementById("cardChip"  + this.slotNumber);
        const name    = STREAM_NAMES[this.streamNumber - 1] || ("Stream " + this.streamNumber);
        if (titleEl) titleEl.textContent = "ЗНАЧЕНИЕ " + this.slotNumber + " · " + name;
        if (chipEl)  { chipEl.textContent = name; chipEl.style.color = STREAM_COLORS[this.streamNumber - 1]; }
    }

    async animateOut() {
        const zone = this.canvas.closest(".arokis-canvas-zone");
        if (!zone) return;
        zone.style.transition = "transform 0.3s ease, opacity 0.3s ease";
        zone.style.transform  = "translateX(60%) scale(0.6)";
        zone.style.opacity    = "0";
        await sleep(320);
    }

    async animateIn() {
        const zone = this.canvas.closest(".arokis-canvas-zone");
        if (!zone) return;
        zone.style.transition = "none";
        zone.style.transform  = "translateX(-60%) scale(0.6)";
        zone.style.opacity    = "0";
        zone.getBoundingClientRect();
        zone.style.transition = "transform 0.4s ease, opacity 0.4s ease";
        zone.style.transform  = "";
        zone.style.opacity    = "";
        await sleep(420);
    }

    attachChart(id) { this.chart = new ThicknessChart(id); }

    updateCanvasSize() {
        const c = this.canvas.parentElement;
        const w = Math.max(200, c.clientWidth - 46);
        const h = Math.max(200, c.clientHeight);
        const s = Math.min(w, h);
        this.canvas.width  = Math.floor(s * devicePixelRatio);
        this.canvas.height = Math.floor(s * devicePixelRatio);
        this.canvas.style.width  = s + "px";
        this.canvas.style.height = s + "px";
        this.ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
        this.circle = { x: s / 2, y: s / 2, radius: s * 0.41, size: s };
        this.draw();
    }

    calculatePointPosition(deg) {
        const a = deg * Math.PI / 180;
        return { x: this.circle.x + this.circle.radius * Math.cos(a),
                 y: this.circle.y + this.circle.radius * Math.sin(a) };
    }

    addLine()    { this.lines.push({ angle: Math.random() * 360 }); this.draw(); }
    removeLine() { if (this.lines.length > 0) this.lines.pop(); this.draw(); }
    resetLines() { this.lines = []; this.addLine(); if (this.chart) this.chart.reset(); this.draw(); }

    async updateThickness() {
        if (!this.lines.length) return;
        const angles = this.lines.map(l => l.angle);
        const vals   = await api.getThicknessMm(this.streamNumber, angles);
        if (vals && vals.length) {
            renderThicknessBlocks(this.slotNumber, vals);
            this.chartCounter++;
            if (this.chart && this.chartCounter % 3 === 0) this.chart.pushData(vals);
        }
    }

    async loadStreamData() {
        const data = await api.getStreamCable(this.streamNumber);
        if (!data || !data.points) { this.streamPoints = []; this.draw(); return; }
        this.streamPoints = data.points
            .map(p => ({ x: Number(p.x), y: Number(p.y) }))
            .filter(p => !isNaN(p.x) && !isNaN(p.y));
        this.draw();
    }

    drawStreamPoints() {
        if (!this.streamPoints.length) return;
        const xs = this.streamPoints.map(p => p.x), ys = this.streamPoints.map(p => p.y);
        const xMin = Math.min(...xs), xMax = Math.max(...xs);
        const yMin = Math.min(...ys), yMax = Math.max(...ys);
        const sc = Math.min(
            this.circle.radius / ((xMax - xMin) / 2 || 1),
            this.circle.radius / ((yMax - yMin) / 2 || 1)
        ) * 0.65;
        const cx = (xMin + xMax) / 2, cy = (yMin + yMax) / 2;
        this.ctx.strokeStyle = STREAM_COLORS[this.streamNumber - 1];
        this.ctx.lineWidth   = Math.max(2, this.circle.size / 200);
        this.ctx.beginPath();
        this.streamPoints.forEach((p, i) => {
            const x = this.circle.x + (p.x - cx) * sc;
            const y = this.circle.y + (p.y - cy) * sc;
            i === 0 ? this.ctx.moveTo(x, y) : this.ctx.lineTo(x, y);
        });
        this.ctx.stroke();
    }

    drawLines() {
        this.lines.forEach((line, idx) => {
            const pA = this.calculatePointPosition(line.angle);
            const pB = this.calculatePointPosition((line.angle + 180) % 360);
            const color = getLineColor(idx);
            this.ctx.strokeStyle = color;
            this.ctx.lineWidth   = Math.max(2.5, this.circle.size / 140);
            this.ctx.beginPath(); this.ctx.moveTo(pA.x, pA.y); this.ctx.lineTo(pB.x, pB.y); this.ctx.stroke();
            this.drawPoint(pA, idx, "A", color);
            this.drawPoint(pB, idx, "B", color);
        });
    }

    drawPoint(pt, li, label, color) {
        const r   = Math.max(6, this.circle.size / 40);
        const num = label === "A" ? li * 2 + 1 : li * 2 + 2;
        this.ctx.fillStyle   = color;
        this.ctx.beginPath(); this.ctx.arc(pt.x, pt.y, r, 0, Math.PI * 2); this.ctx.fill();
        this.ctx.strokeStyle = "#fff";
        this.ctx.lineWidth   = Math.max(1.5, this.circle.size / 220); this.ctx.stroke();
        this.ctx.fillStyle   = "#055f67";
        this.ctx.font        = `bold ${Math.max(12, this.circle.size / 26)}px Arial`;
        this.ctx.textAlign   = "center"; this.ctx.textBaseline = "middle";
        this.ctx.fillText(label, pt.x, pt.y - (r + 10));
        this.ctx.fillStyle   = "#fff";
        this.ctx.font        = `bold ${Math.max(10, this.circle.size / 42)}px Arial`;
        this.ctx.fillText(String(num), pt.x, pt.y);
    }

    draw() {
        const s = this.circle?.size ?? 300;
        this.ctx.clearRect(0, 0, s, s);
        this.drawStreamPoints();
        this.ctx.strokeStyle = "#2fb8c4";
        this.ctx.lineWidth   = Math.max(2.5, s / 140);
        this.ctx.beginPath(); this.ctx.arc(this.circle.x, this.circle.y, this.circle.radius, 0, Math.PI * 2); this.ctx.stroke();
        this.ctx.fillStyle   = "#2fb8c4";
        this.ctx.beginPath(); this.ctx.arc(this.circle.x, this.circle.y, Math.max(3, s / 140), 0, Math.PI * 2); this.ctx.fill();
        this.drawLines();
    }

    // ── Drag ──────────────────────────────────────────────
    handleMouseDown(e)  { const r = this.canvas.getBoundingClientRect(); this.startDrag(e.clientX - r.left, e.clientY - r.top); }
    handleMouseMove(e)  { const r = this.canvas.getBoundingClientRect(), x = e.clientX - r.left, y = e.clientY - r.top; this.dragLineIndex === null ? this.updateCursor(x, y) : this.updateDrag(x, y); }
    handleMouseUp()     { this.dragLineIndex = null; this.dragPointType = null; this.canvas.style.cursor = "default"; this.updateThickness(); this.draw(); }
    handleTouchStart(e) { if (e.touches.length !== 1) return; const r = this.canvas.getBoundingClientRect(), t = e.touches[0]; this.startDrag(t.clientX - r.left, t.clientY - r.top); e.preventDefault(); }
    handleTouchMove(e)  { if (e.touches.length !== 1) return; const r = this.canvas.getBoundingClientRect(), t = e.touches[0], x = t.clientX - r.left, y = t.clientY - r.top; this.dragLineIndex === null ? this.updateCursor(x, y) : this.updateDrag(x, y); e.preventDefault(); }
    handleTouchEnd()    { this.handleMouseUp(); }

    startDrag(x, y) {
        const hit = Math.max(10, this.circle.size / 30);
        for (let i = 0; i < this.lines.length; i++) {
            const pA = this.calculatePointPosition(this.lines[i].angle);
            const pB = this.calculatePointPosition((this.lines[i].angle + 180) % 360);
            if (Math.hypot(x - pA.x, y - pA.y) <= hit) { this.dragLineIndex = i; this.dragPointType = "A"; this.canvas.style.cursor = "grabbing"; return; }
            if (Math.hypot(x - pB.x, y - pB.y) <= hit) { this.dragLineIndex = i; this.dragPointType = "B"; this.canvas.style.cursor = "grabbing"; return; }
        }
    }
    updateDrag(x, y) {
        let a = Math.atan2(y - this.circle.y, x - this.circle.x) * 180 / Math.PI;
        if (a < 0) a += 360;
        const l = this.lines[this.dragLineIndex]; if (!l) return;
        l.angle = this.dragPointType === "A" ? a : (a + 180) % 360;
        this.draw();
    }
    updateCursor(x, y) {
        const hit = Math.max(10, this.circle.size / 30);
        for (const l of this.lines) {
            const pA = this.calculatePointPosition(l.angle);
            const pB = this.calculatePointPosition((l.angle + 180) % 360);
            if (Math.hypot(x - pA.x, y - pA.y) <= hit || Math.hypot(x - pB.x, y - pB.y) <= hit) { this.canvas.style.cursor = "grab"; return; }
        }
        this.canvas.style.cursor = "default";
    }
    async printK() {
        for (let i = 0; i < this.lines.length; i++) {
            const a = this.lines[i].angle * Math.PI / 180;
            if (Math.abs(Math.cos(a)) < 1e-6) continue;
            await api.setK(i + 1, -Math.tan(a));
        }
    }
}

// ═══════════════════════════════════════════════════════
// CombinedDiagram — сводная диаграмма (все 4 стрима)
// ═══════════════════════════════════════════════════════
class CombinedDiagram {
    constructor(canvasId) {
        this.canvas          = document.getElementById(canvasId);
        this.ctx             = this.canvas.getContext("2d");
        this.lines           = [];
        this.allStreamPoints = { 1: [], 2: [], 3: [], 4: [] };
        this.dragLineIndex   = null;
        this.dragPointType   = null;

        new ResizeObserver(() => this.updateCanvasSize()).observe(this.canvas.parentElement);
        this.updateCanvasSize();
        this.addLine();

        this.canvas.addEventListener("mousedown",  e => this.onDown(e));
        this.canvas.addEventListener("mousemove",  e => this.onMove(e));
        this.canvas.addEventListener("mouseup",    () => this.onUp());
        this.canvas.addEventListener("mouseleave", () => this.onUp());
        this.canvas.addEventListener("touchstart", e => { if (e.touches.length !== 1) return; const r = this.canvas.getBoundingClientRect(), t = e.touches[0]; this.startDrag(t.clientX - r.left, t.clientY - r.top); e.preventDefault(); }, { passive: false });
        this.canvas.addEventListener("touchmove",  e => { if (e.touches.length !== 1) return; const r = this.canvas.getBoundingClientRect(), t = e.touches[0]; this.dragLineIndex !== null ? this.doDrag(t.clientX - r.left, t.clientY - r.top) : this.doCursor(t.clientX - r.left, t.clientY - r.top); e.preventDefault(); }, { passive: false });
        this.canvas.addEventListener("touchend",   () => this.onUp());
    }

    updateCanvasSize() {
        const c = this.canvas.parentElement;
        const w = Math.max(200, c.clientWidth), h = Math.max(200, c.clientHeight), s = Math.min(w, h);
        this.canvas.width  = Math.floor(s * devicePixelRatio);
        this.canvas.height = Math.floor(s * devicePixelRatio);
        this.canvas.style.width  = s + "px";
        this.canvas.style.height = s + "px";
        this.ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
        this.circle = { x: s / 2, y: s / 2, radius: s * 0.41, size: s };
        this.draw();
    }

    addLine()    { this.lines.push({ angle: Math.random() * 360 }); this.draw(); }
    removeLine() { if (this.lines.length > 0) this.lines.pop(); this.draw(); }
    resetLines() { this.lines = []; this.addLine(); this.draw(); }

    async loadAllStreams() {
        for (let s = 1; s <= TOTAL_STREAMS; s++) {
            const data = await api.getStreamCable(s);
            this.allStreamPoints[s] = (data && data.points)
                ? data.points.map(p => ({ x: Number(p.x), y: Number(p.y) })).filter(p => !isNaN(p.x))
                : [];
        }
        this.draw();
    }

    draw() {
        const s = this.circle?.size ?? 300;
        this.ctx.clearRect(0, 0, s, s);
        for (let si = 1; si <= TOTAL_STREAMS; si++) {
            const pts = this.allStreamPoints[si];
            if (!pts || !pts.length) continue;
            this.drawShape(pts, STREAM_COLORS[si - 1]);
        }
        this.ctx.strokeStyle = "rgba(47,184,196,0.3)";
        this.ctx.lineWidth   = Math.max(1.5, s / 200);
        this.ctx.beginPath(); this.ctx.arc(this.circle.x, this.circle.y, this.circle.radius, 0, Math.PI * 2); this.ctx.stroke();
        this.lines.forEach((line, idx) => {
            const aA = line.angle * Math.PI / 180, aB = ((line.angle + 180) % 360) * Math.PI / 180;
            const color = getLineColor(idx), r = this.circle.radius;
            this.ctx.strokeStyle = color; this.ctx.lineWidth = Math.max(2, s / 150);
            this.ctx.beginPath();
            this.ctx.moveTo(this.circle.x + r * Math.cos(aA), this.circle.y + r * Math.sin(aA));
            this.ctx.lineTo(this.circle.x + r * Math.cos(aB), this.circle.y + r * Math.sin(aB));
            this.ctx.stroke();
            [[aA, "A"], [aB, "B"]].forEach(([a, lbl]) => {
                const px = this.circle.x + r * Math.cos(a), py = this.circle.y + r * Math.sin(a);
                const pr = Math.max(5, s / 50);
                this.ctx.fillStyle = color; this.ctx.beginPath(); this.ctx.arc(px, py, pr, 0, Math.PI * 2); this.ctx.fill();
                this.ctx.strokeStyle = "#fff"; this.ctx.lineWidth = 1.5; this.ctx.stroke();
                this.ctx.fillStyle = "#fff"; this.ctx.font = `bold ${Math.max(9, s / 48)}px Arial`;
                this.ctx.textAlign = "center"; this.ctx.textBaseline = "middle";
                this.ctx.fillText(lbl, px, py);
            });
        });
    }

    drawShape(pts, color) {
        const xs = pts.map(p => p.x), ys = pts.map(p => p.y);
        const xMin = Math.min(...xs), xMax = Math.max(...xs), yMin = Math.min(...ys), yMax = Math.max(...ys);
        const sc = Math.min(this.circle.radius / ((xMax - xMin) / 2 || 1), this.circle.radius / ((yMax - yMin) / 2 || 1)) * 0.6;
        const cx = (xMin + xMax) / 2, cy = (yMin + yMax) / 2;
        this.ctx.strokeStyle = color; this.ctx.lineWidth = Math.max(1.5, this.circle.size / 220);
        this.ctx.globalAlpha = 0.8; this.ctx.beginPath();
        pts.forEach((p, i) => {
            const x = this.circle.x + (p.x - cx) * sc, y = this.circle.y + (p.y - cy) * sc;
            i === 0 ? this.ctx.moveTo(x, y) : this.ctx.lineTo(x, y);
        });
        this.ctx.closePath(); this.ctx.stroke(); this.ctx.globalAlpha = 1;
    }

    onDown(e) { const r = this.canvas.getBoundingClientRect(); this.startDrag(e.clientX - r.left, e.clientY - r.top); }
    onMove(e) { const r = this.canvas.getBoundingClientRect(), x = e.clientX - r.left, y = e.clientY - r.top; this.dragLineIndex !== null ? this.doDrag(x, y) : this.doCursor(x, y); }
    onUp()    { this.dragLineIndex = null; this.dragPointType = null; this.canvas.style.cursor = "default"; this.draw(); }

    startDrag(x, y) {
        const hit = Math.max(10, this.circle.size / 30), r = this.circle.radius;
        for (let i = 0; i < this.lines.length; i++) {
            const aA = this.lines[i].angle * Math.PI / 180, aB = ((this.lines[i].angle + 180) % 360) * Math.PI / 180;
            if (Math.hypot(x - (this.circle.x + r * Math.cos(aA)), y - (this.circle.y + r * Math.sin(aA))) <= hit) { this.dragLineIndex = i; this.dragPointType = "A"; this.canvas.style.cursor = "grabbing"; return; }
            if (Math.hypot(x - (this.circle.x + r * Math.cos(aB)), y - (this.circle.y + r * Math.sin(aB))) <= hit) { this.dragLineIndex = i; this.dragPointType = "B"; this.canvas.style.cursor = "grabbing"; return; }
        }
    }
    doDrag(x, y) {
        let a = Math.atan2(y - this.circle.y, x - this.circle.x) * 180 / Math.PI;
        if (a < 0) a += 360;
        const l = this.lines[this.dragLineIndex]; if (!l) return;
        l.angle = this.dragPointType === "A" ? a : (a + 180) % 360;
        this.draw();
    }
    doCursor(x, y) {
        const hit = Math.max(10, this.circle.size / 30), r = this.circle.radius;
        for (const l of this.lines) {
            const aA = l.angle * Math.PI / 180, aB = ((l.angle + 180) % 360) * Math.PI / 180;
            if (Math.hypot(x - (this.circle.x + r * Math.cos(aA)), y - (this.circle.y + r * Math.sin(aA))) <= hit ||
                Math.hypot(x - (this.circle.x + r * Math.cos(aB)), y - (this.circle.y + r * Math.sin(aB))) <= hit) {
                this.canvas.style.cursor = "grab"; return;
            }
        }
        this.canvas.style.cursor = "default";
    }
}

// ═══════════════════════════════════════════════════════
// RENDER HELPERS
// ═══════════════════════════════════════════════════════
function renderThicknessBlocks(slot, thicknesses) {
    const el = document.getElementById("thicknessContent" + slot);
    if (!el) return;
    el.innerHTML = thicknesses.map((t, i) => {
        const c = getLineColor(i);
        return `<div class="value-block" style="border-top:3px solid ${c};">
            <div class="param-name" style="color:${c};">Т${i + 1}</div>
            <div class="param-value">${Number.isFinite(t) ? t.toFixed(3) : "—"}</div>
            <div class="param-tolerance">mm</div>
        </div>`;
    }).join("");
}

function renderSummaryThickness(slot, thicknesses) {
    const el = document.getElementById("summaryThickness" + slot);
    if (!el) return;
    el.innerHTML = thicknesses.map((t, i) => {
        const c = getLineColor(i);
        return `<div class="value-block" style="border-top:2px solid ${c};">
            <div class="param-name" style="color:${c};">Л${i + 1}</div>
            <div class="param-value">${Number.isFinite(t) ? t.toFixed(3) : "—"}</div>
            <div class="param-tolerance">mm</div>
        </div>`;
    }).join("");
}

// ═══════════════════════════════════════════════════════
// SUMMARY MODULE
// ═══════════════════════════════════════════════════════
const summaryCanvases = new Map();
let combinedDiagram  = null;
let summaryChart     = null;

function drawSummaryMirror(slotNumber) {
    const main   = diagrams.get(slotNumber);
    const mirror = summaryCanvases.get(slotNumber);
    if (!main || !mirror) return;
    const { canvas, ctx } = mirror;
    const c = canvas.parentElement;
    const w = Math.max(60, c.clientWidth), h = Math.max(60, c.clientHeight), size = Math.min(w, h);
    canvas.width  = Math.floor(size * devicePixelRatio);
    canvas.height = Math.floor(size * devicePixelRatio);
    canvas.style.width  = size + "px";
    canvas.style.height = size + "px";
    ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
    const cx = size / 2, cy = size / 2, radius = size * 0.41;
    ctx.clearRect(0, 0, size, size);
    if (main.streamPoints.length) {
        const xs = main.streamPoints.map(p => p.x), ys = main.streamPoints.map(p => p.y);
        const xMin = Math.min(...xs), xMax = Math.max(...xs), yMin = Math.min(...ys), yMax = Math.max(...ys);
        const sc = Math.min(radius / ((xMax - xMin) / 2 || 1), radius / ((yMax - yMin) / 2 || 1)) * 0.65;
        const dcx = (xMin + xMax) / 2, dcy = (yMin + yMax) / 2;
        ctx.strokeStyle = STREAM_COLORS[main.streamNumber - 1];
        ctx.lineWidth   = Math.max(1.5, size / 200);
        ctx.beginPath();
        main.streamPoints.forEach((p, i) => {
            const x = cx + (p.x - dcx) * sc, y = cy + (p.y - dcy) * sc;
            i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
        });
        ctx.stroke();
    }
    ctx.strokeStyle = "rgba(47,184,196,0.4)";
    ctx.lineWidth   = Math.max(1, size / 200);
    ctx.beginPath(); ctx.arc(cx, cy, radius, 0, Math.PI * 2); ctx.stroke();
    if (combinedDiagram) {
        combinedDiagram.lines.forEach((line, idx) => {
            const aA = line.angle * Math.PI / 180, aB = ((line.angle + 180) % 360) * Math.PI / 180;
            const color = getLineColor(idx);
            ctx.strokeStyle = color; ctx.lineWidth = Math.max(1, size / 180);
            ctx.beginPath();
            ctx.moveTo(cx + radius * Math.cos(aA), cy + radius * Math.sin(aA));
            ctx.lineTo(cx + radius * Math.cos(aB), cy + radius * Math.sin(aB));
            ctx.stroke();
        });
    }
}

async function updateSummaryThicknesses() {
    if (!combinedDiagram || !combinedDiagram.lines.length) return;
    const angles = combinedDiagram.lines.map(l => l.angle);
    const allT   = await api.getAllStreamsThickness(angles);
    for (let s = 0; s < VISIBLE_SLOTS; s++) renderSummaryThickness(s + 1, allT[s] || []);
    if (summaryChart) {
        const flat = [], names = [];
        for (let s = 0; s < VISIBLE_SLOTS; s++) {
            (allT[s] || []).forEach((v, li) => { flat.push(v); names.push(`S${s+1}·Л${li+1}`); });
        }
        const t = (Date.now() - summaryChart.startTime) / 1000;
        summaryChart.ensureSeriesCount(flat.length);
        flat.forEach((v, i) => {
            summaryChart.series[i].name = names[i];
            if (Number.isFinite(v)) {
                summaryChart.series[i].data.push({ x: t, y: v });
                if (summaryChart.series[i].data.length > summaryChart.maxPoints) summaryChart.series[i].data.shift();
            }
        });
        summaryChart.chart.updateOptions({ colors: summaryChart.series.map(s => s.color) }, false, false);
        summaryChart.chart.updateSeries(summaryChart.series.map(s => ({ name: s.name, data: s.data })));
    }
}

async function initSummary() {
    for (let i = 1; i <= VISIBLE_SLOTS; i++) {
        const el = await waitForElement("summaryDiagram" + i, 200);
        if (!el) continue;
        const ctx = el.getContext("2d");
        const ro  = new ResizeObserver(() => drawSummaryMirror(i));
        ro.observe(el.parentElement);
        summaryCanvases.set(i, { canvas: el, ctx, ro });
    }
    const combinedEl = await waitForElement("combinedDiagram", 200);
    if (combinedEl) { combinedDiagram = new CombinedDiagram("combinedDiagram"); await combinedDiagram.loadAllStreams(); }
    const chartEl = await waitForElement("summaryChart", 200);
    if (chartEl) { summaryChart = new ThicknessChart("summaryChart"); }
}

function startSummaryRefresh() {
    setInterval(() => {
        if (combinedDiagram) combinedDiagram.loadAllStreams();
        for (let i = 1; i <= VISIBLE_SLOTS; i++) drawSummaryMirror(i);
    }, 500);
    setInterval(() => updateSummaryThicknesses(), 1500);
}

// ═══════════════════════════════════════════════════════
// MAIN
// ═══════════════════════════════════════════════════════
const diagrams = new Map();
const rotation = new RotationManager();

window.arokisUi = (function () {

    async function init() {
        // Инициализируем 3 слота
        for (let slot = 1; slot <= VISIBLE_SLOTS; slot++) {
            const canvas = await waitForElement("circleDiagram" + slot, 300);
            if (!canvas) { console.error("Canvas not found: circleDiagram" + slot); continue; }

            const d = new CircleDiagram("circleDiagram" + slot, slot);
            d.streamNumber = rotation.getStreamForSlot(slot);
            diagrams.set(slot, d);

            const chartEl = await waitForElement("thicknessChart" + slot, 100);
            if (chartEl) d.attachChart("thicknessChart" + slot);

            await d.loadStreamData();
            await d.updateThickness();
            d.updateHeader();
        }

        // Ротация
        rotation.onStreamChange = async (slot, newStream) => {
            const d = diagrams.get(slot);
            if (!d) return;
            await d.switchStream(newStream);
            await d.updateThickness();
            // Обновляем индикатор подключения — стрим сменился, статус может быть другим
            await updateCardConn(slot, newStream);
        };
        rotation.start();

        await initSummary();

        // Автозапуск всех стримов при старте (иначе IsRunning=false и фигуры не анимируются)
        api.startAll();

        // Циклы обновления
        setInterval(() => diagrams.forEach(d => d.loadStreamData()), 100);
        setInterval(() => diagrams.forEach(d => d.updateThickness()), 1000);
        setInterval(async () => { for (const d of diagrams.values()) await d.printK(); }, 1000);
        startSummaryRefresh();

        // Статус подключения — по индикатору на каждой карточке
        startConnPolling();
    }

    async function addLine(p)    { const d = diagrams.get(p); d?.addLine();    await d?.updateThickness(); }
    async function removeLine(p) { const d = diagrams.get(p); d?.removeLine(); await d?.updateThickness(); }
    async function resetLines(p) { const d = diagrams.get(p); d?.resetLines(); await d?.updateThickness(); }

    function summaryAddLine()    { combinedDiagram?.addLine();    updateSummaryThicknesses(); }
    function summaryRemoveLine() { combinedDiagram?.removeLine(); updateSummaryThicknesses(); }
    function summaryResetLines() { combinedDiagram?.resetLines(); if (summaryChart) summaryChart.reset(); updateSummaryThicknesses(); }

    return { init, addLine, removeLine, resetLines, summaryAddLine, summaryRemoveLine, summaryResetLines };
})();

// ═══════════════════════════════════════════════════════
// CONNECTION STATUS — по одному индикатору на каждую карточку
// ═══════════════════════════════════════════════════════

/**
 * Обновляет индикатор подключения в хедере карточки (слота).
 * Запрашивает GetStreamConnectionStatus(streamId) — реальное состояние
 * TCP-сокета/порта, а не ICMP-пинг по IP из конфига.
 *
 * @param {number} slot    — номер слота (1-3)
 * @param {number} stream  — текущий стрим этого слота (1-4)
 */
async function updateCardConn(slot, stream) {
    const badge = document.getElementById("connCard" + slot);
    const label = document.getElementById("connCardLabel" + slot);
    if (!badge || !label) return;

    try {
        const raw = await window.chrome.webview.hostObjects.arokis.GetStreamConnectionStatus(stream);
        const st  = JSON.parse(raw);

        if (!st.hasDevice) {
            // Ни одного настроенного устройства → индикатор нейтральный
            badge.className       = "card-conn";
            label.textContent     = "Нет устройства";
            return;
        }

        badge.className   = "card-conn " + (st.connected ? "ok" : "err");
        label.textContent = st.connected ? "Подключено" : "Нет связи";
    } catch {
        badge.className   = "card-conn err";
        label.textContent = "Ошибка";
    }
}

/**
 * Запускает периодическое обновление всех трёх индикаторов.
 * Вызывается из arokisUi.init().
 * Использует diagrams Map для получения актуального streamNumber слота.
 */
function startConnPolling() {
    async function poll() {
        for (let slot = 1; slot <= VISIBLE_SLOTS; slot++) {
            const d = diagrams.get(slot);
            const stream = d ? d.streamNumber : slot;
            await updateCardConn(slot, stream);
        }
    }
    poll();                          // сразу при старте
    setInterval(poll, 4000);         // затем каждые 4 сек
}

function startAll() { api.startAll(); }

window.addEventListener("DOMContentLoaded", () => arokisUi.init());