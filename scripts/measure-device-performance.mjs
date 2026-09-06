#!/usr/bin/env node
/**
 * Node 22+ / ADB sampler for the installed debug app. Only whitelisted technical
 * fields are saved: no URLs, accounts, titles, credentials, screenshots, or raw
 * logcat/dumpsys output. Select theme/mode/page on the device before sampling.
 * --navigate sends 5 right / 5 left commands repeatedly at 400 ms intervals using
 * the native bridge, with haptics disabled. First focus a catalog card manually.
 * CPU: app + its renderer, 100% = one core. GPU: system-wide KGSL busy, not power.
 * HWUI combines both windows; counts are not FPS. Empty histograms become null.
 * Temporary page observers and this invocation's ADB forward are cleaned up.
 */
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
const argv = process.argv.slice(2);
const option = (key, fallback) => { const i = argv.indexOf(key); return i < 0 ? fallback : argv[i + 1]; };
const adb = option('--adb', 'adb'), transport = option('--transport', '');
const prefix = transport ? ['-t', transport] : [], pkg = 'com.jellyfinforrayneo.client.debug';
const label = option('--label', 'sample'), seconds = Number(option('--seconds', '20')), out = option('--out', '');
if (!out || !/^[a-z0-9][a-z0-9_-]{0,79}$/.test(label) || !Number.isFinite(seconds) || seconds < 5 || seconds > 300 || (transport && !/^\d+$/.test(transport))) {
    console.error('Usage: node scripts/measure-device-performance.mjs --out <directory> --label <name> [--seconds 20] [--transport <ADB transport id>] [--adb <executable>] [--navigate]');
    process.exit(2);
}
const dir = resolve(out), exec = promisify(execFile);
let port;
export const sleep = ms => new Promise(r => setTimeout(r, ms));
export async function shell(s) { return (await exec(adb, [...prefix, 'shell', s], { maxBuffer: 12 * 1024 * 1024, timeout: 15000 })).stdout; }
const pick = (s, re) => Number(s.match(re)?.[1] ?? NaN);
function perf(r) { return Object.fromEntries(r.metrics.map(x => [x.name, x.value])); }
function subtract(a, b, keys) { return Object.fromEntries(keys.map(k => [k, (b[k] ?? 0) - (a[k] ?? 0)])); }
const pageStart = `(()=>{window.__perfRun?.observer?.disconnect(); const r=window.__perfRun={start:performance.now(),long:[],keys:[],waiting:0,stalled:0,playing:0,video:document.querySelector('video')};r.quality=r.video?.getVideoPlaybackQuality();r.time=r.video?.currentTime;r.observer=new PerformanceObserver(l=>{for(const e of l.getEntries())r.long.push(e.duration)});r.observer.observe({entryTypes:['longtask']});r.handlers={waiting:()=>r.waiting++,stalled:()=>r.stalled++,playing:()=>r.playing++};for(const [k,h]of Object.entries(r.handlers))r.video?.addEventListener(k,h);r.keyHandler=()=>{const t=performance.now();requestAnimationFrame(()=>r.keys.push(performance.now()-t))};window.addEventListener('keydown',r.keyHandler);return {nodes:document.querySelectorAll('*').length,theme:document.documentElement.dataset.uiTheme,page:document.querySelector('.app,.prototype-shell')?.className,videoCount:document.querySelectorAll('video').length}})()`;
const pageEnd = `(()=>{const r=window.__perfRun;r.observer.disconnect();window.removeEventListener('keydown',r.keyHandler);for(const[k,h]of Object.entries(r.handlers))r.video?.removeEventListener(k,h);const v=document.querySelector('video'),q=v?.getVideoPlaybackQuality();return{durationMs:performance.now()-r.start,longTasks:r.long,keyToRafMs:r.keys,waiting:r.waiting,stalled:r.stalled,playing:r.playing,video:v?{sameObject:v===r.video,count:document.querySelectorAll('video').length,width:v.videoWidth,height:v.videoHeight,paused:v.paused,readyState:v.readyState,currentTimeDelta:v.currentTime-r.time,totalFrames:q.totalVideoFrames-(r.quality?.totalVideoFrames||0),droppedFrames:q.droppedVideoFrames-(r.quality?.droppedVideoFrames||0),bufferAhead:(()=>{for(let i=0;i<v.buffered.length;i++)if(v.buffered.start(i)<=v.currentTime&&v.buffered.end(i)>=v.currentTime)return v.buffered.end(i)-v.currentTime;return 0})()}:null,jsHeap:performance.memory?.usedJSHeapSize}})()`;
async function measure(label, seconds = 20, { navigate = false } = {}) {
    const g = await CDP.open('GlassesUI');
    let p;
    try {
        p = await CDP.open('CompanionUI');
        const nativeState = await p.eval(`(()=>{const s=JSON.parse(JellyfinNative.getState());return Object.fromEntries(['uiTheme','activeDisplayMode','displayModeApplied','stereoOutput','stereoScreen','mediaReady'].map(k=>[k,s[k]]))})()`);
        const pid = Number((await shell('pidof ' + pkg)).trim());
        const services = await shell('dumpsys activity services ' + pkg);
        const renders = [...new Set([...services.matchAll(/app=ProcessRecord\{\S+ (\d+):[^\n]*(?:sandboxed_process|SandboxedProcessService)/g)].map(m => Number(m[1])))];
        if (!renders.length)
            throw new Error('Associated WebView renderer not found');
        const procIds = [pid, ...renders];
        const mem = async () => Object.fromEntries(await Promise.all(procIds.map(async (id) => { const s = await shell('dumpsys meminfo ' + id); return [id, { pssKB: pick(s, /TOTAL PSS:\s+(\d+)/), graphicsKB: pick(s, /Graphics:\s+(\d+)/), webViews: pick(s, /WebViews:\s+(\d+)/) }]; })));
        const sys = async () => { const s = await shell("dumpsys battery; dumpsys thermalservice | head -12"); return { batteryC: pick(s, /temperature:\s+(\d+)/) / 10, batteryPercent: pick(s, /level:\s+(\d+)/), charging: /AC powered: true|USB powered: true|Wireless powered: true/.test(s), thermalStatus: pick(s, /Thermal Status:\s+(\d+)/) }; };
        const ticks = async () => { const s = await shell(procIds.map(id => 'cat /proc/' + id + '/stat').join('; ')); return Object.fromEntries(s.trim().split('\n').filter(l => /^\d+ \(/.test(l)).map(l => { const id = l.split(' ')[0], v = l.slice(l.lastIndexOf(')') + 2).split(' '); return [id, Number(v[11]) + Number(v[12])]; })); };
        const before = { memory: await mem(), system: await sys() };
        await g.send('Performance.enable');
        await p.send('Performance.enable');
        const pages = { glasses: await g.eval(pageStart), phone: await p.eval(pageStart) };
        const g0 = perf(await g.send('Performance.getMetrics')), p0 = perf(await p.send('Performance.getMetrics'));
        await shell('dumpsys gfxinfo ' + pkg + ' reset');
        const hz = Number((await shell('getconf CLK_TCK')).trim());
        if (!Number.isFinite(hz) || hz <= 0)
            throw new Error('Invalid clock tick rate');
        const t0 = Date.now(), cpu0 = await ticks();
        await shell('cat /sys/class/kgsl/kgsl-3d0/gpubusy');
        if (navigate) {
            await p.eval(`(()=>{let i=0;window.__perfNav=setInterval(()=>{JellyfinNative.remoteCommand(i++%10<5?'right':'left',false)},400);return true})()`);
        }
        const gpu = [];
        while (Date.now() - t0 < seconds * 1000) {
            await sleep(Math.min(1000, seconds * 1000 - (Date.now() - t0)));
            const s = await shell('cat /sys/class/kgsl/kgsl-3d0/gpubusy');
            const v = s.trim().split(/\s+/).map(Number);
            if (v.length === 2 && v[1] > 0)
                gpu.push(v);
        }
        if (navigate)
            await p.eval('clearInterval(window.__perfNav);true');
        const cpu1 = await ticks(), elapsed = (Date.now() - t0) / 1000;
        const g1 = perf(await g.send('Performance.getMetrics')), p1 = perf(await p.send('Performance.getMetrics'));
        const observations = { glasses: await g.eval(pageEnd), phone: await p.eval(pageEnd) };
        const gfx = await shell('dumpsys gfxinfo ' + pkg + ' framestats');
        const gfxStats = { frames: pick(gfx, /Total frames rendered:\s+(\d+)/), janky: pick(gfx, /Janky frames:\s+(\d+)/), legacyJanky: pick(gfx, /Janky frames \(legacy\):\s+(\d+)/), p50ms: pick(gfx, /50th percentile:\s+(\d+)/), p95ms: pick(gfx, /95th percentile:\s+(\d+)/), gpuP50ms: pick(gfx, /50th gpu percentile:\s+(\d+)/), gpuP95ms: pick(gfx, /95th gpu percentile:\s+(\d+)/), gpuCacheMB: pick(gfx, /Total GPU memory usage:\s+\d+ bytes, ([\d.]+) MB/) };
        if (!gfxStats.frames)
            for (const key of ['p50ms', 'p95ms', 'gpuP50ms', 'gpuP95ms'])
                gfxStats[key] = null;
        const after = { memory: await mem(), system: await sys() };
        const metricsKeys = ['TaskDuration', 'ScriptDuration', 'LayoutDuration', 'RecalcStyleDuration', 'LayoutCount', 'RecalcStyleCount'];
        const result = { label, startedAt: new Date(t0).toISOString(), seconds: elapsed, navigate, nativeState, pages, before, after, cpuOneCorePercent: Object.fromEntries(procIds.map(id => [id, (cpu1[id] - cpu0[id]) / hz / elapsed * 100])), gpuBusyPercent: 100 * gpu.reduce((s, x) => s + x[0], 0) / gpu.reduce((s, x) => s + x[1], 0), gpuSamples: gpu, gfx: gfxStats, rendererMetrics: { glasses: subtract(g0, g1, metricsKeys), phone: subtract(p0, p1, metricsKeys) }, observations };
        await writeFile(dir + '/' + label + '.json', JSON.stringify(result, null, 2));
        console.log(JSON.stringify({ label, seconds: elapsed, cpu: result.cpuOneCorePercent, gpu: result.gpuBusyPercent, gfx: gfxStats, video: observations.glasses.video, memory: after.memory, longTasks: observations.glasses.longTasks.length }));
        return result;
    }
    finally {
        for (const c of [g, p].filter(Boolean))
            await c.eval(`(()=>{const r=window.__perfRun;r?.observer?.disconnect();if(r?.keyHandler)window.removeEventListener('keydown',r.keyHandler);if(r?.handlers)for(const[k,h]of Object.entries(r.handlers))r.video?.removeEventListener(k,h);clearInterval(window.__perfNav);delete window.__perfRun;delete window.__perfNav;return true})()`).catch(() => { });
        g.close();
        p?.close();
    }
}
class CDP {
    constructor(ws) { this.ws = ws; this.id = 0; this.pending = new Map(); this.events = []; ws.onmessage = ({ data }) => { const m = JSON.parse(data); if (m.id) {
        const p = this.pending.get(m.id);
        if (p) {
            this.pending.delete(m.id);
            m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result);
        }
    }
    else
        this.events.push(m); }; }
    static async open(role) { const list = await (await fetch('http://127.0.0.1:' + port + '/json/list', { signal: AbortSignal.timeout(10000) })).json(); const t = list.find(x => x.url.includes('/' + role + '/')); if (!t)
        throw new Error('target missing'); const ws = new WebSocket(t.webSocketDebuggerUrl); await new Promise((r, j) => {
            const timer = setTimeout(() => { ws.close(); j(new Error('CDP connection timed out')); }, 10000);
            ws.onopen = () => { clearTimeout(timer); r(); };
            ws.onerror = () => { clearTimeout(timer); j(new Error('CDP unavailable')); };
        }); return new CDP(ws); }
    send(method, params = {}) { const id = ++this.id; return new Promise((resolve, reject) => { const timer = setTimeout(() => { this.pending.delete(id); reject(new Error('WebView request timed out')); }, 15000); this.pending.set(id, { resolve: v => { clearTimeout(timer); resolve(v); }, reject: e => { clearTimeout(timer); reject(e); } }); this.ws.send(JSON.stringify({ id, method, params })); }); }
    async eval(expression) { const r = await this.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true }); if (r.exceptionDetails)
        throw new Error('WebView evaluation failed'); return r.result.value; }
    close() { this.ws.close(); }
}
try {
    await mkdir(dir, { recursive: true });
    const pid = Number((await shell('pidof ' + pkg)).trim());
    if (!Number.isSafeInteger(pid) || pid <= 0)
        throw new Error('Debug app not running');
    port = Number((await exec(adb, [...prefix, 'forward', 'tcp:0', 'localabstract:webview_devtools_remote_' + pid], { timeout: 10000 })).stdout.trim());
    if (!Number.isInteger(port) || port <= 0 || port > 65535)
        throw new Error('Forwarding failed');
    await measure(label, seconds, { navigate: argv.includes('--navigate') });
}
catch {
    console.error('Sampling failed. Check the selected ADB device, running debug app, and both WebViews. No private device output was saved.');
    process.exitCode = 1;
}
finally {
    if (port)
        await exec(adb, [...prefix, 'forward', '--remove', 'tcp:' + port], { timeout: 10000 }).catch(() => { });
}
