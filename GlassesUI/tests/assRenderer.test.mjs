import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'
const { code } = await transformWithEsbuild(await readFile(new URL('../src/assRenderer.ts',import.meta.url),'utf8'),'assRenderer.ts',{target:'es2022'})
const isolated=code.replace(/^import .+;\n/gm,'')+'\nconst workerSource="worker fixture", wasmAsset="worker.wasm", fallbackFontAsset="SourceHanSansSC-Regular.otf";'
const {loadSubtitleAsset,createAssRenderer}=await import(`data:text/javascript;base64,${Buffer.from(isolated).toString('base64')}`)

function environment(t) {
  const original=new Map(),requests=[],workers=[],revoked=[],blobs=[]
  const set=(key,value)=>{original.set(key,Object.getOwnPropertyDescriptor(globalThis,key));Object.defineProperty(globalThis,key,{configurable:true,writable:true,value})}
  let renderer
  t.after(()=>{renderer?.dispose();for(const[key,value]of original){if(value)Object.defineProperty(globalThis,key,value);else delete globalThis[key]}})
  class Request {
    status=200; response=new Uint8Array([1,2,3]).buffer
    open(_method,url){this.url=url}
    send(){requests.push(this);queueMicrotask(()=>{if(!this.aborted)this.onload?.()})}
    abort(){this.aborted=true}
  }
  class Worker {
    messages=[];terminated=0
    constructor(url){this.url=url;workers.push(this)}
    postMessage(message){this.messages.push(message)}
    terminate(){this.terminated++}
  }
  set('XMLHttpRequest',Request);set('Worker',Worker)
  set('document',{baseURI:'file:///android_asset/GlassesUI/index.html'})
  set('window',{setTimeout,clearTimeout});set('requestAnimationFrame',()=>1);set('cancelAnimationFrame',()=>{})
  t.mock.method(URL,'createObjectURL',blob=>{blobs.push(blob);return `blob:fixture-${blobs.length}`})
  t.mock.method(URL,'revokeObjectURL',url=>revoked.push(url))
  const context={clearRect:()=>{context.cleared++},cleared:0}
  const canvas={width:640,height:360,getContext:()=>context}
  return {requests,workers,revoked,blobs,canvas,context,keep:value=>{renderer=value}}
}

test('APK assets accept file status zero, and oversized or cancelled transfers fail without exposing URLs',async t=>{
  const e=environment(t)
  const controller=new AbortController()
  const pending=loadSubtitleAsset('file:///android_asset/font.otf',10,controller.signal)
  e.requests[0].status=0
  assert.equal((await pending).byteLength,3)
  await assert.rejects(loadSubtitleAsset('https://example.invalid/?api_key=private',2,controller.signal),error=>!error.message.includes('private'))
  const aborted=loadSubtitleAsset('file:///android_asset/worker.wasm',10,controller.signal)
  controller.abort()
  await assert.rejects(aborted,{name:'AbortError'})
  assert.equal(e.requests.at(-1).aborted,true)
})

test('one worker receives original ASS and local font blobs, and abort clears pixels and revokes every blob',async t=>{
  const e=environment(t),controller=new AbortController()
  const source='[Events]\nDialogue: 0,0:00:00.00,0:00:02.00,Default,,0,0,0,,{\\move(0,0,80,80)}中文'
  const renderer=await createAssRenderer(e.canvas,source,['https://example.invalid/font?api_key=private'],controller.signal,()=>assert.fail('unexpected worker failure'))
  e.keep(renderer)
  assert.equal(e.workers.length,1)
  const worker=e.workers[0],init=worker.messages[0]
  assert.equal(init.subContent,source)
  assert.equal(init.dropAllAnimations,false)
  assert.equal(init.renderMode,'wasm-blend')
  assert.match(init.fallbackFont,/^blob:/)
  assert.equal(init.fonts.length,1)
  assert.doesNotMatch(JSON.stringify(worker.messages),/private|https:\/\//)
  renderer.render(121.5);assert.equal(worker.messages.at(-1).currentTime,121.5)
  renderer.resize(800,450);assert.equal(e.canvas.width,800)
  controller.abort();renderer.dispose()
  assert.equal(worker.terminated,1)
  assert.equal(e.revoked.length,e.blobs.length)
  assert.ok(e.context.cleared>0)
  const count=worker.messages.length;renderer.render(122);assert.equal(worker.messages.length,count)
})

test('a worker failure reports once and terminates local rendering without requesting burn-in',async t=>{
  const e=environment(t),controller=new AbortController();let failures=0
  const renderer=await createAssRenderer(e.canvas,'[Events]',[],controller.signal,()=>failures++)
  e.keep(renderer)
  e.workers[0].onerror();e.workers[0].onmessageerror()
  assert.equal(failures,1);assert.equal(e.workers[0].terminated,1)
  assert.equal(e.requests.length,2)
})
