using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace JellyfinForRayNeo
{
    public sealed class JellyfinImageCache : IDisposable
    {
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private readonly Queue<string> _insertionOrder = new Queue<string>();
        private readonly SemaphoreSlim _downloadSlots;
        private readonly int _maximumEntries;
        private bool _disposed;

        public JellyfinImageCache(int maximumEntries = 192, int concurrentDownloads = 4)
        {
            _maximumEntries = Math.Max(16, maximumEntries);
            _downloadSlots = new SemaphoreSlim(Math.Max(1, concurrentDownloads));
        }

        public async Task<Sprite> LoadSpriteAsync(string url, CancellationToken cancellationToken)
        {
            if (_disposed || string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            Sprite cached;
            if (_cache.TryGetValue(url, out cached) && cached != null)
            {
                return cached;
            }

            await _downloadSlots.WaitAsync(cancellationToken);
            try
            {
                if (_disposed)
                {
                    return null;
                }

                if (_cache.TryGetValue(url, out cached) && cached != null)
                {
                    return cached;
                }

                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false))
                {
                    request.timeout = 30;
                    await request.SendRequestAsync(cancellationToken);
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new JellyfinApiException(
                            "海报加载失败：" + request.error,
                            request.responseCode,
                            url);
                    }

                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    if (texture == null)
                    {
                        return null;
                    }

                    texture.name = "JellyfinImage";
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    Sprite created = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);

                    if (_disposed)
                    {
                        UnityEngine.Object.Destroy(created);
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }

                    _cache[url] = created;
                    _insertionOrder.Enqueue(url);
                    TrimCache();
                    return created;
                }
            }
            finally
            {
                _downloadSlots.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (Sprite sprite in _cache.Values)
            {
                if (sprite == null)
                {
                    continue;
                }
                Texture2D texture = sprite.texture;
                UnityEngine.Object.Destroy(sprite);
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            _cache.Clear();
            _insertionOrder.Clear();
        }

        private void TrimCache()
        {
            while (_cache.Count > _maximumEntries && _insertionOrder.Count > 0)
            {
                string key = _insertionOrder.Dequeue();
                Sprite sprite;
                if (!_cache.TryGetValue(key, out sprite))
                {
                    continue;
                }

                _cache.Remove(key);
                if (sprite != null)
                {
                    Texture2D texture = sprite.texture;
                    UnityEngine.Object.Destroy(sprite);
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                }
            }
        }
    }
}
