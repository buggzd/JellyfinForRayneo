import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'
const { code } = await transformWithEsbuild(await readFile(new URL('../src/assVideo.ts', import.meta.url), 'utf8'), 'assVideo.ts', { target:'es2022' })
const { assVideoRect, bindAssVideo } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)

function environment(t, frames = true) {
  const original = new Map()
  let unbind = () => {}
  const set = (name, value) => { original.set(name, Object.getOwnPropertyDescriptor(globalThis, name)); Object.defineProperty(globalThis, name, {configurable:true,writable:true,value}) }
  t.after(() => { unbind(); for (const [name, value] of original) { if (value) Object.defineProperty(globalThis, name, value); else delete globalThis[name] } })
  const doc = Object.assign(new EventTarget(), { hidden:false })
  const win = Object.assign(new EventTarget(), { devicePixelRatio:2 })
  const scheduled = new Map()
  let id = 0
  const schedule = (callback) => { scheduled.set(++id, callback); return id }
  const cancel = (key) => scheduled.delete(key)
  set('document', doc); set('window', win); set('ResizeObserver', undefined)
  set('requestAnimationFrame', schedule); set('cancelAnimationFrame', cancel)
  const video = Object.assign(new EventTarget(), {
    clientWidth:1440,clientHeight:810,videoWidth:1920,videoHeight:1032,
    currentTime:120,paused:true,seeking:false,readyState:4,
    ...(frames ? { requestVideoFrameCallback:schedule,cancelVideoFrameCallback:cancel } : {}),
  })
  const canvas = { width:300,height:150,style:{} }
  const times = []
  const sizes = []
  const renderer = { render:time=>times.push(time), resize:(w,h)=>{canvas.width=w;canvas.height=h;sizes.push([w,h])} }
  unbind = bindAssVideo(video,canvas,renderer)
  const frame = (time) => { const [key, callback] = scheduled.entries().next().value; scheduled.delete(key); callback(0,{mediaTime:time}) }
  const event = (type) => video.dispatchEvent(new Event(type))
  return {doc,win,video,canvas,times,sizes,scheduled,frame,event,unbind}
}

test('fits ASS coordinates to letterboxed and pillarboxed video, rejecting absent metadata', () => {
  assert.deepEqual(assVideoRect(800,500,640,360),{width:800,height:450,left:0,top:25})
  assert.deepEqual(assVideoRect(800,500,400,400),{width:500,height:500,left:150,top:0})
  assert.equal(assVideoRect(0,500,640,360),null)
  assert.equal(assVideoRect(800,500,NaN,360),null)
})

test('resumed ASS uses presented frame timestamps and stops all callbacks on pause and disposal', t => {
  const e=environment(t)
  assert.equal(e.times.at(-1),120)
  assert.equal(e.scheduled.size,0)
  assert.deepEqual(e.sizes,[[1920,1032]])
  e.video.paused=false;e.event('playing');assert.equal(e.scheduled.size,1)
  e.frame(120.042);assert.equal(e.times.at(-1),120.042)
  e.video.currentTime=120.08;e.video.paused=true;e.event('pause')
  assert.equal(e.scheduled.size,0);assert.equal(e.times.at(-1),120.08)
  e.unbind();const count=e.times.length;e.event('playing');e.event('timeupdate')
  assert.equal(e.times.length,count);assert.equal(e.scheduled.size,0)
})

test('seek and hidden documents suppress old subtitles and resume on the new video time', t => {
  const e=environment(t)
  e.video.paused=false;e.event('playing')
  e.video.seeking=true;e.video.currentTime=400;e.event('seeking')
  assert.equal(e.canvas.style.visibility,'hidden');assert.equal(e.scheduled.size,0)
  e.video.seeking=false;e.event('seeked')
  assert.equal(e.times.at(-1),400);assert.equal(e.canvas.style.visibility,'visible')
  e.doc.hidden=true;e.doc.dispatchEvent(new Event('visibilitychange'))
  assert.equal(e.scheduled.size,0);assert.equal(e.canvas.style.visibility,'hidden')
  e.doc.hidden=false;e.video.currentTime=402;e.doc.dispatchEvent(new Event('visibilitychange'))
  assert.equal(e.times.at(-1),402);assert.equal(e.scheduled.size,1)
  e.video.readyState=0;e.event('emptied');assert.equal(e.canvas.style.visibility,'hidden')
})

test('older WebViews use the media clock and do not redraw repeated stalled timestamps', t => {
  const e=environment(t,false)
  e.video.paused=false;e.event('playing')
  const count=e.times.length;e.frame(999);assert.equal(e.times.length,count)
  e.video.currentTime=121;e.frame(999);assert.equal(e.times.at(-1),121)
  e.video.paused=true;e.event('pause');assert.equal(e.scheduled.size,0)
})
