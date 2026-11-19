# PCLGE 2 · PCLGE 1.00（C# / WPF 完整版）

PCLGE 是一个用 **C# / .NET 10 + WPF** 写的《我的世界》启动器，界面结构对齐原版 PCL：顶部六个一级导航（**启动 / 基岩版 / 下载 / 设置 / 工具 / 联机**），每页左列表 + 右内容，底部常驻运行日志与状态栏。

> 本仓库是发布版（releases）：源码 + 编译好的单文件 exe。

## 功能

### 启动
- 自动扫描已安装版本，显示加载器 / 类型 / 占用空间
- 多离线账户管理（新建 / 删除 / 切换），离线 UUID 与官方算法一致
- 内存、额外 JVM 参数、版本隔离（每个版本独立 mods / saves / 配置）
- 按 MC 版本自动挑选 Java（1.17+ → 17，1.20.5+ → 21）
- 完整解析版本 JSON：`arguments.game` / `arguments.jvm`、rules、继承链、老版本 `minecraftArguments`
- 原生库解压、`-cp` 自动拼装、启动后等待退出并显示退出码

### 下载
- 原版游戏：BMCLAPI 镜像 / Mojang 官方源切换，按正式版 / 快照 / 关键词过滤
- 模组加载器：**Fabric / Quilt / Forge / NeoForge / OptiFine**
  - Fabric / Quilt 生成 profile JSON
  - Forge / NeoForge 调用官方 installer `--installClient`
- Modrinth：模组、资源包、光影、整合包搜索（按 MC 版本 + 加载器筛选）
- 整合包按 `modrinth.index.json` 逐文件下载，并解压 `overrides/`
- 已安装管理：查看详情、修复重装、删除版本、打开目录

### 设置
- 游戏目录自定义 / 恢复默认、存档迁移、存档全量备份为 zip
- Java 扫描（JAVA_HOME / PATH / Program Files 各发行版）+ 手动指定 + 设为默认
- 下载源、并发线程、清理 `.tmp` 残留
- 版本管理：模组启用 / 禁用 / 添加 / 删除，资源包、存档备份与删除
- 关于页：配置文件、日志位置、仓库链接

### 工具
- **皮肤**：LittleSkin 下载（用户名 → UUID → textures → png）、本地 PNG 导入、应用到实例、预览
- **成就图**：生成 320×64 原版成就弹窗风格 PNG（可预览、可保存到桌面）
- **崩溃分析**：读取 `crash-reports/*.txt` 与 `logs/latest.log`，匹配 15 类常见原因（内存不足、Java 版本、缺少前置、模组冲突、显卡驱动、权限等），给出处理办法，支持导出报告
- **日志**：启动器日志实时展示、落盘、清空、打开游戏日志

### 联机
- UDP 25567 局域网广播聊天：`[CHAT]昵称||消息`、`[PING]`、`[BYE]`
- 在线玩家列表、心跳、端口可改、同机多开（同时发回环）
- 异地联机可用 EasyTier / ZeroTier 组虚拟局域网

### 基岩版
- 从官网解析 Windows 版 BDS 下载地址并下载解压
- 关联已有 BDS 目录、启动 / 停止服务端、实时输出日志
- `server.properties` 读取 / 编辑 / 端口写入

## 目录结构

```
src/PCLGE/
├─ App.xaml / App.xaml.cs          应用入口与全局异常捕获
├─ MainWindow.xaml(.cs)            顶部导航 + 页面容器 + 日志/状态栏
├─ Ui/UiKit.cs                     卡片、按钮、双栏布局等界面零件
├─ Views/
│  ├─ LaunchPage.cs                启动
│  ├─ BedrockPage.cs               基岩版
│  ├─ DownloadPage.cs              下载
│  ├─ SetupPage.cs                 设置
│  ├─ ToolsPage.cs                 工具
│  └─ LinkPage.cs                 联机
└─ Core/
   ├─ Config.cs                     ini 配置与目录
   ├─ Net.cs                        HTTP / 下载
   ├─ LogService.cs                 日志缓冲与落盘
   ├─ JavaService.cs                Java 扫描与版本探测
   ├─ AccountService.cs             离线账户与离线 UUID
   ├─ Libraries.cs                  libraries / rules 解析（安装与启动共用）
   ├─ VersionService.cs             版本清单、安装、修复、删除
   ├─ LoaderInstaller.cs            Fabric / Quilt / Forge / NeoForge / OptiFine
   ├─ GameLauncher.cs               拼装参数并启动 Java
   ├─ ModManager.cs                 模组 / 资源包 / 存档
   ├─ Modrinth.cs                   Modrinth 搜索与下载
   ├─ SkinService.cs                LittleSkin 与本地皮肤
   ├─ CrashAnalyzer.cs              崩溃分析规则
   ├─ AchievementImage.cs           成就图渲染
   ├─ BedrockService.cs             BDS 下载与启停
   └─ LanChat.cs                    局域网 UDP 聊天
```

## 编译

需要 **.NET 10 SDK**（Windows）或 Linux 上交叉编译（`EnableWindowsTargeting` 已开启）：

```powershell
cd src/PCLGE
dotnet publish -c Release -r win-x64
```

产物：`src/PCLGE/bin/Release/net10.0-windows/win-x64/publish/PCLGE.exe`（单文件，约 62 MB，自带 .NET 运行时，无需安装）。

也可以直接用仓库根目录的 `build.ps1`：

```powershell
./build.ps1
```

## 配置与数据位置

| 内容 | 路径 |
|---|---|
| 配置文件 | `%AppData%\PCLGE\pclge-config.ini` |
| 启动器日志 | `%AppData%\PCLGE\logs\latest.log` |
| 皮肤 | `%AppData%\PCLGE\skins\<账户>.png` |
| 存档备份 | `%AppData%\PCLGE\backups\` |
| 游戏目录 | 默认 `%AppData%\.minecraft`，可在设置里改 |

## 已知限制

- 只有 Windows 版（WPF）；界面未做深色主题 / 动画 / 自定义背景。
- 只支持离线账户登录，没有微软正版登录。
- 皮肤在离线环境下不会自动生效，需要游戏内离线皮肤模组。
- Forge / NeoForge / OptiFine 安装依赖官方 installer，需要本机已有可用 Java。
- 未做自动更新检查。

## 声明

本项目是个人学习用的启动器，与 Mojang / Microsoft 无关。《Minecraft》相关版权归 Mojang Studios 所有。

## 许可

MIT License，见 [LICENSE](LICENSE)。
