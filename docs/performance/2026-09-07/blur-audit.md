# 模糊底层核验

核验日期：2026-09-07；真机 WebView `150.0.7871.181`。

**当前效果是高斯模糊，底层已经有快速处理和近似路径。** 大半径先降采样，在缩小后的图像上计算，再还原输出；适用的 GPU 卷积采用分离滤波和线性采样合并。不能把 CSS 的 `blur()` 或源码中的 `GaussianBlur` 名称理解成“逐像素执行未经优化的全尺寸二维卷积”。

本次保留 CSS 的模糊半径、饱和度、亮度、阴影和动画定义。没有改成 Kawase，也没有额外捕获背景或视频到 Canvas。这样保留 WebView 当前的视觉结果、视频合成和边界处理。

## 项目调用与版本对应

- 液态玻璃使用 CSS `filter: blur(...)`、`backdrop-filter: blur(...)`，以及组合颜色滤镜；项目没有自写高斯卷积、StackBlur 或 Kawase shader。simpleUI 原本禁用这些装饰滤镜。
- 示例：[眼镜背景与玻璃面板](../../../GlassesUI/src/styles.css)、[手机玻璃面板](../../../CompanionUI/src/styles.css)。
- [Chromium 150.0.7871.181 的 DEPS](https://github.com/chromium/chromium/blob/150.0.7871.181/DEPS) 固定 Skia 到 `587c5b0f5a7b0260826a0c19094c2d952195066e`。以下引用均固定到这两个版本，而非浮动主分支。
- 下载源文件的 URL、字节数及 SHA-256 见 [blur-sources.json](blur-sources.json)。本机 WebView 安装包与这些公开源码对应；这不是重新构建并逐条反汇编其二进制的验证。

## 实际实现链

| 层级 | 固定版本源码与行为 |
| --- | --- |
| Chromium 合成滤镜 | [RenderSurfaceFilters::BuildImageFilter](https://github.com/chromium/chromium/blob/150.0.7871.181/cc/paint/render_surface_filters.cc#L193) 将 BLUR 操作变成 `BlurPaintFilter`；[SkiaRenderer](https://github.com/chromium/chromium/blob/150.0.7871.181/components/viz/service/display/skia_renderer.cc#L3096) 对内容滤镜和背景滤镜调用此构建过程。 |
| Blink 滤镜效果 | [FEGaussianBlur::CreateImageFilter](https://github.com/chromium/chromium/blob/150.0.7871.181/third_party/blink/renderer/platform/graphics/filters/fe_gaussian_blur.cc) 同样生成带 sigma 的 `BlurPaintFilter`。 |
| Chromium → Skia | [BlurPaintFilter 构造](https://github.com/chromium/chromium/blob/150.0.7871.181/cc/paint/paint_filter.cc#L340) 调用 `SkImageFilters::Blur`。 |
| Skia 图像滤镜 | [SkBlurImageFilter](https://github.com/google/skia/blob/587c5b0f5a7b0260826a0c19094c2d952195066e/src/effects/imagefilters/SkBlurImageFilter.cpp#L153) 进入滤镜结果构建；[Builder::blur](https://github.com/google/skia/blob/587c5b0f5a7b0260826a0c19094c2d952195066e/src/core/SkImageFilterTypes.cpp#L2110) 按后端选择算法，超过算法 sigma 上限时计算缩放比例，生成 `lowResImage` 并调整 sigma。 |
| GPU 大半径 | [Ganesh GaussianBlur](https://github.com/google/skia/blob/587c5b0f5a7b0260826a0c19094c2d952195066e/src/gpu/ganesh/GrBlurUtils.cpp#L2233) 对超出上限的 sigma 降采样；后续使用 `kRepeatedLinear` 重采样。此时输出是降采样后的高斯近似。 |
| GPU 卷积 | 同一函数对很小的核选一次二维采样，其余适用核选两次一维分离卷积；[线性核计算](https://github.com/google/skia/blob/587c5b0f5a7b0260826a0c19094c2d952195066e/src/core/SkBlurEngine.cpp#L1404) 把相邻高斯采样权重合并，利用 GPU 线性插值减少纹理采样。这一步在既定离散核内保持等价权重。 |
| CPU 后备 | [ThreeBoxApproxPass](https://github.com/google/skia/blob/587c5b0f5a7b0260826a0c19094c2d952195066e/src/core/SkBlurEngine.cpp#L378) 明确采用三次盒式滤波近似高斯。这是 CPU 路径的证据，不能说成真机 GPU 正在执行三次盒式滤波。 |

## 真机运行轨迹

在性能采样窗口以外，打开液态玻璃首页，单独记录约 2.5 秒 WebView CDP trace。捕获到 **20 个 `disabled-by-default-skia.gpu / GaussianBlur` 事件**，包含 sigma 为 63、19、16、11 的调用及 sigma 为 3 的调用，与上面的 Ganesh 大半径降采样递归路径一致；也记录到 Vulkan 提交事件。白名单记录见 [blur-runtime.json](blur-runtime.json)。这补充了运行路径证据，并非仅按 CSS 名称猜测。

事件时长是 CPU 侧记录的函数范围，不能当作 GPU shader 执行时间，也不用于计算本次优化收益。该短轨迹没有逐个标记所有控件的 shader；不据此声称每个小半径滤镜都执行相同算法。

## 是否需要替换

用户要求“没有快速近似时替换”；本版本已具有大半径降采样近似和后端专用加速，因此不再叠加一套自定义模糊管线。小半径使用小核是有意的效率和质量选择。[Skia 的说明](https://github.com/google/skia/blob/587c5b0f5a7b0260826a0c19094c2d952195066e/src/core/SkBlurEngine.h#L82) 特别指出 sigma 小于 2 时盒式近似不够准确；强制把所有小核换成盒式或 Kawase，并不满足“保持 UI 效果”的要求。

这不表示玻璃模糊没有开销。原报告中静态滤镜可接近空闲，而持续动画会反复触发绘制和合成。本次优化针对重复绘制与完全不可见动画；不把 CSS 属性名更换或滤镜重写当作已有性能收益。
