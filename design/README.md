# NexusDash 原型与验收

`index.html` 保留早期三卡片概念稿。`studio.html` 是当前产品布局的可点击规格，补齐原概念稿没有覆盖的侧栏、设置、独立三态八色、四语言和关于更新流程；两者不可当作同一版本的像素对照。

当前实现以 1440 × 760 为默认窗口，最低 1180 × 620；标题栏 40 px，窗口按钮 34 px，左栏 220 px，分隔区 5 px，底部日志默认 88 px。外观下拉文本左对齐，模式不携带配色，配色独立保存。

原型使用固定样例数据，不执行进程采样、结束进程、下载或安装。真实系统行为仍由应用验证。`assets/palettes.js` 固化共享主题令牌，`assets/resources.js` 由宿主 JSON 生成，构建不依赖原型目录。

```powershell
pwsh scripts/sync_design_resources.ps1 -Check
npm ci --prefix design/tests
npm test --prefix design/tests
```

浏览器门禁覆盖四语言、三态八色、系统颜色变化、四个导航入口、关于和更新状态，并要求没有脚本异常。原生布局与控件绑定由 `scripts/ui-verification` 单独验证；锁屏离屏渲染不能替代输入法、多 DPI 和真实桌面交互验收。
