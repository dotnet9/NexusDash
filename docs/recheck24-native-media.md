# 0.1.14 当前原生截图来源

2026-10-16，不可变产品标签 `v0.1.14` 对应提交 `5d870701595267dd3179ded3ffffe392c6bb48cf`。本机 Windows x64 正式 NativeAOT 在测试夹具修复提交 `4f1e3ad9849eea605bb5937eede42f9b59b02964` 后构建，程序集提交元数据如实记录该工作树；编入产品与 UI 验证器的源码均与标签一致，唯一仓库差异是未编入产品的 `scripts/tests/prototype_newlines_test.ps1`。云端正式安装器构建源仍为标签提交。

正式产品隐藏自检、258 项原生 UI 检查及 60 张真实控件离屏截图均使用本轮独立证据目录；安装器只编译及校验，未执行。当前 PNG 从原生验证器原始捕获逐字节复制，通过无窗口 Skia 绘制真实应用控件，系统指标来自本机只读快照。本次文档媒体提交不改变标签对应的产品源。

| 仓库图片 | 原生捕获文件 | SHA-256（复制前后相同） |
| --- | --- | --- |
| `docs/media/nexusdash-native-dark.png` | `nexusdash-dotnet-dark.png` | `8fb9ac6e129eaef826753c6d9d367abd11c89953e17c028359dc43a2fa324974` |
| `docs/media/nexusdash-native-appearance.png` | `appearance-zh-CN.png` | `eda04d8bd245b3d0b8be81e4f6bf95d6262ccc40bc573482c1644c8a62371888` |
| `docs/media/nexusdash-native-about.png` | `about-zh-CN.png` | `3732ae526b8fb4ec52a4437ea1e4cee326c97bd4acdf1a11bb3d963ac2d3271e` |
| `docs/media/nexusdash-native-minimum.png` | `minimum-en-US-processManager-scrolled.png` | `eae614bce37650ee809a939af9fe9a5497078ad91feb768f24c651570bc24018` |

0.1.13 的旧截图、成功收据和两次云端失败日志保留。后续诊断复现过服务元数据瞬态为空，但原始两次 NativeAOT 失败的确切原因仍未证实。
