# NexusDash 原生界面验证

加载实际 App、窗口、XAML 和 ViewModel，使用内存偏好设置，不修改用户工作区。系统进程采样只读；测试不发送结束进程命令，不启动浏览器或安装器。

```powershell
dotnet run --project scripts/ui-verification/UiVerification.csproj -c Release -- ./artifacts/ui-verification
```

断言覆盖窗口样式、旧偏好迁移、三态八色独立保存、四语动态刷新和关于更新状态。失败返回非零退出码，截图写入输出目录。NativeAOT 复核应将本验证器使用 `dotnet publish -r win-x64 -p:PublishAot=true -p:IlcGenerateCompleteTypeMetadata=true -p:IlcTrimMetadata=false` 发布并运行其 EXE；同时执行 App 自身 `scripts/publish.ps1`。不能用框架运行结果替代原生运行。

详见 [验收记录](../../docs/UI-VERIFICATION.md)。
